using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// The end screen of a run: Cleared or Shattered, the Run Total, and how far the run got.
/// Sits on the end panel that HudView opens.
public class RunEndView : MonoBehaviour
{
    [Header("Game references")]
    public GameFlowController flow;

    [Header("UI")]
    [FormerlySerializedAs("totalScoreText")]
    public TMP_Text resultText;

    [Header("Texts")]
    [Tooltip("{0} = cleared / shattered label, {1} = Run Total, {2} = Ante reached, {3} = Antes per run, {4} = last blind")]
    [TextArea(1, 4)]
    public string resultFormat = "{0}\nRun total {1:N0}\nAnte {2} / {3}, {4} blind";
    public string clearedLabel = "RUN CLEARED";
    public string shatteredLabel = "GLASS SHATTERED";

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
        if (resultText == null || flow == null || !flow.IsRunOver) return;

        string label = flow.RunOutcome == RunOutcome.Cleared ? clearedLabel : shatteredLabel;
        resultText.text = string.Format(resultFormat, label, flow.RunTotal, flow.AnteNumber, flow.AntesPerRun, flow.LastBlindKind);
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
