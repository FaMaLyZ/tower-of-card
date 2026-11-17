using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// Small helpers for building HUD objects from editor scripts, so every area (H3-H7) is built the same way:
/// a root that the layout places, a "Visual" child that effects animate, and art and texts inside it.
/// Positions are in 1920 x 1080 canvas pixels. Every created object is registered with Undo.
public static class HudBuildKit
{
    public const string SpriteFolder = "Assets/Images/HUD";
    public const string ThemePath = "Assets/ScriptableObject/HudTheme.asset";

    public static HudTheme LoadTheme() => AssetDatabase.LoadAssetAtPath<HudTheme>(ThemePath);

    public static Sprite LoadSprite(string name)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{name}.png");
        if (sprite == null) Debug.LogWarning($"HudBuildKit: sprite '{name}' not found in {SpriteFolder}");
        return sprite;
    }

    // ---------- Rect transforms ----------

    /// A child placed from the parent's top centre: x to the right, y downward (positive y = further down).
    public static RectTransform TopCenter(string name, Transform parent, float x, float y, float width, float height)
    {
        return Place(name, parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, -y), new Vector2(width, height));
    }

    /// A child placed from the parent's top-left corner: x to the right, y downward.
    public static RectTransform TopLeft(string name, Transform parent, float x, float y, float width, float height)
    {
        return Place(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, height));
    }

    /// A child centred on a point measured from the parent's centre (y upward, Unity's usual way).
    public static RectTransform Center(string name, Transform parent, float x, float y, float width, float height)
    {
        return Place(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(width, height));
    }

    /// A child that fills its parent.
    public static RectTransform Fill(string name, Transform parent)
    {
        RectTransform rect = NewRect(name, parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static RectTransform Place(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        RectTransform rect = NewRect(name, parent);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    /// The "Visual" child every element has: it fills the root, and effects move it instead of the root or the art.
    public static RectTransform Visual(Transform root) => Fill("Visual", root);

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Build HUD");
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // ---------- Art ----------

    /// An image that fills its parent. Sliced if the sprite has stretch borders.
    public static Image FillImage(string name, Transform parent, string spriteName)
    {
        return Art(Fill(name, parent), spriteName);
    }

    public static Image Art(RectTransform rect, string spriteName)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = LoadSprite(spriteName);
        image.type = image.sprite != null && image.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.preserveAspect = image.type == Image.Type.Simple;
        image.raycastTarget = false;            // displays never block clicks; buttons switch this back on
        return image;
    }

    // ---------- Text ----------

    public static TextMeshProUGUI Text(RectTransform rect, TMP_FontAsset font, float size, Color color,
                                       TextAlignmentOptions alignment, string text)
    {
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.text = text;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        return label;
    }

    /// A text that fills its parent.
    public static TextMeshProUGUI FillText(string name, Transform parent, TMP_FontAsset font, float size, Color color,
                                           TextAlignmentOptions alignment, string text)
    {
        return Text(Fill(name, parent), font, size, color, alignment, text);
    }

    // ---------- Buttons ----------

    /// A raised button: normal / pressed / disabled sprites from "{spriteBase}_normal" etc., with a label.
    public static Button RaisedButton(RectTransform root, string spriteBase, TMP_FontAsset font, float fontSize,
                                      Color textColor, string label)
    {
        Image image = Art(root, spriteBase + "_normal");
        image.raycastTarget = true;

        var button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = image.sprite,
            pressedSprite = LoadSprite(spriteBase + "_pressed"),
            selectedSprite = image.sprite,
            disabledSprite = LoadSprite(spriteBase + "_disabled")
        };

        TextMeshProUGUI text = FillText("Label", root, font, fontSize, textColor, TextAlignmentOptions.Center, label);
        text.rectTransform.offsetMin = new Vector2(0f, 6f);     // sit on the raised face, above the 3D edge
        return button;
    }
}
