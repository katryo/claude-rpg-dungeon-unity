using System.Collections.Generic;
using UnityEngine;

namespace HD2DRPG
{
    /// <summary>All static game content: party members, skills, equipment, items and enemies.</summary>
    public static class Database
    {
        public static readonly Dictionary<string, CharacterDef> Characters = new Dictionary<string, CharacterDef>();
        public static readonly Dictionary<string, SkillDef> Skills = new Dictionary<string, SkillDef>();
        public static readonly Dictionary<string, EquipDef> Equips = new Dictionary<string, EquipDef>();
        public static readonly Dictionary<string, ItemDef> Items = new Dictionary<string, ItemDef>();
        public static readonly Dictionary<string, EnemyDef> Enemies = new Dictionary<string, EnemyDef>();
        public static readonly Dictionary<string, string[]> Encounters = new Dictionary<string, string[]>();

        public static int StartLevel = 10;
        public const int MaxLevel = 50;
        public static int StartGold = 480;
        public static readonly List<string> PartyOrder = new List<string>();
        public static readonly List<ItemStack> StartItems = new List<ItemStack>();
        public static readonly List<ItemStack> StartEquipment = new List<ItemStack>();

        /// <summary>Path (under Resources) of the editable data registry created by "Bake Data Assets".</summary>
        public const string DataAssetPath = "HD2D/GameData";

        static bool built;

        /// <summary>True when the content came from the ScriptableObject assets rather than code.</summary>
        public static bool LoadedFromAssets { get; private set; }

        public static void Build()
        {
            if (built) return;
            built = true;
            var data = Resources.Load<GameDataAsset>(DataAssetPath);
            if (data != null && data.Characters.Count > 0)
            {
                LoadFrom(data);
                LoadedFromAssets = true;
            }
            else BuildDefaults();
        }

        /// <summary>Forgets everything so the next <see cref="Build"/> reloads (used by editor tools).</summary>
        public static void Reset()
        {
            built = false;
            LoadedFromAssets = false;
            Characters.Clear(); Skills.Clear(); Equips.Clear(); Items.Clear(); Enemies.Clear(); Encounters.Clear();
            PartyOrder.Clear(); StartItems.Clear(); StartEquipment.Clear();
        }

        /// <summary>Fills the tables with the built-in content defined in this file.</summary>
        public static void BuildDefaults()
        {
            Reset();
            built = true;
            StartLevel = 10;
            StartGold = 480;
            BuildSkills();
            BuildEquipment();
            BuildItems();
            BuildCharacters();
            BuildEnemies();
            PartyOrder.AddRange(new[] { "aren", "gareth", "theia" });
            StartItems.Add(new ItemStack("potion", 6));
            StartItems.Add(new ItemStack("ether", 3));
            StartItems.Add(new ItemStack("phoenix_feather", 2));
            StartItems.Add(new ItemStack("greek_fire", 1));
            StartEquipment.Add(new ItemStack("hermes_sandals", 1));
        }

        static void LoadFrom(GameDataAsset data)
        {
            StartLevel = Mathf.Max(1, data.StartLevel);
            StartGold = data.StartGold;
            foreach (var a in data.Skills) if (a != null && a.Def != null) Skills[a.Def.Id] = a.Def;
            foreach (var a in data.Equipment) if (a != null && a.Def != null) Equips[a.Def.Id] = Normalize(a.Def);
            foreach (var a in data.Items) if (a != null && a.Def != null) Items[a.Def.Id] = a.Def;
            foreach (var a in data.Enemies) if (a != null && a.Def != null) Enemies[a.Def.Id] = Normalize(a.Def);
            foreach (var a in data.Characters) if (a != null && a.Def != null) Characters[a.Def.Id] = Normalize(a.Def);
            foreach (var e in data.Encounters) if (e != null && !string.IsNullOrEmpty(e.Id)) Encounters[e.Id] = e.Enemies;
            foreach (var c in data.Characters) if (c != null && c.Def != null) PartyOrder.Add(c.Def.Id);
            StartItems.AddRange(data.StartItems);
            StartEquipment.AddRange(data.StartEquipment);
            Validate();
        }

