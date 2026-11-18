using UnityEngine;

/// One HUD block running in from its side of the screen: it starts just off screen, leaning with its top
/// trailing behind (as if dragged in), slides in, overshoots its place a little and snaps back upright.
/// Turns around the block's own middle, whatever its pivot. Always ends exactly at rest.
/// Plain C#: RunIntroJuice owns one per block and calls Apply every frame.
public class HudEntrance
{
    readonly RectTransform rect;
    readonly Vector2 restPosition;
    readonly Quaternion restRotation;
    readonly Vector2 from;              // which way it comes from: (-1, 0) left, (1, 0) right, (0, 1) top

    public HudEntrance(RectTransform rect, Vector2 from)
    {
        this.rect = rect;
        this.from = from;
        restPosition = rect.anchoredPosition;
        restRotation = rect.localRotation;
    }

    /// Where it is at progress t (0 = off screen, 1 = in place). distance in canvas pixels past its own size.
    /// overshoot: how far past its place it goes, as a share of the trip. lean: the tilt it starts with, degrees.
    public void Apply(float t, float extraDistance, float overshoot, float lean)
    {
        if (rect == null) return;
        t = Mathf.Clamp01(t);

        // Slides in, passes its place by a little, settles back (an "ease out back")
        float travel = TravelDistance(extraDistance);
        float s = overshoot * 10f;
        float u = t - 1f;
        float arrived = 1f + (s + 1f) * u * u * u + s * u * u;
        Vector2 offset = from * travel * (1f - arrived);

        // Leans with its top trailing the way it moves, swings a little past upright, then stands straight
        float tilt = lean * Mathf.Cos(t * Mathf.PI * 1.5f) * (1f - t) * from.x;

        rect.anchoredPosition = restPosition + offset + KeepMiddleStill(tilt);
        rect.localRotation = restRotation * Quaternion.Euler(0f, 0f, tilt);
    }

    public void ReturnToRest()
    {
        if (rect == null) return;
        rect.anchoredPosition = restPosition;
        rect.localRotation = restRotation;
    }

    // Far enough that the whole block starts off screen: its own size along the way it comes, plus a margin
    float TravelDistance(float extraDistance)
    {
        Vector2 size = rect.rect.size;
        return Mathf.Abs(from.x) * size.x + Mathf.Abs(from.y) * size.y + extraDistance;
    }

    // Turning around the pivot would swing a corner-pinned block off; shift it so its middle stays put
    Vector2 KeepMiddleStill(float degrees)
    {
        if (degrees == 0f) return Vector2.zero;
        Vector2 middleFromPivot = Vector2.Scale(new Vector2(0.5f, 0.5f) - rect.pivot, rect.rect.size);
        Vector3 middle = Vector3.Scale(middleFromPivot, rect.localScale);
        return middle - Quaternion.Euler(0f, 0f, degrees) * middle;
    }
}
