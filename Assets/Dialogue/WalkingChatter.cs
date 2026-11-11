using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using Yarn.Unity;

/// <summary>
/// Walking dialogue: filler conversation while a philosopher walks the player somewhere
/// (backlog W6). Unlike Walking Nodes (checkpoints), which stop the walk and wait for the
/// player, chatter plays WHILE the player keeps walking:
///
///   - When a walk leg has been under way for Start Delay seconds, it looks for a Yarn node
///     named  Walk_FROM_TO  (then  Walk_FROM ) and runs it once per leg.
///       FROM = the walking philosopher, TO = where the walk is heading.
///       Names are shortened the same way as the Talk_ nodes: the bracketed part is dropped,
///       names with "of" are joined ("Gregory of Nyssa" -> GregoryOfNyssa), otherwise the last
///       word is used ("Michael Heim (lead)" -> Heim, "John Chrysostom" -> Chrysostom).
///       Places use their GameObject name with spaces and punctuation removed.
///       If no node exists, the names it looked for are logged once, so you know what to call it.
///   - Lines advance on their own: Min Seconds Per Line, or Seconds Per Word times the line's
///     length if longer. The timer only runs while the player is walking, so stopping pauses
///     the chatter. Continue (or Space in the Editor) skips ahead.
///   - If a chatter node offers options, it waits for the player to choose (keep them rare).
///   - The walk does not treat chatter as dialogue: the philosopher and WalkAlongMover keep
///     moving, the Stop button stays available, and checkpoints wait until it ends.
///   - When the walk arrives, any chatter still running is cut short so the destination's own
///     dialogue starts (PhilosopherWalker.RunNode calls Interrupt).
///
/// Chatter nodes should use only spoken lines and  <<jump>>  to other Walk_ nodes: no walk
/// commands, no handoffs. Tag them  tags: walk  so they are easy to find.
///
/// Setup: add this component to the "Dialogue System" object (next to the DialogueRunner).
/// </summary>
[DisallowMultipleComponent]
public class WalkingChatter : MonoBehaviour
{
    [SerializeField] private DialogueRunner dialogueRunner;
    [Tooltip("Prefix of chatter node names.")]
    [SerializeField] private string nodePrefix = "Walk_";
    [Tooltip("Seconds of walking on a leg before chatter starts.")]
    [SerializeField] private float startDelay = 2f;
    [Tooltip("Shortest time a line stays up, in seconds of walking.")]
    [SerializeField] private float minSecondsPerLine = 4.5f;
    [Tooltip("Reading time per word, in seconds.")]
    [SerializeField] private float secondsPerWord = 0.4f;
    [Tooltip("Player speed (m/s) that counts as walking.")]
    [SerializeField] private float walkingSpeed = 0.25f;
    [Tooltip("Pause the line timer while the player stands still.")]
    [SerializeField] private bool pauseWhenStopped = true;

    private static WalkingChatter instance;

    /// <summary>True while a chatter node is running. Walk systems treat this as "not dialogue".</summary>
    public static bool IsChatting =>
        instance != null && instance.chatting && instance.dialogueRunner != null && instance.dialogueRunner.IsDialogueRunning;

    /// <summary>Cuts any running chatter short (e.g. on arrival). Safe to call any time.</summary>
    public static void Interrupt()
    {
        if (instance != null) instance.StopChatter();
    }

    private readonly List<string> playedLegs = new List<string>();
    private readonly List<string> loggedMissing = new List<string>();
    private bool chatting;
    private string legKey;
    private float walkTimer;
    private float lineTimer;
    private float lineDuration;
    private string lastLine;
    private Vector3 lastPlayerPos;
    private bool hasLastPos;
    private float optionScan;
    private bool optionsShowing;
    private Object linePresenter;
    private FieldInfo lineTextField;

