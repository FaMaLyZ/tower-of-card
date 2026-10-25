using System;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(Renderer), typeof(Rigidbody))]
public class Card : MonoBehaviour
{
    [Header("Break on hard impact")]
    public bool breakOnImpact = true;
    public float breakSpeed = 4f;
    public bool logImpactSpeed = false;

    [Header("Socre and weight")]
    public float socre = 0;
    public float weight = 0;

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
    }

    public void Setup(CardData data)
    {
        Data = data;
        socre = data.score;
        weight = data.weight;
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