using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HD2DRPG
{
    public enum GameMode { Boot, Title, Field, Menu, Battle, Event, Ending }

    /// <summary>
    /// Owns the whole game: builds the world, runs the title screen, exploration, menus,
    /// battles, story events and the ending.
    /// </summary>
    public class Game : MonoBehaviour
    {
        public static Game I { get; private set; }

        public GameMode Mode { get; private set; } = GameMode.Boot;
        public PartyState State { get; private set; }
        public PartyController Party { get; private set; }
        public CastleBuilder.Result World { get; private set; }
        public FieldUI UI { get; private set; }

        PartyState checkpoint;
        MenuScreen menu;
        BattleManager battle;
        CameraRig rig;
        Light moon;
        string lastArea;

        public string LocationName =>
            World != null && Party != null && Party.Leader != null ? World.Map.AreaName(Party.Leader.transform.position) : "";

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            Application.targetFrameRate = 60;
            Database.Build();
            PixelArt.Load();
            RemoveSceneDefaults();
        }

        /// <summary>The project runs from any scene: disable stray cameras/lights from templates.</summary>
        static void RemoveSceneDefaults()
        {
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (cam.GetComponent<CameraRig>() == null) cam.gameObject.SetActive(false);
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) l.gameObject.SetActive(false);
        }

        IEnumerator Start()
        {
            SetupEnvironment();
            UI = new FieldUI(transform);
            UI.Fader.Alpha = 1f;
            yield return null;

            State = PartyState.NewGame();
            World = CastleBuilder.Build(transform, State);
            Party = new GameObject("Party").AddComponent<PartyController>();
            Party.transform.SetParent(transform, false);
            Party.Init(World.Map, State, World.Start);
            Cutaway.Focus = Party.Leader.transform;
            battle = new GameObject("Battle").AddComponent<BattleManager>();
            battle.transform.SetParent(transform, false);
            battle.Init(transform);
            menu = new MenuScreen(transform, State);

            yield return TitleLoop();
        }

        void SetupEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.24f, 0.21f, 0.38f);
            RenderSettings.ambientEquatorColor = new Color(0.15f, 0.12f, 0.23f);
            RenderSettings.ambientGroundColor = new Color(0.07f, 0.05f, 0.09f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.02f;
            RenderSettings.fogColor = new Color(0.05f, 0.035f, 0.09f);
            RenderSettings.skybox = null;

            var moonGo = new GameObject("Moonlight");
            moonGo.transform.SetParent(transform, false);
            moonGo.transform.rotation = Quaternion.Euler(52f, 28f, 0f);
            moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.6f, 0.66f, 1f);
            moon.intensity = 0.5f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.75f;
            RenderSettings.sun = moon;

            rig = CameraRig.Create();
            rig.transform.SetParent(transform, false);
            PostFX.Create(rig.Cam).transform.SetParent(transform, false);
            AudioManager.Create(transform);
        }

        void SetMode(GameMode m)
        {
            Mode = m;
            if (Party != null) Party.InputEnabled = m == GameMode.Field;
            UI.SetFieldHudVisible(m == GameMode.Field);
            if (m != GameMode.Field) UI.Prompt(null);
        }

        // ================================================================== title
        IEnumerator TitleLoop()
        {
            SetMode(GameMode.Title);
            UI.SetFieldHudVisible(false);
            var map = World.Map;
            Vector3 throne = map.TileToWorld(map.Find('T'));
            rig.SetFixedShot(throne + new Vector3(0, 7.5f, -20f), throne + new Vector3(0, 2.5f, -1f), 30f, true);
            AudioManager.Music("bgm_castle", 1.5f);
            yield return UI.Fader.To(0f, 1.5f);
            int choice = -1;
            yield return UI.Title(c => choice = c);
            if (choice == 2)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                yield break;
            }
            yield return UI.Fader.To(1f, 0.8f);
            yield return NewGame();
        }

        IEnumerator NewGame()
        {
            State = PartyState.NewGame();
            State.Position = World.Start;
            ResetWorld();
            Party.Init(World.Map, State, World.Start);
            if (menu != null) menu.Destroy();
            menu = new MenuScreen(transform, State);
            checkpoint = State.Clone();
            SetDawn(false);
            rig.Follow(Party.Leader.transform, true);
            Party.Leader.FacingRight = true;
            yield return UI.Fader.To(0f, 1.2f);
            yield return OpeningEvent();
            SetMode(GameMode.Field);
            lastArea = null;
        }

        void ResetWorld()
        {
            foreach (var c in World.Chests)
                if (State.Flags.Contains(c.FlagKey)) c.SetOpened(); else c.SetClosed();
            foreach (var s in World.Symbols) s.Restore(State.Flags.Contains("enc_" + s.EncounterId));
            if (World.Boss != null) World.Boss.Restore(State.Flags.Contains("boss"));
        }

        // ================================================================== field
        void Update()
        {
            if (Mode == GameMode.Field || Mode == GameMode.Menu) State.PlayTime += Time.unscaledDeltaTime;
            if (Mode != GameMode.Field || Party == null || Party.Leader == null) return;

            string area = LocationName;
            if (area != lastArea)
            {
                lastArea = area;
                UI.ShowLocation(area);
            }

            Vector3 lp = Party.Leader.transform.position;
            object target = null;
            float best = 1.35f;
            foreach (var c in World.Chests)
            {
                if (c.Opened) continue;
                float d = Vector3.Distance(lp, c.GroundPos);
                if (d < best) { best = d; target = c; }
            }
            foreach (var s in World.Crystals)
            {
                float d = Vector3.Distance(lp, s.GroundPos);
                if (d < best + 0.3f) { best = d; target = s; }
            }
            UI.Prompt(target is Chest ? "Z  Open chest" : target is SaveCrystal ? "Z  Touch crystal" : null);

            if (GameInput.Confirm && target != null)
            {
                if (target is Chest chest) StartCoroutine(OpenChest(chest));
                else if (target is SaveCrystal sc) StartCoroutine(UseCrystal(sc));
                return;
            }
            if (GameInput.Menu)
            {
                GameInput.Consume();
                StartCoroutine(OpenMenu());
            }
        }

        IEnumerator OpenMenu()
        {
            SetMode(GameMode.Menu);
            yield return menu.Run();
            SetMode(GameMode.Field);
        }

        IEnumerator OpenChest(Chest chest)
        {
            SetMode(GameMode.Event);
            GameInput.Consume();
            AudioManager.Play("chest", 0.9f);
            chest.SetOpened();
            State.Flags.Add(chest.FlagKey);
            FX.Burst(chest.GroundPos + Vector3.up * 0.6f, new Color(1f, 0.85f, 0.4f), 30, 2f, 0.12f, 1f, -0.5f, 0.3f, true);
            FX.Flash(chest.GroundPos + Vector3.up, new Color(1f, 0.8f, 0.4f), 3f, 4f, 0.6f);
            var lines = new List<string>();
            foreach (var entry in chest.Contents)
            {
                var p = entry.Split(':');
                switch (p[0])
                {
                    case "item":
                        State.AddItem(p[1]);
                        lines.Add(UIKit.Col(Database.Items[p[1]].Name, UIKit.Cyan));
                        break;
                    case "equip":
                        State.AddEquip(p[1]);
                        lines.Add(UIKit.Col(Database.Equips[p[1]].Name, UIKit.Gold));
                        break;
                    case "gold":
                        State.Gold += int.Parse(p[1]);
                        lines.Add(UIKit.Col(p[1] + " Gold", UIKit.Gold));
                        break;
                }
            }
            yield return new WaitForSeconds(0.3f);
            yield return UI.Notice("Obtained!", lines);
            SetMode(GameMode.Field);
        }

        IEnumerator UseCrystal(SaveCrystal sc)
        {
            SetMode(GameMode.Event);
            GameInput.Consume();
            AudioManager.Play("save", 0.9f);
            foreach (var m in State.Members) m.FullRestore();
            FX.Burst(sc.GroundPos + Vector3.up * 0.8f, new Color(0.5f, 0.85f, 1f), 60, 3f, 0.15f, 1.4f, -0.4f, 0.6f, true);
            FX.Flash(sc.GroundPos + Vector3.up, new Color(0.5f, 0.8f, 1f), 6f, 8f, 1f);
            foreach (var a in Party.Actors) a.Flash(new Color(0.6f, 0.9f, 1f), 0.8f);
            State.Position = Party.Leader.transform.position;
            checkpoint = State.Clone();
            yield return new WaitForSeconds(0.4f);
            yield return UI.Notice("Save Crystal", new[]
            {
                "The crystal's light restores the party.",
                "HP and MP fully recovered.",
                UIKit.Col("Progress recorded. You will return here if the party falls.", UIKit.Dim)
            });
            SetMode(GameMode.Field);
        }

        // ================================================================== battles
        public void TriggerEncounter(EnemySymbol symbol)
        {
            if (Mode != GameMode.Field) return;
            StartCoroutine(EncounterRoutine(symbol, false));
        }

        public void TriggerBoss(EnemySymbol symbol)
        {
            if (Mode != GameMode.Field) return;
            StartCoroutine(EncounterRoutine(symbol, true));
        }

        IEnumerator BattleTransitionIn()
        {
            AudioManager.Play("encounter", 0.9f);
            AudioManager.StopMusic(0.4f);
            PostFX.Instance?.Flash(1.5f);
            var lp = Party.Leader.transform.position;
            rig.SetFixedShot(lp + new Vector3(0, 2.2f, -4.5f), lp + Vector3.up, 22f);
            UI.Fader.SetColor(Color.white);
            yield return UI.Fader.To(0.85f, 0.15f);
            yield return UI.Fader.To(0f, 0.15f);
            UI.Fader.SetColor(Color.black);
            yield return UI.Fader.To(1f, 0.35f);
        }

        IEnumerator EncounterRoutine(EnemySymbol symbol, bool boss)
        {
            SetMode(GameMode.Battle);
            if (boss)
            {
                SetMode(GameMode.Event);
                yield return BossIntro(symbol);
                SetMode(GameMode.Battle);
            }
            yield return BattleTransitionIn();
            Party.SetVisible(false);
            var run = battle.Run(State, symbol.EncounterId, boss);
            StartCoroutine(FadeInSoon());
            yield return run;
            var result = battle.Result;

            if (result == BattleResult.Defeat)
            {
                yield return GameOverRoutine();
                yield break;
            }
            yield return UI.Fader.To(1f, 0.5f);
            Party.SetVisible(true);
            if (result == BattleResult.Victory)
            {
                if (boss)
                {
                    State.Flags.Add("boss");
                    yield return EndingRoutine(symbol);
                    yield break;
                }
                State.Flags.Add("enc_" + symbol.EncounterId);
                symbol.Defeat();
            }
            else
            {
                symbol.Retreat();
            }
            rig.Follow(Party.Leader.transform, true);
            AudioManager.Music("bgm_castle", 1f);
            yield return UI.Fader.To(0f, 0.6f);
            SetMode(GameMode.Field);
        }

        IEnumerator FadeInSoon()
        {
            yield return null;
            yield return null;
            yield return UI.Fader.To(0f, 0.45f);
        }

        IEnumerator GameOverRoutine()
        {
            SetMode(GameMode.Event);
            yield return UI.Fader.To(1f, 1f);
            Party.SetVisible(true);
            UI.Fader.Alpha = 0f;
            int choice = 0;
            yield return UI.GameOver(c => choice = c);
            yield return UI.Fader.To(1f, 0.6f);
            if (choice == 0)
            {
                State = checkpoint.Clone();
                ResetWorld();
                Party.Init(World.Map, State, State.Position);
                menu.Destroy();
                menu = new MenuScreen(transform, State);
                rig.Follow(Party.Leader.transform, true);
                AudioManager.Music("bgm_castle", 1f);
                yield return UI.Fader.To(0f, 0.8f);
                lastArea = null;
                SetMode(GameMode.Field);
            }
            else
            {
                yield return TitleLoop();
            }
        }

        // ================================================================== story events
        static Line Aren(string t) => new Line("Aren", "aren_stand", t);
        static Line Gareth(string t) => new Line("Gareth", "gareth_stand", t);
        static Line Theia(string t) => new Line("Theia", "theia_stand", t);
        static Line Lord(string t) => new Line("Malzarath", "darklord", t);
        static Line Narr(string t) => new Line(null, null, t);

        IEnumerator OpeningEvent()
        {
            SetMode(GameMode.Event);
            yield return new WaitForSeconds(0.4f);
            yield return UI.Dialogue(new[]
            {
                Narr("For a hundred years, the Endless Night has smothered the land. No sun has risen; no harvest has ripened."),
                Narr("At its heart stands the castle of Malzarath, the Dark Lord — and tonight, three heroes have breached its gates."),
                Aren("This is it... the Dark Lord's castle. Whatever happens, everything ends tonight."),
                Gareth("Then stay behind my shield, both of you. Anything that crawls out of these shadows goes through me first."),
                Theia("The gods of Olympus watch over us. Strike at a foe's weakness and its guard will shatter — then we press the attack."),
                Aren("The throne room lies to the north, beyond the Moonlit Gallery. Let's go."),
            });
            UI.ShowLocation(LocationName);
            lastArea = LocationName;
        }

        IEnumerator BossIntro(EnemySymbol boss)
        {
            AudioManager.StopMusic(1.5f);
            var bp = boss.transform.position;
            var lp = Party.Leader.transform.position;
            Party.Leader.FacingRight = bp.x >= lp.x;
            rig.SetFixedShot(bp + new Vector3(0, 3.2f, -8.5f), bp + new Vector3(0, 1.6f, -1.5f), 30f);
            yield return new WaitForSeconds(1.2f);
            AudioManager.Play("roar", 0.8f);
            rig.Shake(0.15f, 1f);
            boss.Actor.Flash(new Color(1f, 0.2f, 0.4f), 1f);
            yield return UI.Dialogue(new[]
            {
                Lord("So... the last embers of the Order of the Runic Flame come crawling to my throne."),
                Aren("Your night ends here, Malzarath! We've come to bring back the dawn."),
                Lord("A spellblade, a tin soldier and a priestess of dead gods. How very touching."),
                Gareth("This tin soldier has carried your nightmares on his back for a hundred miles. I'm not tired yet."),
                Theia("By the light of Apollo and the wisdom of Athena — your reign is over!"),
                Lord("Then come! I will show you why the sun has not risen in a hundred years!"),
            });
        }

        IEnumerator EndingRoutine(EnemySymbol boss)
        {
            SetMode(GameMode.Ending);
            boss.Restore(true);
            var map = World.Map;
            Vector3 stand = map.TileToWorld(map.Find('B')) + new Vector3(0, 0, -1f);
            Party.Init(map, State, stand);
            Party.Leader.FacingRight = true;
            Vector3 throne = map.TileToWorld(map.Find('T'));
            rig.SetFixedShot(stand + new Vector3(0, 4f, -9f), stand + new Vector3(0, 1.4f, 1.5f), 32f, true);
            yield return UI.Fader.To(0f, 1f);
            // the Dark Lord's last moments, as a fading silhouette on the throne
            var ghost = SpriteActor.Create("FallenLord", "darklord", transform, 1.45f);
            ghost.transform.position = throne + new Vector3(0, 0, -1.2f);
            ghost.FacingRight = false;
            ghost.SetTint(new Color(0.6f, 0.4f, 0.8f));
            yield return UI.Dialogue(new[]
            {
                Lord("Impossible... The night... is... eternal..."),
                Theia("Nothing is eternal. Not even darkness."),
            });
            ghost.Dissolve();
            AudioManager.Play("dark", 1f, 0.6f);
            FX.Burst(ghost.transform.position + Vector3.up * 1.5f, new Color(0.7f, 0.3f, 1f), 80, 4f, 0.3f, 2f, -0.4f, 0.8f);
            rig.Shake(0.2f, 1.2f);
            yield return new WaitForSeconds(2f);
            Destroy(ghost.gameObject);

            // dawn
            AudioManager.Music("bgm_ending", 2f);
            yield return DawnTransition(4f);
            yield return UI.Dialogue(new[]
            {
                Gareth("Look — the windows. Is that... sunlight?"),
                Theia("Helios rides his chariot across the sky once more. The Endless Night is over."),
                Aren("The first dawn in a hundred years... We did it. All of us."),
                Gareth("Heh. Told you my shield would hold."),
                Aren("Come on. Let's go home — and tell everyone the sun is back."),
            });
            yield return UI.Fader.To(1f, 2f);
            UI.Fader.Alpha = 0f;
            yield return UI.Credits();
            yield return UI.Fader.To(1f, 1f);
            yield return TitleLoop();
        }

        IEnumerator DawnTransition(float time)
        {
            Color m0 = moon.color, m1 = new Color(1f, 0.82f, 0.6f);
            float i0 = moon.intensity, i1 = 1.4f;
            Color a0 = RenderSettings.ambientSkyColor, a1 = new Color(0.65f, 0.55f, 0.5f);
            Color e0 = RenderSettings.ambientEquatorColor, e1 = new Color(0.45f, 0.35f, 0.3f);
            Color f0 = RenderSettings.fogColor, f1 = new Color(0.55f, 0.42f, 0.35f);
            for (float t = 0; t < time; t += Time.deltaTime)
            {
                float k = t / time;
                moon.color = Color.Lerp(m0, m1, k);
                moon.intensity = Mathf.Lerp(i0, i1, k);
                RenderSettings.ambientSkyColor = Color.Lerp(a0, a1, k);
                RenderSettings.ambientEquatorColor = Color.Lerp(e0, e1, k);
                RenderSettings.fogColor = Color.Lerp(f0, f1, k);
                if (rig.Cam) rig.Cam.backgroundColor = Color.Lerp(new Color(0.02f, 0.012f, 0.035f), new Color(0.5f, 0.38f, 0.32f), k);
                yield return null;
            }
            PostFX.Instance?.Flash(0.4f);
        }

        void SetDawn(bool dawn)
        {
            if (dawn) return;
            moon.color = new Color(0.6f, 0.66f, 1f);
            moon.intensity = 0.5f;
            RenderSettings.ambientSkyColor = new Color(0.24f, 0.21f, 0.38f);
            RenderSettings.ambientEquatorColor = new Color(0.15f, 0.12f, 0.23f);
            RenderSettings.fogColor = new Color(0.05f, 0.035f, 0.09f);
            if (rig && rig.Cam) rig.Cam.backgroundColor = new Color(0.02f, 0.012f, 0.035f);
        }
    }

    /// <summary>Starts the game automatically in whatever scene is played.</summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Object.FindAnyObjectByType<Game>() != null) return;
            var go = new GameObject("HD2D RPG");
            go.AddComponent<Game>();
        }
    }
}
