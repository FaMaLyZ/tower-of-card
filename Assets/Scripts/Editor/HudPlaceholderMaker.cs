using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Draws a plain placeholder for every sprite in Docs/HUD Art Assets.md, at the final name and size,
/// with the import settings the artist's art will keep (sprite, 2x pixels per unit, 9-slice borders).
///
/// It NEVER overwrites a file: once the artist's art is in Assets/Images/HUD, running it again only
/// fills in what is still missing. Menu: Tower of Cards > HUD > Make Missing Placeholders.
public static class HudPlaceholderMaker
{
    const string Folder = "Assets/Images/HUD";
    const int Scale = 2;                    // art is delivered at 2x the 1920 x 1080 size
    const float PixelsPerUnit = 100f * Scale;

    [MenuItem("Tower of Cards/HUD/Make Missing Placeholders")]
    public static void MakeMissing()
    {
        Directory.CreateDirectory(Folder);
        var made = new List<string>();

        foreach (PlaceholderSpec spec in HudPlaceholderList.All())
        {
            string path = $"{Folder}/{spec.FileName}.png";
            if (File.Exists(path)) continue;

            Texture2D texture = spec.Draw(Scale);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            made.Add(path);
        }

        AssetDatabase.Refresh();
        foreach (string path in made) ApplyImportSettings(path, FindSpec(path));

        Debug.Log($"HUD placeholders: made {made.Count}, kept every existing file.");
    }

    static PlaceholderSpec FindSpec(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        foreach (PlaceholderSpec spec in HudPlaceholderList.All())
            if (spec.FileName == name) return spec;
        return null;
    }

    static void ApplyImportSettings(string path, PlaceholderSpec spec)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null || spec == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = spec.Tiles ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;     // needed for sliced and tiled images
        importer.SetTextureSettings(settings);

        importer.spriteBorder = spec.Border * Scale;            // left, bottom, right, top
        importer.SaveAndReimport();
    }
}

// =====================================================================
//  The list: one entry per file in Docs/HUD Art Assets.md (sizes at 1x)
// =====================================================================

static class HudPlaceholderList
{
    // Colours from HUD Design §2.1
    static readonly Color Navy = HudTheme.Hex("22203F");
    static readonly Color NavyDark = HudTheme.Hex("191730");
    static readonly Color Well = HudTheme.Hex("100E22");
    static readonly Color Gold = HudTheme.Hex("D9A441");
    static readonly Color GoldLight = HudTheme.Hex("F2D58A");
    static readonly Color Silver = HudTheme.Hex("C9CED6");
    static readonly Color Salmon = HudTheme.Hex("FF9C8F");
    static readonly Color Cream = HudTheme.Hex("F4E6C8");
    static readonly Color Red = HudTheme.Hex("D8452F");
    static readonly Color Blue = HudTheme.Hex("2F6FD6");
    static readonly Color RoyalBlue = HudTheme.Hex("3148BD");
    static readonly Color ChipDark = HudTheme.Hex("1C1A33");
    static readonly Color GlassRed = HudTheme.Hex("D8322A");
    static readonly Color GlassDark = HudTheme.Hex("4A1512");
    static readonly Color Green = HudTheme.Hex("7DFFB6");
    static readonly Color SwapBlue = HudTheme.Hex("5B9CFF");
    static readonly Color Lacquer = HudTheme.Hex("B5352B");
    static readonly Color Grey = HudTheme.Hex("6B6878");
    static readonly Color White = Color.white;
    static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

