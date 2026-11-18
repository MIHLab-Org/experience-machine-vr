using UnityEngine;
using Yarn.Unity;

/// <summary>
/// The forest soundscape (2026-11-18). Creates itself when any scene loads, so nothing has to be
/// placed in a scene. Clips are loaded from Resources/MIHLabAudio:
///   Ambient/  forest ambience, looped quietly the whole time (talks included)
///   Footsteps/ grass footsteps, played at walking pace while the player is moving
///   Music/    deep-house tracks, shuffled; fades in while walking, fades out during conversations
///
/// "Walking" = the head moving horizontally faster than walkSpeed, smoothed, so it covers the
/// wrist-button walk, philosopher-led walks and walking in the room. Teleport jumps (more than
/// 2 m in one frame) are ignored.
///
/// Credits: nature sounds by Antoine Goumain (Nature Sounds Pack, Unity Asset Store,
/// https://antoinegoumain.fr/). Music: Pixabay tracks by arnaud136, Dimdel, Sunset House Grooves
/// and Tunetank (Pixabay Content License). See Docs/CREDITS.md.
/// </summary>
public class MIHLabSoundscape : MonoBehaviour
{
    [Range(0f, 1f)] public float ambientVolume = 0.45f;
    [Range(0f, 1f)] public float musicVolume = 0.22f;
    [Tooltip("Music level while standing still and not talking, as a fraction of Music Volume.")]
    [Range(0f, 1f)] public float musicIdleLevel = 0.5f;
    [Tooltip("Music level while walking AND a philosopher is chatting, as a fraction of Music Volume.")]
    [Range(0f, 1f)] public float musicChatLevel = 0.6f;
    [Range(0f, 1f)] public float footstepVolume = 0.35f;
    public float walkSpeed = 0.35f;
    public float stepLength = 0.75f;
    public float fadeSeconds = 2.5f;

    private AudioSource ambient, music, steps;
    private AudioClip[] musicClips, stepClips;
    private int musicIndex = -1;
    private DialogueRunner runner;
    private Transform head;
    private Vector3 lastHead;
    private bool hasLast;
    private float speed, walkingTimer, stillTimer, stepTimer;
    private bool walking;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (FindAnyObjectByType<MIHLabSoundscape>() != null) return;
        var go = new GameObject("MIHLab Soundscape");
        DontDestroyOnLoad(go);
        go.AddComponent<MIHLabSoundscape>();
    }

    private void Start()
    {
        ambient = NewSource(true);
        music = NewSource(false);
        steps = NewSource(false);

        AudioClip[] amb = Resources.LoadAll<AudioClip>("MIHLabAudio/Ambient");
        if (amb.Length > 0) { ambient.clip = amb[0]; ambient.volume = 0f; ambient.Play(); }
        stepClips = Resources.LoadAll<AudioClip>("MIHLabAudio/Footsteps");
        musicClips = Resources.LoadAll<AudioClip>("MIHLabAudio/Music");
        Shuffle(musicClips);
        music.volume = 0f;
        NextTrack();
    }

    private AudioSource NewSource(bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.loop = loop; s.playOnAwake = false; s.spatialBlend = 0f; s.dopplerLevel = 0f;
        return s;
    }

    private void NextTrack()
    {
        if (musicClips == null || musicClips.Length == 0) return;
        musicIndex = (musicIndex + 1) % musicClips.Length;
        music.clip = musicClips[musicIndex];
        music.Play();
    }

    private static void Shuffle(AudioClip[] a)
    {
        for (int i = a.Length - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (a[i], a[j]) = (a[j], a[i]); }
    }

    private void Update()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (runner == null) runner = FindAnyObjectByType<DialogueRunner>();
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;

        // Walking detection from horizontal head speed.
        if (head != null)
        {
            Vector3 p = head.position;
            if (hasLast)
            {
                Vector3 d = p - lastHead; d.y = 0f;
                float frameSpeed = d.magnitude > 2f ? 0f : d.magnitude / dt; // ignore teleports
                speed = Mathf.Lerp(speed, frameSpeed, 1f - Mathf.Exp(-4f * dt));
            }
            lastHead = p; hasLast = true;
        }
        if (speed > walkSpeed) { walkingTimer += dt; stillTimer = 0f; } else { stillTimer += dt; walkingTimer = 0f; }
        if (!walking && walkingTimer > 0.3f) walking = true;
        if (walking && stillTimer > 0.6f) walking = false;

        bool talking = runner != null && runner.IsDialogueRunning;
        bool chatting = talking && WalkingChatter.IsChatting;

        float musicTarget = walking ? (chatting ? musicChatLevel : 1f) : (talking ? 0f : musicIdleLevel);
        float rate = dt / Mathf.Max(0.1f, fadeSeconds);
        music.volume = Mathf.MoveTowards(music.volume, musicVolume * musicTarget, musicVolume * rate);
        ambient.volume = Mathf.MoveTowards(ambient.volume, ambientVolume, ambientVolume * rate);
        if (music.clip != null && !music.isPlaying && Application.isFocused) NextTrack();

        // Footsteps.
        if (walking && stepClips != null && stepClips.Length > 0)
        {
            stepTimer -= dt;
            if (stepTimer <= 0f)
            {
                steps.pitch = Random.Range(0.94f, 1.06f);
                steps.PlayOneShot(stepClips[Random.Range(0, stepClips.Length)], footstepVolume * Random.Range(0.8f, 1f));
                stepTimer = Mathf.Clamp(stepLength / Mathf.Max(speed, 0.1f), 0.42f, 0.85f);
            }
        }
        else stepTimer = 0.15f;
    }
}
