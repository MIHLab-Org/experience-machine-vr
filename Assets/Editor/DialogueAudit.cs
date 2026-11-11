using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Yarn;
using Yarn.Unity;

/// <summary>
/// MIHLab > Audit dialogue: checks every conversation in the open scene without playing it.
///
///   1. Entry points: every node name set on PhilosopherNPC, PhilosopherWalker, DialogueTrigger
///      and YarnWalkCommands must exist in the Yarn project.
///   2. Runs each entry node (and every Converse_ / Walk_ / Talk_ node) in Yarn's own virtual
///      machine with fresh variables, many times, choosing a different option each time until
///      every option it meets has been taken at least once. Reports runtime errors (missing
///      jump targets, type errors), unknown commands, <<handoff>> and <<walk_to>> targets the
///      scene can't resolve, runs that never end, and options that are never available.
///   3. Lists nodes no entry point ever reaches.
///
/// Read-only: changes nothing in the scene or the project. Results go to the Console.
/// </summary>
public static class DialogueAudit
{
    private const string ProjectPath = "Assets/Dialogue/Socrates.yarnproject";
    private const int MaxRunsPerStart = 120;
    private const int MaxStepsPerRun = 4000;

    [MenuItem("MIHLab/Audit dialogue")]
    private static void Menu()
    {
        Debug.Log(Run());
    }