    public static IEnumerable<PlaceholderSpec> All()
    {
        // ---------- §3 Shared ----------
        yield return Box("panel_frame", 340, 760, Navy, Gold, 2, top: 90, bottom: 14, border: new Vector4(24, 24, 24, 96), innerLine: true);
        yield return Spec("corner_curl", 48, 48, p => p.Ring(Gold, 3).Dot(Gold, 0.18f));
        yield return Box("well_gold", 300, 64, Well, Gold, 2, 10, 10, new Vector4(16, 16, 16, 16));
        yield return Box("well_silver", 140, 64, Well, Silver, 2, 10, 10, new Vector4(16, 16, 16, 16));
        yield return Box("well_salmon", 300, 48, Well, Salmon, 2, 10, 10, new Vector4(14, 14, 14, 14));
        foreach (var b in Buttons("button_red", Red)) yield return b;
        foreach (var b in Buttons("button_gold", Gold)) yield return b;
        foreach (var b in Buttons("button_navy", Navy)) yield return b;
        foreach (var b in Buttons("button_blue", Blue)) yield return b;
        yield return Box("pill", 64, 40, Well, Gold, 2, 20, 20, new Vector4(18, 18, 18, 18));
        yield return Box("marquee_frame", 300, 150, NavyDark, Gold, 8, 16, 16, new Vector4(28, 28, 28, 28));
        yield return Spec("marquee_bulb_on", 12, 12, p => p.Circle(White, Clear, 0));
        yield return Spec("marquee_bulb_off", 12, 12, p => p.Circle(Grey, Clear, 0));

        // ---------- §4 Left HUD ----------
        yield return Spec("hand_pip_lit", 28, 28, p => p.Circle(Gold, GoldLight, 2));
        yield return Spec("hand_pip_spent", 28, 28, p => p.Circle(Well, Gold, 2));
        yield return Spec("glass_star_whole", 72, 72, p => p.Star(GlassRed, Gold));
        yield return Spec("glass_star_cracked", 72, 72, p => p.Star(GlassDark, Gold));
        yield return Spec("glass_crack_line", 72, 72, p => p.Zigzag(White, 2));
        for (int i = 1; i <= 3; i++) { int v = i; yield return Spec($"glass_shard_{v}", 16, 16, p => p.Shard(GlassRed, v)); }

        // ---------- §5 Hand plaque ----------
        yield return Box("plaque_hand", 760, 120, RoyalBlue, Gold, 3, 18, 18, new Vector4(80, 24, 40, 24));
        yield return Spec("chip_dark", 72, 72, p => p.Chip(ChipDark));
        yield return Spec("chip_blue", 72, 72, p => p.Chip(Blue));
        yield return Spec("chip_red", 72, 72, p => p.Chip(Red));

        // ---------- §6 Right HUD and ladder ----------
        yield return Box("blind_plaque", 300, 150, White, Gold, 3, 24, 24, new Vector4(28, 28, 28, 28));
        yield return Spec("chip_icon", 28, 28, p => p.Chip(Gold));
        yield return Box("bar_frame", 280, 20, Well, Gold, 2, 8, 8, new Vector4(8, 8, 8, 8));
        yield return Box("bar_fill_green", 32, 14, Green, Clear, 0, 6, 6, new Vector4(6, 6, 6, 6));
        yield return Box("bar_fill_gold", 32, 14, Gold, Clear, 0, 6, 6, new Vector4(6, 6, 6, 6));
        yield return Spec("bar_fill_striped", 32, 14, p => p.Stripes(new Color(0.49f, 1f, 0.71f, 0.55f)), tiles: true);
        yield return Spec("broken_card_icon", 28, 36, p => p.CrackedCard(Red, White));
        for (int i = 1; i <= 3; i++) { int v = i; yield return Spec($"broken_fragment_{v}", 12, 12, p => p.Shard(Red, v)); }
        yield return Spec("ladder_line", 4, 64, p => p.Fill(Gold), tiles: true);
        yield return Spec("ladder_dot_lit", 22, 22, p => p.Circle(Gold, GoldLight, 2));
        yield return Spec("ladder_dot_unlit", 22, 22, p => p.Circle(Navy, Gold, 2));

        // ---------- §7 Cashier drawer ----------
        yield return Box("drawer_body", 340, 230, Lacquer, Gold, 3, 8, 40, Vector4.zero);
        yield return Box("drawer_plate", 120, 28, Gold, GoldLight, 2, 6, 6, Vector4.zero);
        yield return Box("receipt_strip", 300, 40, Cream, Clear, 0, 4, 4, new Vector4(12, 12, 12, 12));
        yield return Key("key_bank_normal", Gold, 0);
        yield return Key("key_bank_pressed", Gold, 5);
        yield return Key("key_ride_normal", Red, 0);
        yield return Key("key_ride_pressed", Red, 5);
        yield return Key("key_ride_disabled", Grey, 0);

        // ---------- §8 Deck ----------
        yield return Spec("swap_pip_lit", 20, 20, p => p.Circle(SwapBlue, White, 1));
        yield return Spec("swap_pip_spent", 20, 20, p => p.Circle(Well, SwapBlue, 2));

        // ---------- §10 Full-screen panels ----------
        yield return Spec("blind_chip_big", 140, 140, p => p.Chip(White));

        // ---------- §11 Glows and particles ----------
        yield return Spec("glow_soft", 128, 128, p => p.Glow(White));
        yield return Spec("chip_coin", 24, 24, p => p.Chip(Gold));

        // §9 card textures are 3D and sized to the current cards, so they are not made here.
    }