        /// <summary>Drops entries with missing ids or dangling references so bad data can't crash the game.</summary>
        static void Validate()
        {
            void DropEmpty<T>(Dictionary<string, T> d, string what)
            {
                if (d.Remove("")) Debug.LogWarning("[HD-2D RPG] A " + what + " asset has no Id and was ignored.");
            }
            DropEmpty(Skills, "skill"); DropEmpty(Equips, "equipment"); DropEmpty(Items, "item");
            DropEmpty(Enemies, "enemy"); DropEmpty(Characters, "character");
            PartyOrder.RemoveAll(id => !Characters.ContainsKey(id));
            foreach (var c in Characters.Values)
            {
                c.SkillTable.RemoveAll(t =>
                {
                    bool bad = string.IsNullOrEmpty(t.skillId) || !Skills.ContainsKey(t.skillId);
                    if (bad) Debug.LogWarning("[HD-2D RPG] " + c.Id + ": unknown skill '" + t.skillId + "' removed.");
                    return bad;
                });
                if (string.IsNullOrEmpty(c.SpritePrefix)) c.SpritePrefix = c.Id;
                for (int i = 0; i < c.StartEquip.Length; i++)
                    if (c.StartEquip[i] != null && !Equips.ContainsKey(c.StartEquip[i])) c.StartEquip[i] = null;
            }
            foreach (var e in Enemies.Values)
                if (e.Skills.Count == 0)
                    e.Skills.Add(new EnemySkill { Name = "Attack" });
            foreach (var key in new List<string>(Encounters.Keys))
            {
                var valid = System.Array.FindAll(Encounters[key] ?? new string[0], id => !string.IsNullOrEmpty(id) && Enemies.ContainsKey(id));
                if (valid.Length == 0) { Encounters.Remove(key); Debug.LogWarning("[HD-2D RPG] Encounter '" + key + "' has no valid enemies and was ignored."); }
                else Encounters[key] = valid;
            }
            Encounters.Remove("");
        }

        // Unity serializes null strings as "", which the game treats as "nothing".
        static string N(string s) => string.IsNullOrEmpty(s) ? null : s;

        static CharacterDef Normalize(CharacterDef d)
        {
            if (d.StartEquip == null || d.StartEquip.Length < 4) System.Array.Resize(ref d.StartEquip, 4);
            for (int i = 0; i < d.StartEquip.Length; i++) d.StartEquip[i] = N(d.StartEquip[i]);
            return d;
        }

        static EnemyDef Normalize(EnemyDef d)
        {
            d.AttackSpriteId = N(d.AttackSpriteId);
            d.Drop = N(d.Drop);
            foreach (var s in d.Skills) s.Line = N(s.Line);
            return d;
        }

        static EquipDef Normalize(EquipDef d)
        {
            if (d.Users == null) d.Users = new string[0];
            return d;
        }

        /// <summary>Total experience required to reach a level.</summary>
        public static int ExpForLevel(int level)
        {
            if (level <= 1) return 0;
            return Mathf.RoundToInt(30f * Mathf.Pow(level, 2.2f));
        }

        public static string ElementName(Element e)
        {
            switch (e)
            {
                case Element.Sword: return "Sword";
                case Element.Axe: return "Axe";
                case Element.Staff: return "Staff";
                case Element.Fire: return "Fire";
                case Element.Ice: return "Ice";
                case Element.Thunder: return "Thunder";
                case Element.Light: return "Light";
                case Element.Dark: return "Dark";
                default: return "-";
            }
        }

        public static Color ElementColor(Element e)
        {
            switch (e)
            {
                case Element.Fire: return new Color(1f, 0.45f, 0.15f);
                case Element.Ice: return new Color(0.45f, 0.85f, 1f);
                case Element.Thunder: return new Color(1f, 0.95f, 0.35f);
                case Element.Light: return new Color(1f, 0.95f, 0.7f);
                case Element.Dark: return new Color(0.7f, 0.35f, 1f);
                default: return Color.white;
            }
        }

        // ------------------------------------------------------------------ skills
        static SkillDef S(string id, string name, int mp, SkillKind kind, TargetType target, Element el,
                          float power, string fx, string desc, int hits = 1)
        {
            var s = new SkillDef
            {
                Id = id, Name = name, MpCost = mp, Kind = kind, Target = target, Element = el,
                Power = power, Fx = fx, Description = desc, Hits = hits
            };
            Skills[id] = s;
            return s;
        }