    private void Awake()
    {
        instance = this;
        if (dialogueRunner == null) dialogueRunner = FindAnyObjectByType<DialogueRunner>();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (dialogueRunner == null) return;
        if (chatting && !dialogueRunner.IsDialogueRunning) chatting = false;

        PhilosopherWalker walker = PhilosopherWalker.Active;
        float speed = PlayerSpeed(walker);

        if (!chatting)
        {
            bool onLeg = walker != null && walker.isActiveAndEnabled && !walker.WaitingForDialogue &&
                         (walker.State == PhilosopherWalker.WalkState.Walking ||
                          walker.State == PhilosopherWalker.WalkState.HandingOff) &&
                         walker.Goal != null;
            if (!onLeg || dialogueRunner.IsDialogueRunning) { walkTimer = 0f; legKey = null; return; }

            string from = Key(walker.Id);
            string to = Key(walker.Goal.name);
            string key = from + "_" + to;
            if (key != legKey) { legKey = key; walkTimer = 0f; }
            if (speed >= walkingSpeed) walkTimer += Time.deltaTime;
            if (walkTimer < startDelay || playedLegs.Contains(key)) return;

            playedLegs.Add(key);
            string node = FindNode(from, to);
            if (node == null) return;

            chatting = true;
            lastLine = null;
            lineTimer = 0f;
            _ = dialogueRunner.StartDialogue(node);
            return;
        }

        // Chatting: advance lines on a timer while the player walks.
        if (OptionsShowing()) return;

        string line = CurrentLineText();
        if (line != lastLine)
        {
            lastLine = line;
            lineTimer = 0f;
            lineDuration = Mathf.Max(minSecondsPerLine, WordCount(line) * secondsPerWord);
        }
        if (string.IsNullOrEmpty(line)) return;

        if (!pauseWhenStopped || speed >= walkingSpeed) lineTimer += Time.deltaTime;
        if (lineTimer >= lineDuration)
        {
            lineTimer = 0f;
            lastLine = null; // the next line (even an identical one) gets a fresh timer
            dialogueRunner.RequestNextLine();
        }
    }

    private void StopChatter()
    {
        if (chatting && dialogueRunner != null && dialogueRunner.IsDialogueRunning) _ = dialogueRunner.Stop();
        chatting = false;
    }

    private string FindNode(string from, string to)
    {
        string specific = nodePrefix + from + "_" + to;
        string general = nodePrefix + from;
        if (NodeExists(specific)) return specific;
        if (NodeExists(general)) return general;
        if (!loggedMissing.Contains(specific))
        {
            loggedMissing.Add(specific);
            Debug.Log($"[WalkingChatter] No chatter for this walk. Add a Yarn node named {specific} (or {general}).", this);
        }
        return null;
    }

    private bool NodeExists(string node)
    {
        var project = dialogueRunner.YarnProject;
        return project != null && System.Array.IndexOf(project.NodeNames, node) >= 0;
    }

    /// <summary>Short Yarn-safe name: "Michael Heim (lead)" -> Heim, "Gregory of Nyssa" -> GregoryOfNyssa.</summary>
    public static string Key(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        int bracket = name.IndexOf('(');
        if (bracket >= 0) name = name.Substring(0, bracket);
        string[] words = name.Trim().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return "";

        bool joined = false;
        foreach (string w in words) if (w == "of") joined = true;

        var sb = new StringBuilder();
        if (joined)
        {
            foreach (string w in words) sb.Append(Clean(char.ToUpperInvariant(w[0]) + w.Substring(1)));
        }
        else
        {
            sb.Append(Clean(words[words.Length - 1]));
        }
        return sb.ToString();
    }

    private static string Clean(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s) if (char.IsLetterOrDigit(c)) sb.Append(c);
        return sb.ToString();
    }

    private float PlayerSpeed(PhilosopherWalker walker)
    {
        Transform p = walker != null ? walker.Player : null;
        if (p == null) { hasLastPos = false; return 0f; }
        Vector3 pos = p.position;
        float speed = 0f;
        if (hasLastPos && Time.deltaTime > 0f)
        {
            Vector3 d = pos - lastPlayerPos;
            d.y = 0f;
            speed = d.magnitude / Time.deltaTime;
        }
        lastPlayerPos = pos;
        hasLastPos = true;
        return speed;
    }

    private bool OptionsShowing()
    {
        optionScan -= Time.unscaledDeltaTime;
        if (optionScan > 0f) return optionsShowing;
        optionScan = 0.2f;
        optionsShowing = false;
        foreach (var item in FindObjectsByType<OptionItem>(FindObjectsInactive.Exclude))
        {
            if (item.isActiveAndEnabled) { optionsShowing = true; break; }
        }
        return optionsShowing;
    }

    private string CurrentLineText()
    {
        if (linePresenter == null)
        {
            linePresenter = FindAnyObjectByType<LinePresenter>();
            lineTextField = linePresenter != null ? linePresenter.GetType().GetField("lineText") : null;
        }
        if (lineTextField == null) return null;
        object textObject = lineTextField.GetValue(linePresenter);
        if (textObject == null) return null;
        PropertyInfo textProperty = textObject.GetType().GetProperty("text");
        return textProperty != null ? textProperty.GetValue(textObject) as string : null;
    }

    private static int WordCount(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        return s.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
