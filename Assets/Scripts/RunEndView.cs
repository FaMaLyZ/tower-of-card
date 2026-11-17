using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// The end screen of a run (HUD Design §10.4): Run cleared or Glass shattered, the Run Score in a
/// marquee sign, and how far the run got. Sits on the end panel that HudView opens.
public class RunEndView : MonoBehaviour
{
    [Header("Game references")]
    public GameFlowController flow;

    [Header("UI")]
    public TMP_Text titleText;
    public TMP_Text runScoreText;
    public TMP_Text detailText;
    [Tooltip("Optional: the old one-text result. Leave empty when the three texts above are used")]
    [FormerlySerializedAs("totalScoreText")]
    public TMP_Text resultText;

    [Header("Texts")]
    [Tooltip("{0} = cleared / shattered label, {1} = Run Total, {2} = Ante reached, {3} = Antes per run, {4} = last blind")]
    [TextArea(1, 4)]
    public string resultFormat = "{0}\nRun total {1:N0}\nAnte {2} / {3}, {4} blind";
    public string clearedLabel = "Run cleared";
    public string shatteredLabel = "Glass shattered";
    public string runScoreFormat = "{0:N0}";
    [Tooltip("{0} = Ante reached, {1} = Antes per run, {2} = last blind, {3} = Glass lives left, {4} = max lives")]
    public string detailFormat = "Ante {0} / {1} · {2} blind · Glass {3} / {4}";

    [Header("Fade In Settings")]
    public CanvasGroup canvasGroup;
    public float fadeDuration = 1f;

    Coroutine fadeCoroutine;

    void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        RefreshResult();
        StartFadeIn();
    }

    void Update()
    {
        RefreshResult();
    }

    void RefreshResult()
    {
        if (flow == null || !flow.IsRunOver) return;

        string label = flow.RunOutcome == RunOutcome.Cleared ? clearedLabel : shatteredLabel;
        SetText(titleText, label);
        SetText(runScoreText, string.Format(runScoreFormat, flow.RunTotal));
        SetText(detailText, string.Format(detailFormat, flow.AnteNumber, flow.AntesPerRun, flow.LastBlindKind, flow.GlassLives, flow.GlassMaxLives));
        SetText(resultText, string.Format(resultFormat, label, flow.RunTotal, flow.AnteNumber, flow.AntesPerRun, flow.LastBlindKind));
    }

    static void SetText(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
    }

    void StartFadeIn()
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = 0f;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        fadeCoroutine = null;
    }
}
