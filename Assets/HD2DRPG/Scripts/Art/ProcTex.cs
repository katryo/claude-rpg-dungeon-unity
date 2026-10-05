using System.Collections.Generic;
using UnityEngine;

namespace HD2DRPG
{
    /// <summary>Procedurally generated pixel-art environment textures and soft FX textures.</summary>
    public static class ProcTex
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        static Texture2D New(string name, int w, int h, bool point = true, bool repeat = true)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = name,
                filterMode = point ? FilterMode.Point : FilterMode.Bilinear,
                wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                anisoLevel = point ? 0 : 1
            };
            return t;
        }

        static Texture2D Cached(string key, System.Func<Texture2D> make)
        {
            if (cache.TryGetValue(key, out var t) && t != null) return t;
            t = make();
            cache[key] = t;
            return t;
        }

        static Color Shade(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 982451653;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        /// <summary>Large flagstone floor, 2x2 tiles per texture (texture repeats every 2 world units).</summary>
        public static Texture2D Floor => Cached("floor", () =>
        {
            const int S = 48;
            var t = New("floor", S, S);
            var baseC = new Color(0.36f, 0.33f, 0.38f);
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int tx = x % 24, ty = y % 24;
                    int tileId = (x / 24) + (y / 24) * 2;
                    float tileShade = 0.9f + 0.18f * Hash(tileId, 7, 3);
                    float n = Hash(x, y, 11) * 0.12f + Hash(x / 3, y / 3, 5) * 0.1f;
                    Color c = Shade(baseC, tileShade + n - 0.08f);
                    if (tx == 0 || ty == 0) c = Shade(baseC, 0.45f);
                    else if (tx == 1 || ty == 23) c = Shade(baseC, tileShade * 1.15f);
                    else if (tx == 23 || ty == 1) c = Shade(baseC, tileShade * 0.72f);
                    // cracks
                    if (Hash(x, y, 99) > 0.985f) c = Shade(baseC, 0.5f);
                    px[y * S + x] = c;
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        });

        /// <summary>Castle wall bricks (texture repeats every 1 world unit).</summary>
        public static Texture2D Wall => Cached("wall", () =>
        {
            const int S = 24;
            var t = New("wall", S, S);
            var baseC = new Color(0.30f, 0.28f, 0.34f);
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int row = y / 6;
                    int off = (row % 2) * 6;
                    int bx = (x + off) % 12, by = y % 6;
                    int brickId = ((x + off) / 12) * 13 + row * 7;
                    float shade = 0.85f + 0.25f * Hash(brickId, row, 2);
                    float n = Hash(x, y, 17) * 0.1f;
                    Color c = Shade(baseC, shade + n);
                    if (bx == 0 || by == 0) c = Shade(baseC, 0.42f);
                    else if (by == 5) c = Shade(baseC, shade * 1.18f);
                    else if (bx == 11) c = Shade(baseC, shade * 0.75f);
                    if (Hash(x, y, 51) > 0.97f) c = Color.Lerp(c, new Color(0.25f, 0.32f, 0.22f), 0.5f); // moss
                    px[y * S + x] = c;
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        });

        /// <summary>Dark trim stone for wall tops, plinths and stairs.</summary>
        public static Texture2D Trim => Cached("trim", () =>
        {
            const int S = 24;
            var t = New("trim", S, S);
            var baseC = new Color(0.22f, 0.2f, 0.26f);
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Hash(x, y, 23) * 0.15f;
                    Color c = Shade(baseC, 0.95f + n);
                    if (y == 0) c = Shade(baseC, 0.5f);
                    if (y == S - 1 || y == S - 2) c = Shade(baseC, 1.35f);
                    if (y == 12) c = new Color(0.55f, 0.42f, 0.2f);
                    px[y * S + x] = c;
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        });

        /// <summary>Fluted pillar texture; u wraps around, v repeats per unit.</summary>
        public static Texture2D Pillar => Cached("pillar", () =>
        {
            const int W = 24, H = 24;
            var t = New("pillar", W, H);
            var baseC = new Color(0.46f, 0.43f, 0.48f);
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float flute = (x % 6) switch { 0 => 0.6f, 1 => 0.85f, 2 => 1.05f, 3 => 1.1f, 4 => 1f, _ => 0.8f };
                    float n = Hash(x, y, 31) * 0.1f;
                    px[y * W + x] = Shade(baseC, flute + n);
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        });

        /// <summary>Royal carpet: crimson with gold borders on both u edges; v repeats per unit.</summary>
        public static Texture2D Carpet => Cached("carpet", () =>
        {
            const int W = 48, H = 16;
            var t = New("carpet", W, H);
            var red = new Color(0.55f, 0.07f, 0.1f);
            var gold = new Color(0.85f, 0.65f, 0.25f);
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color c = Shade(red, 0.9f + Hash(x, y, 41) * 0.15f);
                    int e = Mathf.Min(x, W - 1 - x);
                    if (e == 0) c = Shade(red, 0.45f);
                    else if (e == 2 || e == 4) c = gold;
                    else if (e == 3) c = Shade(gold, 0.6f);
                    // diamond pattern
                    int cx = Mathf.Abs(x - W / 2), cy = Mathf.Abs(y - H / 2);
                    if (cx + cy == 5) c = Shade(gold, 0.85f);
                    px[y * W + x] = c;
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        });

        /// <summary>Gothic stained-glass window, used with emission so it glows with moonlight.</summary>
        public static Texture2D Window => Cached("window", () =>
        {
            const int W = 24, H = 48;
            var t = New("window", W, H, true, false);
            var px = new Color[W * H];
            var frame = new Color(0.12f, 0.1f, 0.14f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float cx = x - (W - 1) / 2f;
                    // pointed arch: inside if below arch curve
                    float archY = H - 1 - Mathf.Max(0f, (Mathf.Abs(cx) * 1.4f) - 2f) * 1.2f;
                    bool inArch = y < archY - (Mathf.Abs(cx) > 9 ? 4 : 0);
                    bool inside = inArch && Mathf.Abs(cx) < 10 && y > 1;
                    Color c = new Color(0, 0, 0, 0);
                    if (inside)
                    {
                        bool lead = (x % 6 == 0) || (y % 8 == 0) || Mathf.Abs(cx) > 9.2f || ((x + y) % 11 == 0 && y > 30);
                        if (lead) c = frame;
                        else
                        {
                            float h = Hash(x / 6, y / 8, 7);
                            Color glass = h < 0.35f ? new Color(0.25f, 0.4f, 0.95f)
                                : h < 0.6f ? new Color(0.55f, 0.3f, 0.85f)
                                : h < 0.8f ? new Color(0.3f, 0.75f, 0.95f)
                                : new Color(0.9f, 0.75f, 0.35f);
                            c = Shade(glass, 0.85f + Hash(x, y, 3) * 0.3f);
                        }
                        c.a = 1;
                    }
                    else if (inArch && Mathf.Abs(cx) < 11.5f) c = frame;
                    px[y * W + x] = c;
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        });

        /// <summary>Hanging banner with the Dark Lord's sigil.</summary>
        public static Texture2D Banner => Cached("banner", () =>
        {
            const int W = 16, H = 40;
            var t = New("banner", W, H, true, false);
            var px = new Color[W * H];
            var cloth = new Color(0.32f, 0.08f, 0.38f);
            var gold = new Color(0.85f, 0.65f, 0.25f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color c = new Color(0, 0, 0, 0);
                    // pointed pennant tip at the bottom
                    float fromCenter = Mathf.Abs(x - (W - 1) / 2f);
                    bool inCloth = y >= 7 || fromCenter <= y * 1.15f;
                    if (inCloth)
                    {
                        c = Shade(cloth, 0.85f + 0.25f * Mathf.Sin(x * 0.7f) * 0.5f + Hash(x, y, 9) * 0.1f);
                        if (x == 1 || x == W - 2) c = gold;
                        if (y >= H - 3) c = gold;
                        // sigil: eye/crescent
                        float dx = x - (W - 1) / 2f, dy = y - 24;
                        float r = Mathf.Sqrt(dx * dx + dy * dy * 0.7f);
                        if (r > 3.6f && r < 5f) c = gold;
                        if (r < 1.6f) c = new Color(0.95f, 0.2f, 0.2f);
                        c.a = 1;
                    }
                    if (y == H - 1) c = new Color(0.25f, 0.18f, 0.12f, 1);
                    px[y * W + x] = c;
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        });

        /// <summary>Soft round particle (bilinear).</summary>
        public static Texture2D SoftDot => Cached("softdot", () =>
        {
            const int S = 32;
            var t = New("softdot", S, S, false, false);
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x + 0.5f) / S * 2 - 1, dy = (y + 0.5f) / S * 2 - 1;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1 - d);
                    a = a * a * (3 - 2 * a);
                    px[y * S + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        });

        /// <summary>Square pixel particle (point) for retro sparks.</summary>
        public static Texture2D PixelDot => Cached("pixeldot", () =>
        {
            const int S = 4;
            var t = New("pixeldot", S, S, true, false);
            var px = new Color[S * S];
            for (int i = 0; i < px.Length; i++)
            {
                int x = i % S, y = i / S;
                bool edge = x == 0 || y == 0 || x == S - 1 || y == S - 1;
                px[i] = new Color(1, 1, 1, edge ? 0.5f : 1f);
            }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        });

        /// <summary>Vertical gradient used for fake volumetric light shafts.</summary>
        public static Texture2D Shaft => Cached("shaft", () =>
        {
            const int W = 32, H = 64;
            var t = New("shaft", W, H, false, false);
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
                    float side = Mathf.Sin(u * Mathf.PI);
                    side *= side;
                    float a = side * Mathf.SmoothStep(0, 1, v) * Mathf.SmoothStep(0, 0.15f, 1 - v);
                    px[y * W + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        });

        /// <summary>Crescent slash arc for weapon swing effects.</summary>
        public static Texture2D SlashArc => Cached("slash", () =>
        {
            const int S = 64;
            var t = New("slash", S, S, false, false);
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x + 0.5f) / S * 2 - 1, dy = (y + 0.5f) / S * 2 - 1;
                    float d1 = Mathf.Sqrt(dx * dx + dy * dy);
                    float d2 = Mathf.Sqrt((dx - 0.22f) * (dx - 0.22f) + (dy + 0.12f) * (dy + 0.12f));
                    float a = Mathf.Clamp01((1 - d1) * 6f) * Mathf.Clamp01((d2 - 0.78f) * 6f);
                    float ang = Mathf.Atan2(dy, dx);
                    a *= Mathf.Clamp01((ang + 2.6f) / 2.2f);
                    px[y * S + x] = new Color(1, 1, 1, Mathf.Clamp01(a));
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        });

        /// <summary>Ring texture for buff/shockwave effects.</summary>
        public static Texture2D Ring => Cached("ring", () =>
        {
            const int S = 64;
            var t = New("ring", S, S, false, false);
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x + 0.5f) / S * 2 - 1, dy = (y + 0.5f) / S * 2 - 1;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1 - Mathf.Abs(d - 0.82f) * 9f);
                    px[y * S + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        });

        /// <summary>Magic circle glyph for spell casting.</summary>
        public static Texture2D MagicCircle => Cached("magiccircle", () =>
        {
            const int S = 128;
            var t = New("magiccircle", S, S, false, false);
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x + 0.5f) / S * 2 - 1, dy = (y + 0.5f) / S * 2 - 1;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    float a = 0;
                    a += Mathf.Clamp01(1 - Mathf.Abs(d - 0.95f) * 40f);
                    a += Mathf.Clamp01(1 - Mathf.Abs(d - 0.86f) * 50f);
                    a += Mathf.Clamp01(1 - Mathf.Abs(d - 0.55f) * 45f);
                    // runes between rings
                    if (d > 0.87f && d < 0.94f)
                    {
                        float seg = Mathf.Repeat(ang * 12f / Mathf.PI, 1f);
                        if (seg > 0.2f && seg < 0.7f && Hash(Mathf.FloorToInt(ang * 12f / Mathf.PI) + 40, Mathf.FloorToInt(d * 60), 5) > 0.4f) a += 0.8f;
                    }
                    // hexagram
                    for (int k = 0; k < 6; k++)
                    {
                        float a0 = k * Mathf.PI / 3f, a1 = a0 + 2f * Mathf.PI / 3f;
                        Vector2 p0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * 0.86f;
                        Vector2 p1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * 0.86f;
                        Vector2 p = new Vector2(dx, dy);
                        Vector2 ab = p1 - p0;
                        float tt = Mathf.Clamp01(Vector2.Dot(p - p0, ab) / ab.sqrMagnitude);
                        float dist = (p - (p0 + ab * tt)).magnitude;
                        a += Mathf.Clamp01(1 - dist * 60f);
                    }
                    px[y * S + x] = new Color(1, 1, 1, Mathf.Clamp01(a));
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        });

        public static Texture2D Solid(Color c) => Cached("solid" + ColorUtility.ToHtmlStringRGBA(c), () =>
        {
            var t = New("solid", 4, 4, true, true);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = c;
            t.SetPixels(px);
            t.Apply(true);
            return t;
        });
    }
}
