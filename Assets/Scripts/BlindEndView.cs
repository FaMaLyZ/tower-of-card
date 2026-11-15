using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// The end screen of a blind: won or failed, and the Safe Points against the target.
///
/// A view: it only reads the conductor and shows the result. It sits on the end panel, which
/// GameUI turns on when the blind is over. The panel's Play Again button restarts the game.
/// Replaces FinalScores (A3c), which showed the old running total.
public class BlindEndView : MonoBehaviour
{
    [Header("Game references (auto-found if empty)")]
    public GameFlowController flow;

    [Header("UI")]
    [FormerlySerializedAs("totalScoreText")]
    public TMP_Text resultText;

    [Header("Texts")]
    [Tooltip("{0} = won / failed label, {1} = Safe Points, {2} = target")]
    public string resultFormat = "{0}\n{1:N0} / {2:N0}";
    public string wonLabel = "BLIND WON";
    public string failedLabel = "BLIND FAILED";

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
        if (resultText == null || flow == null || !flow.IsBlindOver) return;

        string label = flow.LastBlindResult == BlindResult.Won ? wonLabel : failedLabel;
        resultText.text = string.Format(resultFormat, label, flow.SafePoints, flow.Target);
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
