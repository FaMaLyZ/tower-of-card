
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SparkleLoop : MonoBehaviour
{
    [Header("Rotation")]
    public Vector3 rotationSpeed = new Vector3(0f, 0f, 90f);

    [Header("Twinkle")]
    public float twinkleSpeed = 3f;
    public float minScale = 0.7f;
    public float maxScale = 1.2f;

    private Vector3 originalScale;
    private float randomOffset;

    void Start()
    {
        originalScale = transform.localScale;
        randomOffset = Random.Range(0f, 10f);
    }

    void Update()
    {
        transform.Rotate(
            rotationSpeed * Time.deltaTime,
            Space.Self
        );

        float t = (Mathf.Sin(
            Time.time * twinkleSpeed + randomOffset
        ) + 1f) / 2f;

        float scale = Mathf.Lerp(minScale, maxScale, t);

        transform.localScale = originalScale * scale;
    }
}
