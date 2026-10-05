using System.Collections.Generic;
using UnityEngine;

namespace HD2DRPG
{
    /// <summary>
    /// Loads the hand-authored ASCII pixel art in Resources/HD2D/sprites.txt and turns it into
    /// point-filtered textures and sprites. Everything is drawn facing right.
    /// </summary>
    public static class PixelArt
    {
        /// <summary>Texture pixels per world unit for characters (a 32px tall hero is 1.6 units).</summary>
        public const float PixelsPerUnit = 20f;

        class SpriteData
        {
            public int W, H;
            public Color32[] Pixels; // bottom-up, row-major
        }

        static readonly Dictionary<string, SpriteData> data = new Dictionary<string, SpriteData>();
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static bool loaded;

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            var ta = Resources.Load<TextAsset>("HD2D/sprites");
            if (ta == null)
            {
                Debug.LogError("[HD2D] Resources/HD2D/sprites.txt not found.");
                return;
            }
            Parse(ta.text);
        }

        static void Parse(string text)
        {
            var palettes = new Dictionary<string, Dictionary<char, Color32>>();
            var rowsByName = new Dictionary<string, List<string>>();
            var palByName = new Dictionary<string, string>();
            var lines = text.Replace("\r", "").Split('\n');
            int i = 0;
            while (i < lines.Length)
            {
                string l = lines[i++].Trim();
                if (l.Length == 0 || l.StartsWith("#")) continue;
                var p = l.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (p[0] == "palette")
                {
                    var pal = new Dictionary<char, Color32>();
                    if (p.Length > 3 && p[2] == "from" && palettes.TryGetValue(p[3], out var basePal))
                        foreach (var kv in basePal) pal[kv.Key] = kv.Value;
                    while (i < lines.Length)
                    {
                        string pl = lines[i++].Trim();
                        if (pl == "end") break;
                        if (pl.Length == 0 || pl.StartsWith("#")) continue;
                        var pp = pl.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                        if (pp.Length >= 2 && ColorUtility.TryParseHtmlString("#" + pp[1], out var c))
                            pal[pp[0][0]] = c;
                    }
                    palettes[p[1]] = pal;
                }
                else if (p[0] == "sprite")
                {
                    string name = p[1];
                    string palName = p[2];
                    var rows = new List<string>();
                    while (i < lines.Length)
                    {
                        string rl = lines[i++].Trim();
                        if (rl == "end") break;
                        rows.Add(rl);
                    }
                    if (p.Length > 5 && p[3] == "from" && rowsByName.TryGetValue(p[4], out var baseRows))
                    {
                        int start = int.Parse(p[5]);
                        var merged = new List<string>(baseRows);
                        for (int k = 0; k < rows.Count; k++)
                        {
                            if (start + k < merged.Count) merged[start + k] = rows[k];
                            else merged.Add(rows[k]);
                        }
                        rows = merged;
                    }
                    rowsByName[name] = rows;
                    palByName[name] = palName;
                }
            }

            foreach (var kv in rowsByName)
            {
                var rows = kv.Value;
                palettes.TryGetValue(palByName[kv.Key], out var pal);
                int w = 0;
                foreach (var r in rows) w = Mathf.Max(w, r.Length);
                int h = rows.Count;
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                {
                    string r = rows[y];
                    int ty = h - 1 - y; // flip: text top row is texture top
                    for (int x = 0; x < w; x++)
                    {
                        char ch = x < r.Length ? r[x] : '.';
                        Color32 col = new Color32(0, 0, 0, 0);
                        if (ch != '.' && pal != null)
                        {
                            if (!pal.TryGetValue(ch, out col)) col = new Color32(255, 0, 255, 255);
                            col.a = 255;
                        }
                        px[ty * w + x] = col;
                    }
                }
                data[kv.Key] = new SpriteData { W = w, H = h, Pixels = px };
            }
        }

        public static bool Has(string id)
        {
            Load();
            return data.ContainsKey(id);
        }

        public static Vector2Int Size(string id)
        {
            Load();
            return data.TryGetValue(id, out var d) ? new Vector2Int(d.W, d.H) : new Vector2Int(8, 8);
        }

        /// <summary>World-space size of a sprite at the standard pixel density.</summary>
        public static Vector2 WorldSize(string id)
        {
            var s = Size(id);
            return new Vector2(s.x / PixelsPerUnit, s.y / PixelsPerUnit);
        }

        public static Texture2D Tex(string id)
        {
            Load();
            if (textures.TryGetValue(id, out var t) && t != null) return t;
            Texture2D tex;
            if (data.TryGetValue(id, out var d))
            {
                tex = new Texture2D(d.W, d.H, TextureFormat.RGBA32, false);
                tex.SetPixels32(d.Pixels);
            }
            else
            {
                tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                var px = new Color32[64];
                for (int k = 0; k < 64; k++) px[k] = ((k + k / 8) % 2 == 0) ? new Color32(255, 0, 255, 255) : new Color32(0, 0, 0, 255);
                tex.SetPixels32(px);
            }
            tex.name = id;
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply(false, false);
            textures[id] = tex;
            return tex;
        }

        /// <summary>UI sprite (pivot bottom-center).</summary>
        public static Sprite Sprite(string id)
        {
            if (sprites.TryGetValue(id, out var s) && s != null) return s;
            var tex = Tex(id);
            s = UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), PixelsPerUnit);
            s.name = id;
            sprites[id] = s;
            return s;
        }

        /// <summary>Crop of the top part of a sprite, used for portrait/turn-order icons.</summary>
        public static Sprite HeadSprite(string id, int rowsFromTop = 16)
        {
            string key = id + "#head" + rowsFromTop;
            if (sprites.TryGetValue(key, out var s) && s != null) return s;
            var tex = Tex(id);
            int h = Mathf.Min(rowsFromTop, tex.height);
            // Find the horizontal extent of opaque pixels inside the crop for a centered square.
            int minX = tex.width, maxX = 0;
            var px = tex.GetPixels32();
            for (int y = tex.height - h; y < tex.height; y++)
                for (int x = 0; x < tex.width; x++)
                    if (px[y * tex.width + x].a > 0) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); }
            if (minX > maxX) { minX = 0; maxX = tex.width - 1; }
            int w = maxX - minX + 1;
            int size = Mathf.Max(w, h);
            int cx = (minX + maxX) / 2;
            int x0 = Mathf.Clamp(cx - size / 2, 0, Mathf.Max(0, tex.width - size));
            int wClamped = Mathf.Min(size, tex.width - x0);
            s = UnityEngine.Sprite.Create(tex, new Rect(x0, tex.height - h, wClamped, h), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            s.name = key;
            sprites[key] = s;
            return s;
        }

        public static string ElementIcon(Element e)
        {
            switch (e)
            {
                case Element.Sword: return "icon_sword";
                case Element.Axe: return "icon_axe";
                case Element.Staff: return "icon_staff";
                case Element.Fire: return "icon_fire";
                case Element.Ice: return "icon_ice";
                case Element.Thunder: return "icon_thunder";
                case Element.Light: return "icon_light";
                case Element.Dark: return "icon_dark";
                default: return "icon_unknown";
            }
        }
    }
}
