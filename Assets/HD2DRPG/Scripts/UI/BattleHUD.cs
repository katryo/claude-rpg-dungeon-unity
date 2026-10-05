using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HD2DRPG
{
    /// <summary>All battle UI widgets. Logic lives in <see cref="BattleManager"/>.</summary>
    public class BattleHUD
    {
        public readonly Canvas Canvas;
        readonly RectTransform root;
        readonly RectTransform world;  // bottom-left anchored container for world-attached widgets

        class PartyRow
        {
            public Battler B;
            public RectTransform Root;
            public Image Frame;
            public Image Head;
            public Text Name, HP, MP;
            public Gauge HPGauge, MPGauge;
            public Image[] Pips;
            public Text Buffs;
        }

        class EnemyTag
        {
            public Battler B;
            public RectTransform Root;
            public Image ShieldIcon;
            public Text ShieldText;
            public Image[] Weak;
            public Text NameText;
            public Gauge HPGauge;
            public Text BreakText;
        }

        readonly List<PartyRow> partyRows = new List<PartyRow>();
        readonly List<EnemyTag> enemyTags = new List<EnemyTag>();
        readonly RectTransform turnBar;
        readonly Image helpPanel;
        readonly Text helpText;
        readonly Image bannerPanel;
        readonly Text bannerText;
        readonly Image commandPanel;
        public readonly ListView CommandList;
        readonly Image subPanel;
        public readonly ListView SubList;
        readonly Text subTitle;
        readonly Text boostHint;
        readonly Image targetArrow;
        readonly List<Image> extraArrows = new List<Image>();

        public BattleHUD(Transform parent)
        {
            Canvas = UIKit.CreateCanvas("BattleHUD", 10, parent);
            root = UIKit.Fill("Root", Canvas.transform);
            world = UIKit.Fill("World", root);
            world.anchorMin = world.anchorMax = Vector2.zero;
            world.pivot = Vector2.zero;
            world.sizeDelta = Vector2.zero;

            // turn order bar (top-left)
            turnBar = UIKit.Rect("TurnOrder", root, 24, 18, 1100, 84);

            // help bar (top)
            helpPanel = UIKit.Panel(root, 330, 104, 1260, 78, "Help");
            helpText = UIKit.Label(helpPanel.transform, "", 34, 0, 1200, 78, 28);
            helpPanel.gameObject.SetActive(false);

            // banner (action name)
            bannerPanel = UIKit.Box(root, 560, 190, 800, 64, Color.white, UIKit.PlateSprite, "Banner");
            bannerText = UIKit.Label(bannerPanel.transform, "", 0, 0, 800, 64, 32, TextAnchor.MiddleCenter, UIKit.Gold);
            bannerPanel.gameObject.SetActive(false);

            // command window
            commandPanel = UIKit.Panel(root, 870, 712, 330, 348, "Commands");
            CommandList = new ListView(commandPanel.transform, 32, 24, 280, 58, 5) { FontSize = 32 };
            boostHint = UIKit.Label(root, "", 870, 676, 520, 36, 22, TextAnchor.MiddleLeft, UIKit.Gold);
            commandPanel.gameObject.SetActive(false);

            // skill / item window
            subPanel = UIKit.Panel(root, 300, 300, 880, 520, "SubWindow");
            subTitle = UIKit.Label(subPanel.transform, "", 34, 14, 600, 44, 28, TextAnchor.MiddleLeft, UIKit.Gold);
            SubList = new ListView(subPanel.transform, 40, 66, 800, 54, 8) { FontSize = 28 };
            subPanel.gameObject.SetActive(false);

            targetArrow = UIKit.Icon(world, PixelArt.Sprite("icon_cursor"), 0, 0, 40, 40, "TargetArrow");
            targetArrow.rectTransform.anchorMin = targetArrow.rectTransform.anchorMax = Vector2.zero;
            targetArrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            targetArrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -90);
            targetArrow.enabled = false;
        }

        public void SetVisible(bool v) => Canvas.gameObject.SetActive(v);

        // ------------------------------------------------------------------ party panel
        public void BuildParty(List<Battler> party)
        {
            foreach (var r in partyRows) Object.Destroy(r.Root.gameObject);
            partyRows.Clear();
            for (int i = 0; i < party.Count; i++)
            {
                var b = party[i];
                float y = 742 + i * 108;
                var row = new PartyRow { B = b };
                var panel = UIKit.Panel(root, 1220, y, 680, 104, "PartyRow");
                row.Root = panel.rectTransform;
                row.Frame = UIKit.Box(row.Root, 6, 6, 668, 92, new Color(1f, 0.85f, 0.5f, 0.0f), UIKit.HighlightSprite, "Active");
                row.Head = UIKit.Icon(row.Root, PixelArt.HeadSprite(b.SpriteHead, 16), 22, 14, 76, 76);
                row.Name = UIKit.Label(row.Root, b.Name, 112, 10, 200, 40, 30);
                row.Buffs = UIKit.Label(row.Root, "", 112, 52, 200, 36, 20, TextAnchor.MiddleLeft, UIKit.Cyan);
                row.HP = UIKit.Label(row.Root, "", 300, 6, 210, 36, 26, TextAnchor.MiddleRight);
                row.HPGauge = new Gauge(row.Root, 316, 44, 194, 10, UIKit.HPColor);
                row.MP = UIKit.Label(row.Root, "", 300, 50, 210, 36, 22, TextAnchor.MiddleRight, UIKit.MPColor);
                row.MPGauge = new Gauge(row.Root, 316, 84, 194, 7, UIKit.MPColor);
                row.Pips = new Image[5];
                for (int k = 0; k < 5; k++)
                    row.Pips[k] = UIKit.Icon(row.Root, PixelArt.Sprite("bp_empty"), 528 + k * 28, 36, 26, 26, "BP");
                UIKit.Label(row.Root, "BP", 528, 4, 120, 30, 18, TextAnchor.MiddleLeft, UIKit.DimGold);
                partyRows.Add(row);
            }
            RefreshParty(null, 0, true);
        }

        public void RefreshParty(Battler active, int boostPreview, bool instant = false)
        {
            foreach (var r in partyRows)
            {
                var b = r.B;
                bool act = b == active;
                r.Frame.color = new Color(1f, 0.85f, 0.5f, act ? 0.8f : 0f);
                r.Root.anchoredPosition = new Vector2(act ? 1190 : 1220, r.Root.anchoredPosition.y);
                r.HP.text = "<size=18>HP</size> " + b.HP + "<size=20>/" + b.MaxHP + "</size>";
                r.MP.text = "<size=16>MP</size> " + b.MP + "<size=16>/" + b.MaxMP + "</size>";
                r.HPGauge.Set(b.HP / (float)Mathf.Max(1, b.MaxHP), instant);
                r.MPGauge.Set(b.MP / (float)Mathf.Max(1, b.MaxMP), instant);
                r.HP.color = !b.Alive ? UIKit.Red : b.HP < b.MaxHP / 4 ? new Color(1f, 0.8f, 0.3f) : UIKit.Cream;
                r.Name.color = b.Alive ? UIKit.Cream : UIKit.Red;
                r.Head.color = b.Alive ? Color.white : new Color(0.5f, 0.3f, 0.3f);
                for (int k = 0; k < 5; k++)
                {
                    bool full = k < b.BP;
                    bool spending = act && k >= b.BP - boostPreview && k < b.BP;
                    r.Pips[k].sprite = PixelArt.Sprite(full ? "bp_full" : "bp_empty");
                    r.Pips[k].color = spending ? new Color(1f, 0.55f, 0.3f) : Color.white;
                    r.Pips[k].rectTransform.localScale = spending ? Vector3.one * 1.25f : Vector3.one;
                }
                r.Buffs.text = BuffString(b);
            }
        }

        static string BuffString(Battler b)
        {
            var parts = new List<string>();
            foreach (var kv in b.Buffs)
            {
                switch (kv.Key)
                {
                    case BuffType.AtkUp: parts.Add(UIKit.Col("ATK▲", UIKit.Up)); break;
                    case BuffType.DefUp: parts.Add(UIKit.Col("DEF▲", UIKit.Up)); break;
                    case BuffType.ResUp: parts.Add(UIKit.Col("RES▲", UIKit.Up)); break;
                    case BuffType.MagUp: parts.Add(UIKit.Col("MAG▲", UIKit.Up)); break;
                    case BuffType.SpdUp: parts.Add(UIKit.Col("SPD▲", UIKit.Up)); break;
                    case BuffType.AtkDown: parts.Add(UIKit.Col("ATK▼", UIKit.Down)); break;
                    case BuffType.DefDown: parts.Add(UIKit.Col("DEF▼", UIKit.Down)); break;
                    case BuffType.Taunt: parts.Add(UIKit.Col("GUARD", UIKit.Gold)); break;
                }
            }
            if (b.Defending) parts.Add(UIKit.Col("DEFEND", UIKit.Gold));
            if (!b.Alive) return UIKit.Col("KO", UIKit.Red);
            return string.Join(" ", parts);
        }

        // ------------------------------------------------------------------ enemy tags
        public void BuildEnemies(List<Battler> enemies)
        {
            foreach (var t in enemyTags) Object.Destroy(t.Root.gameObject);
            enemyTags.Clear();
            foreach (var e in enemies)
            {
                var tag = new EnemyTag { B = e };
                var rt = UIKit.Rect("EnemyTag", world, 0, 0, 300, 120);
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 1f);
                tag.Root = rt;
                tag.NameText = UIKit.Label(rt, e.Name, -100, -2, 500, 34, 24, TextAnchor.MiddleCenter, UIKit.Cream);
                tag.HPGauge = new Gauge(rt, 70, 36, 160, 8, new Color(1f, 0.4f, 0.4f));
                tag.ShieldIcon = UIKit.Icon(rt, PixelArt.Sprite("icon_shield"), 0, 52, 52, 56, "Shield");
                tag.ShieldText = UIKit.Label(tag.ShieldIcon.transform, "", 0, 0, 52, 50, 28, TextAnchor.MiddleCenter, Color.white);
                tag.BreakText = UIKit.Label(rt, "BREAK", -10, 56, 120, 44, 30, TextAnchor.MiddleLeft, new Color(1f, 0.6f, 0.2f));
                int n = e.Weaknesses.Count;
                tag.Weak = new Image[n];
                for (int k = 0; k < n; k++)
                {
                    var bg = UIKit.Box(rt, 58 + k * 40, 62, 36, 36, new Color(0.02f, 0.02f, 0.06f, 0.75f), UIKit.White, "WeakBg");
                    tag.Weak[k] = UIKit.Icon(bg.transform, PixelArt.Sprite("icon_unknown"), 3, 3, 30, 30, "Weak");
                }
                enemyTags.Add(tag);
            }
            UpdateEnemyTags(null, true);
        }

        public void UpdateEnemyTags(Battler targeted, bool instant = false)
        {
            foreach (var t in enemyTags)
            {
                var b = t.B;
                bool show = b.Alive && b.Actor != null && b.Actor.gameObject.activeInHierarchy;
                t.Root.gameObject.SetActive(show);
                if (!show) continue;
                Vector3 feet = b.Actor.transform.position + Vector3.down * 0.05f;
                t.Root.anchoredPosition = UIKit.WorldToCanvas(Canvas, feet) + new Vector2(0, -6);
                t.NameText.enabled = targeted == b;
                t.HPGauge.Root.gameObject.SetActive(targeted == b);
                t.HPGauge.Set(b.HP / (float)Mathf.Max(1, b.MaxHP), instant);
                t.HPGauge.Tick(Time.unscaledDeltaTime);
                t.ShieldIcon.enabled = !b.Broken;
                t.ShieldText.enabled = !b.Broken;
                t.ShieldText.text = b.Shield.ToString();
                t.BreakText.enabled = b.Broken;
                for (int k = 0; k < t.Weak.Length && k < b.Weaknesses.Count; k++)
                {
                    var el = b.Weaknesses[k];
                    t.Weak[k].sprite = PixelArt.Sprite(b.Revealed.Contains(el) ? PixelArt.ElementIcon(el) : "icon_unknown");
                }
            }
        }

        // ------------------------------------------------------------------ turn order
        public void ShowTurnOrder(List<Battler> current, int currentIndex, List<Battler> next)
        {
            UIKit.Clear(turnBar);
            float x = 0;
            var lbl = UIKit.Label(turnBar, "TURN", x, 0, 90, 30, 18, TextAnchor.MiddleLeft, UIKit.DimGold);
            x = 0;
            for (int i = currentIndex; i < current.Count; i++)
            {
                var b = current[i];
                if (!b.Alive) continue;
                bool now = i == currentIndex;
                float s = now ? 76 : 58;
                var frame = UIKit.Box(turnBar, x, now ? 22 : 30, s, s, b.IsPlayer ? new Color(0.15f, 0.25f, 0.55f, 0.85f) : new Color(0.45f, 0.1f, 0.15f, 0.85f), UIKit.White, "Slot");
                UIKit.Box(frame.transform, 0, 0, s, s, Color.white, UIKit.FrameSprite, "Frame").type = Image.Type.Sliced;
                var ic = UIKit.Icon(frame.transform, PixelArt.HeadSprite(b.SpriteHead, b.IsPlayer ? 16 : 20), 4, 4, s - 8, s - 8);
                if (b.Broken) ic.color = new Color(0.5f, 0.5f, 0.5f);
                x += s + 6;
            }
            x += 16;
            UIKit.Label(turnBar, "NEXT", x, 22, 80, 30, 18, TextAnchor.MiddleLeft, UIKit.DimGold);
            x += 66;
            foreach (var b in next)
            {
                if (!b.Alive) continue;
                float s = 46;
                var frame = UIKit.Box(turnBar, x, 36, s, s, b.IsPlayer ? new Color(0.15f, 0.25f, 0.55f, 0.6f) : new Color(0.45f, 0.1f, 0.15f, 0.6f), UIKit.White, "Slot");
                var ic = UIKit.Icon(frame.transform, PixelArt.HeadSprite(b.SpriteHead, b.IsPlayer ? 16 : 20), 3, 3, s - 6, s - 6);
                ic.color = new Color(1, 1, 1, 0.75f);
                x += s + 4;
            }
            lbl.enabled = false;
        }

        // ------------------------------------------------------------------ windows
        public void SetHelp(string text)
        {
            helpPanel.gameObject.SetActive(!string.IsNullOrEmpty(text));
            helpText.text = text ?? "";
        }

        public void ShowCommands(bool show, Battler b = null, int boost = 0)
        {
            commandPanel.gameObject.SetActive(show);
            boostHint.gameObject.SetActive(show);
            if (show && b != null)
            {
                float y = 742 + partyRowsIndex(b) * 108 - 30;
                commandPanel.rectTransform.anchoredPosition = new Vector2(870, -Mathf.Min(y, 712));
                boostHint.rectTransform.anchoredPosition = new Vector2(870, -(Mathf.Min(y, 712) - 36));
                boostHint.text = b.BP > 0
                    ? (boost > 0 ? "BOOST ×" + boost + "   " : "") + "<size=18>[Q]/[E] Boost  (BP " + b.BP + ")</size>"
                    : "<size=18>No BP to boost</size>";
            }
        }

        int partyRowsIndex(Battler b)
        {
            for (int i = 0; i < partyRows.Count; i++) if (partyRows[i].B == b) return i;
            return 0;
        }

        public void ShowSub(bool show, string title = "")
        {
            subPanel.gameObject.SetActive(show);
            subTitle.text = title;
        }

        public void Banner(string text)
        {
            bannerPanel.gameObject.SetActive(!string.IsNullOrEmpty(text));
            bannerText.text = text ?? "";
        }

        public void SetTargets(List<Battler> targets)
        {
            targetArrow.enabled = targets != null && targets.Count > 0;
            while (extraArrows.Count < 4)
            {
                var a = UIKit.Icon(world, PixelArt.Sprite("icon_cursor"), 0, 0, 40, 40, "TargetArrow");
                a.rectTransform.anchorMin = a.rectTransform.anchorMax = Vector2.zero;
                a.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                a.rectTransform.localRotation = Quaternion.Euler(0, 0, -90);
                extraArrows.Add(a);
            }
            for (int i = 0; i < extraArrows.Count; i++) extraArrows[i].enabled = targets != null && i + 1 < targets.Count;
            currentTargets = targets;
        }

        List<Battler> currentTargets;

        public void Tick()
        {
            foreach (var r in partyRows)
            {
                r.HPGauge.Tick(Time.unscaledDeltaTime);
                r.MPGauge.Tick(Time.unscaledDeltaTime);
            }
            CommandList.Animate();
            SubList.Animate();
            if (currentTargets != null)
            {
                float bob = Mathf.Sin(Time.unscaledTime * 8f) * 6f;
                for (int i = 0; i < currentTargets.Count; i++)
                {
                    var img = i == 0 ? targetArrow : extraArrows[i - 1];
                    img.rectTransform.anchoredPosition = UIKit.WorldToCanvas(Canvas, currentTargets[i].Top) + new Vector2(0, 26 + bob);
                }
            }
        }

        // ------------------------------------------------------------------ popups
        public void Popup(Vector3 worldPos, string text, Color color, int size = 46, float delay = 0f)
        {
            Game.I.StartCoroutine(PopupRoutine(worldPos, text, color, size, delay));
        }

        IEnumerator PopupRoutine(Vector3 worldPos, string text, Color color, int size, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            var t = UIKit.Label(world, text, 0, 0, 400, 80, size, TextAnchor.MiddleCenter, color);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            var ol = t.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(0, 0, 0, 0.9f);
            ol.effectDistance = new Vector2(2, -2);
            float dur = 1.1f;
            for (float e = 0; e < dur; e += Time.deltaTime)
            {
                if (t == null || Canvas == null) yield break;
                float k = e / dur;
                Vector2 basePos = UIKit.WorldToCanvas(Canvas, worldPos);
                float rise = k < 0.15f ? Mathf.Lerp(0, 46, k / 0.15f) : 46 + (k - 0.15f) * 30f;
                rt.anchoredPosition = basePos + new Vector2(0, rise);
                float sc = k < 0.12f ? Mathf.Lerp(1.8f, 1f, k / 0.12f) : 1f;
                rt.localScale = Vector3.one * sc;
                var c = color;
                c.a = k < 0.75f ? 1f : 1f - (k - 0.75f) / 0.25f;
                t.color = c;
                yield return null;
            }
            if (t != null) Object.Destroy(t.gameObject);
        }

        /// <summary>Big centered flash text ("BREAK!", "VICTORY").</summary>
        public IEnumerator BigText(string text, Color color, float duration = 1.1f, int size = 110)
        {
            var t = UIKit.Label(root, text, 0, 380, 1920, 200, size, TextAnchor.MiddleCenter, color);
            var ol = t.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(0.1f, 0.02f, 0, 0.95f);
            ol.effectDistance = new Vector2(4, -4);
            for (float e = 0; e < duration; e += Time.deltaTime)
            {
                if (t == null || Canvas == null) yield break;
                float k = e / duration;
                float sc = k < 0.15f ? Mathf.Lerp(2.2f, 1f, k / 0.15f) : 1f + (k - 0.15f) * 0.08f;
                t.rectTransform.localScale = new Vector3(sc, sc, 1);
                var c = color;
                c.a = k < 0.7f ? 1 : 1 - (k - 0.7f) / 0.3f;
                t.color = c;
                yield return null;
            }
            if (t != null) Object.Destroy(t.gameObject);
        }

        // ------------------------------------------------------------------ results
        public RectTransform ShowResults(string title, List<string> lines)
        {
            var panel = UIKit.Panel(root, 460, 220, 1000, 140 + lines.Count * 46, "Results");
            UIKit.Label(panel.transform, title, 0, 24, 1000, 60, 46, TextAnchor.MiddleCenter, UIKit.Gold);
            for (int i = 0; i < lines.Count; i++)
                UIKit.Label(panel.transform, lines[i], 70, 100 + i * 46, 880, 44, 30);
            return panel.rectTransform;
        }

        public void Destroy() => Object.Destroy(Canvas.gameObject);
    }
}
