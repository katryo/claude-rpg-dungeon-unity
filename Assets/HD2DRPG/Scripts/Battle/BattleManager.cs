using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HD2DRPG
{
    public enum BattleResult { Victory, Defeat, Fled }

    /// <summary>
    /// Turn-based battles in the style of HD-2D JRPGs:
    ///  - Speed-ordered rounds with a visible turn order (current + next round)
    ///  - Shield &amp; Break: hitting a weakness chips an enemy's shield; at zero it is Broken,
    ///    loses its turns until the end of the next round and takes double damage
    ///  - Boost Points: +1 BP each round (max 5); spend up to 3 to add extra hits or potency
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        public BattleStage NormalStage, BossStage;
        public BattleResult Result { get; private set; }

        BattleStage stage;
        BattleHUD hud;
        readonly List<Battler> party = new List<Battler>();
        readonly List<Battler> enemies = new List<Battler>();
        List<Battler> order = new List<Battler>(), nextOrder = new List<Battler>();
        int round;
        bool isBoss, canFlee, fled;
        bool bossPhasePending;
        PartyState state;
        Transform actorsRoot;

        public void Init(Transform parent)
        {
            NormalStage = BattleStage.Build(parent, new Vector3(200, 0, 0), false);
            BossStage = BattleStage.Build(parent, new Vector3(260, 0, 0), true);
        }

        IEnumerable<Battler> All => party.Concat(enemies);
        List<Battler> AliveEnemies => enemies.Where(e => e.Alive).ToList();
        List<Battler> AliveParty => party.Where(p => p.Alive).ToList();

        // ================================================================== setup
        public IEnumerator Run(PartyState partyState, string encounterId, bool boss)
        {
            state = partyState;
            isBoss = boss;
            canFlee = !boss && encounterId != "guard";
            fled = false;
            stage = boss ? BossStage : NormalStage;
            round = 0;
            party.Clear();
            enemies.Clear();
            actorsRoot = new GameObject("BattleActors").transform;

            for (int i = 0; i < state.Members.Count; i++)
            {
                var b = Battler.FromMember(state.Members[i], i);
                b.Home = stage.Origin + BattleStage.PartySlots[i];
                b.Actor = PartyController.CreateMemberActor(b.Member, actorsRoot);
                b.Actor.transform.position = b.Home;
                b.Actor.FacingRight = false;
                if (!b.Alive) SetKO(b, true);
                party.Add(b);
            }

            var ids = Database.Encounters[encounterId];
            var slots = BattleStage.EnemySlots(ids.Length, boss);
            var counts = ids.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
            var seen = new Dictionary<string, int>();
            for (int i = 0; i < ids.Length; i++)
            {
                var def = Database.Enemies[ids[i]];
                string suffix = "";
                if (counts[ids[i]] > 1)
                {
                    seen.TryGetValue(ids[i], out int n);
                    suffix = " " + (char)('A' + n);
                    seen[ids[i]] = n + 1;
                }
                var e = Battler.FromEnemy(def, i, suffix);
                e.Home = stage.Origin + slots[i];
                e.Actor = SpriteActor.Create(e.Name, def.SpriteId, actorsRoot, def.Scale);
                e.Actor.AddAnim("idle", def.SpriteId);
                e.Actor.AddAnim("attack", def.AttackSpriteId ?? def.SpriteId);
                e.Actor.transform.position = e.Home;
                e.Actor.FacingRight = true;
                e.Actor.Floating = def.Floating;
                e.Actor.SelfLight = 0.35f;
                if (def.Tint != Color.white) e.Actor.SetTint(def.Tint);
                enemies.Add(e);
            }

            hud = new BattleHUD(transform);
            hud.BuildParty(party);
            hud.BuildEnemies(enemies);
            PostFX.Instance?.SetBattleMood(true, boss);

            // opening camera sweep
            var rig = CameraRig.Instance;
            rig.SetFixedShot(stage.CameraPos + new Vector3(-3.5f, -1.2f, 3.5f), stage.CameraLook + new Vector3(-2.5f, 0, 0), 26f, true);
            rig.SetFixedShot(stage.CameraPos, stage.CameraLook, 34f);
            AudioManager.Music(boss ? "bgm_boss" : "bgm_battle", 0.3f);
            hud.Banner(boss ? "Malzarath, the Dark Lord" : EnemyListName());
            yield return Wait(1.3f);
            hud.Banner(null);

            nextOrder = ComputeOrder();
            yield return MainLoop();

            yield return Cleanup();
        }

        string EnemyListName()
        {
            var names = enemies.Select(e => e.Enemy.Name).Distinct().ToList();
            return names.Count == 1 ? names[0] + (enemies.Count > 1 ? " ×" + enemies.Count : "") + " appear!" : string.Join(", ", names) + " appear!";
        }

        List<Battler> ComputeOrder()
        {
            var list = new List<Battler>();
            foreach (var b in All)
            {
                if (!b.Alive) continue;
                int actions = !b.IsPlayer && b.Enemy.IsBoss && b.Phase >= 1 ? 2 : 1;
                for (int k = 0; k < actions; k++) list.Add(b);
            }
            var rolled = list.Select(b => (b, b.Get(Stat.Spd) * Random.Range(0.85f, 1.15f))).OrderByDescending(t => t.Item2).Select(t => t.b).ToList();
            return rolled;
        }

        IEnumerator Wait(float t)
        {
            for (float e = 0; e < t; e += Time.deltaTime)
            {
                hud.Tick();
                hud.UpdateEnemyTags(null);
                yield return null;
            }
        }

        // ================================================================== main loop
        IEnumerator MainLoop()
        {
            while (true)
            {
                round++;
                foreach (var b in All)
                {
                    if (b.Broken && round > b.BrokenUntilRound)
                    {
                        b.Broken = false;
                        b.Shield = b.MaxShield;
                        hud.Popup(b.Top, "Recovered", UIKit.Cyan, 30);
                    }
                }
                foreach (var p in party)
                {
                    if (p.Alive && round > 1 && !p.BoostedThisRound) p.BP = Mathf.Min(5, p.BP + 1);
                    p.BoostedThisRound = false;
                }
                order = nextOrder.Where(b => b.Alive).ToList();
                nextOrder = ComputeOrder();
                hud.RefreshParty(null, 0);

                for (int i = 0; i < order.Count; i++)
                {
                    var b = order[i];
                    if (!b.Alive || b.Broken) continue;
                    hud.ShowTurnOrder(order, i, nextOrder);
                    b.Defending = false;
                    b.TickBuffs(); // tick at turn start so self-buffs last as long as allies' copies
                    if (b.IsPlayer) yield return PlayerTurn(b);
                    else yield return EnemyTurn(b);
                    hud.RefreshParty(null, 0);

                    if (bossPhasePending) { bossPhasePending = false; yield return BossPhaseTwo(); nextOrder = ComputeOrder(); }

                    if (fled) { Result = BattleResult.Fled; yield break; }
                    if (AliveEnemies.Count == 0) { Result = BattleResult.Victory; yield return Victory(); yield break; }
                    if (AliveParty.Count == 0) { Result = BattleResult.Defeat; yield return Defeat(); yield break; }
                }
            }
        }

        // ================================================================== player input
        enum Cmd { Attack, Skills, Items, Defend, Flee }

        IEnumerator PlayerTurn(Battler b)
        {
            b.Actor.Play("idle");
            var stepFwd = b.Home + new Vector3(-0.6f, 0, 0);
            yield return MoveActor(b.Actor, stepFwd, 0.12f);
            b.Actor.Flash(new Color(1f, 0.9f, 0.6f), 0.3f);

            int boost = 0;
            var cmds = new List<Cmd> { Cmd.Attack, Cmd.Skills, Cmd.Items, Cmd.Defend };
            if (canFlee) cmds.Add(Cmd.Flee);
            hud.CommandList.SetItems(cmds.Select(c => new ListView.Item { Label = CmdName(c, b), Data = c }), false);
            bool done = false;

            while (!done)
            {
                hud.ShowCommands(true, b, boost);
                hud.CommandList.SetActive(true);
                hud.RefreshParty(b, boost);
                hud.SetHelp(CmdHelp((Cmd)hud.CommandList.Current.Data, b));
                yield return null;

                while (true)
                {
                    hud.Tick();
                    hud.UpdateEnemyTags(null);
                    if (hud.CommandList.Navigate()) hud.SetHelp(CmdHelp((Cmd)hud.CommandList.Current.Data, b));
                    if (GameInput.PageRight && boost < Mathf.Min(3, b.BP))
                    {
                        boost++;
                        AudioManager.Play("boost", 0.8f, 1f + boost * 0.15f);
                        b.Actor.Flash(new Color(1f, 0.6f, 0.2f), 0.4f);
                        FX.Burst(b.Center, new Color(1f, 0.6f, 0.25f), 12 * boost, 2f, 0.15f, 0.6f, -0.5f, 0.4f, true);
                        hud.RefreshParty(b, boost);
                        hud.ShowCommands(true, b, boost);
                    }
                    if (GameInput.PageLeft && boost > 0)
                    {
                        boost--;
                        AudioManager.Play("cursor", 0.6f);
                        hud.RefreshParty(b, boost);
                        hud.ShowCommands(true, b, boost);
                    }
                    if (GameInput.Confirm) break;
                    yield return null;
                }

                AudioManager.Play("confirm", 0.6f);
                var cmd = (Cmd)hud.CommandList.Current.Data;
                hud.CommandList.SetActive(false);
                yield return null;

                switch (cmd)
                {
                    case Cmd.Attack:
                    {
                        var sel = new List<Battler>();
                        yield return SelectTargets(TargetType.OneEnemy, b, sel, "Attack with " + Database.ElementName(b.Member.Def.WeaponType).ToLower());
                        if (sel.Count == 0) continue;
                        hud.ShowCommands(false);
                        SpendBoost(b, boost);
                        yield return DoAttack(b, sel[0], boost);
                        done = true;
                        break;
                    }
                    case Cmd.Skills:
                    {
                        SkillDef chosen = null;
                        var targets = new List<Battler>();
                        yield return ChooseSkill(b, boost, s => chosen = s, targets);
                        if (chosen == null) continue;
                        hud.ShowCommands(false);
                        SpendBoost(b, boost);
                        b.MP -= chosen.MpCost;
                        yield return DoSkill(b, chosen, targets, boost);
                        done = true;
                        break;
                    }
                    case Cmd.Items:
                    {
                        ItemDef chosen = null;
                        var targets = new List<Battler>();
                        yield return ChooseItem(b, i => chosen = i, targets);
                        if (chosen == null) continue;
                        hud.ShowCommands(false);
                        state.UseItem(chosen.Id);
                        yield return DoItem(b, chosen, targets);
                        done = true;
                        break;
                    }
                    case Cmd.Defend:
                        hud.ShowCommands(false);
                        b.Defending = true;
                        hud.Popup(b.Top, "Defend", UIKit.Gold, 34);
                        FX.Decal(b.Home + Vector3.up * 0.06f, FX.Flat, ProcTex.Ring, new Color(0.6f, 0.75f, 1f) * 1.3f, 1.8f, 1.2f, 0.4f);
                        AudioManager.Play("buff", 0.6f);
                        yield return Wait(0.35f);
                        done = true;
                        break;
                    case Cmd.Flee:
                        hud.ShowCommands(false);
                        if (Random.value < 0.75f)
                        {
                            hud.Banner("The party escaped!");
                            AudioManager.Play("cancel");
                            foreach (var p in AliveParty) StartCoroutine(MoveActor(p.Actor, p.Home + new Vector3(6, 0, 0), 0.6f));
                            yield return Wait(0.9f);
                            hud.Banner(null);
                            fled = true;
                        }
                        else
                        {
                            hud.Banner("Couldn't escape!");
                            AudioManager.Play("error");
                            yield return Wait(0.9f);
                            hud.Banner(null);
                        }
                        done = true;
                        break;
                }
            }
            hud.ShowCommands(false);
            hud.ShowSub(false);
            hud.SetHelp(null);
            hud.SetTargets(null);
            if (b.Alive) yield return MoveActor(b.Actor, b.Home, 0.12f);
        }

        static string CmdName(Cmd c, Battler b)
        {
            switch (c)
            {
                case Cmd.Attack: return "Attack";
                case Cmd.Skills: return b.Member.Def.Id == "theia" ? "Arcana" : b.Member.Def.Id == "gareth" ? "Arts" : "Spellblade";
                case Cmd.Items: return "Items";
                case Cmd.Defend: return "Defend";
                default: return "Flee";
            }
        }

        static string CmdHelp(Cmd c, Battler b)
        {
            switch (c)
            {
                case Cmd.Attack: return "Strike one foe with your " + Database.ElementName(b.Member.Def.WeaponType).ToLower() + ". Boosting adds extra hits.";
                case Cmd.Skills: return "Use a special ability. Boosting increases its potency.";
                case Cmd.Items: return "Use an item from the party's satchel.";
                case Cmd.Defend: return "Brace for impact, halving damage taken until your next turn.";
                default: return "Attempt to run from battle.";
            }
        }

        void SpendBoost(Battler b, int boost)
        {
            if (boost <= 0) return;
            b.BP -= boost;
            b.BoostedThisRound = true;
            hud.RefreshParty(b, 0);
        }

        IEnumerator ChooseSkill(Battler b, int boost, System.Action<SkillDef> result, List<Battler> targets)
        {
            var skills = b.Member.Skills;
            hud.ShowSub(true, CmdName(Cmd.Skills, b));
            hud.SubList.SetItems(skills.Select(s => new ListView.Item
            {
                Label = s.Name,
                Right = s.MpCost + " MP",
                Enabled = b.MP >= s.MpCost,
                Icon = PixelArt.Sprite(s.Element != Element.None ? PixelArt.ElementIcon(s.Element) : (s.Kind == SkillKind.Heal || s.Kind == SkillKind.Revive ? "icon_light" : "icon_shield")),
                Data = s
            }), false);
            hud.SubList.SetActive(true);
            yield return null;
            while (true)
            {
                var cur = (SkillDef)hud.SubList.Current.Data;
                hud.SetHelp(cur.Description + (boost > 0 ? UIKit.Col("  (Boost ×" + boost + ")", UIKit.Gold) : ""));
                hud.Tick();
                hud.UpdateEnemyTags(null);
                hud.SubList.Navigate();
                if (GameInput.Cancel) { AudioManager.Play("cancel"); hud.ShowSub(false); yield break; }
                if (GameInput.Confirm)
                {
                    if (b.MP < cur.MpCost) { AudioManager.Play("error"); yield return null; continue; }
                    AudioManager.Play("confirm", 0.6f);
                    hud.SubList.SetActive(false);
                    yield return null;
                    targets.Clear();
                    yield return SelectTargets(cur.Target, b, targets, cur.Name);
                    if (targets.Count > 0) { hud.ShowSub(false); result(cur); yield break; }
                    hud.SubList.SetActive(true);
                }
                yield return null;
            }
        }

        IEnumerator ChooseItem(Battler b, System.Action<ItemDef> result, List<Battler> targets)
        {
            var items = state.Items.Where(kv => kv.Value > 0).Select(kv => Database.Items[kv.Key]).ToList();
            if (items.Count == 0)
            {
                AudioManager.Play("error");
                hud.SetHelp("The satchel is empty.");
                yield return Wait(0.6f);
                yield break;
            }
            hud.ShowSub(true, "Items");
            hud.SubList.SetItems(items.Select(i => new ListView.Item { Label = i.Name, Right = "×" + state.ItemCount(i.Id), Data = i }), false);
            hud.SubList.SetActive(true);
            yield return null;
            while (true)
            {
                var cur = (ItemDef)hud.SubList.Current.Data;
                hud.SetHelp(cur.Description);
                hud.Tick();
                hud.UpdateEnemyTags(null);
                hud.SubList.Navigate();
                if (GameInput.Cancel) { AudioManager.Play("cancel"); hud.ShowSub(false); yield break; }
                if (GameInput.Confirm)
                {
                    AudioManager.Play("confirm", 0.6f);
                    hud.SubList.SetActive(false);
                    yield return null;
                    targets.Clear();
                    yield return SelectTargets(cur.Target, b, targets, cur.Name);
                    if (targets.Count > 0) { hud.ShowSub(false); result(cur); yield break; }
                    hud.SubList.SetActive(true);
                }
                yield return null;
            }
        }

        IEnumerator SelectTargets(TargetType tt, Battler user, List<Battler> outTargets, string what)
        {
            List<Battler> candidates;
            bool all = false;
            switch (tt)
            {
                case TargetType.Self: candidates = new List<Battler> { user }; all = true; break;
                case TargetType.AllAllies: candidates = AliveParty; all = true; break;
                case TargetType.OneAlly: candidates = AliveParty; break;
                case TargetType.OneDeadAlly: candidates = party.Where(p => !p.Alive).ToList(); break;
                case TargetType.AllEnemies: candidates = AliveEnemies; all = true; break;
                default: candidates = AliveEnemies; break;
            }
            if (candidates.Count == 0)
            {
                AudioManager.Play("error");
                hud.SetHelp("There is no valid target.");
                yield return Wait(0.6f);
                yield break;
            }
            bool enemySide = tt == TargetType.OneEnemy || tt == TargetType.AllEnemies;
            int idx = 0;
            yield return null;
            while (true)
            {
                var sel = all ? candidates : new List<Battler> { candidates[idx] };
                hud.SetTargets(sel);
                var focus = sel[0];
                if (enemySide && !all)
                {
                    string weak = string.Join(" ", focus.Weaknesses.Select(w => focus.Revealed.Contains(w) ? Database.ElementName(w) : "?"));
                    hud.SetHelp(what + " → " + UIKit.Col(focus.Name, UIKit.Gold) + "   HP " + focus.HP + "/" + focus.MaxHP + "   Weak: " + weak);
                }
                else if (all) hud.SetHelp(what + " → " + (enemySide ? "all foes" : tt == TargetType.Self ? "self" : "whole party"));
                else hud.SetHelp(what + " → " + UIKit.Col(focus.Name, UIKit.Gold) + "   HP " + focus.HP + "/" + focus.MaxHP + "   MP " + focus.MP + "/" + focus.MaxMP);
                hud.Tick();
                hud.UpdateEnemyTags(enemySide && !all ? focus : null);

                var nav = GameInput.Nav;
                if (!all && (nav.x != 0 || nav.y != 0))
                {
                    int d = (nav.x > 0 || nav.y < 0) ? 1 : -1;
                    idx = (idx + d + candidates.Count) % candidates.Count;
                    AudioManager.Play("cursor", 0.5f);
                }
                if (GameInput.Cancel) { AudioManager.Play("cancel"); hud.SetTargets(null); yield break; }
                if (GameInput.Confirm)
                {
                    AudioManager.Play("confirm", 0.6f);
                    outTargets.AddRange(sel);
                    hud.SetTargets(null);
                    hud.SetHelp(null);
                    yield break;
                }
                yield return null;
            }
        }

        // ================================================================== actions
        IEnumerator MoveActor(SpriteActor a, Vector3 to, float time)
        {
            if (a == null) yield break;
            Vector3 from = a.transform.position;
            for (float t = 0; t < time; t += Time.deltaTime)
            {
                float k = t / time;
                k = k * k * (3 - 2 * k);
                a.transform.position = Vector3.Lerp(from, to, k);
                hud.Tick();
                hud.UpdateEnemyTags(null);
                yield return null;
            }
            a.transform.position = to;
        }

        void FocusCamera(Battler a, Battler t)
        {
            var mid = (a.Home + (t != null ? t.Home : a.Home)) * 0.5f;
            CameraRig.Instance.SetFixedShot(Vector3.Lerp(stage.CameraPos, mid + new Vector3(0, 3.2f, -7.5f), 0.35f), Vector3.Lerp(stage.CameraLook, mid + Vector3.up * 1.1f, 0.4f), 30f);
        }

        void ResetCamera() => CameraRig.Instance.SetFixedShot(stage.CameraPos, stage.CameraLook, 34f);

        IEnumerator DoAttack(Battler user, Battler target, int boost)
        {
            int hits = 1 + boost;
            Element el = user.IsPlayer ? user.Member.Def.WeaponType : Element.None;
            FocusCamera(user, target);
            Vector3 dir = (target.Home - user.Home).normalized;
            Vector3 strikePos = target.Home - dir * 1.5f;
            user.Actor.Play("walk", 14f);
            yield return MoveActor(user.Actor, strikePos, 0.22f);
            for (int h = 0; h < hits && target.Alive; h++)
            {
                user.Actor.Play("attack", 1f, false);
                user.Actor.Squash(0.9f);
                yield return BattleFX.Impact(boost > 0 && h == hits - 1 ? "heavy" : "slash", el, target.Center, target.Actor.transform.position);
                yield return Hit(user, target, true, el, 1f, h * 0.05f);
                yield return Wait(hits > 1 ? 0.2f : 0.3f);
                user.Actor.Play("idle");
                yield return Wait(0.06f);
            }
            user.Actor.Play("walk", 14f);
            yield return MoveActor(user.Actor, user.IsPlayer ? user.Home + new Vector3(-0.6f, 0, 0) : user.Home, 0.2f);
            user.Actor.Play("idle");
            ResetCamera();
        }

        IEnumerator DoSkill(Battler user, SkillDef s, List<Battler> targets, int boost)
        {
            float mult = 1f + 0.6f * boost;
            hud.Banner(s.Name);
            Color c = BattleFX.ColorOf(s.Fx, s.Element);
            bool singleEnemy = s.Target == TargetType.OneEnemy;
            FocusCamera(user, singleEnemy ? targets[0] : null);

            if (s.Kind == SkillKind.Physical && singleEnemy)
            {
                var t = targets[0];
                Vector3 dir = (t.Home - user.Home).normalized;
                user.Actor.Flash(c, 0.4f);
                FX.Burst(user.Center, c, 16, 1.5f, 0.15f, 0.5f, -0.5f, 0.3f, true);
                user.Actor.Play("walk", 14f);
                yield return MoveActor(user.Actor, t.Home - dir * 1.5f, 0.22f);
                for (int h = 0; h < s.Hits && t.Alive; h++)
                {
                    user.Actor.Play("attack", 1f, false);
                    user.Actor.Squash(0.88f);
                    yield return BattleFX.Impact(s.Fx, s.Element, t.Center, t.Actor.transform.position);
                    if (s.Fx != "slash" && s.Fx != "heavy") yield return BattleFX.Impact("slash", s.Element, t.Center, t.Actor.transform.position);
                    yield return Hit(user, t, true, s.Element, s.Power * mult, 0);
                    yield return Wait(0.25f);
                }
                user.Actor.Play("walk", 14f);
                yield return MoveActor(user.Actor, user.Home + new Vector3(-0.6f, 0, 0), 0.2f);
                user.Actor.Play("idle");
            }
            else
            {
                user.Actor.Play(user.IsPlayer ? "cast" : "attack", 1f, false);
                BattleFX.CastCircle(user.Actor.transform.position, c);
                user.Actor.Flash(c, 0.6f);
                yield return Wait(0.55f);
                if (s.Kind == SkillKind.Physical || s.Kind == SkillKind.Debuff)
                {
                    // sweeping physical arts (Earthsplitter, Armor Break) leap in
                    user.Actor.Squash(0.85f);
                }
                foreach (var t in targets)
                {
                    StartCoroutine(BattleFX.Impact(s.Fx, s.Element, t.Center, t.Actor.transform.position));
                    yield return Wait(targets.Count > 1 ? 0.12f : 0.25f);
                }
                yield return Wait(0.2f);
                foreach (var t in targets)
                {
                    switch (s.Kind)
                    {
                        case SkillKind.Physical:
                        case SkillKind.Magic:
                            for (int h = 0; h < s.Hits; h++)
                                if (t.Alive) StartCoroutine(Hit(user, t, s.Kind == SkillKind.Physical, s.Element, s.Power * mult, h * 0.15f));
                            break;
                        case SkillKind.Debuff:
                            if (s.Power > 0 && t.Alive) StartCoroutine(Hit(user, t, true, s.Element, s.Power * mult, 0));
                            t.AddBuff(s.Buff, s.BuffTurns + boost);
                            hud.Popup(t.Top + Vector3.up * 0.4f, s.Buff == BuffType.DefDown ? "DEF Down" : "ATK Down", UIKit.Down, 30, 0.2f);
                            break;
                        case SkillKind.Heal:
                        {
                            int amt = Mathf.RoundToInt((user.Get(Stat.Mag) * s.Power + 40) * mult * Random.Range(0.95f, 1.05f));
                            Heal(t, amt, 0);
                            break;
                        }
                        case SkillKind.Revive:
                            Revive(t, Mathf.Max(1, Mathf.RoundToInt(t.MaxHP * Mathf.Min(1f, s.Power * mult))));
                            break;
                        case SkillKind.Buff:
                            t.AddBuff(s.Buff, s.BuffTurns + boost);
                            hud.Popup(t.Top, BuffName(s.Buff), UIKit.Up, 30);
                            break;
                        case SkillKind.Taunt:
                            t.AddBuff(BuffType.Taunt, s.BuffTurns + boost);
                            hud.Popup(t.Top, "Guarding allies", UIKit.Gold, 30);
                            break;
                    }
                }
                yield return Wait(0.8f);
                user.Actor.Play("idle");
            }
            hud.Banner(null);
            hud.RefreshParty(null, 0);
            ResetCamera();
        }

        static string BuffName(BuffType b)
        {
            switch (b)
            {
                case BuffType.AtkUp: return "ATK Up";
                case BuffType.DefUp: return "DEF Up";
                case BuffType.ResUp: return "RES Up";
                case BuffType.MagUp: return "MAG Up";
                case BuffType.SpdUp: return "SPD Up";
                default: return b.ToString();
            }
        }

        IEnumerator DoItem(Battler user, ItemDef item, List<Battler> targets)
        {
            hud.Banner(item.Name);
            user.Actor.Play("cast", 1f, false);
            yield return Wait(0.35f);
            foreach (var t in targets)
            {
                if (item.Damage > 0)
                {
                    StartCoroutine(BattleFX.Impact(item.Element == Element.Fire ? "fire" : "thunder", item.Element, t.Center, t.Actor.transform.position));
                    float raw = item.Damage * Random.Range(0.95f, 1.05f);
                    if (t.IsWeakTo(item.Element)) raw *= 1.3f;
                    if (t.Broken) raw *= 2f;
                    int dmg = Mathf.RoundToInt(raw);
                    yield return Wait(0.1f);
                    StartCoroutine(ApplyDamage(t, dmg, item.Element, t.IsWeakTo(item.Element), false, 0));
                    continue;
                }
                if (item.Revive)
                {
                    StartCoroutine(BattleFX.Impact("revive", Element.Light, t.Center, t.Actor.transform.position));
                    Revive(t, Mathf.Max(1, Mathf.RoundToInt(t.MaxHP * item.HealPercent)));
                    continue;
                }
                StartCoroutine(BattleFX.Impact("heal", Element.None, t.Center, t.Actor.transform.position));
                int hp = item.HealHP + Mathf.RoundToInt(t.MaxHP * item.HealPercent);
                if (hp > 0) Heal(t, hp, 0);
                if (item.HealMP > 0)
                {
                    int before = t.MP;
                    t.MP += item.HealMP;
                    hud.Popup(t.Top + Vector3.up * 0.3f, "+" + (t.MP - before) + " MP", UIKit.MPColor, 36, 0.2f);
                }
            }
            yield return Wait(0.9f);
            user.Actor.Play("idle");
            hud.Banner(null);
            hud.RefreshParty(null, 0);
        }

        // ================================================================== damage
        int ComputeDamage(Battler src, Battler dst, bool physical, Element el, float power, out bool weak, out bool crit)
        {
            float atk = src.Get(physical ? Stat.Atk : Stat.Mag);
            float def = dst.Get(physical ? Stat.Def : Stat.Res);
            float dmg = Mathf.Max(atk * 2f - def, atk * 0.35f) * power;
            dmg *= Random.Range(0.92f, 1.08f);
            weak = dst.IsWeakTo(el);
            crit = physical && Random.value < src.Get(Stat.Luck) / 350f;
            if (weak) dmg *= 1.3f;
            if (crit) dmg *= 1.5f;
            if (dst.Broken) dmg *= 2f;
            if (dst.Defending) dmg *= 0.5f;
            if (dst.Resists(el)) dmg *= 0.5f;
            return Mathf.Max(1, Mathf.RoundToInt(dmg));
        }

        IEnumerator Hit(Battler src, Battler dst, bool physical, Element el, float power, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            if (!dst.Alive) yield break;
            int dmg = ComputeDamage(src, dst, physical, el, power, out bool weak, out bool crit);
            yield return ApplyDamage(dst, dmg, el, weak, crit, 0);
        }

        IEnumerator ApplyDamage(Battler dst, int dmg, Element el, bool weak, bool crit, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            if (!dst.Alive) yield break;
            dst.HP -= dmg;
            dst.Actor.Flash(Color.white, 0.15f);
            dst.Actor.Squash(0.85f);
            StartCoroutine(Knockback(dst));
            AudioManager.Play(weak || crit ? "heavy" : "hit", 0.7f, Random.Range(0.95f, 1.1f));
            Color col = dst.IsPlayer ? new Color(1f, 0.85f, 0.8f) : weak ? new Color(1f, 0.85f, 0.35f) : Color.white;
            hud.Popup(dst.Center, dmg.ToString(), col, weak || crit ? 58 : 48);
            if (crit) hud.Popup(dst.Top + Vector3.up * 0.3f, "CRITICAL", new Color(1f, 0.5f, 0.3f), 28);
            if (weak)
            {
                hud.Popup(dst.Top + Vector3.up * 0.15f, "WEAK", UIKit.Gold, 30);
                dst.Revealed.Add(el);
                if (!dst.Broken && dst.Shield > 0)
                {
                    dst.Shield--;
                    if (dst.Shield <= 0) StartCoroutine(Break(dst));
                }
            }
            if (!dst.IsPlayer && dst.Enemy.IsBoss && dst.Phase == 0 && dst.HP > 0 && dst.HP <= dst.MaxHP / 2) bossPhasePending = true;
            if (dst.HP <= 0) yield return Die(dst);
            hud.RefreshParty(null, 0);
        }

        IEnumerator Knockback(Battler b)
        {
            if (b.Actor == null) yield break;
            Vector3 home = b.Actor.transform.position;
            Vector3 dir = b.IsPlayer ? Vector3.right : Vector3.left;
            for (float t = 0; t < 0.16f; t += Time.deltaTime)
            {
                float k = Mathf.Sin(t / 0.16f * Mathf.PI);
                b.Actor.transform.position = home + dir * 0.18f * k;
                yield return null;
            }
            b.Actor.transform.position = home;
        }

        IEnumerator Break(Battler b)
        {
            b.Broken = true;
            b.BrokenUntilRound = round + 1;
            AudioManager.Play("break", 1f);
            CameraRig.Instance.Shake(0.25f, 0.35f);
            PostFX.Instance?.Flash(0.6f);
            FX.Burst(b.Center, new Color(0.6f, 0.8f, 1f), 50, 6f, 0.15f, 0.9f, 1.5f, 0.3f, true);
            FX.Decal(b.Center, FX.FaceCamera(), ProcTex.Ring, new Color(1f, 0.7f, 0.3f) * 2f, 0.5f, 4f, 0.4f);
            b.Actor.Flash(new Color(1f, 0.6f, 0.2f), 0.5f);
            yield return hud.BigText("BREAK!", new Color(1f, 0.72f, 0.25f), 0.9f);
        }

        IEnumerator Die(Battler b)
        {
            if (b.IsPlayer)
            {
                AudioManager.Play("death", 0.8f);
                hud.Popup(b.Top, "KO", UIKit.Red, 36, 0.2f);
                SetKO(b, true);
                b.Buffs.Clear();
                b.BP = 0;
            }
            else
            {
                AudioManager.Play("death", 0.8f, 0.8f);
                yield return new WaitForSeconds(0.25f);
                b.Actor.Dissolve();
                FX.Burst(b.Center, new Color(0.8f, 0.5f, 1f), 40, 2.5f, 0.2f, 1.2f, -0.6f, 0.5f, true);
            }
        }

        void SetKO(Battler b, bool ko)
        {
            b.Actor.KneelScale = ko ? 0.62f : 1f;
            b.Actor.SetTint(ko ? new Color(0.55f, 0.45f, 0.55f) : Color.white);
            b.Actor.Play("idle");
        }

        void Heal(Battler t, int amount, float delay)
        {
            if (!t.Alive) return;
            int before = t.HP;
            t.HP += amount;
            hud.Popup(t.Center, (t.HP - before).ToString(), UIKit.HPColor, 48, delay);
            hud.RefreshParty(null, 0);
        }

        void Revive(Battler t, int hp)
        {
            if (t.Alive) { Heal(t, hp, 0); return; }
            t.HP = hp;
            SetKO(t, false);
            if (!nextOrder.Contains(t)) nextOrder.Add(t); // act again next round
            hud.Popup(t.Center, "Revived", UIKit.Gold, 38);
            FX.Burst(t.Center, new Color(1f, 0.85f, 0.5f), 40, 2f, 0.2f, 1.2f, -0.5f, 0.5f, true);
            hud.RefreshParty(null, 0);
        }

        // ================================================================== enemies
        IEnumerator EnemyTurn(Battler e)
        {
            var phase = e.Phase;
            var pool = e.Enemy.Skills.Where(s => s.MinPhase <= phase).ToList();
            float total = pool.Sum(s => s.Weight);
            float r = Random.value * total;
            var skill = pool[0];
            foreach (var s in pool) { r -= s.Weight; if (r <= 0) { skill = s; break; } }

            var alive = AliveParty;
            if (alive.Count == 0) yield break;
            List<Battler> targets;
            if (skill.Target == TargetType.AllEnemies) targets = alive;
            else
            {
                var taunt = alive.FirstOrDefault(p => p.Has(BuffType.Taunt));
                targets = new List<Battler> { taunt ?? alive[Random.Range(0, alive.Count)] };
            }

            if (!string.IsNullOrEmpty(skill.Line))
            {
                hud.Banner(UIKit.Col(e.Enemy.Name.Split(',')[0] + ":", UIKit.Red) + " “" + skill.Line + "”");
                yield return Wait(1.2f);
            }
            hud.Banner(skill.Name);
            Color c = BattleFX.ColorOf(skill.Fx, skill.Element);
            e.Actor.Flash(new Color(1f, 0.3f, 0.3f), 0.35f);
            yield return Wait(0.35f);

            bool lunge = skill.Kind == SkillKind.Physical && skill.Target != TargetType.AllEnemies;
            if (lunge)
            {
                var t = targets[0];
                Vector3 dir = (t.Home - e.Home).normalized;
                FocusCamera(e, t);
                yield return MoveActor(e.Actor, t.Home - dir * (1.4f + 0.4f * e.Enemy.Scale), 0.2f);
                for (int h = 0; h < skill.Hits && t.Alive; h++)
                {
                    e.Actor.Play("attack", 1f, false);
                    e.Actor.Squash(0.9f);
                    yield return BattleFX.Impact(skill.Fx, skill.Element, t.Center, t.Actor.transform.position);
                    yield return Hit(e, t, true, skill.Element, skill.Power, 0);
                    yield return Wait(0.25f);
                }
                e.Actor.Play("idle");
                yield return MoveActor(e.Actor, e.Home, 0.22f);
            }
            else
            {
                e.Actor.Play("attack", 1f, false);
                BattleFX.CastCircle(e.Actor.transform.position, c);
                yield return Wait(0.5f);
                foreach (var t in targets)
                {
                    StartCoroutine(BattleFX.Impact(skill.Fx, skill.Element, t.Center, t.Actor.transform.position));
                    yield return Wait(0.12f);
                }
                yield return Wait(0.25f);
                foreach (var t in targets)
                {
                    if (skill.Kind == SkillKind.Debuff)
                    {
                        if (skill.Power > 0) StartCoroutine(Hit(e, t, false, skill.Element, skill.Power, 0));
                        t.AddBuff(skill.Buff, 3);
                        hud.Popup(t.Top + Vector3.up * 0.4f, skill.Buff == BuffType.DefDown ? "DEF Down" : "ATK Down", UIKit.Down, 30, 0.2f);
                    }
                    else
                    {
                        for (int h = 0; h < skill.Hits; h++)
                            StartCoroutine(Hit(e, t, skill.Kind == SkillKind.Physical, skill.Element, skill.Power, h * 0.15f));
                    }
                }
                yield return Wait(0.8f);
                e.Actor.Play("idle");
            }
            hud.Banner(null);
            ResetCamera();
        }

        IEnumerator BossPhaseTwo()
        {
            var boss = enemies.FirstOrDefault(e => e.Enemy.IsBoss && e.Alive);
            if (boss == null) yield break;
            boss.Phase = 1;
            CameraRig.Instance.SetFixedShot(boss.Home + new Vector3(1.5f, 2.6f, -6.5f), boss.Center + Vector3.up * 0.4f, 28f);
            hud.Banner(UIKit.Col("Malzarath:", UIKit.Red) + " “Enough! Kneel before the true face of the Endless Night!”");
            yield return Wait(1.8f);
            AudioManager.Play("roar", 1f);
            CameraRig.Instance.Shake(0.35f, 1.4f);
            PostFX.Instance?.Flash(1.2f);
            for (int i = 0; i < 4; i++)
            {
                FX.Burst(boss.Center, new Color(0.8f, 0.2f, 1f), 50, 6f, 0.4f, 1f, -0.3f, 0.8f);
                FX.Decal(boss.Actor.transform.position + Vector3.up * 0.06f, FX.Flat, ProcTex.MagicCircle, new Color(1f, 0.2f, 0.4f) * 1.6f, 1f, 7f, 0.8f, 90f);
                yield return Wait(0.3f);
            }
            boss.Actor.SetTint(new Color(1f, 0.6f, 0.75f));
            boss.Actor.SelfLight = 0.6f;
            var s = boss.EnemyStats;
            s.Atk = Mathf.RoundToInt(s.Atk * 1.15f);
            s.Mag = Mathf.RoundToInt(s.Mag * 1.15f);
            s.Spd += 6;
            boss.EnemyStats = s;
            boss.Weaknesses = new List<Element> { Element.Axe, Element.Fire, Element.Light, Element.Staff };
            boss.Revealed.Clear();
            boss.MaxShield = 9;
            boss.Shield = 9;
            boss.Broken = false;
            hud.BuildEnemies(enemies);
            yield return hud.BigText("THE ENDLESS NIGHT", new Color(0.9f, 0.35f, 1f), 1.6f, 80);
            hud.Banner("The Dark Lord's weaknesses have changed! He now acts twice per turn.");
            yield return Wait(1.8f);
            hud.Banner(null);
            ResetCamera();
        }

        // ================================================================== end of battle
        IEnumerator Victory()
        {
            hud.SetTargets(null);
            hud.ShowCommands(false);
            yield return Wait(0.8f);
            if (isBoss)
            {
                AudioManager.StopMusic(1.5f);
                yield break; // the ending sequence takes over
            }
            AudioManager.Music("bgm_victory", 0.2f, false);
            foreach (var p in AliveParty) p.Actor.Play("attack", 1f, false);
            StartCoroutine(hud.BigText("VICTORY", UIKit.Gold, 1.6f, 100));
            yield return Wait(1.5f);

            int exp = enemies.Sum(e => e.Enemy.Exp);
            int gold = enemies.Sum(e => e.Enemy.Gold);
            state.Gold += gold;
            var lines = new List<string> { "EXP  " + UIKit.Col("+" + exp, UIKit.Gold), "Gold  " + UIKit.Col("+" + gold, UIKit.Gold) };
            foreach (var e in enemies)
                if (!string.IsNullOrEmpty(e.Enemy.Drop) && Random.value < 0.45f)
                {
                    state.AddItem(e.Enemy.Drop);
                    lines.Add("Obtained " + UIKit.Col(Database.Items[e.Enemy.Drop].Name, UIKit.Cyan));
                }
            bool leveled = false;
            foreach (var p in party)
            {
                var learned = p.Member.GainExp(exp, out int lv);
                if (lv > 0)
                {
                    leveled = true;
                    lines.Add(UIKit.Col(p.Name, UIKit.Gold) + " reached level " + p.Member.Level + "!");
                    FX.Burst(p.Center, UIKit.Gold, 40, 2f, 0.15f, 1.4f, -0.6f, 0.5f, true);
                    hud.Popup(p.Top, "LEVEL UP!", UIKit.Gold, 36);
                }
                foreach (var s in learned) lines.Add("   " + p.Name + " learned " + UIKit.Col(s.Name, UIKit.Cyan) + "!");
            }
            if (leveled) AudioManager.Play("levelup", 0.9f);
            hud.RefreshParty(null, 0);
            var panel = hud.ShowResults("Battle Results", lines);
            yield return Wait(0.4f);
            while (!GameInput.Confirm && !GameInput.Cancel) { hud.Tick(); yield return null; }
            AudioManager.Play("confirm", 0.6f);
            Destroy(panel.gameObject);
        }

        IEnumerator Defeat()
        {
            hud.ShowCommands(false);
            AudioManager.StopMusic(1f);
            yield return hud.BigText("The party has fallen...", UIKit.Red, 2.2f, 70);
        }

        IEnumerator Cleanup()
        {
            yield return null;
            foreach (var p in party) { p.Defending = false; p.Buffs.Clear(); }
            hud.Destroy();
            hud = null;
            if (actorsRoot) Destroy(actorsRoot.gameObject);
            PostFX.Instance?.SetBattleMood(false, false);
        }
    }
}
