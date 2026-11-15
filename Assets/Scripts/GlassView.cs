using TMPro;
using UnityEngine;

/// Shows the Glass (the run's lives), e.g. "Glass  ##-", and says what just changed.
///
/// A view: it reads the lives from the conductor every frame and listens to GlassChanged only for
/// the short "cracked / repaired" note. It never cracks or repairs anything itself.
/// Every field is optional.
public class GlassView : MonoBehaviour
{
    [Header("Game references (auto-found if empty)")]
    public GameFlowController flow;

    [Header("UI")]
    public TMP_Text livesText;
    [Tooltip("Optional: a short note after a crack or repair")]
    public TMP_Text changeText;

    [Header("Texts")]
    [Tooltip("{0} = the life marks, {1} = lives left, {2} = max lives")]
    public string livesFormat = "Glass {0}  ({1} / {2})";
    public string wholeMark = "#";
    public string crackedMark = "-";
    public string crackedNote = "The Glass cracked!";
    public string repairedNote = "The Glass was repaired";
    [Min(0f)] public float noteSeconds = 2.5f;

    float noteHideTime;

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
        if (changeText != null) changeText.raycastTarget = false;
        if (livesText != null) livesText.raycastTarget = false;
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
        RefreshLives();
        RefreshNote();
    }

    void RefreshLives()
    {
        if (livesText == null || flow == null || !flow.IsReady || flow.Phase == GamePhase.None) return;
        livesText.text = string.Format(livesFormat, BuildMarks(), flow.GlassLives, flow.GlassMaxLives);
    }

    // Whole marks first, then cracked ones: "##-" = 2 of 3 lives left
    string BuildMarks()
    {
        int whole = Mathf.Max(0, flow.GlassLives);
        int cracked = Mathf.Max(0, flow.GlassMaxLives - whole);
        return Repeat(wholeMark, whole) + Repeat(crackedMark, cracked);
    }

    static string Repeat(string mark, int count)
    {
        var builder = new System.Text.StringBuilder();
        for (int i = 0; i < count; i++) builder.Append(mark);
        return builder.ToString();
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