        static void BuildSkills()
        {
            // Aren — Spellblade (magic warrior): elemental sword arts
            S("flame_edge", "Flame Edge", 7, SkillKind.Physical, TargetType.OneEnemy, Element.Fire, 1.55f, "fire",
              "Wreathe the blade in fire and strike one foe. Fire-type physical attack.");
            S("frost_edge", "Frost Edge", 7, SkillKind.Physical, TargetType.OneEnemy, Element.Ice, 1.55f, "ice",
              "A chilling slash that freezes on impact. Ice-type physical attack.");
            S("thunder_rend", "Thunder Rend", 14, SkillKind.Magic, TargetType.AllEnemies, Element.Thunder, 1.15f, "thunder",
              "Raise the sword to the heavens and call down lightning on all foes.");
            S("twin_rune", "Twin Rune Slash", 12, SkillKind.Physical, TargetType.OneEnemy, Element.Sword, 0.85f, "slash",
              "Two lightning-fast rune-etched sword strikes.", 2);
            S("valor", "Valor Rally", 10, SkillKind.Buff, TargetType.AllAllies, Element.None, 1f, "buff",
              "A rousing cry that raises the party's Phys. Atk for 3 turns.").Buff = BuffType.AtkUp;
            S("aether_blade", "Aether Blade", 22, SkillKind.Physical, TargetType.OneEnemy, Element.Light, 2.6f, "light",
              "Channel pure aether into the sword for a radiant finishing strike. Light-type.");

            // Gareth — Guardian (armored warrior)
            S("cleave", "Cleave", 5, SkillKind.Physical, TargetType.OneEnemy, Element.Axe, 1.5f, "heavy",
              "A heavy downward axe blow.");
            S("bulwark", "Bulwark", 6, SkillKind.Taunt, TargetType.Self, Element.None, 1f, "shield",
              "Raise the tower shield and draw all enemy attacks for 3 turns. Raises own Phys. Def.").Buff = BuffType.Taunt;
            S("iron_wall", "Iron Wall", 12, SkillKind.Buff, TargetType.AllAllies, Element.None, 1f, "shield",
              "Form a shield wall, raising the party's Phys. Def for 3 turns.").Buff = BuffType.DefUp;
            S("earthsplitter", "Earthsplitter", 16, SkillKind.Physical, TargetType.AllEnemies, Element.Axe, 1.1f, "quake",
              "Slam the axe into the ground, sending shockwaves through all foes.");
            S("armor_break", "Armor Break", 9, SkillKind.Debuff, TargetType.OneEnemy, Element.Axe, 0.9f, "heavy",
              "Shatter the foe's guard: deals damage and lowers its Phys. Def.").Buff = BuffType.DefDown;
            S("titan_cleave", "Titan's Cleave", 24, SkillKind.Physical, TargetType.OneEnemy, Element.Axe, 2.9f, "heavy",
              "A colossal strike said to split mountains.");

            // Theia — Arch Mage of the Olympian rite
            var heal = S("heal", "Asclepian Heal", 6, SkillKind.Heal, TargetType.OneAlly, Element.None, 3.2f, "heal",
              "The healing art of Asclepius. Restores one ally's HP.");
            heal.UsableInField = true;
            S("glacies", "Boreas' Gale", 11, SkillKind.Magic, TargetType.AllEnemies, Element.Ice, 1.15f, "ice",
              "Summon the North Wind to freeze all foes. Ice magic.");
            S("pyre", "Hephaestus' Pyre", 9, SkillKind.Magic, TargetType.OneEnemy, Element.Fire, 1.8f, "fire",
              "Forge-fire of the smith-god engulfs one foe. Fire magic.");
            S("zeus_bolt", "Thunderbolt of Zeus", 15, SkillKind.Magic, TargetType.OneEnemy, Element.Thunder, 2.3f, "thunder",
              "Hurl the king of the gods' thunderbolt at one foe. Thunder magic.");
            S("apollo_hymn", "Hymn of Apollo", 18, SkillKind.Magic, TargetType.OneEnemy, Element.Light, 2.5f, "light",
              "A radiant hymn of the sun god scorches the wicked. Light magic.");
            var grace = S("olympian_grace", "Olympian Grace", 18, SkillKind.Heal, TargetType.AllAllies, Element.None, 2.2f, "heal",
              "Blessing of the Olympians. Restores HP to the whole party.");
            grace.UsableInField = true;
            S("aegis", "Aegis of Athena", 12, SkillKind.Buff, TargetType.AllAllies, Element.None, 1f, "shield",
              "Athena's shield protects the party, raising Elem. Def for 3 turns.").Buff = BuffType.ResUp;
            var phoenix = S("phoenix", "Phoenix Rite", 22, SkillKind.Revive, TargetType.OneDeadAlly, Element.None, 0.5f, "revive",
              "Rekindle the flame of life. Revives a fallen ally with half HP.");
            phoenix.UsableInField = true;
            S("wrath_olympus", "Wrath of Olympus", 34, SkillKind.Magic, TargetType.AllEnemies, Element.Light, 1.9f, "light",
              "The full fury of Olympus rains from the sky upon all foes. Light magic.");
        }