    public static string Run()
    {
        var sb = new StringBuilder();
        var project = AssetDatabase.LoadAssetAtPath<YarnProject>(ProjectPath);
        if (project == null || project.Program == null) return "[DialogueAudit] Yarn project missing or did not compile.";
        Program program = project.Program;
        var nodeNames = new HashSet<string>(project.NodeNames);

        // ---------------------------------------------------------------- 1. entry points
        var entries = new SortedDictionary<string, List<string>>();
        var missingEntries = new List<string>();
        foreach (MonoBehaviour mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            if (mb == null) continue;
            string type = mb.GetType().Name;
            if (type != "PhilosopherNPC" && type != "PhilosopherWalker" && type != "DialogueTrigger" && type != "YarnWalkCommands") continue;
            var so = new SerializedObject(mb);
            SerializedProperty it = so.GetIterator();
            while (it.Next(true))
            {
                if (it.name.IndexOf("node", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (it.propertyType != SerializedPropertyType.String) continue;
                string node = it.stringValue;
                if (string.IsNullOrEmpty(node)) continue;
                string who = mb.gameObject.name + "." + it.propertyPath;
                if (!nodeNames.Contains(node)) missingEntries.Add($"{who} = {node}");
                else
                {
                    if (!entries.TryGetValue(node, out var list)) entries[node] = list = new List<string>();
                    list.Add(who);
                }
            }
        }

        // Also explore every authored conversation node directly.
        var starts = new SortedSet<string>(entries.Keys);
        foreach (string n in nodeNames)
            if (n.StartsWith("Converse_") || n.StartsWith("Walk_") || n.StartsWith("Talk_") || n.StartsWith("Gate_") || n.StartsWith("Contest_"))
                starts.Add(n);

        // ---------------------------------------------------------------- 2. run them
        var commandsKnown = new HashSet<string> { "handoff", "walk_to", "stop_walk", "resume_walk", "wait" };
        var walkCommands = UnityEngine.Object.FindAnyObjectByType<YarnWalkCommands>();
        MethodInfo findPhil = typeof(YarnWalkCommands).GetMethod("TryFindPhilosopher", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo findPlace = typeof(YarnWalkCommands).GetMethod("FindPlace", BindingFlags.Instance | BindingFlags.NonPublic);

        var reached = new HashSet<string>();
        var problems = new SortedDictionary<string, HashSet<string>>(); // problem -> starts
        var handoffs = new SortedDictionary<string, string>();
        int totalRuns = 0;

        void Problem(string text, string start)
        {
            if (!problems.TryGetValue(text, out var s)) problems[text] = s = new HashSet<string>();
            s.Add(start);
        }

        foreach (string start in starts)
        {
            var tried = new Dictionary<string, HashSet<int>>();   // option-set key -> option IDs tried
            var sizes = new Dictionary<string, int>();
            for (int run = 0; run < MaxRunsPerStart; run++)
            {
                totalRuns++;
                bool madeNewChoice = false;
                var storage = new MemoryVariableStore();
                var dialogue = new Dialogue(storage);
                string current = start;
                string nextNode = null;          // handoff / walk_to arrival: the game starts this next
                int chain = 0;
                var runChoices = new Dictionary<string, HashSet<int>>();
                OptionSet pendingOptions = default;
                bool hasOptions = false;
                bool waiting = false, complete = false;

                dialogue.LogErrorMessage = m => Problem($"Yarn error in {dialogue.CurrentNode ?? current}: {m}", start);
                dialogue.LogDebugMessage = m => { };
                dialogue.LineHandler = l => { waiting = true; };
                dialogue.OptionsHandler = o => { pendingOptions = o; hasOptions = true; };
                dialogue.CommandHandler = c =>
                {
                    waiting = true;
                    string text = c.Text.Trim();
                    string[] parts = SplitCommand(text);
                    if (parts.Length == 0) return;
                    string name = parts[0];
                    string where = dialogue.CurrentNode ?? current;
                    if (!commandsKnown.Contains(name)) { Problem($"Unknown command <<{text}>> in {where}", start); return; }
                    if (walkCommands == null) return;
                    if (name == "handoff" && parts.Length > 1)
                    {
                        object[] args = { parts[1], null, null, null };
                        bool ok = (bool)findPhil.Invoke(walkCommands, args);
                        string entry = args[2] as string;
                        if (!ok) Problem($"<<handoff {parts[1]}>> in {where}: no philosopher with that name in the scene", start);
                        else
                        {
                            handoffs[$"{where} -> {parts[1]}"] = $"{((Transform)args[1]).name}, entry {entry}";
                            nextNode = entry;
                            if (!string.IsNullOrEmpty(entry) && !nodeNames.Contains(entry))
                                Problem($"<<handoff {parts[1]}>> in {where}: entry node {entry} does not exist", start);
                        }
                    }
                    if (name == "walk_to" && parts.Length > 1)
                    {
                        var place = findPlace.Invoke(walkCommands, new object[] { parts[1] }) as Transform;
                        if (place == null) Problem($"<<walk_to {parts[1]}>> in {where}: no place with that name in the scene", start);
                        if (parts.Length > 2 && !nodeNames.Contains(parts[2])) Problem($"<<walk_to {parts[1]} {parts[2]}>> in {where}: arrival node does not exist", start);
                        else if (parts.Length > 2) nextNode = parts[2];
                    }
                };
                dialogue.NodeStartHandler = n => { current = n; reached.Add(n); };
                dialogue.NodeCompleteHandler = n => { };
                dialogue.DialogueCompleteHandler = () => { complete = true; };
                dialogue.PrepareForLinesHandler = ids => { };

                try
                {
                    dialogue.SetProgram(program);
                    dialogue.SetNode(start);
                    int steps = 0;
                    dialogue.Continue();
                    while (steps++ < MaxStepsPerRun)
                    {
                        if (complete)
                        {
                            // The game starts the handed-off philosopher's entry node next, same variables.
                            if (nextNode == null || chain >= 6) break;
                            string n = nextNode;
                            nextNode = null;
                            chain++;
                            complete = false;
                            dialogue.SetNode(n);
                            dialogue.Continue();
                            continue;
                        }
                        if (hasOptions)
                        {
                            OptionSet set = pendingOptions;
                            hasOptions = false;
                            string key = (dialogue.CurrentNode ?? current) + "|" + OptionKey(set);
                            if (!tried.TryGetValue(key, out var done)) tried[key] = done = new HashSet<int>();
                            int pick = -1;
                            var available = new List<int>();
                            foreach (var o in set.Options) if (o.IsAvailable) available.Add(o.ID);
                            sizes[key] = available.Count;
                            if (available.Count == 0) { Problem($"Options in {dialogue.CurrentNode ?? current} with none available (dialogue stalls)", start); break; }
                            if (!runChoices.TryGetValue(key, out var mine)) runChoices[key] = mine = new HashSet<int>();
                            foreach (int id in available) if (!done.Contains(id)) { pick = id; break; }
                            if (pick >= 0) madeNewChoice = true;
                            else foreach (int id in available) if (!mine.Contains(id)) { pick = id; break; }
                            if (pick < 0) { Problem($"Every option at {dialogue.CurrentNode ?? current} has been tried in one run and it still comes back (real loop)", start); break; }
                            done.Add(pick);
                            mine.Add(pick);
                            dialogue.SetSelectedOption(pick);
                            dialogue.Continue();
                            continue;
                        }
                        if (waiting) { waiting = false; dialogue.Continue(); continue; }
                        if (!dialogue.IsActive) break;
                        dialogue.Continue();
                    }
                    if (steps >= MaxStepsPerRun) Problem($"Run from {start} did not end after {MaxStepsPerRun} steps (loop?) - last node {dialogue.CurrentNode ?? current}", start);
                }
                catch (Exception e)
                {
                    Problem($"Runtime error in {dialogue.CurrentNode ?? current}: {e.Message}", start);
                }

                // Stop once every option met on this start has been tried.
                bool allTried = true;
                foreach (var kv in tried) if (kv.Value.Count < sizes[kv.Key]) { allTried = false; break; }
                if (!madeNewChoice && allTried) break;
            }
        }

        // ---------------------------------------------------------------- 3. report
        sb.AppendLine($"[DialogueAudit] {nodeNames.Count} nodes, {entries.Count} entry nodes from the scene, {starts.Count} starts explored, {totalRuns} runs.");
        sb.AppendLine(missingEntries.Count == 0 ? "Entry points: all exist." : $"Entry points that do not exist ({missingEntries.Count}):");
        foreach (string m in missingEntries) sb.AppendLine("  MISSING " + m);
        sb.AppendLine(problems.Count == 0 ? "Runtime: no problems found." : $"Runtime problems ({problems.Count}):");
        foreach (var kv in problems)
        {
            var list = new List<string>(kv.Value);
            list.Sort();
            sb.AppendLine($"  {kv.Key}   [from: {string.Join(", ", list.GetRange(0, Math.Min(4, list.Count)))}{(list.Count > 4 ? ", ..." : "")}]");
        }
        sb.AppendLine("Handoffs resolved:");
        foreach (var kv in handoffs) sb.AppendLine($"  {kv.Key}  =>  {kv.Value}");
        var unreached = new List<string>();
        foreach (string n in nodeNames) if (!reached.Contains(n)) unreached.Add(n);
        unreached.Sort();
        sb.AppendLine($"Nodes never reached from any start ({unreached.Count}): {string.Join(", ", unreached)}");
        return sb.ToString();
    }

    private static string OptionKey(OptionSet set)
    {
        var sb = new StringBuilder();
        foreach (var o in set.Options) sb.Append(o.Line.ID).Append(';');
        return sb.ToString();
    }

    private static string[] SplitCommand(string text)
    {
        var parts = new List<string>();
        var cur = new StringBuilder();
        bool quoted = false;
        foreach (char c in text)
        {
            if (c == '"') { quoted = !quoted; continue; }
            if (c == ' ' && !quoted) { if (cur.Length > 0) { parts.Add(cur.ToString()); cur.Clear(); } continue; }
            cur.Append(c);
        }
        if (cur.Length > 0) parts.Add(cur.ToString());
        return parts.ToArray();
    }
}