    // A raised button: bright face on a dark 3D edge. Pressed sits lower with almost no edge; disabled is grey.
    static IEnumerable<PlaceholderSpec> Buttons(string name, Color face)
    {
        var border = new Vector4(20, 20, 20, 20);
        yield return Spec(name + "_normal", 200, 56, p => p.Button(face, 6), border);
        yield return Spec(name + "_pressed", 200, 56, p => p.Button(face * 0.85f, 1), border);
        yield return Spec(name + "_disabled", 200, 56, p => p.Button(Grey, 2), border);
    }

    static PlaceholderSpec Key(string name, Color face, int pressDepth)
    {
        return Spec(name, 140, 70, p => p.Button(face, 8 - pressDepth));
    }

    static PlaceholderSpec Box(string name, int w, int h, Color fill, Color rim, int rimWidth,
                               int top, int bottom, Vector4 border, bool innerLine = false)
    {
        return Spec(name, w, h, p => p.RoundBox(fill, rim, rimWidth, top, bottom, innerLine), border);
    }

    static PlaceholderSpec Spec(string name, int w, int h, System.Action<PlaceholderPainter> draw,
                                Vector4 border = default, bool tiles = false)
    {
        return new PlaceholderSpec(name, w, h, draw, border, tiles);
    }
}

// =====================================================================
//  One file to make
// =====================================================================

class PlaceholderSpec
{
    public string FileName { get; }
    public Vector4 Border { get; }          // at 1x: left, bottom, right, top
    public bool Tiles { get; }

    readonly int width;
    readonly int height;
    readonly System.Action<PlaceholderPainter> draw;

    public PlaceholderSpec(string fileName, int width, int height, System.Action<PlaceholderPainter> draw, Vector4 border, bool tiles)
    {
        FileName = fileName;
        this.width = width;
        this.height = height;
        this.draw = draw;
        Border = border;
        Tiles = tiles;
    }

    public Texture2D Draw(int scale)
    {
        var painter = new PlaceholderPainter(width * scale, height * scale, scale);
        draw(painter);
        return painter.ToTexture();
    }
}

// =====================================================================
//  Simple shape drawing on a pixel grid (y = 0 is the bottom row)
// =====================================================================

class PlaceholderPainter
{
    readonly int w, h, scale;
    readonly Color[] pixels;

    public PlaceholderPainter(int width, int height, int scale)
    {
        w = width;
        h = height;
        this.scale = scale;
        pixels = new Color[w * h];
    }