        // ------------------------------------------------------------------ equipment
        static EquipDef E(string id, string name, EquipSlot slot, Stats bonus, string desc, params string[] users)
        {
            var e = new EquipDef { Id = id, Name = name, Slot = slot, Bonus = bonus, Description = desc, Users = users };
            Equips[id] = e;
            return e;
        }

        static Stats B(int hp = 0, int mp = 0, int atk = 0, int def = 0, int mag = 0, int res = 0, int spd = 0, int luck = 0)
            => new Stats(hp, mp, atk, def, mag, res, spd, luck);

        static void BuildEquipment()
        {
            // Weapons
            E("runeblade", "Runeblade", EquipSlot.Weapon, B(atk: 14, mag: 8), "A sword etched with glowing runes of the old kingdom.", "aren");
            E("starfire_saber", "Starfire Saber", EquipSlot.Weapon, B(atk: 26, mag: 16, spd: 3), "Forged from a fallen star. Its edge burns with cold blue fire.", "aren");
            E("iron_greataxe", "Iron Greataxe", EquipSlot.Weapon, B(atk: 18), "A plain but trusty two-handed axe.", "gareth");
            E("titans_cleaver", "Titan's Cleaver", EquipSlot.Weapon, B(atk: 32, def: 4), "An enormous axe once wielded by a titan's champion.", "gareth");
            E("olive_wand", "Olive Wand", EquipSlot.Weapon, B(atk: 4, mag: 15, mp: 10), "A wand carved from the sacred olive tree of Athena.", "theia");
            E("caduceus", "Wand of Asclepius", EquipSlot.Weapon, B(atk: 6, mag: 27, res: 6, mp: 20), "The serpent-entwined rod of the healer god.", "theia");
            // Armor
            E("spellweave_mail", "Spellweave Mail", EquipSlot.Armor, B(def: 12, res: 8), "Light mail threaded with enchanted silver.", "aren");
            E("knights_plate", "Knight's Plate", EquipSlot.Armor, B(def: 20, res: 4, spd: -2), "Heavy steel plate of the royal guard.", "gareth");
            E("aegis_plate", "Aegis Plate", EquipSlot.Armor, B(hp: 60, def: 32, res: 10, spd: -2), "Blessed plate that turns aside even dark magic.", "gareth");
            E("linen_chiton", "Linen Chiton", EquipSlot.Armor, B(def: 6, res: 12), "A white chiton embroidered with a golden meander.", "theia");
            E("peplos_hera", "Peplos of Hera", EquipSlot.Armor, B(def: 10, res: 22, mp: 20), "A regal robe woven by the queen of the gods.", "theia");
            // Head
            E("leather_band", "Leather Headband", EquipSlot.Head, B(def: 3, spd: 2), "A simple headband. Keeps the hair out of the eyes.");
            E("plumed_helm", "Plumed Greathelm", EquipSlot.Head, B(def: 9, res: 2), "A full helm crowned with a crimson plume.", "gareth");
            E("laurel_wreath", "Golden Laurel", EquipSlot.Head, B(res: 6, mag: 4), "A laurel wreath of a champion of Delphi.", "theia");
            E("circlet_dawn", "Circlet of Dawn", EquipSlot.Head, B(def: 6, mag: 8, res: 8), "A circlet that glimmers like the first light of morning.");
            // Accessories
            E("ring_vigor", "Ring of Vigor", EquipSlot.Accessory, B(hp: 90), "A ruby ring pulsing with life force.");
            E("hermes_sandals", "Sandals of Hermes", EquipSlot.Accessory, B(spd: 10, luck: 4), "Winged sandals of the messenger god.");
            E("selene_amulet", "Amulet of Selene", EquipSlot.Accessory, B(mp: 30, mag: 6), "A moonstone amulet blessed by the moon goddess.");
            E("dawn_ward", "Dawnward Talisman", EquipSlot.Accessory, B(res: 10), "A talisman of the dawn. Halves Dark damage.").Resist = Element.Dark;
        }

