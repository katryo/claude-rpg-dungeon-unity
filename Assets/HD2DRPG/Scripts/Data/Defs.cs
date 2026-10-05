using System.Collections.Generic;
using UnityEngine;

namespace HD2DRPG
{
    /// <summary>Damage types. The first three are weapon types, the rest are magic elements.
    /// Enemies have a set of weaknesses among these (Octopath-style "shield &amp; break").</summary>
    public enum Element { Sword, Axe, Staff, Fire, Ice, Thunder, Light, Dark, None }

    public enum Stat { MaxHP, MaxMP, Atk, Def, Mag, Res, Spd, Luck }

    public enum EquipSlot { Weapon, Armor, Head, Accessory }

    public enum TargetType { Self, OneAlly, AllAllies, OneEnemy, AllEnemies, OneDeadAlly }

    public enum SkillKind { Physical, Magic, Heal, Revive, Buff, Debuff, Taunt }

    public enum BuffType { AtkUp, DefUp, MagUp, ResUp, SpdUp, AtkDown, DefDown, Taunt, Regen }

    [System.Serializable]
    public struct Stats
    {
        public int MaxHP, MaxMP, Atk, Def, Mag, Res, Spd, Luck;

        public Stats(int hp, int mp, int atk, int def, int mag, int res, int spd, int luck)
        {
            MaxHP = hp; MaxMP = mp; Atk = atk; Def = def; Mag = mag; Res = res; Spd = spd; Luck = luck;
        }

        public int this[Stat s]
        {
            get
            {
                switch (s)
                {
                    case Stat.MaxHP: return MaxHP;
                    case Stat.MaxMP: return MaxMP;
                    case Stat.Atk: return Atk;
                    case Stat.Def: return Def;
                    case Stat.Mag: return Mag;
                    case Stat.Res: return Res;
                    case Stat.Spd: return Spd;
                    default: return Luck;
                }
            }
            set
            {
                switch (s)
                {
                    case Stat.MaxHP: MaxHP = value; break;
                    case Stat.MaxMP: MaxMP = value; break;
                    case Stat.Atk: Atk = value; break;
                    case Stat.Def: Def = value; break;
                    case Stat.Mag: Mag = value; break;
                    case Stat.Res: Res = value; break;
                    case Stat.Spd: Spd = value; break;
                    default: Luck = value; break;
                }
            }
        }

        public static Stats operator +(Stats a, Stats b) =>
            new Stats(a.MaxHP + b.MaxHP, a.MaxMP + b.MaxMP, a.Atk + b.Atk, a.Def + b.Def,
                      a.Mag + b.Mag, a.Res + b.Res, a.Spd + b.Spd, a.Luck + b.Luck);

        public static readonly Stat[] All =
            { Stat.MaxHP, Stat.MaxMP, Stat.Atk, Stat.Def, Stat.Mag, Stat.Res, Stat.Spd, Stat.Luck };

        public static string Label(Stat s)
        {
            switch (s)
            {
                case Stat.MaxHP: return "Max HP";
                case Stat.MaxMP: return "Max MP";
                case Stat.Atk: return "Phys. Atk";
                case Stat.Def: return "Phys. Def";
                case Stat.Mag: return "Elem. Atk";
                case Stat.Res: return "Elem. Def";
                case Stat.Spd: return "Speed";
                default: return "Luck";
            }
        }

        public static string Short(Stat s)
        {
            switch (s)
            {
                case Stat.MaxHP: return "HP";
                case Stat.MaxMP: return "MP";
                case Stat.Atk: return "ATK";
                case Stat.Def: return "DEF";
                case Stat.Mag: return "MAG";
                case Stat.Res: return "RES";
                case Stat.Spd: return "SPD";
                default: return "LUK";
            }
        }
    }

    [System.Serializable]
    public class SkillDef
    {
        public string Id, Name, Description;
        public int MpCost;
        public SkillKind Kind;
        public TargetType Target;
        public Element Element = Element.None;
        /// <summary>Damage / heal multiplier.</summary>
        public float Power = 1f;
        /// <summary>Number of hits (each hit can reduce a shield point).</summary>
        public int Hits = 1;
        public BuffType Buff;
        public int BuffTurns = 3;
        public bool UsableInField;
        /// <summary>Visual effect key, see BattleFX.</summary>
        public string Fx = "slash";
    }

    [System.Serializable]
    public class ItemDef
    {
        public string Id, Name, Description;
        public TargetType Target = TargetType.OneAlly;
        public int HealHP, HealMP;
        public float HealPercent;
        public bool Revive;
        public int Price;
        /// <summary>Damage item (usable in battle only).</summary>
        public int Damage;
        public Element Element = Element.None;
        public bool BattleOnly => Damage > 0;
    }

    [System.Serializable]
    public class EquipDef
    {
        public string Id, Name, Description;
        public EquipSlot Slot;
        public Stats Bonus;
        /// <summary>Character ids allowed to equip this. Empty = anyone.</summary>
        public string[] Users = new string[0];
        /// <summary>Optional elemental resistance (damage of this element halved).</summary>
        public Element Resist = Element.None;

        public bool CanEquip(string charId)
        {
            if (Users.Length == 0) return true;
            foreach (var u in Users) if (u == charId) return true;
            return false;
        }
    }

    [System.Serializable]
    public class EnemySkill
    {
        public string Name;
        public SkillKind Kind = SkillKind.Physical;
        public TargetType Target = TargetType.OneEnemy;
        public Element Element = Element.None;
        public float Power = 1f;
        public int Hits = 1;
        public float Weight = 1f;
        public BuffType Buff;
        public string Fx = "slash";
        /// <summary>Only used when the enemy is in this phase or later (0 = always).</summary>
        public int MinPhase;
        public string Line;
    }

    [System.Serializable]
    public class EnemyDef
    {
        public string Id, Name, SpriteId, AttackSpriteId;
        public Stats Stats;
        public int Shield;
        public Element[] Weaknesses;
        public int Exp, Gold;
        public List<EnemySkill> Skills = new List<EnemySkill>();
        public bool Floating;
        public float Scale = 1f;
        public bool IsBoss;
        public string Drop;
        public Color Tint = Color.white;
        /// <summary>Actions per round.</summary>
        public int Actions = 1;
    }

    /// <summary>A skill learned at a given level.</summary>
    [System.Serializable]
    public struct SkillLearn
    {
        public int level;
        public string skillId;

        public SkillLearn(int level, string skillId) { this.level = level; this.skillId = skillId; }

        public static implicit operator SkillLearn((int level, string skillId) t) => new SkillLearn(t.level, t.skillId);
    }

    [System.Serializable]
    public class EncounterDef
    {
        public string Id;
        public string[] Enemies = new string[0];
    }

    [System.Serializable]
    public struct ItemStack
    {
        public string Id;
        public int Count;
        public ItemStack(string id, int count) { Id = id; Count = count; }
    }

    [System.Serializable]
    public class CharacterDef
    {
        public string Id, Name, Title, SpritePrefix;
        public Stats BaseStats;  // at level 1
        public Stats Growth;     // per level (x10, i.e. 25 = +2.5 per level)
        public Element WeaponType;
        public List<SkillLearn> SkillTable = new List<SkillLearn>();
        public string[] StartEquip;
        public Color ThemeColor;
        public string Bio;
    }
}
