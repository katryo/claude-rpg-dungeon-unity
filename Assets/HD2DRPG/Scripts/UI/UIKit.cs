using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HD2DRPG
{
    /// <summary>
    /// Code-built uGUI toolkit in the classic HD-2D JRPG style: translucent navy windows with gold
    /// filigree borders, serif text, pointing-hand cursor lists and gauges.
    /// Coordinates are in a 1920x1080 reference space, measured from the top-left, y downwards.
    /// </summary>
    public static class UIKit
    {
        public static readonly Color Cream = new Color(0.97f, 0.94f, 0.86f);
        public static readonly Color Gold = new Color(0.98f, 0.82f, 0.45f);
        public static readonly Color DimGold = new Color(0.72f, 0.58f, 0.32f);
        public static readonly Color Dim = new Color(0.58f, 0.58f, 0.66f);
        public static readonly Color HPColor = new Color(0.45f, 0.9f, 0.45f);
        public static readonly Color MPColor = new Color(0.45f, 0.7f, 1f);
        public static readonly Color Red = new Color(1f, 0.38f, 0.35f);
        public static readonly Color Up = new Color(0.5f, 1f, 0.6f);
        public static readonly Color Down = new Color(1f, 0.45f, 0.4f);
        public static readonly Color Cyan = new Color(0.55f, 0.92f, 1f);

        static Font font;
        public static Font Font
        {
            get
            {
                if (font != null) return font;
                string[] serif = { "Georgia", "Palatino Linotype", "Book Antiqua", "Times New Roman", "Cambria", "DejaVu Serif", "Liberation Serif", "Noto Serif", "Serif" };
                try
                {
                    var installed = new HashSet<string>(Font.GetOSInstalledFontNames());
                    foreach (var n in serif)
                        if (installed.Contains(n)) { font = Font.CreateDynamicFontFromOSFont(n, 32); break; }
                }
                catch { font = null; }
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        // ------------------------------------------------------------------ generated sprites
        static Sprite windowSprite, frameSprite, highlightSprite, whiteSprite, gaugeSprite, plateSprite;

        public static Sprite White
        {
            get
            {
                if (whiteSprite != null) return whiteSprite;
                var t = new Texture2D(4, 4) { filterMode = FilterMode.Point };
                var px = new Color[16];
                for (int i = 0; i < 16; i++) px[i] = Color.white;
                t.SetPixels(px); t.Apply();
                whiteSprite = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(1, 1, 1, 1));
                return whiteSprite;
            }
        }

        /// <summary>Window with gradient fill and gold double border + corner jewels (9-sliced).</summary>
        public static Sprite WindowSprite
        {
            get
            {
                if (windowSprite != null) return windowSprite;
                const int S = 48;
                var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[S * S];
                var top = new Color(0.09f, 0.1f, 0.22f, 0.9f);
                var bottom = new Color(0.03f, 0.035f, 0.1f, 0.92f);
                var gold = new Color(0.86f, 0.7f, 0.38f, 1f);
                var goldDark = new Color(0.45f, 0.33f, 0.16f, 1f);
                var edge = new Color(0.01f, 0.01f, 0.03f, 1f);
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        int d = Mathf.Min(Mathf.Min(x, S - 1 - x), Mathf.Min(y, S - 1 - y));
                        Color c = Color.Lerp(bottom, top, y / (float)(S - 1));
                        if (d == 0) c = edge;
                        else if (d == 1) c = goldDark;
                        else if (d == 2) c = gold;
                        else if (d == 3) c = goldDark;
                        else if (d == 5) c = new Color(gold.r, gold.g, gold.b, 0.35f);
                        // corner jewels
                        int cx = Mathf.Min(x, S - 1 - x), cy = Mathf.Min(y, S - 1 - y);
                        if (cx + cy <= 8 && cx <= 6 && cy <= 6 && (cx >= 1 && cy >= 1))
                        {
                            if (cx + cy >= 7) c = goldDark;
                            else if (cx + cy >= 5) c = gold;
                            else c = new Color(0.9f, 0.25f, 0.3f, 1f);
                            if (cx + cy == 3 && cx == 1) c = new Color(1f, 0.7f, 0.7f, 1f);
                        }
                        px[y * S + x] = c;
                    }
                t.SetPixels(px);
                t.Apply();
                windowSprite = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
                return windowSprite;
            }
        }

        /// <summary>Thin gold frame (for portraits and selected cards).</summary>
        public static Sprite FrameSprite
        {
            get
            {
                if (frameSprite != null) return frameSprite;
                const int S = 16;
                var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        int d = Mathf.Min(Mathf.Min(x, S - 1 - x), Mathf.Min(y, S - 1 - y));
                        px[y * S + x] = d == 0 ? new Color(0.98f, 0.85f, 0.5f, 1f) : d == 1 ? new Color(0.6f, 0.45f, 0.2f, 1f) : new Color(0, 0, 0, 0);
                    }
                t.SetPixels(px); t.Apply();
                frameSprite = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(3, 3, 3, 3));
                return frameSprite;
            }
        }

        /// <summary>Horizontal glow used behind the selected list row.</summary>
        public static Sprite HighlightSprite
        {
            get
            {
                if (highlightSprite != null) return highlightSprite;
                const int W = 64, H = 8;
                var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float u = x / (float)(W - 1);
                        float a = Mathf.Clamp01(1f - Mathf.Abs(u - 0.35f) / 0.65f);
                        a = Mathf.Pow(a, 0.7f) * 0.55f;
                        if (y == 0 || y == H - 1) a *= 1.5f;
                        px[y * W + x] = new Color(1f, 0.85f, 0.5f, a);
                    }
                t.SetPixels(px); t.Apply();
                highlightSprite = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(0, 2, 0, 2));
                return highlightSprite;
            }
        }

        /// <summary>Rounded gauge shape.</summary>
        public static Sprite GaugeSprite
        {
            get
            {
                if (gaugeSprite != null) return gaugeSprite;
                const int W = 16, H = 8;
                var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float shine = y >= H - 3 ? 1f : 0.82f - (H - 3 - y) * 0.05f;
                        bool corner = (x == 0 || x == W - 1) && (y == 0 || y == H - 1);
                        px[y * W + x] = corner ? new Color(1, 1, 1, 0) : new Color(shine, shine, shine, 1);
                    }
                t.SetPixels(px); t.Apply();
                gaugeSprite = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(2, 2, 2, 2));
                return gaugeSprite;
            }
        }

        /// <summary>Dark translucent plate used for name tags and status rows.</summary>
        public static Sprite PlateSprite
        {
            get
            {
                if (plateSprite != null) return plateSprite;
                const int W = 32, H = 16;
                var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float u = x / (float)(W - 1);
                        float a = 0.78f * Mathf.Clamp01(u * 6f) * Mathf.Clamp01((1 - u) * 1.5f + 0.25f);
                        Color c = new Color(0.02f, 0.02f, 0.06f, a);
                        if (y == H - 1) c = new Color(0.86f, 0.7f, 0.38f, a * 1.1f);
                        px[y * W + x] = c;
                    }
                t.SetPixels(px); t.Apply();
                plateSprite = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(6, 2, 6, 2));
                return plateSprite;
            }
        }

        // ------------------------------------------------------------------ builders
        public static Canvas CreateCanvas(string name, int sortOrder, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = sortOrder;
            c.pixelPerfect = false;
            var cs = go.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            cs.matchWidthOrHeight = 0.5f;
            return c;
        }

        public static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Fill(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static Image Panel(Transform parent, float x, float y, float w, float h, string name = "Window")
        {
            var rt = Rect(name, parent, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = WindowSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.5f; // doubles border thickness for crisp pixels at 1080p
            img.raycastTarget = false;
            return img;
        }

        public static Image Box(Transform parent, float x, float y, float w, float h, Color c, Sprite s = null, string name = "Box")
        {
            var rt = Rect(name, parent, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s != null ? s : White;
            img.type = Image.Type.Sliced;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        public static Image Icon(Transform parent, Sprite s, float x, float y, float w, float h, string name = "Icon")
        {
            var rt = Rect(name, parent, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string text, float x, float y, float w, float h, int size = 30,
                                 TextAnchor align = TextAnchor.MiddleLeft, Color? color = null, bool outline = true)
        {
            var rt = Rect("Text", parent, x, y, w, h);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? Cream;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.lineSpacing = 1.05f;
            if (outline)
            {
                var sh = rt.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0, 0, 0, 0.85f);
                sh.effectDistance = new Vector2(2, -2);
            }
            return t;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject);
        }

        /// <summary>Converts a world position to anchored coordinates for a bottom-left anchored rect.</summary>
        public static Vector2 WorldToCanvas(Canvas canvas, Vector3 world)
        {
            var cam = CameraRig.MainCamera;
            if (cam == null) return Vector2.zero;
            Vector3 sp = cam.WorldToScreenPoint(world);
            return new Vector2(sp.x, sp.y) / canvas.scaleFactor;
        }

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
        public static string Col(string s, Color c) => "<color=#" + Hex(c) + ">" + s + "</color>";
    }

    /// <summary>Horizontal gauge.</summary>
    public class Gauge
    {
        public RectTransform Root;
        readonly RectTransform fill;
        readonly Image fillImg;
        readonly RectTransform ghost;
        readonly float width;
        float shown = -1, ghostValue;

        public Gauge(Transform parent, float x, float y, float w, float h, Color color)
        {
            width = w;
            var bg = UIKit.Box(parent, x, y, w, h, new Color(0.02f, 0.02f, 0.05f, 0.9f), UIKit.GaugeSprite, "Gauge");
            Root = bg.rectTransform;
            var g = UIKit.Box(Root, 0, 0, w, h, new Color(1f, 0.9f, 0.6f, 0.7f), UIKit.GaugeSprite, "Ghost");
            ghost = g.rectTransform;
            fillImg = UIKit.Box(Root, 0, 0, w, h, color, UIKit.GaugeSprite, "Fill");
            fill = fillImg.rectTransform;
        }

        public void Set(float v, bool instant = false)
        {
            v = Mathf.Clamp01(v);
            if (shown < 0 || instant) { ghostValue = v; }
            else if (v > ghostValue) ghostValue = v;
            shown = v;
            fill.sizeDelta = new Vector2(width * v, fill.sizeDelta.y);
            ghost.sizeDelta = new Vector2(width * ghostValue, ghost.sizeDelta.y);
        }

        public void Tick(float dt)
        {
            if (ghostValue > shown)
            {
                ghostValue = Mathf.MoveTowards(ghostValue, shown, dt * 0.6f);
                ghost.sizeDelta = new Vector2(width * ghostValue, ghost.sizeDelta.y);
            }
        }

        public void SetColor(Color c) => fillImg.color = c;
    }

    /// <summary>Scrollable cursor list (keyboard/gamepad driven).</summary>
    public class ListView
    {
        public class Item
        {
            public string Label;
            public string Right;
            public bool Enabled = true;
            public Sprite Icon;
            public Color? Color;
            public object Data;
        }

        public readonly RectTransform Root;
        public readonly List<Item> Items = new List<Item>();
        public int Index;
        public int Scroll;
        public bool Active = true;
        public int FontSize = 30;
        public float RowHeight;
        public int VisibleRows;
        public System.Action<int> OnChanged;
        readonly float width;
        readonly Image highlight;
        readonly Image cursor;
        readonly List<Text> labels = new List<Text>();
        readonly List<Text> rights = new List<Text>();
        readonly List<Image> icons = new List<Image>();
        readonly Text upArrow, downArrow;
        float cursorAnim;

        public ListView(Transform parent, float x, float y, float w, float rowHeight, int visibleRows)
        {
            Root = UIKit.Rect("List", parent, x, y, w, rowHeight * visibleRows);
            width = w;
            RowHeight = rowHeight;
            VisibleRows = visibleRows;
            highlight = UIKit.Box(Root, 0, 0, w, rowHeight, Color.white, UIKit.HighlightSprite, "Highlight");
            for (int i = 0; i < visibleRows; i++)
            {
                icons.Add(UIKit.Icon(Root, null, 36, i * rowHeight + (rowHeight - 30) / 2, 30, 30));
                labels.Add(UIKit.Label(Root, "", 40, i * rowHeight, w - 60, rowHeight, FontSize));
                rights.Add(UIKit.Label(Root, "", 40, i * rowHeight, w - 60, rowHeight, FontSize, TextAnchor.MiddleRight));
            }
            cursor = UIKit.Icon(Root, PixelArt.Sprite("icon_cursor"), -6, 0, 30, 30, "Cursor");
            upArrow = UIKit.Label(Root, "▲", w / 2 - 20, -26, 40, 24, 18, TextAnchor.MiddleCenter, UIKit.Gold);
            downArrow = UIKit.Label(Root, "▼", w / 2 - 20, rowHeight * visibleRows, 40, 24, 18, TextAnchor.MiddleCenter, UIKit.Gold);
        }

        public Item Current => Index >= 0 && Index < Items.Count ? Items[Index] : null;

        public void SetItems(IEnumerable<Item> items, bool keepIndex = true)
        {
            Items.Clear();
            Items.AddRange(items);
            if (!keepIndex) { Index = 0; Scroll = 0; }
            Index = Mathf.Clamp(Index, 0, Mathf.Max(0, Items.Count - 1));
            Refresh();
        }

        public void Refresh()
        {
            if (Index < Scroll) Scroll = Index;
            if (Index >= Scroll + VisibleRows) Scroll = Index - VisibleRows + 1;
            Scroll = Mathf.Clamp(Scroll, 0, Mathf.Max(0, Items.Count - VisibleRows));
            for (int i = 0; i < VisibleRows; i++)
            {
                int idx = Scroll + i;
                bool has = idx < Items.Count;
                var it = has ? Items[idx] : null;
                float textX = it != null && it.Icon != null ? 76 : 40;
                labels[i].rectTransform.anchoredPosition = new Vector2(textX, -i * RowHeight);
                labels[i].fontSize = FontSize;
                rights[i].fontSize = FontSize;
                labels[i].text = has ? it.Label : "";
                rights[i].text = has ? (it.Right ?? "") : "";
                Color c = has ? (it.Color ?? UIKit.Cream) : UIKit.Cream;
                if (has && !it.Enabled) c = UIKit.Dim;
                labels[i].color = c;
                rights[i].color = has && !it.Enabled ? UIKit.Dim : UIKit.Cream;
                icons[i].enabled = has && it.Icon != null;
                if (has && it.Icon != null) icons[i].sprite = it.Icon;
            }
            bool show = Items.Count > 0;
            int row = Index - Scroll;
            highlight.enabled = show && Active;
            cursor.enabled = show && Active;
            highlight.rectTransform.anchoredPosition = new Vector2(0, -row * RowHeight);
            cursor.rectTransform.anchoredPosition = new Vector2(-6, -row * RowHeight - (RowHeight - 30) / 2);
            upArrow.enabled = Scroll > 0;
            downArrow.enabled = Scroll + VisibleRows < Items.Count;
        }

        /// <summary>Handles up/down navigation. Returns true if the index changed.</summary>
        public bool Navigate()
        {
            if (!Active || Items.Count == 0) return false;
            var nav = GameInput.Nav;
            int prev = Index;
            if (nav.y < 0) Index = (Index + 1) % Items.Count;
            else if (nav.y > 0) Index = (Index - 1 + Items.Count) % Items.Count;
            if (prev != Index)
            {
                AudioManager.Play("cursor", 0.5f);
                Refresh();
                OnChanged?.Invoke(Index);
                return true;
            }
            return false;
        }

        public void Animate()
        {
            if (!cursor.enabled) return;
            cursorAnim += Time.unscaledDeltaTime * 6f;
            int row = Index - Scroll;
            float off = Mathf.Sin(cursorAnim) * 4f;
            cursor.rectTransform.anchoredPosition = new Vector2(-10 + off, -row * RowHeight - (RowHeight - 30) / 2);
        }

        public void SetActive(bool a)
        {
            Active = a;
            Refresh();
        }
    }

    /// <summary>Full-screen fader.</summary>
    public class Fader
    {
        readonly Image img;
        public Fader(Transform canvas)
        {
            img = UIKit.Box(UIKit.Fill("Fader", canvas), 0, 0, 0, 0, new Color(0, 0, 0, 0));
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public float Alpha
        {
            get => img.color.a;
            set { var c = img.color; c.a = value; img.color = c; img.enabled = value > 0.001f; }
        }

        public void SetColor(Color c)
        {
            float a = img.color.a;
            img.color = new Color(c.r, c.g, c.b, a);
        }

        public IEnumerator To(float target, float time)
        {
            float start = Alpha;
            for (float t = 0; t < time; t += Time.unscaledDeltaTime)
            {
                Alpha = Mathf.Lerp(start, target, t / time);
                yield return null;
            }
            Alpha = target;
        }
    }
}