        // ------------------------------------------------------------------ items
        static ItemDef I(string id, string name, string desc, TargetType target)
        {
            var i = new ItemDef { Id = id, Name = name, Description = desc, Target = target };
            Items[id] = i;
            return i;
        }

        static void BuildItems()
        {
            I("potion", "Healing Grape", "A sun-ripened grape from Dionysus' vine. Restores 200 HP.", TargetType.OneAlly).HealHP = 200;
            I("hi_potion", "Nectar", "Drink of the gods. Restores 500 HP.", TargetType.OneAlly).HealHP = 500;
            I("ether", "Inspiring Plum", "Restores 40 MP.", TargetType.OneAlly).HealMP = 40;
            var elixir = I("elixir", "Elixir", "Fully restores one ally's HP and MP.", TargetType.OneAlly);
            elixir.HealPercent = 1f; elixir.HealMP = 999;
            var phoenix = I("phoenix_feather", "Phoenix Feather", "Revives a fallen ally with 40% HP.", TargetType.OneDeadAlly);
            phoenix.Revive = true; phoenix.HealPercent = 0.4f;
            I("ambrosia", "Ambrosia", "Food of the immortals. Restores 400 HP to the whole party.", TargetType.AllAllies).HealHP = 400;
            var fireJar = I("greek_fire", "Greek Fire", "An alchemical flask. Deals Fire damage to all foes.", TargetType.AllEnemies);
            fireJar.Damage = 220; fireJar.Element = Element.Fire;
            var bolt = I("storm_shard", "Storm Shard", "A crystal of bottled lightning. Deals Thunder damage to one foe.", TargetType.OneEnemy);
            bolt.Damage = 380; bolt.Element = Element.Thunder;
        }

