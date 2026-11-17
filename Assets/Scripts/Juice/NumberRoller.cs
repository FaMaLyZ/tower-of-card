using TMPro;
using UnityEngine;

/// Makes a number text count to its new value instead of jumping (52 → 312), then punch as it lands.
/// It can be held: the old number stays up until something (a flying "+260") arrives, then it counts.
/// Views call NumberRoller.Show(text, value, format, ...); with no active NumberRoller on that text, that simply
/// writes the text, so the value is always right without juice. The counted number is {0} in the format;
/// any extra values (a target, a preview) fill {1}, {2}... and are shown as they are.
[RequireComponent(typeof(TMP_Text))]
public class NumberRoller : MonoBehaviour
{
    [Tooltip("How long one count takes")]
    public float rollSeconds = 0.6f;
    [Tooltip("Size of the punch when the count lands (0.3 = 130% at the peak)")]
    public float punchSize = 0.35f;
    public float punchSeconds = 0.3f;

    // Not kept across a script reload in Play mode: the roller then starts fresh with the next number it is given
    [System.NonSerialized] TMP_Text text;
    [System.NonSerialized] UiPunch punch;
    [System.NonSerialized] string format = "{0:N0}";
    [System.NonSerialized] object[] args = new object[1];
    [System.NonSerialized] bool hasValue;
    [System.NonSerialized] bool held;
    [System.NonSerialized] long shown, target, rollFrom;
    [System.NonSerialized] float rollStart = -1f;

    /// Shows a number on a text: counts there if the text has an active NumberRoller, otherwise writes it.
    /// Safe to call every frame.
    public static void Show(TMP_Text text, long value, string format, params object[] extra)
    {
        if (text == null) return;
        NumberRoller roller = text.GetComponent<NumberRoller>();
        if (roller != null && roller.isActiveAndEnabled) roller.SetTarget(value, format, extra);
        else text.text = string.Format(format, Combine(value, extra, null));
    }

    /// The number a text is showing right now: mid-count if it has a NumberRoller, else `actual`.
    /// Lets a bar fill along with its counting number.
    public static long Shown(TMP_Text text, long actual)
    {
        NumberRoller roller = text != null ? text.GetComponent<NumberRoller>() : null;
        return roller != null && roller.isActiveAndEnabled && roller.hasValue ? roller.shown : actual;
    }

    void Awake() => EnsureParts();

    void EnsureParts()
    {
        if (text == null) text = GetComponent<TMP_Text>();
        if (punch == null && text != null) punch = new UiPunch(text.rectTransform);
    }

    /// Keep showing the current number until Release, even if the value changes meanwhile
    public void Hold() => held = true;

    /// Count to the latest value now
    public void Release()
    {
        held = false;
        if (hasValue && shown != target) StartRoll();
    }

    /// A punch on its own (the debug panel uses it)
    public void Punch() => punch?.Play(punchSize, 0f, punchSeconds);

    void SetTarget(long value, string newFormat, object[] extra)
    {
        EnsureParts();
        format = newFormat;
        args = Combine(value, extra, args);

        if (!hasValue)                                  // the first number just appears
        {
            hasValue = true;
            shown = target = value;
            Write();
            return;
        }

        if (value == target) return;
        target = value;
        if (!held) StartRoll();
    }

    void StartRoll()
    {
        rollFrom = shown;
        rollStart = Time.unscaledTime;
    }

    // After the view's Update, so this frame's number is the counted one
    void LateUpdate()
    {
        if (!hasValue || punch == null) return;
        if (rollStart >= 0f) StepRoll();
        Write();
        punch.Apply();
    }

    // Fast at first, slowing into the final number
    void StepRoll()
    {
        float t = (Time.unscaledTime - rollStart) / Mathf.Max(0.01f, rollSeconds);
        if (t >= 1f)
        {
            shown = target;
            rollStart = -1f;
            Punch();
            return;
        }

        float eased = 1f - (1f - t) * (1f - t) * (1f - t);
        shown = rollFrom + (long)System.Math.Round((target - rollFrom) * (double)eased);
    }

    void Write()
    {
        args[0] = shown;
        text.text = string.Format(format, args);
    }

    // { value, extra... }, reusing the old array when it is the right size
    static object[] Combine(long value, object[] extra, object[] reuse)
    {
        int length = 1 + (extra != null ? extra.Length : 0);
        object[] all = reuse != null && reuse.Length == length ? reuse : new object[length];
        all[0] = value;
        for (int i = 1; i < length; i++) all[i] = extra[i - 1];
        return all;
    }

    // Switched off: show the real number at once and stop holding
    void OnDisable()
    {
        held = false;
        rollStart = -1f;
        if (!hasValue || text == null) return;
        shown = target;
        Write();
        punch?.ReturnToRest();
    }
}