    public Texture2D ToTexture()
    {
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    public PlaceholderPainter Fill(Color color)
    {
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        return this;
    }

    /// Rounded box with its own top and bottom corner radius (1x pixels), an outer rim and an optional inner line.
    public PlaceholderPainter RoundBox(Color fill, Color rim, int rimWidth, int topRadius, int bottomRadius, bool innerLine)
    {
        float rimPx = rimWidth * scale;
        ForEachPixel((x, y) =>
        {
            float d = RoundBoxDistance(x, y, 0f, topRadius * scale, bottomRadius * scale);
            if (d > 0.5f) return;
            Color c = fill;
            if (rimPx > 0f && d > -rimPx) c = rim;
            else if (innerLine && d < -4f * scale && d > -5f * scale) c = Color.Lerp(fill, rim, 0.45f);
            Blend(x, y, c, Coverage(d));
        });
        return this;
    }

    /// A raised button: a dark edge underneath and the face on top, lifted by "edge" 1x pixels.
    public PlaceholderPainter Button(Color face, int edge)
    {
        float lift = edge * scale;
        face.a = 1f;                        // a darkened colour (face * 0.85) would also lose opacity
        Color dark = face * 0.45f;
        dark.a = 1f;
        ForEachPixel((x, y) =>
        {
            float faceD = RoundBoxDistance(x, y, lift, 12 * scale, 12 * scale);
            float edgeD = RoundBoxDistance(x, y + lift, lift, 12 * scale, 12 * scale);
            if (edgeD <= 0.5f) Blend(x, y, dark, Coverage(edgeD));
            if (faceD <= 0.5f)
            {
                Color c = y > h - 6 * scale ? Color.Lerp(face, Color.white, 0.35f) : face;   // light top edge
                Blend(x, y, c, Coverage(faceD));
            }
        });
        return this;
    }

    public PlaceholderPainter Circle(Color fill, Color rim, int rimWidth)
    {
        float r = Mathf.Min(w, h) * 0.5f - 1f;
        ForEachPixel((x, y) =>
        {
            float d = Distance(x, y) - r;
            if (d > 0.5f) return;
            Blend(x, y, rimWidth > 0 && d > -rimWidth * scale ? rim : fill, Coverage(d));
        });
        return this;
    }

    public PlaceholderPainter Ring(Color color, int width)
    {
        float r = Mathf.Min(w, h) * 0.5f - 1f;
        ForEachPixel((x, y) =>
        {
            float d = Mathf.Abs(Distance(x, y) - (r - width * scale * 0.5f)) - width * scale * 0.5f;
            if (d <= 0.5f) Blend(x, y, color, Coverage(d));
        });
        return this;
    }

    public PlaceholderPainter Dot(Color color, float radiusShare)
    {
        float r = Mathf.Min(w, h) * radiusShare;
        ForEachPixel((x, y) =>
        {
            float d = Distance(x, y) - r;
            if (d <= 0.5f) Blend(x, y, color, Coverage(d));
        });
        return this;
    }

    /// A casino chip: coloured disc, white dashed edge, a darker inner ring. The middle stays empty for the number.
    public PlaceholderPainter Chip(Color color)
    {
        float r = Mathf.Min(w, h) * 0.5f - 1f;
        Color inner = Color.Lerp(color, Color.black, 0.25f);
        ForEachPixel((x, y) =>
        {
            float dist = Distance(x, y);
            float d = dist - r;
            if (d > 0.5f) return;
            Color c = color;
            bool inEdgeBand = dist > r * 0.78f;
            float angle = Mathf.Atan2(y + 0.5f - h * 0.5f, x + 0.5f - w * 0.5f);
            if (inEdgeBand && Mathf.Repeat(angle / (2f * Mathf.PI) * 16f, 1f) < 0.5f) c = Color.white;
            else if (dist > r * 0.62f && dist < r * 0.68f) c = inner;
            Blend(x, y, c, Coverage(d));
        });
        return this;
    }

    public PlaceholderPainter Star(Color fill, Color lead)
    {
        Vector2[] points = StarPoints(5, Mathf.Min(w, h) * 0.48f, Mathf.Min(w, h) * 0.22f);
        Vector2[] inner = StarPoints(5, Mathf.Min(w, h) * 0.40f, Mathf.Min(w, h) * 0.17f);
        ForEachPixel((x, y) =>
        {
            Vector2 p = new Vector2(x + 0.5f - w * 0.5f, y + 0.5f - h * 0.5f);
            if (!InPolygon(points, p)) return;
            Blend(x, y, InPolygon(inner, p) ? fill : lead, 1f);
        });
        return this;
    }

    public PlaceholderPainter Zigzag(Color color, int width)
    {
        var points = new[] { new Vector2(0.15f, 0.75f), new Vector2(0.4f, 0.55f), new Vector2(0.3f, 0.45f), new Vector2(0.6f, 0.3f), new Vector2(0.85f, 0.2f) };
        float half = width * scale * 0.5f;
        ForEachPixel((x, y) =>
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 a = new Vector2(points[i].x * w, points[i].y * h);
                Vector2 b = new Vector2(points[i + 1].x * w, points[i + 1].y * h);
                float d = SegmentDistance(p, a, b) - half;
                if (d <= 0.5f) { Blend(x, y, color, Coverage(d)); return; }
            }
        });
        return this;
    }

    /// A small triangle; the variant number turns it so the three pieces look different.
    public PlaceholderPainter Shard(Color color, int variant)
    {
        float turn = variant * 2.1f;
        var tri = new Vector2[3];
        for (int i = 0; i < 3; i++)
        {
            float a = turn + i * 2.3f + (i == 2 ? 0.6f : 0f);
            tri[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Min(w, h) * 0.46f;
        }
        ForEachPixel((x, y) =>
        {
            if (InPolygon(tri, new Vector2(x + 0.5f - w * 0.5f, y + 0.5f - h * 0.5f))) Blend(x, y, color, 1f);
        });
        return this;
    }

    public PlaceholderPainter Stripes(Color color)
    {
        ForEachPixel((x, y) =>
        {
            if (Mathf.Repeat((x + y) / (8f * scale), 1f) < 0.5f) Blend(x, y, color, 1f);
        });
        return this;
    }

    public PlaceholderPainter CrackedCard(Color back, Color crack)
    {
        RoundBox(back, Color.Lerp(back, Color.white, 0.4f), 1, 4, 4, false);
        ForEachPixel((x, y) =>
        {
            float d = SegmentDistance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(w * 0.2f, h * 0.85f), new Vector2(w * 0.8f, h * 0.15f)) - scale;
            if (d <= 0.5f && pixels[y * w + x].a > 0f) Blend(x, y, crack, Coverage(d));
        });
        return this;
    }

    /// Soft round glow: full in the middle, fading to nothing at the edge.
    public PlaceholderPainter Glow(Color color)
    {
        float r = Mathf.Min(w, h) * 0.5f;
        ForEachPixel((x, y) =>
        {
            float t = 1f - Mathf.Clamp01(Distance(x, y) / r);
            Color c = color;
            c.a = t * t;
            pixels[y * w + x] = c;
        });
        return this;
    }

    // ---------- helpers ----------

    void ForEachPixel(System.Action<int, int> action)
    {
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                action(x, y);
    }

    // Signed distance to a box filling the image (inset at the bottom by "bottomInset"), with separate corner radii
    float RoundBoxDistance(float x, float y, float bottomInset, float topRadius, float bottomRadius)
    {
        float boxH = h - bottomInset;
        float px = x + 0.5f - w * 0.5f;
        float py = y + 0.5f - bottomInset - boxH * 0.5f;
        float halfW = w * 0.5f, halfH = boxH * 0.5f;
        float r = Mathf.Min(py > 0f ? topRadius : bottomRadius, halfW, halfH);
        float qx = Mathf.Abs(px) - halfW + r;
        float qy = Mathf.Abs(py) - halfH + r;
        return Mathf.Min(Mathf.Max(qx, qy), 0f) + new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude - r;
    }

    float Distance(int x, int y)
    {
        return new Vector2(x + 0.5f - w * 0.5f, y + 0.5f - h * 0.5f).magnitude;
    }

    static float Coverage(float distance) => Mathf.Clamp01(0.5f - distance);

    static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).magnitude;
    }

    static Vector2[] StarPoints(int tips, float outer, float inner)
    {
        var points = new Vector2[tips * 2];
        for (int i = 0; i < points.Length; i++)
        {
            float a = Mathf.PI * 0.5f + i * Mathf.PI / tips;
            float r = i % 2 == 0 ? outer : inner;
            points[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        return points;
    }

    static bool InPolygon(Vector2[] poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        }
        return inside;
    }

    void Blend(int x, int y, Color color, float coverage)
    {
        if (coverage <= 0f) return;
        int i = y * w + x;
        Color under = pixels[i];
        float a = color.a * coverage;
        float outA = a + under.a * (1f - a);
        if (outA <= 0f) return;
        Color rgb = (color * a + under * under.a * (1f - a)) / outA;
        rgb.a = outA;
        pixels[i] = rgb;
    }
}