        // ------------------------------------------------------------------ party
        static void BuildCharacters()
        {
            var aren = new CharacterDef
            {
                Id = "aren", Name = "Aren", Title = "Spellblade", SpritePrefix = "aren",
                BaseStats = new Stats(180, 30, 24, 18, 22, 16, 22, 14),
                Growth = new Stats(140, 40, 25, 18, 22, 16, 15, 10),
                WeaponType = Element.Sword,
                StartEquip = new[] { "runeblade", "spellweave_mail", "leather_band", null },
                ThemeColor = new Color(0.35f, 0.55f, 1f),
                Bio = "Last knight of the Order of the Runic Flame. Wields sword and spell alike to end the Long Night."
            };
            aren.SkillTable.Add((1, "flame_edge"));
            aren.SkillTable.Add((1, "frost_edge"));
            aren.SkillTable.Add((5, "twin_rune"));
            aren.SkillTable.Add((8, "thunder_rend"));
            aren.SkillTable.Add((10, "valor"));
            aren.SkillTable.Add((12, "aether_blade"));
            Characters[aren.Id] = aren;

            var gareth = new CharacterDef
            {
                Id = "gareth", Name = "Gareth", Title = "Guardian", SpritePrefix = "gareth",
                BaseStats = new Stats(240, 16, 28, 26, 8, 14, 14, 10),
                Growth = new Stats(180, 20, 26, 26, 8, 15, 10, 8),
                WeaponType = Element.Axe,
                StartEquip = new[] { "iron_greataxe", "knights_plate", "plumed_helm", null },
                ThemeColor = new Color(0.85f, 0.25f, 0.25f),
                Bio = "Aren's sworn shield and oldest friend. A mountain of steel with a heart of gold."
            };
            gareth.SkillTable.Add((1, "cleave"));
            gareth.SkillTable.Add((1, "bulwark"));
            gareth.SkillTable.Add((6, "iron_wall"));
            gareth.SkillTable.Add((9, "armor_break"));
            gareth.SkillTable.Add((10, "earthsplitter"));
            gareth.SkillTable.Add((12, "titan_cleave"));
            Characters[gareth.Id] = gareth;

            var theia = new CharacterDef
            {
                Id = "theia", Name = "Theia", Title = "Arch Mage", SpritePrefix = "theia",
                BaseStats = new Stats(140, 50, 10, 12, 30, 26, 20, 18),
                Growth = new Stats(100, 60, 8, 12, 28, 24, 14, 12),
                WeaponType = Element.Staff,
                StartEquip = new[] { "olive_wand", "linen_chiton", "laurel_wreath", null },
                ThemeColor = new Color(0.95f, 0.8f, 0.35f),
                Bio = "High priestess of the Temple of the Twelve. Her wand commands the wrath and mercy of Olympus."
            };
            theia.SkillTable.Add((1, "heal"));
            theia.SkillTable.Add((1, "pyre"));
            theia.SkillTable.Add((3, "glacies"));
            theia.SkillTable.Add((6, "zeus_bolt"));
            theia.SkillTable.Add((7, "aegis"));
            theia.SkillTable.Add((8, "olympian_grace"));
            theia.SkillTable.Add((9, "phoenix"));
            theia.SkillTable.Add((10, "apollo_hymn"));
            theia.SkillTable.Add((13, "wrath_olympus"));
            Characters[theia.Id] = theia;
        }

        // ------------------------------------------------------------------ enemies
        static EnemySkill ES(string name, SkillKind kind, TargetType t, Element el, float power, float weight,
                             string fx, int minPhase = 0, int hits = 1, string line = null)
            => new EnemySkill
            {
                Name = name, Kind = kind, Target = t, Element = el, Power = power, Weight = weight, Fx = fx,
                MinPhase = minPhase, Hits = hits, Line = line
            };

