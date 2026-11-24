using UnityEngine;

/// A quick "boing" on one UI element: it grows past its size, dips under, and settles, with an optional
/// little twist. Scales around the element's visual middle, whatever its pivot, so a top-left-pivoted
/// text grows evenly instead of from its corner. Always ends exactly at rest.
/// Plain C#: a juice component owns one per element and calls Apply every frame.
public class UiPunch
{
    readonly RectTransform rect;
    readonly Vector2 restPosition;
    readonly Vector3 restScale;
    readonly Quaternion restRotation;

    float start = -1f;
    float size, twistDegrees, seconds;

    public UiPunch(RectTransform rect)
    {
        this.rect = rect;
        restPosition = rect.anchoredPosition;
        restScale = rect.localScale;
        restRotation = rect.localRotation;
    }

    public RectTransform Rect => rect;

    /// size 0.3 = grows to 130% at the peak. A new punch restarts from the top.
    public void Play(float size, float twistDegrees, float seconds)
    {
        start = Time.unscaledTime;
        this.size = size;
        this.twistDegrees = twistDegrees;
        this.seconds = Mathf.Max(0.01f, seconds);
    }

    public void Apply()
    {
        if (rect == null) return;

        float amount = 0f, twist = 0f;
        if (start >= 0f)
        {
            float t = (Time.unscaledTime - start) / seconds;
            if (t >= 1f) start = -1f;
            else
            {
                float wave = Mathf.Sin(t * Mathf.PI * 2.5f) * (1f - t) * (1f - t);   // up, a dip under, settle
                amount = wave * size;
                twist = wave * twistDegrees;
            }
        }
        SetPose(1f + amount, twist);
    }

    public void ReturnToRest()
    {
        start = -1f;
        SetPose(1f, 0f);
    }

    // Scale and turn about the rect's middle: move it so the middle stays put
    void SetPose(float scale, float twist)
    {
        Quaternion turn = Quaternion.Euler(0f, 0f, twist);
        Vector2 middleFromPivot = Vector2.Scale(new Vector2(0.5f, 0.5f) - rect.pivot, rect.rect.size);
        Vector3 scaledMiddle = turn * Vector3.Scale(middleFromPivot, restScale * scale);
        Vector3 restMiddle = Vector3.Scale(middleFromPivot, restScale);

        rect.localScale = restScale * scale;
        rect.localRotation = restRotation * turn;
        rect.anchoredPosition = restPosition + (Vector2)(restMiddle - scaledMiddle);
    }
}
