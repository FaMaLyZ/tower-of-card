using System;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEditor.Animations;


/// How a 3D card model is fitted into the game. Lives on TowerBuilder.
[Serializable]
public class CardModelSettings
{
    [Header("Card size in the tower (world units)")]
    public float length = 1.5f;        // long side - the card stands on this
    public float width = 1.07f;        // short side (depth of the tower)
    public float thickness = 0.02f;    // physics thickness

    [Header("Physics")]
    [Tooltip("Multiplies every card's mass (CardData > Mass). Handy for making the whole tower heavier or lighter.")]
    public float massMultiplier = 1f;

    [Header("Model orientation")]
    [Tooltip("Detects how the model was exported (longest side = length, thinnest = thickness).")]
    public bool autoOrientModel = true;
    [Tooltip("Used when Auto Orient is off. Use multiples of 90.")]
    public Vector3 manualEuler = Vector3.zero;
    [Tooltip("Turn this on if the card BACK is showing instead of the face.")]
    public bool flipFace = false;
}

/// Card root prefab = empty-ish object with Rigidbody + BoxCollider + this script.
/// The 3D model from CardData is spawned as a child and fitted to the size above.
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class Card : MonoBehaviour
{
    [Header("Break on hard impact")]
    [Tooltip("Destroy this card when it hits ANYTHING fast enough.")]
    public bool breakOnImpact = true;
    [Tooltip("Impact speed (m/s) needed to destroy the card. Lower = more fragile.")]
    public float breakSpeed = 4f;
    [Tooltip("Print every impact speed to the Console (helps you tune Break Speed).")]
    public bool logImpactSpeed = false;

    [Header("Card Break")]
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject cardBreakPre;
    private readonly int isBreakHash = Animator.StringToHash("Break");

    // Raised just before a card breaks (TowerBuilder listens so it can forget the card)
    public static event Action<Card> Broken;

    public CardData Data { get; private set; }

    BoxCollider box;
    Rigidbody rb;
    Renderer ownRenderer;       // the root's own mesh (e.g. the prefab's cube) - fallback look
    Renderer[] renderers;
    bool hasModel;
    bool isBroken;


    void Awake()
    {
        box = GetComponent<BoxCollider>();
        rb = GetComponent<Rigidbody>();
        ownRenderer = GetComponent<Renderer>();
        renderers = GetComponentsInChildren<Renderer>(true);

        // thin cards can pass through each other when they fall fast
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    // ---------- Setup ----------

    public void Setup(CardData data, CardModelSettings settings)
    {
        Data = data;
        hasModel = data.model != null;

        rb.mass = Mathf.Max(0.001f, data.mass * settings.massMultiplier);   // each card has its own mass

        if (hasModel) SetupModel(data.model, settings);
        else SetupFallbackBox(data, settings);

        renderers = GetComponentsInChildren<Renderer>(true);
    }

    // No model assigned: show a plain coloured box of the right size
    void SetupFallbackBox(CardData data, CardModelSettings s)
    {
        transform.localScale = new Vector3(s.thickness, s.length, s.width);
        box.center = Vector3.zero;
        box.size = Vector3.one;

        if (ownRenderer != null)
        {
            ownRenderer.enabled = true;
            ownRenderer.material.color = data.tint;
        }
    }

    // Spawn the 3D model as a child and fit it: upright, correct size, centred
    void SetupModel(GameObject modelPrefab, CardModelSettings s)
    {
        transform.localScale = Vector3.one;
        box.center = Vector3.zero;
        box.size = new Vector3(s.thickness, s.length, s.width);
        if (ownRenderer != null) ownRenderer.enabled = false;   // hide the placeholder cube

        GameObject m = Instantiate(modelPrefab, transform);
        m.transform.localPosition = Vector3.zero;
        m.transform.localRotation = Quaternion.identity;
        m.transform.localScale = Vector3.one;

        // the model must not bring its own physics
        foreach (var c in m.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (var r in m.GetComponentsInChildren<Rigidbody>(true)) Destroy(r);

        Bounds b = CardModelUtil.GetLocalBounds(m.transform);
        if (b.size == Vector3.zero) return;                     // model has no mesh

        Quaternion fit = s.autoOrientModel
            ? CardModelUtil.GetOrientation(b.size)
            : Quaternion.Euler(s.manualEuler);
        if (s.flipFace) fit = Quaternion.Euler(0f, 180f, 0f) * fit;

        // size of the model after rotating it upright
        Vector3 rotated = fit * b.size;
        float length = Mathf.Abs(rotated.y);
        float scale = length > 0.0001f ? s.length / length : 1f;

        m.transform.localRotation = fit;
        m.transform.localScale = Vector3.one * scale;
        m.transform.localPosition = -(fit * (b.center * scale));   // put the model's centre at the card's centre
    }

    // ---------- Visual / physics states ----------

    Color BaseColor => hasModel ? Color.white : Data.tint;

    // Ghost = preview card following the mouse. Green tint = snapped, red = no slot nearby.
    public void SetGhost(bool snapped)
    {
        box.enabled = false;                    // ghost has no collision

        rb.isKinematic = true;                  // ghost must not fall
        rb.useGravity = false;

        SetTint(snapped
            ? Color.Lerp(BaseColor, Color.green, 0.35f)
            : Color.Lerp(BaseColor, Color.red, 0.6f));
    }

    // Card is now part of the tower: solid, but frozen in place until physics is applied
    public void SetPlaced()
    {
        box.enabled = true;

        rb.isKinematic = false;
        rb.useGravity = true;
        Freeze();

        ClearTint();
    }

    // Tint without touching the models' materials (works with URP Lit and Standard shaders)
    void SetTint(Color color)
    {
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);

        foreach (var r in renderers)
            if (r != null) r.SetPropertyBlock(block);
    }

    void ClearTint()
    {
        foreach (var r in renderers)
            if (r != null) r.SetPropertyBlock(null);
    }

    // Locks position and rotation (used while the player is building)
    public void Freeze()
    {
        animator.SetInteger("Shake" ,0);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.constraints = RigidbodyConstraints.FreezeAll;
    }

    // Releases the card so gravity and collisions move it (used when the turn ends)
    public void Unfreeze()
    {
        rb.constraints = RigidbodyConstraints.None;
        rb.WakeUp();
        animator.SetInteger("Shake" , UnityEngine.Random.Range(1,4));
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
        StartCoroutine(BreakCoroutine());
    }
    IEnumerator BreakCoroutine()
    {
        //play anim and sound
        animator.SetBool(isBreakHash , true);
        AudioManager.Instance.PlayOneShotSFX(SoundType.CardBreak);
        Destroy(gameObject);
        Instantiate(cardBreakPre,transform.position, transform.rotation);
        yield return new WaitForSeconds(1.5f);
    }
}