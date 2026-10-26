
using System.Collections;
using TMPro;
using UnityEngine;

public class FinalScores : MonoBehaviour
{
    public ScoreManager scoreManager;
    public TMP_Text totalScoreText;

    public string totalScoreFormat = "Score: {0:N0}";

    [Header("Fade In Settings")]
    public CanvasGroup canvasGroup;
    public float fadeDuration = 1f;

    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;

            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(FadeIn());
        }
    }

    private void Update()
    {
        if (totalScoreText != null && scoreManager != null)
        {
            totalScoreText.text = string.Format(
                totalScoreFormat,
                scoreManager.TotalScore
            );
        }
    }

    private IEnumerator FadeIn()
    {
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            canvasGroup.alpha = Mathf.Clamp01(
                elapsed / fadeDuration
            );

            yield return null;
        }

        canvasGroup.alpha = 1f;
        fadeCoroutine = null;
    }
}