        static void BuildEnemies()
        {
            var skel = new EnemyDef
            {
                Id = "skeleton", Name = "Skeleton Knight", SpriteId = "skeleton", AttackSpriteId = "skeleton_attack",
                Stats = new Stats(340, 0, 62, 34, 20, 18, 24, 8), Shield = 3,
                Weaknesses = new[] { Element.Axe, Element.Fire, Element.Light }, Exp = 140, Gold = 60, Drop = "potion"
            };
            skel.Skills.Add(ES("Rusted Blade", SkillKind.Physical, TargetType.OneEnemy, Element.Sword, 1f, 3f, "slash"));
            skel.Skills.Add(ES("Bone Crusher", SkillKind.Physical, TargetType.OneEnemy, Element.Axe, 1.45f, 1f, "heavy"));
            Enemies[skel.Id] = skel;

            var wraith = new EnemyDef
            {
                Id = "wraith", Name = "Shadow Wraith", SpriteId = "wraith",
                Stats = new Stats(270, 99, 40, 26, 58, 42, 34, 12), Shield = 2,
                Weaknesses = new[] { Element.Staff, Element.Fire, Element.Light }, Exp = 130, Gold = 50, Floating = true,
                Drop = "ether"
            };
            wraith.Skills.Add(ES("Grave Touch", SkillKind.Physical, TargetType.OneEnemy, Element.Dark, 0.9f, 1.5f, "dark"));
            wraith.Skills.Add(ES("Dark Bolt", SkillKind.Magic, TargetType.OneEnemy, Element.Dark, 1.25f, 2f, "dark"));
            wraith.Skills.Add(ES("Wail of the Lost", SkillKind.Magic, TargetType.AllEnemies, Element.Dark, 0.7f, 1f, "dark"));
            Enemies[wraith.Id] = wraith;

            var garg = new EnemyDef
            {
                Id = "gargoyle", Name = "Gargoyle", SpriteId = "gargoyle",
                Stats = new Stats(460, 0, 70, 52, 30, 26, 18, 10), Shield = 4,
                Weaknesses = new[] { Element.Axe, Element.Ice, Element.Thunder }, Exp = 200, Gold = 90, Floating = true,
                Drop = "hi_potion"
            };
            garg.Skills.Add(ES("Stone Claw", SkillKind.Physical, TargetType.OneEnemy, Element.None, 0.65f, 2f, "slash", hits: 2));
            garg.Skills.Add(ES("Granite Dive", SkillKind.Physical, TargetType.OneEnemy, Element.None, 1.4f, 1f, "heavy"));
            var gaze = ES("Petrifying Gaze", SkillKind.Debuff, TargetType.OneEnemy, Element.None, 0f, 0.8f, "dark");
            gaze.Buff = BuffType.DefDown;
            garg.Skills.Add(gaze);
            Enemies[garg.Id] = garg;

            var doom = new EnemyDef
            {
                Id = "doom_knight", Name = "Doom Knight", SpriteId = "skeleton", AttackSpriteId = "skeleton_attack",
                Stats = new Stats(1300, 0, 84, 48, 40, 34, 26, 12), Shield = 6,
                Weaknesses = new[] { Element.Fire, Element.Thunder, Element.Light, Element.Staff }, Exp = 520, Gold = 300,
                Scale = 1.35f, Tint = new Color(1f, 0.55f, 0.55f), Drop = "elixir"
            };
            doom.Skills.Add(ES("Doom Blade", SkillKind.Physical, TargetType.OneEnemy, Element.Sword, 1.1f, 3f, "slash"));
            doom.Skills.Add(ES("Execution", SkillKind.Physical, TargetType.OneEnemy, Element.Sword, 1.7f, 1f, "heavy"));
            doom.Skills.Add(ES("Hellfire Sweep", SkillKind.Physical, TargetType.AllEnemies, Element.Fire, 0.8f, 1.2f, "fire"));
            Enemies[doom.Id] = doom;

            var lord = new EnemyDef
            {
                Id = "dark_lord", Name = "Malzarath, the Dark Lord", SpriteId = "darklord",
                Stats = new Stats(5400, 999, 96, 56, 92, 52, 34, 20), Shield = 7,
                Weaknesses = new[] { Element.Sword, Element.Thunder, Element.Light }, Exp = 0, Gold = 0,
                IsBoss = true, Scale = 1.6f, Actions = 1
            };
            lord.Skills.Add(ES("Dread Blade", SkillKind.Physical, TargetType.OneEnemy, Element.Dark, 1.15f, 3f, "dark"));
            lord.Skills.Add(ES("Abyssal Nova", SkillKind.Magic, TargetType.AllEnemies, Element.Dark, 1.0f, 2f, "nova",
                line: "Drown in the endless night!"));
            var chains = ES("Shadow Chains", SkillKind.Debuff, TargetType.OneEnemy, Element.Dark, 0.6f, 1.2f, "dark");
            chains.Buff = BuffType.AtkDown;
            lord.Skills.Add(chains);
            lord.Skills.Add(ES("Void Meteor", SkillKind.Magic, TargetType.AllEnemies, Element.Dark, 1.35f, 1.6f, "meteor", 1,
                line: "Behold the sky itself fall!"));
            lord.Skills.Add(ES("Soul Reaver", SkillKind.Physical, TargetType.OneEnemy, Element.Dark, 0.75f, 1.5f, "dark", 1, 3));
            Enemies[lord.Id] = lord;

            Encounters["hall_a"] = new[] { "skeleton", "skeleton" };
            Encounters["hall_b"] = new[] { "wraith", "skeleton", "wraith" };
            Encounters["gallery_a"] = new[] { "gargoyle", "gargoyle" };
            Encounters["gallery_b"] = new[] { "wraith", "gargoyle", "wraith" };
            Encounters["guard"] = new[] { "wraith", "doom_knight", "wraith" };
            Encounters["boss"] = new[] { "dark_lord" };
        }
    }
}
