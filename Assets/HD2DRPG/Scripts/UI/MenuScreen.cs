using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace HD2DRPG
{
    /// <summary>
    /// The field menu: party overview cards, Items, Skills, Equipment (with stat preview),
    /// Status (full parameters) and Config. Keyboard / gamepad driven.
    /// </summary>
    public class MenuScreen
    {
        readonly Canvas canvas;
        readonly RectTransform root;
        RectTransform content;
        Text helpText;
        readonly PartyState state;
        int member;

        static readonly string[] Commands = { "Items", "Skills", "Equipment", "Status", "Config", "Close" };
        static readonly string[] CommandHelp =
        {
            "Use restorative items on the party.",
            "View abilities. Healing arts can be used outside of battle.",
            "Change weapons, armor and accessories.",
            "View a party member's parameters in detail.",
            "Adjust volume and the tilt-shift depth of field.",
            "Return to the castle."
        };

        public MenuScreen(Transform parent, PartyState state)
        {
            this.state = state;
            canvas = UIKit.CreateCanvas("Menu", 20, parent);
            root = UIKit.Fill("Root", canvas.transform);
            var dim = UIKit.Box(root, 0, 0, 1920, 1080, new Color(0.01f, 0.0f, 0.04f, 0.55f), null, "Dim");
            dim.rectTransform.anchorMin = Vector2.zero; dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;
            canvas.gameObject.SetActive(false);
        }

        // ================================================================== entry
        public IEnumerator Run()
        {
            canvas.gameObject.SetActive(true);
            PostFX.Instance?.SetMenuBlur(true);
            AudioManager.Play("confirm", 0.6f);
            int cmd = 0;
            while (true)
            {
                var list = BuildMain(cmd);
                yield return null;
                bool close = false;
                while (true)
                {
                    list.Animate();
                    if (list.Navigate()) helpText.text = CommandHelp[list.Index];
                    if (GameInput.Cancel || GameInput.Menu) { close = true; break; }
                    if (GameInput.Confirm) break;
                    yield return null;
                }
                if (close) break;
                cmd = list.Index;
                AudioManager.Play("confirm", 0.6f);
                yield return null;
                switch (Commands[cmd])
                {
                    case "Items": yield return ItemsScreen(); break;
                    case "Skills": yield return SkillsScreen(); break;
                    case "Equipment": yield return EquipScreen(); break;
                    case "Status": yield return StatusScreen(); break;
                    case "Config": yield return ConfigScreen(); break;
                    case "Close": close = true; break;
                }
                if (close) break;
            }
            AudioManager.Play("cancel", 0.6f);
            PostFX.Instance?.SetMenuBlur(false);
            canvas.gameObject.SetActive(false);
            GameInput.Consume();
        }

        RectTransform NewContent()
        {
            if (content) Object.Destroy(content.gameObject);
            content = UIKit.Fill("Content", root);
            return content;
        }

        void HelpBar(string text)
        {
            var bar = UIKit.Panel(content, 60, 982, 1800, 74, "HelpBar");
            helpText = UIKit.Label(bar.transform, text, 36, 0, 1730, 74, 26);
        }

        // ================================================================== main
        ListView BuildMain(int cmd)
        {
            var c = NewContent();
            var cmdPanel = UIKit.Panel(c, 60, 70, 360, 470, "Commands");
            UIKit.Label(cmdPanel.transform, "MENU", 0, 14, 360, 40, 24, TextAnchor.MiddleCenter, UIKit.DimGold);
            var list = new ListView(cmdPanel.transform, 46, 64, 280, 64, 6) { FontSize = 34 };
            list.SetItems(Commands.Select(x => new ListView.Item { Label = x }), false);
            list.Index = cmd;
            list.Refresh();

            var info = UIKit.Panel(c, 60, 560, 360, 190, "Info");
            UIKit.Label(info.transform, "Gold", 36, 22, 140, 44, 26, TextAnchor.MiddleLeft, UIKit.DimGold);
            UIKit.Label(info.transform, state.Gold.ToString("N0") + " <size=22>G</size>", 120, 22, 200, 44, 32, TextAnchor.MiddleRight);
            UIKit.Label(info.transform, "Time", 36, 76, 140, 44, 26, TextAnchor.MiddleLeft, UIKit.DimGold);
            int sec = (int)state.PlayTime;
            UIKit.Label(info.transform, string.Format("{0:00}:{1:00}:{2:00}", sec / 3600, sec / 60 % 60, sec % 60), 120, 76, 200, 44, 32, TextAnchor.MiddleRight);
            UIKit.Label(info.transform, "Chests", 36, 128, 140, 44, 26, TextAnchor.MiddleLeft, UIKit.DimGold);
            UIKit.Label(info.transform, state.Flags.Count(f => f.StartsWith("chest_")) + "/8 <size=20>chests</size>", 120, 128, 200, 44, 28, TextAnchor.MiddleRight);

            var loc = UIKit.Panel(c, 60, 770, 360, 190, "Location");
            UIKit.Label(loc.transform, "Location", 36, 20, 290, 36, 22, TextAnchor.MiddleLeft, UIKit.DimGold);
            UIKit.Label(loc.transform, Game.I != null ? Game.I.LocationName : "", 36, 58, 300, 70, 28);
            UIKit.Label(loc.transform, "Castle of Endless Night", 36, 128, 300, 40, 20, TextAnchor.MiddleLeft, UIKit.Dim);

            for (int i = 0; i < state.Members.Count; i++) PartyCard(c, state.Members[i], 450, 70 + i * 300, 1410, 284);
            HelpBar(CommandHelp[cmd]);
            return list;
        }

        void PartyCard(Transform parent, PartyMember m, float x, float y, float w, float h)
        {
            var card = UIKit.Panel(parent, x, y, w, h, "Card_" + m.Name);
            var t = card.transform;
            var s = m.Stats;
            // portrait
            var bg = UIKit.Box(t, 30, 28, 190, 228, new Color(0.02f, 0.02f, 0.06f, 0.6f), UIKit.White, "PortraitBG");
            UIKit.Box(bg.transform, 0, 0, 190, 228, Color.white, UIKit.FrameSprite, "Frame");
            var size = PixelArt.Size(m.Def.SpritePrefix + "_stand");
            float scale = Mathf.Floor(Mathf.Min(170f / size.x, 210f / size.y));
            var img = UIKit.Icon(bg.transform, PixelArt.Sprite(m.Def.SpritePrefix + "_stand"), (190 - size.x * scale) / 2, (228 - size.y * scale) / 2 + 4, size.x * scale, size.y * scale);
            if (m.IsDead) img.color = new Color(0.5f, 0.4f, 0.5f);

            UIKit.Label(t, m.Name, 250, 24, 300, 54, 44);
            UIKit.Label(t, m.Def.Title, 250, 76, 300, 36, 24, TextAnchor.MiddleLeft, UIKit.Gold);
            UIKit.Label(t, "Lv", 560, 30, 60, 44, 24, TextAnchor.MiddleLeft, UIKit.DimGold);
            UIKit.Label(t, m.Level.ToString(), 600, 22, 80, 54, 44);
            if (m.IsDead) UIKit.Label(t, "KO", 250, 112, 120, 40, 28, TextAnchor.MiddleLeft, UIKit.Red);

            // vitals
            UIKit.Label(t, "HP", 250, 150, 60, 40, 24, TextAnchor.MiddleLeft, UIKit.DimGold);
            UIKit.Label(t, m.HP + " <size=24>/ " + s.MaxHP + "</size>", 300, 146, 290, 44, 34, TextAnchor.MiddleRight);
            new Gauge(t, 300, 192, 290, 10, UIKit.HPColor).Set(m.HP / (float)s.MaxHP, true);
            UIKit.Label(t, "MP", 250, 206, 60, 40, 24, TextAnchor.MiddleLeft, UIKit.DimGold);
            UIKit.Label(t, m.MP + " <size=22>/ " + s.MaxMP + "</size>", 300, 202, 290, 44, 30, TextAnchor.MiddleRight, UIKit.MPColor);
            new Gauge(t, 300, 246, 290, 8, UIKit.MPColor).Set(m.MP / (float)Mathf.Max(1, s.MaxMP), true);

            // stats summary
            float sx = 650;
            Stat[] shown = { Stat.Atk, Stat.Def, Stat.Mag, Stat.Res, Stat.Spd, Stat.Luck };
            for (int i = 0; i < shown.Length; i++)
            {
                float cx = sx + (i % 2) * 210, cy = 30 + (i / 2) * 50;
                UIKit.Label(t, Stats.Short(shown[i]), cx, cy, 90, 44, 24, TextAnchor.MiddleLeft, UIKit.DimGold);
                UIKit.Label(t, s[shown[i]].ToString(), cx + 70, cy, 110, 44, 32, TextAnchor.MiddleRight);
            }
            UIKit.Label(t, "EXP", sx, 186, 90, 36, 22, TextAnchor.MiddleLeft, UIKit.DimGold);
            UIKit.Label(t, m.Exp.ToString("N0"), sx + 60, 186, 200, 36, 24, TextAnchor.MiddleRight);
            UIKit.Label(t, "Next", sx + 280, 186, 90, 36, 22, TextAnchor.MiddleLeft, UIKit.DimGold);
            UIKit.Label(t, m.ExpToNext.ToString("N0"), sx + 330, 186, 150, 36, 24, TextAnchor.MiddleRight);
            new Gauge(t, sx, 226, 480, 7, UIKit.Gold).Set(m.ExpProgress, true);

            // equipment summary
            float ex = 1150;
            string[] slotNames = { "Weapon", "Armor", "Head", "Acc." };
            for (int i = 0; i < 4; i++)
            {
                string id = m.Equipment[i];
                UIKit.Icon(t, PixelArt.Sprite(SlotIcon((EquipSlot)i, m)), ex, 30 + i * 52, 30, 30);
                UIKit.Label(t, id != null ? Database.Equips[id].Name : UIKit.Col("—", UIKit.Dim), ex + 40, 22 + i * 52, 230, 46, 22);
            }
        }

        static string SlotIcon(EquipSlot slot, PartyMember m)
        {
            switch (slot)
            {
                case EquipSlot.Weapon: return PixelArt.ElementIcon(m.Def.WeaponType);
                case EquipSlot.Armor: return "icon_shield";
                case EquipSlot.Head: return "icon_light";
                default: return "icon_ice";
            }
        }

        // ================================================================== shared helpers
        /// <summary>Compact member list used as a target picker.</summary>
        ListView PartyTargetList(Transform parent, float x, float y)
        {
            var panel = UIKit.Panel(parent, x, y, 520, 330, "Targets");
            UIKit.Label(panel.transform, "Party", 30, 12, 300, 36, 22, TextAnchor.MiddleLeft, UIKit.DimGold);
            var list = new ListView(panel.transform, 40, 56, 440, 84, 3) { FontSize = 28 };
            RefreshTargets(list);
            list.SetActive(false);
            return list;
        }

        void RefreshTargets(ListView list)
        {
            list.SetItems(state.Members.Select(m => new ListView.Item
            {
                Label = m.Name + (m.IsDead ? UIKit.Col("  KO", UIKit.Red) : "") +
                        "\n<size=20>" + UIKit.Col("HP", UIKit.DimGold) + " " + m.HP + "/" + m.Stats.MaxHP + "   " +
                        UIKit.Col("MP", UIKit.DimGold) + " " + m.MP + "/" + m.Stats.MaxMP + "</size>",
                Icon = PixelArt.HeadSprite(m.Def.SpritePrefix + "_stand", 16),
                Data = m
            }));
        }

        IEnumerator PickTarget(ListView list, System.Func<PartyMember, bool> valid, System.Action<PartyMember> result)
        {
            list.SetActive(true);
            yield return null;
            while (true)
            {
                list.Animate();
                list.Navigate();
                if (GameInput.Cancel) { AudioManager.Play("cancel", 0.6f); list.SetActive(false); result(null); yield break; }
                if (GameInput.Confirm)
                {
                    var m = (PartyMember)list.Current.Data;
                    if (!valid(m)) { AudioManager.Play("error", 0.7f); yield return null; continue; }
                    list.SetActive(false);
                    result(m);
                    yield break;
                }
                yield return null;
            }
        }

        bool SwitchMember()
        {
            if (GameInput.PageRight) { member = (member + 1) % state.Members.Count; AudioManager.Play("cursor", 0.5f); return true; }
            if (GameInput.PageLeft) { member = (member - 1 + state.Members.Count) % state.Members.Count; AudioManager.Play("cursor", 0.5f); return true; }
            return false;
        }

        void MemberHeader(Transform c, PartyMember m, float x, float y, float w)
        {
            var p = UIKit.Panel(c, x, y, w, 120, "Header");
            UIKit.Icon(p.transform, PixelArt.HeadSprite(m.Def.SpritePrefix + "_stand", 16), 30, 20, 80, 80);
            UIKit.Label(p.transform, m.Name, 130, 14, 260, 54, 40);
            UIKit.Label(p.transform, m.Def.Title + "   " + UIKit.Col("Lv", UIKit.DimGold) + " " + m.Level, 130, 62, 400, 40, 24, TextAnchor.MiddleLeft, UIKit.Gold);
            UIKit.Label(p.transform, "◀ Q        E ▶", w - 260, 30, 230, 60, 24, TextAnchor.MiddleRight, UIKit.DimGold);
            for (int i = 0; i < state.Members.Count; i++)
            {
                var dot = UIKit.Box(p.transform, w / 2 - 40 + i * 30, 50, 18, 18, i == member ? UIKit.Gold : new Color(0.3f, 0.3f, 0.4f), UIKit.White, "Dot");
            }
        }

        // ================================================================== items
        IEnumerator ItemsScreen()
        {
            int index = 0;
            while (true)
            {
                var c = NewContent();
                var panel = UIKit.Panel(c, 450, 70, 880, 890, "ItemList");
                UIKit.Label(panel.transform, "Items", 34, 14, 400, 44, 28, TextAnchor.MiddleLeft, UIKit.Gold);
                var list = new ListView(panel.transform, 44, 70, 790, 58, 13) { FontSize = 30 };
                var items = state.Items.Where(kv => kv.Value > 0).Select(kv => Database.Items[kv.Key]).OrderBy(i => i.BattleOnly).ThenBy(i => i.Name).ToList();
                list.SetItems(items.Select(i => new ListView.Item
                {
                    Label = i.Name, Right = "×" + state.ItemCount(i.Id), Enabled = !i.BattleOnly,
                    Icon = PixelArt.Sprite(i.BattleOnly ? PixelArt.ElementIcon(i.Element) : i.Revive ? "icon_light" : i.HealMP > 0 && i.HealHP == 0 ? "icon_ice" : "icon_fire"),
                    Data = i
                }), false);
                list.Index = Mathf.Clamp(index, 0, Mathf.Max(0, items.Count - 1));
                list.Refresh();

                var desc = UIKit.Panel(c, 1350, 70, 510, 520, "Description");
                var descTitle = UIKit.Label(desc.transform, "", 34, 20, 450, 50, 32, TextAnchor.MiddleLeft, UIKit.Gold);
                var descText = UIKit.Label(desc.transform, "", 34, 80, 450, 400, 26, TextAnchor.UpperLeft);
                var targets = PartyTargetList(c, 1345, 610);
                HelpBar("Z: Use   X: Back");
                System.Action upd = () =>
                {
                    var it = list.Current?.Data as ItemDef;
                    descTitle.text = it?.Name ?? "No items";
                    descText.text = it == null ? "" : it.Description + (it.BattleOnly ? "\n\n" + UIKit.Col("Can only be used in battle.", UIKit.Dim) : "");
                };
                upd();
                if (items.Count == 0) list.SetActive(false);
                yield return null;
                bool back = false, used = false;
                while (true)
                {
                    list.Animate();
                    if (list.Navigate()) upd();
                    if (GameInput.Cancel) { AudioManager.Play("cancel", 0.6f); back = true; break; }
                    if (GameInput.Confirm && items.Count > 0)
                    {
                        var it = (ItemDef)list.Current.Data;
                        if (it.BattleOnly) { AudioManager.Play("error", 0.7f); yield return null; continue; }
                        AudioManager.Play("confirm", 0.6f);
                        list.SetActive(false);
                        if (it.Target == TargetType.AllAllies)
                        {
                            UseItemOn(it, state.Members.Where(m => !m.IsDead).ToList());
                            used = true;
                            break;
                        }
                        PartyMember target = null;
                        yield return PickTarget(targets, m => it.Revive ? m.IsDead : !m.IsDead, m => target = m);
                        if (target != null) { UseItemOn(it, new List<PartyMember> { target }); used = true; break; }
                        list.SetActive(true);
                    }
                    yield return null;
                }
                index = list.Index;
                if (back) yield break;
                if (used) yield return null;
            }
        }

        void UseItemOn(ItemDef it, List<PartyMember> targets)
        {
            if (!state.UseItem(it.Id)) return;
            foreach (var m in targets)
            {
                var s = m.Stats;
                if (it.Revive)
                {
                    if (m.IsDead) m.HP = Mathf.Max(1, Mathf.RoundToInt(s.MaxHP * it.HealPercent));
                    continue;
                }
                if (m.IsDead) continue;
                m.HP = Mathf.Min(s.MaxHP, m.HP + it.HealHP + Mathf.RoundToInt(s.MaxHP * it.HealPercent));
                m.MP = Mathf.Min(s.MaxMP, m.MP + it.HealMP);
            }
            AudioManager.Play(it.Revive ? "light" : "heal", 0.8f);
        }

        // ================================================================== skills
        IEnumerator SkillsScreen()
        {
            int index = 0;
            while (true)
            {
                var m = state.Members[member];
                var c = NewContent();
                MemberHeader(c, m, 450, 70, 880);
                var panel = UIKit.Panel(c, 450, 205, 880, 755, "SkillList");
                var skills = m.Skills;
                var list = new ListView(panel.transform, 44, 30, 790, 62, 11) { FontSize = 30 };
                list.SetItems(skills.Select(s => new ListView.Item
                {
                    Label = s.Name, Right = s.MpCost + " <size=20>MP</size>",
                    Enabled = s.UsableInField && m.MP >= s.MpCost && !m.IsDead,
                    Color = s.UsableInField ? (Color?)null : new Color(0.85f, 0.85f, 0.9f),
                    Icon = PixelArt.Sprite(s.Element != Element.None ? PixelArt.ElementIcon(s.Element) : (s.Kind == SkillKind.Heal || s.Kind == SkillKind.Revive ? "icon_light" : "icon_shield")),
                    Data = s
                }), false);
                list.Index = Mathf.Clamp(index, 0, Mathf.Max(0, skills.Count - 1));
                list.Refresh();
                var desc = UIKit.Panel(c, 1350, 70, 510, 520, "Description");
                var descTitle = UIKit.Label(desc.transform, "", 34, 20, 450, 50, 32, TextAnchor.MiddleLeft, UIKit.Gold);
                var descInfo = UIKit.Label(desc.transform, "", 34, 70, 450, 40, 22, TextAnchor.MiddleLeft, UIKit.Cyan);
                var descText = UIKit.Label(desc.transform, "", 34, 120, 450, 380, 26, TextAnchor.UpperLeft);
                var targets = PartyTargetList(c, 1345, 610);
                HelpBar("Z: Use (field arts)   X: Back   Q/E: Switch member");
                System.Action upd = () =>
                {
                    var s = list.Current?.Data as SkillDef;
                    descTitle.text = s?.Name ?? "";
                    descInfo.text = s == null ? "" : KindLabel(s) + (s.Element != Element.None ? "  ·  " + Database.ElementName(s.Element) : "") + "  ·  " + TargetLabel(s.Target);
                    descText.text = s == null ? "" : s.Description + (s.UsableInField ? "\n\n" + UIKit.Col("Usable outside battle.", UIKit.Up) : "");
                };
                upd();
                yield return null;
                bool back = false, rebuild = false;
                while (true)
                {
                    list.Animate();
                    if (list.Navigate()) upd();
                    if (SwitchMember()) { index = 0; rebuild = true; break; }
                    if (GameInput.Cancel) { AudioManager.Play("cancel", 0.6f); back = true; break; }
                    if (GameInput.Confirm && skills.Count > 0)
                    {
                        var s = (SkillDef)list.Current.Data;
                        if (!list.Current.Enabled) { AudioManager.Play("error", 0.7f); yield return null; continue; }
                        AudioManager.Play("confirm", 0.6f);
                        list.SetActive(false);
                        if (s.Target == TargetType.AllAllies)
                        {
                            m.MP -= s.MpCost;
                            foreach (var t in state.Members) FieldSkill(m, s, t);
                            rebuild = true; break;
                        }
                        PartyMember target = null;
                        yield return PickTarget(targets, t => s.Kind == SkillKind.Revive ? t.IsDead : !t.IsDead, t => target = t);
                        if (target != null) { m.MP -= s.MpCost; FieldSkill(m, s, target); rebuild = true; break; }
                        list.SetActive(true);
                    }
                    yield return null;
                }
                index = list.Index;
                if (back) yield break;
                if (rebuild) yield return null;
            }
        }

        static string KindLabel(SkillDef s)
        {
            switch (s.Kind)
            {
                case SkillKind.Physical: return "Physical";
                case SkillKind.Magic: return "Magic";
                case SkillKind.Heal: return "Healing";
                case SkillKind.Revive: return "Revival";
                case SkillKind.Buff: return "Support";
                case SkillKind.Debuff: return "Hindrance";
                default: return "Guard";
            }
        }

        static string TargetLabel(TargetType t)
        {
            switch (t)
            {
                case TargetType.Self: return "Self";
                case TargetType.OneAlly: return "One ally";
                case TargetType.AllAllies: return "All allies";
                case TargetType.OneEnemy: return "One foe";
                case TargetType.AllEnemies: return "All foes";
                default: return "One fallen ally";
            }
        }

        void FieldSkill(PartyMember user, SkillDef s, PartyMember t)
        {
            var ts = t.Stats;
            if (s.Kind == SkillKind.Revive)
            {
                if (t.IsDead) t.HP = Mathf.Max(1, Mathf.RoundToInt(ts.MaxHP * s.Power));
                AudioManager.Play("light", 0.8f);
                return;
            }
            if (t.IsDead) return;
            int amt = Mathf.RoundToInt(user.Stats.Mag * s.Power + 40);
            t.HP = Mathf.Min(ts.MaxHP, t.HP + amt);
            AudioManager.Play("heal", 0.8f);
        }

        // ================================================================== equipment
        IEnumerator EquipScreen()
        {
            int slotIndex = 0;
            while (true)
            {
                var m = state.Members[member];
                var c = NewContent();
                MemberHeader(c, m, 450, 70, 880);
                var slotPanel = UIKit.Panel(c, 450, 205, 880, 320, "Slots");
                var slots = new ListView(slotPanel.transform, 44, 26, 790, 66, 4) { FontSize = 30 };
                string[] slotNames = { "Weapon", "Armor", "Head", "Accessory" };
                slots.SetItems(Enumerable.Range(0, 4).Select(i => new ListView.Item
                {
                    Label = UIKit.Col(slotNames[i], UIKit.DimGold),
                    Right = m.Equipment[i] != null ? Database.Equips[m.Equipment[i]].Name : UIKit.Col("— none —", UIKit.Dim),
                    Icon = PixelArt.Sprite(SlotIcon((EquipSlot)i, m)),
                    Data = i
                }), false);
                slots.Index = slotIndex;
                slots.Refresh();

                var candPanel = UIKit.Panel(c, 450, 540, 880, 420, "Candidates");
                var candTitle = UIKit.Label(candPanel.transform, "Inventory", 34, 12, 400, 40, 24, TextAnchor.MiddleLeft, UIKit.DimGold);
                var cands = new ListView(candPanel.transform, 44, 60, 790, 56, 6) { FontSize = 28 };
                cands.SetActive(false);

                var statPanel = UIKit.Panel(c, 1350, 70, 510, 890, "Stats");
                UIKit.Label(statPanel.transform, "Parameters", 34, 18, 400, 44, 28, TextAnchor.MiddleLeft, UIKit.Gold);
                var statRows = new List<(Text cur, Text arrow, Text next)>();
                for (int i = 0; i < Stats.All.Length; i++)
                {
                    float y = 80 + i * 62;
                    UIKit.Label(statPanel.transform, Stats.Label(Stats.All[i]), 34, y, 200, 50, 26, TextAnchor.MiddleLeft, UIKit.DimGold);
                    var cur = UIKit.Label(statPanel.transform, "", 220, y, 100, 50, 30, TextAnchor.MiddleRight);
                    var arrow = UIKit.Label(statPanel.transform, "", 330, y, 40, 50, 24, TextAnchor.MiddleCenter, UIKit.Dim);
                    var next = UIKit.Label(statPanel.transform, "", 370, y, 100, 50, 30, TextAnchor.MiddleRight);
                    statRows.Add((cur, arrow, next));
                }
                var itemDesc = UIKit.Label(statPanel.transform, "", 34, 590, 450, 280, 24, TextAnchor.UpperLeft);
                HelpBar("Z: Change   X: Back   Q/E: Switch member");

                System.Action<string[]> preview = eq =>
                {
                    var a = m.Stats;
                    var b = eq != null ? m.StatsWith(eq) : a;
                    for (int i = 0; i < Stats.All.Length; i++)
                    {
                        var st = Stats.All[i];
                        statRows[i].cur.text = a[st].ToString();
                        bool show = eq != null;
                        statRows[i].arrow.text = show ? "→" : "";
                        statRows[i].next.text = show ? b[st].ToString() : "";
                        statRows[i].next.color = b[st] > a[st] ? UIKit.Up : b[st] < a[st] ? UIKit.Down : UIKit.Cream;
                    }
                };
                System.Action showSlotDesc = () =>
                {
                    string id = m.Equipment[slots.Index];
                    itemDesc.text = id != null ? UIKit.Col(Database.Equips[id].Name, UIKit.Gold) + "\n" + Database.Equips[id].Description + BonusText(Database.Equips[id]) : "";
                };
                preview(null);
                showSlotDesc();
                yield return null;

                bool back = false, rebuild = false;
                while (true)
                {
                    slots.Animate();
                    if (slots.Navigate()) showSlotDesc();
                    if (SwitchMember()) { rebuild = true; break; }
                    if (GameInput.Cancel) { AudioManager.Play("cancel", 0.6f); back = true; break; }
                    if (GameInput.Confirm)
                    {
                        AudioManager.Play("confirm", 0.6f);
                        int si = slots.Index;
                        var slot = (EquipSlot)si;
                        var options = new List<ListView.Item>();
                        foreach (var kv in state.EquipBag.Where(kv => kv.Value > 0))
                        {
                            var e = Database.Equips[kv.Key];
                            if (e.Slot != slot) continue;
                            bool ok = e.CanEquip(m.Id);
                            options.Add(new ListView.Item { Label = e.Name, Right = "×" + kv.Value, Enabled = ok, Data = e.Id });
                        }
                        if (m.Equipment[si] != null) options.Add(new ListView.Item { Label = UIKit.Col("Remove", UIKit.Dim), Data = "" });
                        if (options.Count == 0)
                        {
                            candTitle.text = "Nothing to equip in this slot";
                            AudioManager.Play("error", 0.6f);
                            yield return null;
                            continue;
                        }
                        candTitle.text = "Choose " + slotNames[si].ToLower();
                        slots.SetActive(false);
                        cands.SetItems(options, false);
                        cands.SetActive(true);
                        System.Action previewCand = () =>
                        {
                            string id = (string)cands.Current.Data;
                            var eq = (string[])m.Equipment.Clone();
                            eq[si] = id == "" ? null : id;
                            preview(eq);
                            itemDesc.text = id == "" ? "Unequip the current item." :
                                UIKit.Col(Database.Equips[id].Name, UIKit.Gold) + "\n" + Database.Equips[id].Description + BonusText(Database.Equips[id]) +
                                (cands.Current.Enabled ? "" : "\n" + UIKit.Col(m.Name + " cannot equip this.", UIKit.Red));
                        };
                        previewCand();
                        yield return null;
                        while (true)
                        {
                            cands.Animate();
                            if (cands.Navigate()) previewCand();
                            if (GameInput.Cancel) { AudioManager.Play("cancel", 0.6f); break; }
                            if (GameInput.Confirm)
                            {
                                if (!cands.Current.Enabled) { AudioManager.Play("error", 0.6f); yield return null; continue; }
                                string id = (string)cands.Current.Data;
                                state.Equip(m, slot, id == "" ? null : id);
                                AudioManager.Play("buff", 0.6f);
                                break;
                            }
                            yield return null;
                        }
                        slotIndex = si;
                        rebuild = true;
                        break;
                    }
                    yield return null;
                }
                if (back) yield break;
                if (rebuild) { yield return null; continue; }
            }
        }

        static string BonusText(EquipDef e)
        {
            var parts = new List<string>();
            foreach (var st in Stats.All)
            {
                int v = e.Bonus[st];
                if (v != 0) parts.Add(Stats.Short(st) + (v > 0 ? " +" : " ") + v);
            }
            string users = e.Users.Length == 0 ? "Anyone" : string.Join(", ", e.Users.Select(u => Database.Characters[u].Name));
            string res = e.Resist != Element.None ? "\nHalves " + Database.ElementName(e.Resist) + " damage." : "";
            return "\n" + UIKit.Col(string.Join("  ", parts), UIKit.Cyan) + res + "\n" + UIKit.Col("Equip: " + users, UIKit.Dim);
        }

        // ================================================================== status
        IEnumerator StatusScreen()
        {
            while (true)
            {
                var m = state.Members[member];
                var s = m.Stats;
                var c = NewContent();

                var left = UIKit.Panel(c, 450, 70, 420, 890, "Portrait");
                var size = PixelArt.Size(m.Def.SpritePrefix + "_stand");
                float scale = Mathf.Floor(Mathf.Min(340f / size.x, 420f / size.y));
                var glow = UIKit.Box(left.transform, 40, 40, 340, 470, new Color(m.Def.ThemeColor.r, m.Def.ThemeColor.g, m.Def.ThemeColor.b, 0.18f), UIKit.White, "Glow");
                UIKit.Box(glow.transform, 0, 0, 340, 470, Color.white, UIKit.FrameSprite, "Frame");
                UIKit.Icon(left.transform, PixelArt.Sprite(m.Def.SpritePrefix + "_stand"), 40 + (340 - size.x * scale) / 2, 40 + (470 - size.y * scale) - 20, size.x * scale, size.y * scale);
                UIKit.Label(left.transform, m.Name, 40, 530, 340, 60, 50, TextAnchor.MiddleCenter);
                UIKit.Label(left.transform, m.Def.Title, 40, 590, 340, 40, 28, TextAnchor.MiddleCenter, UIKit.Gold);
                UIKit.Label(left.transform, UIKit.Col("Level", UIKit.DimGold) + "  " + m.Level, 40, 640, 340, 50, 34, TextAnchor.MiddleCenter);
                UIKit.Label(left.transform, UIKit.Col("Weapon type", UIKit.DimGold) + "  " + Database.ElementName(m.Def.WeaponType), 40, 700, 340, 40, 24, TextAnchor.MiddleCenter);
                UIKit.Icon(left.transform, PixelArt.Sprite(PixelArt.ElementIcon(m.Def.WeaponType)), 190, 750, 40, 40);
                if (m.IsDead) UIKit.Label(left.transform, "Knocked out", 40, 800, 340, 40, 26, TextAnchor.MiddleCenter, UIKit.Red);

                var mid = UIKit.Panel(c, 890, 70, 470, 600, "Parameters");
                UIKit.Label(mid.transform, "Parameters", 34, 16, 400, 44, 28, TextAnchor.MiddleLeft, UIKit.Gold);
                UIKit.Label(mid.transform, "HP", 34, 70, 120, 46, 26, TextAnchor.MiddleLeft, UIKit.DimGold);
                UIKit.Label(mid.transform, m.HP + " / " + s.MaxHP, 150, 70, 280, 46, 30, TextAnchor.MiddleRight);
                new Gauge(mid.transform, 150, 116, 280, 8, UIKit.HPColor).Set(m.HP / (float)s.MaxHP, true);
                UIKit.Label(mid.transform, "MP", 34, 130, 120, 46, 26, TextAnchor.MiddleLeft, UIKit.DimGold);
                UIKit.Label(mid.transform, m.MP + " / " + s.MaxMP, 150, 130, 280, 46, 30, TextAnchor.MiddleRight, UIKit.MPColor);
                new Gauge(mid.transform, 150, 176, 280, 8, UIKit.MPColor).Set(m.MP / (float)Mathf.Max(1, s.MaxMP), true);
                Stat[] rows = { Stat.Atk, Stat.Def, Stat.Mag, Stat.Res, Stat.Spd, Stat.Luck };
                var baseStats = m.BaseStatsAtLevel(m.Level);
                var bonus = m.EquipBonus;
                for (int i = 0; i < rows.Length; i++)
                {
                    float y = 200 + i * 62;
                    UIKit.Label(mid.transform, Stats.Label(rows[i]), 34, y, 200, 50, 26, TextAnchor.MiddleLeft, UIKit.DimGold);
                    UIKit.Label(mid.transform, s[rows[i]].ToString(), 220, y, 100, 50, 32, TextAnchor.MiddleRight);
                    int b = bonus[rows[i]];
                    UIKit.Label(mid.transform, "<size=20>" + baseStats[rows[i]] + (b != 0 ? UIKit.Col((b > 0 ? " +" : " ") + b, b > 0 ? UIKit.Up : UIKit.Down) : "") + "</size>", 330, y, 120, 50, 20, TextAnchor.MiddleRight, UIKit.Dim);
                }

                var exp = UIKit.Panel(c, 890, 690, 470, 270, "Experience");
                UIKit.Label(exp.transform, "Experience", 34, 16, 400, 44, 28, TextAnchor.MiddleLeft, UIKit.Gold);
                UIKit.Label(exp.transform, "Total EXP", 34, 70, 200, 46, 26, TextAnchor.MiddleLeft, UIKit.DimGold);
                UIKit.Label(exp.transform, m.Exp.ToString("N0"), 200, 70, 230, 46, 30, TextAnchor.MiddleRight);
                UIKit.Label(exp.transform, "To next level", 34, 126, 200, 46, 26, TextAnchor.MiddleLeft, UIKit.DimGold);
                UIKit.Label(exp.transform, m.ExpToNext.ToString("N0"), 200, 126, 230, 46, 30, TextAnchor.MiddleRight);
                new Gauge(exp.transform, 34, 190, 400, 10, UIKit.Gold).Set(m.ExpProgress, true);

                var eqp = UIKit.Panel(c, 1380, 70, 480, 330, "Equipment");
                UIKit.Label(eqp.transform, "Equipment", 34, 16, 400, 44, 28, TextAnchor.MiddleLeft, UIKit.Gold);
                for (int i = 0; i < 4; i++)
                {
                    string id = m.Equipment[i];
                    UIKit.Icon(eqp.transform, PixelArt.Sprite(SlotIcon((EquipSlot)i, m)), 34, 76 + i * 60, 32, 32);
                    UIKit.Label(eqp.transform, id != null ? Database.Equips[id].Name : UIKit.Col("—", UIKit.Dim), 80, 66 + i * 60, 380, 50, 26);
                }

                var sk = UIKit.Panel(c, 1380, 420, 480, 540, "Skills");
                UIKit.Label(sk.transform, "Skills", 34, 16, 400, 44, 28, TextAnchor.MiddleLeft, UIKit.Gold);
                var learned = m.Skills;
                var all = m.Def.SkillTable;
                int row = 0;
                foreach (var entry in all)
                {
                    var skill = Database.Skills[entry.skillId];
                    bool has = learned.Contains(skill);
                    UIKit.Label(sk.transform, has ? skill.Name : UIKit.Col("Lv " + entry.level + "  ???", UIKit.Dim), 34, 66 + row * 48, 330, 46, 24);
                    if (has) UIKit.Label(sk.transform, skill.MpCost + " MP", 330, 66 + row * 48, 110, 46, 22, TextAnchor.MiddleRight, UIKit.MPColor);
                    row++;
                }

                HelpBar(m.Def.Bio + "      " + UIKit.Col("Q/E: Switch   X: Back", UIKit.DimGold));
                yield return null;
                while (true)
                {
                    if (SwitchMember()) break;
                    if (GameInput.Cancel || GameInput.Confirm) { AudioManager.Play("cancel", 0.6f); yield break; }
                    yield return null;
                }
                yield return null;
            }
        }

        // ================================================================== config
        IEnumerator ConfigScreen()
        {
            var c = NewContent();
            var panel = UIKit.Panel(c, 450, 70, 1000, 420, "Config");
            UIKit.Label(panel.transform, "Config", 34, 16, 400, 44, 28, TextAnchor.MiddleLeft, UIKit.Gold);
            var list = new ListView(panel.transform, 44, 80, 900, 70, 4) { FontSize = 30 };
            HelpBar("Up/Down: Select   Left/Right: Adjust   X: Back");
            string[] dofNames = { "Off", "Soft", "Strong (HD-2D)" };
            System.Action refresh = () =>
            {
                var am = AudioManager.Instance;
                int dof = PostFX.Instance != null ? PostFX.Instance.DofLevel : 2;
                list.SetItems(new[]
                {
                    new ListView.Item { Label = "Music Volume", Right = Bar(am != null ? am.BgmVolume : 0) },
                    new ListView.Item { Label = "Sound Effects", Right = Bar(am != null ? am.SfxVolume : 0) },
                    new ListView.Item { Label = "Tilt-shift Depth of Field", Right = "◀  " + dofNames[dof] + "  ▶" },
                    new ListView.Item { Label = "Back" },
                });
            };
            refresh();
            yield return null;
            while (true)
            {
                list.Animate();
                list.Navigate();
                var nav = GameInput.Nav;
                if (nav.x != 0)
                {
                    var am = AudioManager.Instance;
                    switch (list.Index)
                    {
                        case 0: if (am) am.BgmVolume += nav.x * 0.1f; break;
                        case 1: if (am) am.SfxVolume += nav.x * 0.1f; break;
                        case 2: if (PostFX.Instance) PostFX.Instance.SetDofLevel((PostFX.Instance.DofLevel + nav.x + 3) % 3); break;
                    }
                    AudioManager.Play("cursor", 0.6f);
                    refresh();
                }
                if (GameInput.Cancel || (GameInput.Confirm && list.Index == 3)) { AudioManager.Play("cancel", 0.6f); yield break; }
                yield return null;
            }
        }

        static string Bar(float v)
        {
            int n = Mathf.RoundToInt(v * 10);
            return "◀  " + UIKit.Col(new string('■', n), UIKit.Gold) + UIKit.Col(new string('■', 10 - n), new Color(0.25f, 0.25f, 0.3f)) + "  ▶";
        }

        public void Destroy() => Object.Destroy(canvas.gameObject);
    }
}
