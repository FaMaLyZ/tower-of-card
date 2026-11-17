using TMPro;
using UnityEngine;

/// The HUD's colours and fonts, in one place (HUD Design §2).
/// Views read these instead of hard-coding colours, so the look can be tuned without touching code.
[CreateAssetMenu(fileName = "HudTheme", menuName = "Tower of Cards/HUD Theme")]
public class HudTheme : ScriptableObject
{
    [Header("Fonts")]
    [Tooltip("Big titles and names: Small Blind, Wall, Won!")]
    public TMP_FontAsset titleFont;
    [Tooltip("Small labels: TURN SCORE, ANTE, Broken")]
    public TMP_FontAsset labelFont;
    [Tooltip("Numbers and button text")]
    public TMP_FontAsset numberFont;
    [Tooltip("Big numbers that need extra weight (Run Score, the Pot)")]
    public TMP_FontAsset numberBoldFont;

    [Header("Table and panels")]
    public Color tableBackground = Hex("0D0C1D");
    public Color feltGlow = Hex("1F4A46");
    public Color panelTop = Hex("22203F");
    public Color panelBottom = Hex("191730");
    public Color wellFill = Hex("100E22");

    [Header("Gold and cream")]
    public Color gold = Hex("D9A441");
    public Color goldLight = Hex("F2D58A");
    public Color goldText = Hex("FFE3A0");
    public Color cream = Hex("F4E6C8");
    public Color creamMuted = Hex("D8C49B");
    public Color creamDim = Hex("BFA77A");

    [Header("Score")]
    public Color baseChips = Hex("1C1A33");
    public Color cardChips = Hex("2F6FD6");
    public Color mult = Hex("D8452F");
    public Color plaqueTop = Hex("3148BD");
    public Color plaqueBottom = Hex("1D2A78");

    [Header("Good and bad")]
    public Color safe = Hex("7DFFB6");
    public Color safeDark = Hex("2FA66A");
    public Color danger = Hex("FF9C8F");

    [Header("Glass")]
    public Color glassWhole = Hex("D8322A");
    public Color glassCracked = Hex("4A1512");

    [Header("Selection")]
    public Color swapSelection = Hex("5B9CFF");
    public Color commitSelection = Hex("FFCF4D");

    [Header("Blinds")]
    public Color smallBlind = Hex("2F6FD6");
    public Color bigBlind = Hex("C8902F");
    public Color bossBlind = Hex("C8322B");

    /// The colour of a Small, Big or Boss blind.
    public Color GetBlindColor(BlindKind kind)
    {
        switch (kind)
        {
            case BlindKind.Small: return smallBlind;
            case BlindKind.Big:   return bigBlind;
            default:              return bossBlind;
        }
    }

    /// The ring colour of a picked hand card: blue while swapping, gold while committing.
    public Color GetSelectionColor(GamePhase phase)
    {
        return phase == GamePhase.Swap ? swapSelection : commitSelection;
    }

    // "D9A441" -> Color. Plain C#, so it is safe in field initialisers (Unity runs them while loading assets).
    public static Color Hex(string hex)
    {
        int value = System.Convert.ToInt32(hex, 16);
        return new Color32((byte)(value >> 16), (byte)(value >> 8), (byte)value, 255);
    }
}
