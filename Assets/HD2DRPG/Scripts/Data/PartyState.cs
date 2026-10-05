using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HD2DRPG
{
    /// <summary>Runtime state of one party member.</summary>
    public class PartyMember
    {
        public CharacterDef Def;
        public int Level;
        public int Exp;
        public int HP, MP;
        public readonly string[] Equipment = new string[4];

        public string Id => Def.Id;
        public string Name => Def.Name;
        public bool IsDead => HP <= 0;

        public PartyMember(CharacterDef def, int level)
        {
            Def = def;
            Level = level;
            Exp = Database.ExpForLevel(level);
            for (int i = 0; i < 4; i++) Equipment[i] = def.StartEquip[i];
            HP = Stats.MaxHP;
            MP = Stats.MaxMP;
        }

        public PartyMember Clone()
        {
            var copy = new PartyMember(Def, Level) { Exp = Exp };
            for (int i = 0; i < 4; i++) copy.Equipment[i] = Equipment[i];
            copy.HP = HP;
            copy.MP = MP;
            return copy;
        }

        public Stats BaseStatsAtLevel(int level)
        {
            var b = Def.BaseStats;
            var g = Def.Growth;
            int l = level - 1;
            return new Stats(
                b.MaxHP + g.MaxHP * l / 10, b.MaxMP + g.MaxMP * l / 10,
                b.Atk + g.Atk * l / 10, b.Def + g.Def * l / 10,
                b.Mag + g.Mag * l / 10, b.Res + g.Res * l / 10,
                b.Spd + g.Spd * l / 10, b.Luck + g.Luck * l / 10);
        }

        public Stats EquipBonus
        {
            get
            {
                var s = new Stats();
                foreach (var id in Equipment)
                    if (id != null && Database.Equips.TryGetValue(id, out var e)) s = s + e.Bonus;
                return s;
            }
        }

        public Stats Stats => StatsWith(Equipment);

        /// <summary>Stats if the given equipment set were worn (used for equip previews).</summary>
        public Stats StatsWith(string[] equipment)
        {
            var s = BaseStatsAtLevel(Level);
            foreach (var id in equipment)
                if (id != null && Database.Equips.TryGetValue(id, out var e)) s = s + e.Bonus;
            foreach (var st in Stats.All) s[st] = Mathf.Max(st == Stat.MaxHP ? 1 : 0, s[st]);
            return s;
        }

        public bool Resists(Element e)
        {
            if (e == Element.None) return false;
            foreach (var id in Equipment)
                if (id != null && Database.Equips.TryGetValue(id, out var d) && d.Resist == e) return true;
            return false;
        }

        public List<SkillDef> Skills =>
            Def.SkillTable.Where(t => t.level <= Level).Select(t => Database.Skills[t.skillId]).ToList();

        public int ExpToNext => Level >= Database.MaxLevel ? 0 : Database.ExpForLevel(Level + 1) - Exp;

        public float ExpProgress
        {
            get
            {
                if (Level >= Database.MaxLevel) return 1f;
                int a = Database.ExpForLevel(Level), b = Database.ExpForLevel(Level + 1);
                return Mathf.Clamp01((Exp - a) / (float)(b - a));
            }
        }

        /// <summary>Adds experience. Returns list of newly learned skills; levelsGained receives the count.</summary>
        public List<SkillDef> GainExp(int amount, out int levelsGained)
        {
            levelsGained = 0;
            var learned = new List<SkillDef>();
            if (IsDead) return learned;
            Exp += amount;
            while (Level < Database.MaxLevel && Exp >= Database.ExpForLevel(Level + 1))
            {
                var before = Stats;
                Level++;
                levelsGained++;
                var after = Stats;
                HP += after.MaxHP - before.MaxHP;
                MP += after.MaxMP - before.MaxMP;
                foreach (var t in Def.SkillTable)
                    if (t.level == Level) learned.Add(Database.Skills[t.skillId]);
            }
            return learned;
        }

        public void ClampVitals()
        {
            var s = Stats;
            HP = Mathf.Clamp(HP, 0, s.MaxHP);
            MP = Mathf.Clamp(MP, 0, s.MaxMP);
        }

        public void FullRestore()
        {
            var s = Stats;
            HP = s.MaxHP;
            MP = s.MaxMP;
        }
    }

    /// <summary>Whole-party state: members, inventory, gold, flags. Snapshot-able for retry.</summary>
    public class PartyState
    {
        public List<PartyMember> Members = new List<PartyMember>();
        public Dictionary<string, int> Items = new Dictionary<string, int>();
        public Dictionary<string, int> EquipBag = new Dictionary<string, int>();
        public HashSet<string> Flags = new HashSet<string>();
        public int Gold;
        public float PlayTime;
        public Vector3 Position;

        public static PartyState NewGame()
        {
            Database.Build();
            var p = new PartyState();
            foreach (var id in Database.PartyOrder)
                if (Database.Characters.TryGetValue(id, out var def))
                    p.Members.Add(new PartyMember(def, Database.StartLevel));
            foreach (var it in Database.StartItems) if (Database.Items.ContainsKey(it.Id)) p.AddItem(it.Id, it.Count);
            foreach (var it in Database.StartEquipment) if (Database.Equips.ContainsKey(it.Id)) p.AddEquip(it.Id, it.Count);
            p.Gold = Database.StartGold;
            return p;
        }

        public PartyState Clone()
        {
            var c = new PartyState
            {
                Gold = Gold, PlayTime = PlayTime, Position = Position,
                Items = new Dictionary<string, int>(Items),
                EquipBag = new Dictionary<string, int>(EquipBag),
                Flags = new HashSet<string>(Flags)
            };
            foreach (var m in Members) c.Members.Add(m.Clone());
            return c;
        }

        public void AddItem(string id, int n = 1)
        {
            Items.TryGetValue(id, out int c);
            Items[id] = c + n;
        }

        public bool UseItem(string id)
        {
            if (!Items.TryGetValue(id, out int c) || c <= 0) return false;
            if (c == 1) Items.Remove(id); else Items[id] = c - 1;
            return true;
        }

        public int ItemCount(string id) => Items.TryGetValue(id, out int c) ? c : 0;

        public void AddEquip(string id, int n = 1)
        {
            EquipBag.TryGetValue(id, out int c);
            EquipBag[id] = c + n;
        }

        public bool TakeEquip(string id)
        {
            if (!EquipBag.TryGetValue(id, out int c) || c <= 0) return false;
            if (c == 1) EquipBag.Remove(id); else EquipBag[id] = c - 1;
            return true;
        }

        /// <summary>Swaps equipment on a member, returning the old item to the bag.</summary>
        public void Equip(PartyMember m, EquipSlot slot, string newId)
        {
            int i = (int)slot;
            string old = m.Equipment[i];
            if (newId != null && !TakeEquip(newId)) return;
            if (old != null) AddEquip(old);
            m.Equipment[i] = newId;
            m.ClampVitals();
        }

        public PartyMember Get(string id) => Members.FirstOrDefault(m => m.Id == id);

        public bool AllDead => Members.All(m => m.IsDead);
    }
}
