using System.Collections.Generic;
using UnityEngine;

namespace HD2DRPG
{
    /// <summary>A participant in battle: either a party member or an enemy instance.</summary>
    public class Battler
    {
        public bool IsPlayer;
        public PartyMember Member;
        public EnemyDef Enemy;
        public string Name;
        public SpriteActor Actor;
        public Vector3 Home;
        public int Index;

        // enemy-only vitals (party vitals live on PartyMember)
        int enemyHP, enemyMP;
        public Stats EnemyStats;

        public int Shield, MaxShield;
        public bool Broken;
        public int BrokenUntilRound = -1;
        public readonly HashSet<Element> Revealed = new HashSet<Element>();
        public List<Element> Weaknesses = new List<Element>();
        public int BP = 1;
        public bool BoostedThisRound;
        public bool Defending;
        public int Phase;
        public readonly Dictionary<BuffType, int> Buffs = new Dictionary<BuffType, int>();

        public static Battler FromMember(PartyMember m, int index)
        {
            return new Battler { IsPlayer = true, Member = m, Name = m.Name, Index = index, BP = 1 };
        }

        public static Battler FromEnemy(EnemyDef e, int index, string suffix)
        {
            var b = new Battler
            {
                IsPlayer = false, Enemy = e, Name = e.Name + suffix, Index = index,
                EnemyStats = e.Stats, MaxShield = e.Shield, Shield = e.Shield
            };
            b.enemyHP = e.Stats.MaxHP;
            b.enemyMP = e.Stats.MaxMP;
            b.Weaknesses.AddRange(e.Weaknesses);
            return b;
        }

        public Stats BaseStats => IsPlayer ? Member.Stats : EnemyStats;

        public int HP
        {
            get => IsPlayer ? Member.HP : enemyHP;
            set
            {
                int v = Mathf.Clamp(value, 0, MaxHP);
                if (IsPlayer) Member.HP = v; else enemyHP = v;
            }
        }

        public int MP
        {
            get => IsPlayer ? Member.MP : enemyMP;
            set
            {
                int v = Mathf.Clamp(value, 0, MaxMP);
                if (IsPlayer) Member.MP = v; else enemyMP = v;
            }
        }

        public int MaxHP => BaseStats.MaxHP;
        public int MaxMP => BaseStats.MaxMP;
        public bool Alive => HP > 0;
        public bool CanAct => Alive && !Broken;

        public int Get(Stat s)
        {
            float v = BaseStats[s];
            switch (s)
            {
                case Stat.Atk:
                    if (Has(BuffType.AtkUp)) v *= 1.3f;
                    if (Has(BuffType.AtkDown)) v *= 0.75f;
                    break;
                case Stat.Def:
                    if (Has(BuffType.DefUp)) v *= 1.35f;
                    if (Has(BuffType.DefDown)) v *= 0.7f;
                    if (Has(BuffType.Taunt)) v *= 1.2f;
                    break;
                case Stat.Res:
                    if (Has(BuffType.ResUp)) v *= 1.35f;
                    break;
                case Stat.Mag:
                    if (Has(BuffType.MagUp)) v *= 1.3f;
                    break;
                case Stat.Spd:
                    if (Has(BuffType.SpdUp)) v *= 1.3f;
                    break;
            }
            return Mathf.RoundToInt(v);
        }

        public bool Has(BuffType b) => Buffs.TryGetValue(b, out int t) && t > 0;

        public void AddBuff(BuffType b, int turns)
        {
            // opposite buffs cancel
            if (b == BuffType.AtkUp && Buffs.Remove(BuffType.AtkDown)) return;
            if (b == BuffType.AtkDown && Buffs.Remove(BuffType.AtkUp)) return;
            if (b == BuffType.DefUp && Buffs.Remove(BuffType.DefDown)) return;
            if (b == BuffType.DefDown && Buffs.Remove(BuffType.DefUp)) return;
            Buffs[b] = Mathf.Max(turns, Buffs.TryGetValue(b, out int t) ? t : 0);
        }

        /// <summary>Called at the end of this battler's turn.</summary>
        public void TickBuffs()
        {
            var keys = new List<BuffType>(Buffs.Keys);
            foreach (var k in keys)
            {
                Buffs[k]--;
                if (Buffs[k] <= 0) Buffs.Remove(k);
            }
        }

        public bool IsWeakTo(Element e) => !IsPlayer && e != Element.None && Weaknesses.Contains(e);

        public bool Resists(Element e) => IsPlayer && Member.Resists(e);

        public string SpriteHead => IsPlayer ? Member.Def.SpritePrefix + "_stand" : Enemy.SpriteId;

        public Vector3 Center => Actor != null ? Actor.transform.position + Vector3.up * (Actor.Height * 0.55f + (Actor.Floating ? Actor.FloatHeight : 0f)) : Home;
        public Vector3 Top => Actor != null ? Actor.transform.position + Vector3.up * (Actor.Height + 0.2f + (Actor.Floating ? Actor.FloatHeight : 0f)) : Home;
    }
}
