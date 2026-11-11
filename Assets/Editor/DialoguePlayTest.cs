using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Yarn.Unity;

/// <summary>
/// Play-mode checks for the dialogue (results in .claude-runs/ and the Console):
///   MIHLab > Play test > Talk buttons    press every philosopher's Talk button in turn and
///                                         check which node starts (run at the start of Play).
///   MIHLab > Play test > Contest route   plays a whole contest route by itself at 4x speed:
///                                         answers Socrates, follows the handoffs, walks, runs
///                                         the quiz, takes the slice ending and the walk back.
///                                         Set Answer below ("know" = the "I don't know" route).
/// Editor only; it only presses the same buttons a player would.
/// </summary>
public static class DialoguePlayTest
{
    public static string Answer = "know";
    private static readonly StringBuilder log = new StringBuilder();
    private static double nextTick;
    private static double startTime;
    private static string lastNode;
    private static readonly Dictionary<string, int> attempts = new Dictionary<string, int>();
    private static List<HandTalkPrompt> prompts;
    private static int promptIndex;
    private static int phase;
    private static string expected;
    private static string mode;

    [MenuItem("MIHLab/Play test/Talk buttons")]
    public static void StartTalkButtons()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[PlayTest] Enter Play mode first."); return; }
        Reset("talk");
        prompts = new List<HandTalkPrompt>(Object.FindObjectsByType<HandTalkPrompt>(FindObjectsInactive.Include));
        prompts.Sort((a, b) => (a.GetComponentInParent<PhilosopherWalker>() != null).CompareTo(b.GetComponentInParent<PhilosopherWalker>() != null));
        EditorApplication.update += TickTalk;
    }

    [MenuItem("MIHLab/Play test/Contest route")]
    public static void StartRoute()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[PlayTest] Enter Play mode first."); return; }
        Reset("route");
        Time.timeScale = 4f;
        Application.runInBackground = true;
        EditorApplication.update += TickRoute;
    }

    public static bool Running => mode != null;

    private static void Reset(string m)
    {
        log.Clear();
        attempts.Clear();
        lastNode = null;
        promptIndex = 0;
        phase = 0;
        mode = m;
        startTime = EditorApplication.timeSinceStartup;
        nextTick = startTime;
        Log($"start {m}");
    }

    private static void Finish(string why)
    {
        Log("end: " + why);
        EditorApplication.update -= TickTalk;
        EditorApplication.update -= TickRoute;
        Time.timeScale = 1f;
        Directory.CreateDirectory(".claude-runs");
        File.WriteAllText($".claude-runs/playtest-{mode}.txt", log.ToString());
        Debug.Log("[PlayTest]\n" + log);
        mode = null;
    }

    private static void Log(string s)
    {
        log.AppendLine($"{EditorApplication.timeSinceStartup - startTime,6:F1}s  {s}");
    }

    private static DialogueRunner Runner => Object.FindAnyObjectByType<DialogueRunner>();

    private static string CurrentNode()
    {
        var r = Runner;
        return r != null && r.IsDialogueRunning ? r.Dialogue.CurrentNode : null;
    }

    // ---------------------------------------------------------------- Talk buttons

    private static void TickTalk()
    {
        if (!EditorApplication.isPlaying) { Finish("left Play"); return; }
        if (EditorApplication.timeSinceStartup < nextTick) return;
        nextTick = EditorApplication.timeSinceStartup + 0.4;
        var runner = Runner;

        if (phase == 0)
        {
            if (runner.IsDialogueRunning) { _ = runner.Stop(); return; }
            if (promptIndex >= prompts.Count) { Finish("all buttons pressed"); return; }
            HandTalkPrompt p = prompts[promptIndex];
            var npc = p.GetComponentInParent<PhilosopherNPC>();
            var walker = p.GetComponentInParent<PhilosopherWalker>();
            expected = walker != null && walker.isActiveAndEnabled ? walker.JoinNode : StartNodeOf(npc);
            string state = walker != null ? $" walker state {walker.State}, CanStartWalk {walker.CanStartWalk}" : $" CanTalk {(npc != null && npc.CanTalk)}";
            p.Talk();
            phase = 1;
            Log($"pressed {p.name}:{state}");
            return;
        }

        if (phase >= 1)
        {
            string node = CurrentNode();
            HandTalkPrompt p = prompts[promptIndex];
            if (node == null && phase < 6) { phase++; return; } // walkers stand up first
            Log(node == null ? $"  FAIL {p.name}: nothing started (expected {expected})"
                : node == expected ? $"  ok   {p.name}: {node}"
                : $"  ?    {p.name}: started {node}, expected {expected}");
            if (runner.IsDialogueRunning) _ = runner.Stop();
            promptIndex++;
            phase = 0;
        }
    }

    private static string StartNodeOf(PhilosopherNPC npc)
    {
        if (npc == null) return "?";
        var so = new SerializedObject(npc);
        var p = so.FindProperty("startNode");
        return p != null ? p.stringValue : "?";
    }

    // ---------------------------------------------------------------- Contest route

    private static void TickRoute()
    {
        if (!EditorApplication.isPlaying) { Finish("left Play"); return; }
        if (EditorApplication.timeSinceStartup < nextTick) return;
        nextTick = EditorApplication.timeSinceStartup + 0.15;
        if (EditorApplication.timeSinceStartup - startTime > 600) { Finish("timed out after 10 minutes"); return; }

        var runner = Runner;
        string node = CurrentNode();
        if (node != lastNode)
        {
            var w = PhilosopherWalker.Active;
            Transform head = Camera.main != null ? Camera.main.transform : null;
            Log(node == null
                ? $"(no dialogue)  walker {(w != null ? w.Id + " " + w.State + " -> " + (w.Goal != null ? w.Goal.name : "-") : "none")}"
                : $"node {node}{(WalkingChatter.IsChatting ? "  [chatter]" : "")}  player at {(head != null ? head.position.ToString("F0") : "?")}");
            lastNode = node;
            if (node == "Contest_Return") phase = 1;
        }
        if (phase == 1 && node == null)
        {
            // Back with Socrates and handed on again: one more node proves the loop closes.
            phase = 2;
        }
        if (phase == 2 && node != null && node.StartsWith("Talk_") && node != "Talk_Socrates") { Finish("route complete (back with Socrates and handed on again)"); return; }
        if (!runner.IsDialogueRunning) return;
        if (WalkingChatter.IsChatting) return; // let it advance itself

        var options = Object.FindObjectsByType<OptionItem>(FindObjectsInactive.Exclude);
        var live = new List<OptionItem>();
        foreach (var o in options) if (o.isActiveAndEnabled && o.interactable) live.Add(o);
        if (live.Count > 0)
        {
            live.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            OptionItem pick = null;
            if (node == "Contest_Question" || node == "Contest_Return")
                foreach (var o in live) if (Text(o).ToLowerInvariant().Contains(Answer)) pick = o;
            if (node == "Slice_End")
                foreach (var o in live) if (Text(o).Contains("Socrates")) pick = o;
            if (pick == null)
            {
                string key = node + "|" + live.Count;
                attempts.TryGetValue(key, out int n);
                attempts[key] = n + 1;
                pick = live[n % live.Count];
            }
            Log($"  choose \"{Text(pick)}\"");
            pick.InvokeOptionSelected();
            nextTick = EditorApplication.timeSinceStartup + 0.4;
            return;
        }

        var panel = Object.FindAnyObjectByType<WorldSpaceDialoguePanel>();
        if (panel != null) panel.Continue();
    }

    private static string Text(OptionItem o)
    {
        foreach (var t in o.GetComponentsInChildren<TMPro.TMP_Text>(true)) if (!string.IsNullOrEmpty(t.text)) return t.text.Trim();
        return o.name;
    }
}
