using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HD2DRPG
{
    public struct Line
    {
        public string Speaker;   // null = narration
        public string Portrait;  // sprite id or null
        public string Text;
        public Line(string speaker, string portrait, string text) { Speaker = speaker; Portrait = portrait; Text = text; }
    }

    /// <summary>Field overlay: location banner, interaction prompt, dialogue, notices, title &amp; game over.</summary>
    public class FieldUI
    {
        public readonly Canvas Canvas;
        readonly RectTransform root;
        readonly Image locPanel;
        readonly Text locText;
        readonly Image promptPanel;
        readonly Text promptText;
        readonly Text controls;
        public readonly Fader Fader;
        Coroutine locRoutine;

        public FieldUI(Transform parent)
        {
            Canvas = UIKit.CreateCanvas("FieldUI", 5, parent);
            root = UIKit.Fill("Root", Canvas.transform);

            locPanel = UIKit.Box(root, 0, 60, 760, 86, Color.white, UIKit.PlateSprite, "Location");
            locText = UIKit.Label(locPanel.transform, "", 60, 0, 680, 86, 38, TextAnchor.MiddleLeft, UIKit.Cream);
            locPanel.gameObject.SetActive(false);

            promptPanel = UIKit.Box(root, 810, 900, 300, 64, Color.white, UIKit.PlateSprite, "Prompt");
            promptText = UIKit.Label(promptPanel.transform, "", 0, 0, 300, 64, 28, TextAnchor.MiddleCenter, UIKit.Cream);
            promptPanel.gameObject.SetActive(false);

            controls = UIKit.Label(root, "", 1320, 1020, 580, 40, 20, TextAnchor.MiddleRight, new Color(1, 1, 1, 0.55f));
            controls.text = "Move: WASD/Arrows   Run: Shift   Check: Z   Menu: C/Tab";

            var fadeCanvas = UIKit.CreateCanvas("Fade", 100, parent);
            Fader = new Fader(fadeCanvas.transform);
        }

        public void SetFieldHudVisible(bool v)
        {
            controls.gameObject.SetActive(v);
            if (!v) { promptPanel.gameObject.SetActive(false); locPanel.gameObject.SetActive(false); }
        }

        public void Prompt(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            if (promptPanel.gameObject.activeSelf != show) promptPanel.gameObject.SetActive(show);
            if (show) promptText.text = text;
        }

        public void ShowLocation(string name)
        {
            if (locRoutine != null) Game.I.StopCoroutine(locRoutine);
            locRoutine = Game.I.StartCoroutine(LocationRoutine(name));
        }

        IEnumerator LocationRoutine(string name)
        {
            locPanel.gameObject.SetActive(true);
            locText.text = name;
            var rt = locPanel.rectTransform;
            for (float t = 0; t < 0.5f; t += Time.deltaTime)
            {
                float k = 1 - (1 - t / 0.5f) * (1 - t / 0.5f);
                rt.anchoredPosition = new Vector2(Mathf.Lerp(-500, 0, k), -60);
                SetAlpha(locPanel, locText, k);
                yield return null;
            }
            yield return new WaitForSeconds(2.6f);
            for (float t = 0; t < 0.6f; t += Time.deltaTime)
            {
                SetAlpha(locPanel, locText, 1 - t / 0.6f);
                yield return null;
            }
            locPanel.gameObject.SetActive(false);
            locRoutine = null;
        }

        static void SetAlpha(Image i, Text t, float a)
        {
            i.color = new Color(1, 1, 1, a);
            var c = t.color; c.a = a; t.color = c;
        }

        // ------------------------------------------------------------------ dialogue
        public IEnumerator Dialogue(IList<Line> lines)
        {
            var panel = UIKit.Panel(root, 160, 760, 1600, 270, "Dialogue");
            var nameBg = UIKit.Box(root, 200, 712, 420, 60, Color.white, UIKit.PlateSprite, "NamePlate");
            var nameText = UIKit.Label(nameBg.transform, "", 40, 0, 380, 60, 32, TextAnchor.MiddleLeft, UIKit.Gold);
            var portraitBg = UIKit.Box(panel.transform, 36, 36, 196, 196, new Color(0.02f, 0.02f, 0.06f, 0.7f), UIKit.White, "PortraitBG");
            UIKit.Box(portraitBg.transform, 0, 0, 196, 196, Color.white, UIKit.FrameSprite, "Frame");
            var portrait = UIKit.Icon(portraitBg.transform, null, 8, 8, 180, 180);
            var text = UIKit.Label(panel.transform, "", 270, 40, 1280, 200, 34, TextAnchor.UpperLeft);
            var arrow = UIKit.Label(panel.transform, "▼", 1530, 216, 40, 40, 24, TextAnchor.MiddleCenter, UIKit.Gold);

            foreach (var line in lines)
            {
                bool hasSpeaker = !string.IsNullOrEmpty(line.Speaker);
                nameBg.gameObject.SetActive(hasSpeaker);
                nameText.text = line.Speaker ?? "";
                bool hasPortrait = !string.IsNullOrEmpty(line.Portrait);
                portraitBg.gameObject.SetActive(hasPortrait);
                if (hasPortrait) portrait.sprite = PixelArt.HeadSprite(line.Portrait, line.Portrait == "darklord" ? 20 : 16);
                text.rectTransform.anchoredPosition = new Vector2(hasPortrait ? 270 : 70, -40);
                text.rectTransform.sizeDelta = new Vector2(hasPortrait ? 1280 : 1460, 200);
                text.fontStyle = hasSpeaker ? FontStyle.Normal : FontStyle.Italic;

                arrow.enabled = false;
                string full = line.Text;
                float shown = 0;
                yield return null;
                while (shown < full.Length)
                {
                    shown += Time.deltaTime * 55f;
                    int n = Mathf.Min(full.Length, (int)shown);
                    text.text = full.Substring(0, n);
                    if (GameInput.Confirm || GameInput.Cancel) { shown = full.Length; text.text = full; yield return null; break; }
                    yield return null;
                }
                text.text = full;
                arrow.enabled = true;
                while (!(GameInput.Confirm || GameInput.Cancel))
                {
                    arrow.rectTransform.anchoredPosition = new Vector2(1530, -216 - Mathf.Abs(Mathf.Sin(Time.time * 5f)) * 6f);
                    yield return null;
                }
                AudioManager.Play("cursor", 0.5f);
                yield return null;
            }
            Object.Destroy(panel.gameObject);
            Object.Destroy(nameBg.gameObject);
            GameInput.Consume();
        }

        /// <summary>Centered notice window, dismissed with Z/X.</summary>
        public IEnumerator Notice(string title, IList<string> lines)
        {
            float h = 120 + lines.Count * 50;
            var panel = UIKit.Panel(root, 560, 400 - h / 2, 800, h, "Notice");
            UIKit.Label(panel.transform, title, 0, 22, 800, 54, 36, TextAnchor.MiddleCenter, UIKit.Gold);
            for (int i = 0; i < lines.Count; i++)
                UIKit.Label(panel.transform, lines[i], 0, 86 + i * 50, 800, 48, 30, TextAnchor.MiddleCenter);
            yield return null;
            yield return new WaitForSeconds(0.2f);
            while (!(GameInput.Confirm || GameInput.Cancel)) yield return null;
            AudioManager.Play("cursor", 0.5f);
            Object.Destroy(panel.gameObject);
            GameInput.Consume();
        }

        // ------------------------------------------------------------------ title
        public IEnumerator Title(System.Action<int> result)
        {
            var c = UIKit.Fill("Title", root);
            var shade = UIKit.Box(c, 0, 0, 1920, 1080, new Color(0, 0, 0, 0.25f), null, "Shade");
            shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = Vector2.one;
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;

            var sub = UIKit.Label(c, "— AN HD-2D TALE —", 0, 210, 1920, 50, 26, TextAnchor.MiddleCenter, UIKit.DimGold);
            var title = UIKit.Label(c, "Chronicle of the\nEndless Night", 0, 260, 1920, 260, 108, TextAnchor.MiddleCenter, UIKit.Cream);
            var ol = title.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(0.25f, 0.12f, 0.02f, 0.9f);
            ol.effectDistance = new Vector2(3, -3);
            UIKit.Box(c, 660, 540, 600, 4, UIKit.DimGold, null, "Rule");

            var panel = UIKit.Panel(c, 760, 610, 400, 250, "TitleMenu");
            var list = new ListView(panel.transform, 70, 28, 280, 64, 3) { FontSize = 34 };
            list.SetItems(new[]
            {
                new ListView.Item { Label = "New Game" },
                new ListView.Item { Label = "Controls" },
                new ListView.Item { Label = "Quit" },
            }, false);
            var hint = UIKit.Label(c, "Z / Enter: Select      Arrows: Move", 0, 900, 1920, 40, 22, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.5f));
            UIKit.Label(c, "Aren · Gareth · Theia  vs.  Malzarath, the Dark Lord", 0, 1010, 1920, 40, 20, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.35f));

            float a = 0;
            yield return null;
            while (true)
            {
                a = Mathf.MoveTowards(a, 1, Time.deltaTime * 0.8f);
                title.color = new Color(UIKit.Cream.r, UIKit.Cream.g, UIKit.Cream.b, a);
                list.Animate();
                list.Navigate();
                if (GameInput.Confirm)
                {
                    AudioManager.Play("confirm", 0.8f);
                    if (list.Index == 1)
                    {
                        panel.gameObject.SetActive(false);
                        yield return Notice("Controls", new[]
                        {
                            "Move: WASD / Arrow keys / Left stick",
                            "Run: hold Shift",
                            "Confirm / Check: Z, Enter, Space (A)",
                            "Cancel: X, Backspace, Esc (B)",
                            "Menu: C, Tab, M (Y / Start)",
                            "Battle Boost: E up / Q down (RB / LB)",
                            "Hit weaknesses to break shields!",
                        });
                        panel.gameObject.SetActive(true);
                        yield return null;
                        continue;
                    }
                    result(list.Index);
                    break;
                }
                yield return null;
            }
            Object.Destroy(c.gameObject);
            GameInput.Consume();
        }

        public IEnumerator GameOver(System.Action<int> result)
        {
            var c = UIKit.Fill("GameOver", root);
            var shade = UIKit.Box(c, 0, 0, 1920, 1080, new Color(0.05f, 0, 0.02f, 0.85f), null, "Shade");
            shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = Vector2.one;
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            UIKit.Label(c, "The light fades...", 0, 330, 1920, 120, 80, TextAnchor.MiddleCenter, UIKit.Red);
            var panel = UIKit.Panel(c, 700, 560, 520, 190, "GameOverMenu");
            var list = new ListView(panel.transform, 70, 28, 400, 64, 2) { FontSize = 32 };
            list.SetItems(new[] { new ListView.Item { Label = "Retry from crystal" }, new ListView.Item { Label = "Return to title" } }, false);
            yield return null;
            while (true)
            {
                list.Animate();
                list.Navigate();
                if (GameInput.Confirm) { AudioManager.Play("confirm"); result(list.Index); break; }
                yield return null;
            }
            Object.Destroy(c.gameObject);
            GameInput.Consume();
        }

        public IEnumerator Credits()
        {
            var c = UIKit.Fill("Credits", root);
            var shade = UIKit.Box(c, 0, 0, 1920, 1080, new Color(0, 0, 0, 0.92f), null, "Shade");
            shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = Vector2.one;
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            string[] lines =
            {
                UIKit.Col("Chronicle of the Endless Night", UIKit.Gold), "",
                "The Dark Lord Malzarath has fallen.", "Dawn breaks over the castle for the first time in a hundred years.", "",
                UIKit.Col("Aren", UIKit.Gold) + " — Spellblade", UIKit.Col("Gareth", UIKit.Gold) + " — Guardian", UIKit.Col("Theia", UIKit.Gold) + " — Arch Mage of Olympus", "",
                "Pixel art, music & code generated procedurally", "Built with Unity 6.3 LTS · Universal Render Pipeline", "",
                UIKit.Col("THE END", UIKit.Cream),
            };
            var texts = new List<Text>();
            for (int i = 0; i < lines.Length; i++)
                texts.Add(UIKit.Label(c, lines[i], 0, 1100 + i * 64, 1920, 60, i == 0 ? 56 : i == lines.Length - 1 ? 72 : 34, TextAnchor.MiddleCenter));
            float scroll = 0, target = 1100 + (lines.Length - 1) * 64 - 480;
            while (scroll < target)
            {
                scroll += Time.deltaTime * 70f * (GameInput.Run || GameInput.Move.y < -0.5f ? 6f : 1f);
                for (int i = 0; i < texts.Count; i++)
                    texts[i].rectTransform.anchoredPosition = new Vector2(0, -(1100 + i * 64 - scroll));
                yield return null;
            }
            yield return new WaitForSeconds(1f);
            while (!GameInput.Confirm) yield return null;
            Object.Destroy(c.gameObject);
            GameInput.Consume();
        }
    }
}
