using System;
using UnityEngine;

[RequireComponent(typeof(Renderer), typeof(Rigidbody))]
public class Card : MonoBehaviour
{
    [Header("Break on hard impact")]
    [Tooltip("Destroy this card when it hits ANYTHING fast enough.")]
    public bool breakOnImpact = true;
    [Tooltip("Impact speed (m/s) needed to destroy the card. Lower = more fragile.")]
    public float breakSpeed = 4f;
    [Tooltip("Print every impact speed to the Console (helps you tune Break Speed).")]
    public bool logImpactSpeed = false;

    // Raised just before a card breaks (TowerBuilder listens so it can forget the card)
    public static event Action<Card> Broken;

    public CardData Data { get; private set; }

    Renderer rend;
    Collider col;
    Rigidbody rb;
    bool isBroken;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        col = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();

        // thin cards can pass through each other when they fall fast
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.mass = UnityEngine.Random.Range(1,11);
    }

    public void Setup(CardData data)
    {
        Data = data;
        rend.material.color = data.tint;   // works with URP Lit (_BaseColor)
    }

    // ---------- Visual / physics states ----------

    // Ghost = preview card following the mouse. Green tint = snapped, red = no slot nearby.
    public void SetGhost(bool snapped)
    {
        if (col != null) col.enabled = false;   // ghost has no collision

        rb.isKinematic = true;                  // ghost must not fall
        rb.useGravity = false;

        rend.material.color = snapped
            ? Color.Lerp(Data.tint, Color.green, 0.35f)
            : Color.Lerp(Data.tint, Color.red, 0.6f);
    }

    // Card is now part of the tower: solid, but frozen in place until physics is applied
    public void SetPlaced()
    {
        if (col != null) col.enabled = true;

        rb.isKinematic = false;
        rb.useGravity = true;
        Freeze();

        rend.material.color = Data.tint;
    }

    // Locks position and rotation (used while the player is building)
    public void Freeze()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.constraints = RigidbodyConstraints.FreezeAll;
    }

    // Releases the card so gravity and collisions move it (used when the turn ends)
    public void Unfreeze()
    {
        rb.constraints = RigidbodyConstraints.None;
        rb.WakeUp();
    }

    // ---------- Breaking ----------

    void OnCollisionEnter(Collision collision)
    {
        if (!breakOnImpact || isBroken) return;
        if (IsFrozenOrGhost()) return;                 // only break while physics is active

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (logImpactSpeed)
            Debug.Log(name + " hit " + collision.collider.name + " at " + impactSpeed.ToString("F1") +
                      " m/s (break at " + breakSpeed + ")");

        if (impactSpeed >= breakSpeed) Break();
    }

    bool IsFrozenOrGhost()
    {
        return rb.isKinematic || rb.constraints == RigidbodyConstraints.FreezeAll;
    }

    // Destroys the card. Add effects / sounds here.
    public void Break()
    {
        if (isBroken) return;
        isBroken = true;

        Broken?.Invoke(this);
        Destroy(gameObject);
    }
}