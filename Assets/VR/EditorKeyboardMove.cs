#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// EDITOR ONLY: keyboard walking for testing the VR scene without a headset. Never compiled into
/// a build, so the Quest version stays hands-only.
///
///   W A S D        walk (relative to where the headset camera looks)
///   Left / Right   turn
///   Shift          walk faster
///
/// No setup: after each scene loads it attaches itself to the camera rig (Meta's OVRCameraRig,
/// or the object tagged Player) and keeps the rig on the NavMesh or the ground. If the rig has a
/// CharacterController it moves through it. While WalkAlongMover is carrying the player it
/// stays out of the way unless a key is pressed.
/// </summary>
public class EditorKeyboardMove : MonoBehaviour
{
    private const float WalkSpeed = 2.0f;
    private const float FastSpeed = 5.0f;
    private const float TurnSpeed = 90f; // degrees per second

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        Attach();
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Attach();

    private static void Attach()
    {
        Transform rig = FindRig();
        if (rig == null || rig.GetComponent<EditorKeyboardMove>() != null) return;
        rig.gameObject.AddComponent<EditorKeyboardMove>();
        Debug.Log($"[EditorKeyboardMove] Editor only: WASD to walk, Left/Right to turn, Shift to go faster (moving '{rig.name}').", rig);
    }

    private static Transform FindRig()
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type t = asm.GetType("OVRCameraRig", false);
            if (t == null) continue;
            var c = FindAnyObjectByType(t, FindObjectsInactive.Exclude) as Component;
            if (c == null) break;
            for (Transform p = c.transform; p != null; p = p.parent)
            {
                if (p.GetComponent<CharacterController>() != null) return p;
            }
            return c.transform;
        }
        GameObject player = GameObject.FindWithTag("Player");
        return player != null ? player.transform : null;
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        float turn = (kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed ? 1f : 0f);
        if (turn != 0f) transform.Rotate(0f, turn * TurnSpeed * Time.deltaTime, 0f, Space.World);

        Vector2 input = new Vector2(
            (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
            (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f));
        if (input.sqrMagnitude < 0.01f) return;

        Transform view = Camera.main != null ? Camera.main.transform : transform;
        Vector3 forward = view.forward; forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        float speed = kb.shiftKey.isPressed ? FastSpeed : WalkSpeed;
        Vector3 step = (forward * input.y + right * input.x).normalized * speed * Time.deltaTime;
        Vector3 target = Ground(transform.position + step);

        if (TryGetComponent(out CharacterController cc) && cc.enabled) cc.Move(target - transform.position);
        else transform.position = target;
    }

    private Vector3 Ground(Vector3 p)
    {
        if (NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas)) return hit.position;
        // Ray from above (terrain colliders are one-sided), skipping the rig's own colliders.
        float best = float.MaxValue;
        Vector3 result = p;
        foreach (RaycastHit rh in Physics.RaycastAll(p + Vector3.up * 50f, Vector3.down, 100f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (rh.collider.transform.IsChildOf(transform)) continue;
            if (rh.distance < best) { best = rh.distance; result = rh.point; }
        }
        return result;
    }
}
#endif
