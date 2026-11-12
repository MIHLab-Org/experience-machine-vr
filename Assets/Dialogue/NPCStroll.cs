using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Peripatos: philosophers stroll slowly around their spot instead of standing still.
/// Only the children (Body, Label, ...) move; the root object, its colliders, walker and
/// talk logic stay at home. When the player comes within Stop Radius the body walks back
/// home and stands, so greeting, Talk buttons and PhilosopherWalker work exactly as before.
/// While a PhilosopherWalker on this object is doing anything but Idle, the body snaps home.
/// Runs after PhilosopherWalker (execution order 100) so its walk bool wins while strolling.
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public class NPCStroll : MonoBehaviour
{
    [Tooltip("How far from home the body may wander (m).")]
    public float wanderRadius = 3f;
    [Tooltip("Strolling speed (m/s). The walk clip looks right at about 0.6-1.0.")]
    public float speed = 0.7f;
    [Tooltip("Speed when walking back home because the player is coming (m/s).")]
    public float returnSpeed = 1.2f;
    [Tooltip("Player closer than this (m): go home and stand.")]
    public float stopRadius = 8f;
    [Tooltip("Seconds to pause at each turn (random between x and y). 0,0 = never stop.")]
    public Vector2 pause = new Vector2(0.2f, 1.2f);
    public float turnSpeed = 5f;
    public string walkBoolParam = "IsWalking";

    private readonly List<Transform> kids = new List<Transform>();
    private readonly List<Vector3> kidLocal = new List<Vector3>();
    private Transform body;
    private Quaternion bodyLocalRot;
    private Animator animator;
    private PhilosopherWalker walker;
    private Transform player;
    private Vector3 offset;          // world-space horizontal offset from home
    private Vector3 target;          // world-space horizontal offset we walk to
    private float homeGroundY;
    private float wait;
    private bool hasNav;

    private void Start()
    {
        walker = GetComponent<PhilosopherWalker>();
        foreach (Transform c in transform) { kids.Add(c); kidLocal.Add(c.localPosition); }
        body = transform.Find("Body");
        if (body != null) { bodyLocalRot = body.localRotation; animator = body.GetComponent<Animator>(); }
        hasNav = NavMesh.SamplePosition(transform.position, out NavMeshHit h, 2f, NavMesh.AllAreas);
        homeGroundY = hasNav ? h.position.y : transform.position.y;
        target = Vector3.zero;
        wait = Random.Range(0f, 2f);
        if (body == null || animator == null) enabled = false;
    }

    private Transform Player()
    {
        if (player != null && player.gameObject.activeInHierarchy) return player;
        if (walker != null && walker.Player != null && walker.Player.gameObject.activeInHierarchy) player = walker.Player;
        else if (Camera.main != null) player = Camera.main.transform;
        return player;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        bool walkerBusy = walker != null && (walker.State != PhilosopherWalker.WalkState.Idle || PhilosopherWalker.Active == walker);
        if (walkerBusy)
        {
            if (offset != Vector3.zero) { offset = Vector3.zero; target = Vector3.zero; Place(); body.localRotation = bodyLocalRot; }
            return; // the walker owns the animator
        }

        Transform p = Player();
        bool playerNear = p != null && Flat(p.position - transform.position).magnitude < stopRadius;
        bool moving = false;

        if (!hasNav)
        {
            // nowhere to walk: just stand
        }
        else if (playerNear)
        {
            target = Vector3.zero;
            moving = Step(returnSpeed, dt);
        }
        else if (wait > 0f)
        {
            wait -= dt;
        }
        else
        {
            moving = Step(speed, dt);
            if (!moving) { PickTarget(); wait = Random.Range(pause.x, pause.y); }
        }

        Place();

        if (moving)
        {
            Vector3 dir = Flat(target - offset);
            if (dir.sqrMagnitude > 0.0001f)
                body.rotation = Quaternion.Slerp(body.rotation, Quaternion.LookRotation(dir.normalized, Vector3.up), dt * turnSpeed);
        }
        else if (offset.sqrMagnitude < 0.0004f)
        {
            body.localRotation = Quaternion.Slerp(body.localRotation, bodyLocalRot, dt * turnSpeed);
        }

        if (!string.IsNullOrEmpty(walkBoolParam)) animator.SetBool(walkBoolParam, moving);
    }

    // Moves offset toward target; false when there.
    private bool Step(float v, float dt)
    {
        Vector3 d = target - offset;
        if (d.magnitude < 0.05f) { offset = target; return false; }
        offset += d.normalized * Mathf.Min(d.magnitude, v * dt);
        return true;
    }

    private void PickTarget()
    {
        Vector3 home = transform.position;
        for (int i = 0; i < 6; i++)
        {
            Vector2 r = Random.insideUnitCircle * wanderRadius;
            Vector3 want = home + new Vector3(r.x, 0f, r.y);
            if (!NavMesh.SamplePosition(want, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;
            Vector3 from = home + offset;
            if (NavMesh.SamplePosition(from, out NavMeshHit fromHit, 1.5f, NavMesh.AllAreas) &&
                NavMesh.Raycast(fromHit.position, hit.position, out _, NavMesh.AllAreas)) continue; // something in the way
            Vector3 o = Flat(hit.position - home);
            if (o.magnitude > wanderRadius + 0.5f) continue;
            target = o;
            return;
        }
        target = Vector3.zero; // could not find a clear spot: go home
    }

    private void Place()
    {
        float y = 0f;
        if (offset.sqrMagnitude > 0.0001f && NavMesh.SamplePosition(transform.position + offset, out NavMeshHit h, 2f, NavMesh.AllAreas))
            y = h.position.y - homeGroundY;
        Vector3 shift = offset + Vector3.up * y;
        for (int i = 0; i < kids.Count; i++)
        {
            if (kids[i] == null) continue;
            kids[i].position = transform.TransformPoint(kidLocal[i]) + shift;
        }
    }

    private void OnDisable()
    {
        offset = Vector3.zero;
        if (kids.Count > 0) Place();
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
