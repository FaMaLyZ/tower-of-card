using TMPro;
using UnityEngine;

/// Shows a short note when the Glass cracks or is repaired. The stars themselves are GlassStarsView.
/// A view: it never cracks or repairs anything itself.
public class GlassView : MonoBehaviour
{
    [Header("Game references")]
    public GameFlowController flow;

    [Header("UI")]
    public TMP_Text changeText;

    [Header("Texts")]
    public string crackedNote = "The Glass cracked!";
    public string repairedNote = "The Glass was repaired";
    [Min(0f)] public float noteSeconds = 2.5f;

    float noteHideTime;

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
        if (changeText != null) changeText.raycastTarget = false;
    }

    void OnEnable()
    {
        GameEvents.GlassChanged += OnGlassChanged;
    }

    void OnDisable()
    {
        GameEvents.GlassChanged -= OnGlassChanged;
    }

    void Update()
    {
        RefreshNote();
    }

    void RefreshNote()
    {
        if (changeText == null) return;
        changeText.gameObject.SetActive(Time.time < noteHideTime);
    }

    void OnGlassChanged(int oldLives, int newLives, GlassChangeReason reason)
    {
        if (changeText == null) return;
        changeText.text = newLives < oldLives ? crackedNote : repairedNote;
        noteHideTime = Time.time + noteSeconds;
    }
}
