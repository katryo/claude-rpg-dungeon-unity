using System.Collections.Generic;
using UnityEngine;

namespace HD2DRPG
{
    /// <summary>Visible wandering enemy on the field. Touching it starts a battle.</summary>
    [SelectionBase]
    public class EnemySymbol : MonoBehaviour
    {
        [Tooltip("Encounter id from the game data (Encounters list).")]
        public string EncounterId;
        public bool Wanders = true;
        public bool IsBoss;
        public SpriteActor Actor;
        public float Cooldown;
        [Tooltip("Radius around the spawn point it wanders in.")]
        public float WanderRadius = 2.5f;
        [Tooltip("Distance at which it starts chasing the party.")]
        public float ChaseRange = 4.2f;
        [SerializeField, HideInInspector] ParticleSystem aura;
        Vector3 home;
        Vector3 wanderTarget;
        float wanderTimer;
        bool homeSet;

        void Awake() => SetHome();

        void SetHome()
        {
            if (homeSet) return;
            homeSet = true;
            home = transform.position;
            wanderTarget = home;
        }

        public static EnemySymbol Create(Transform parent, Vector3 pos, string encounterId, string sprite,
                                         bool wanders, float scale, Color tint)
        {
            var go = new GameObject("Symbol_" + encounterId);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var s = go.AddComponent<EnemySymbol>();
            s.EncounterId = encounterId;
            s.Wanders = wanders;
            s.Actor = SpriteActor.Create("Sprite", sprite, go.transform, scale);
            s.Actor.transform.localPosition = Vector3.zero;
            s.Actor.FacingRight = false;
            s.Actor.Floating = sprite == "wraith" || sprite == "gargoyle";
            if (tint != Color.white) s.Actor.SetTint(tint);
            s.aura = EnvKit.Ambient(go.transform, pos + Vector3.up * 0.6f, new Color(0.6f, 0.2f, 1f), 8f, 0.12f, 1.2f,
                                    new Vector3(0, 0.7f, 0), 0.45f, false);
            return s;
        }

        public bool Defeated { get; private set; }

        /// <summary>Puts the symbol back at its spawn point (used when retrying from a save crystal).</summary>
        public void Restore(bool defeated)
        {
            SetHome();
            Defeated = defeated;
            transform.position = home;
            wanderTarget = home;
            Cooldown = 0;
            gameObject.SetActive(!defeated);
            enabled = !defeated;
            if (!defeated)
            {
                Actor.ResetVisual();
                Actor.gameObject.SetActive(true);
                if (aura) aura.Play();
            }
        }

        /// <summary>After the party flees: retreat to the spawn point and pause briefly.</summary>
        public void Retreat()
        {
            SetHome();
            transform.position = home;
            wanderTarget = home;
            Cooldown = 3f;
        }

        public void Defeat()
        {
            Defeated = true;
            if (aura) aura.Stop();
            Actor.Dissolve();
            FX.Burst(transform.position + Vector3.up * 0.8f, new Color(0.7f, 0.4f, 1f), 40, 3f, 0.25f, 1f, -0.3f);
            enabled = false;
        }

        void Update()
        {
            if (Game.I == null || Game.I.Mode != GameMode.Field) return;
            var party = Game.I.Party;
            if (party == null || party.Leader == null) return;
            Vector3 lp = party.Leader.transform.position;
            Vector3 me = transform.position;
            float dist = Vector3.Distance(new Vector3(lp.x, 0, lp.z), new Vector3(me.x, 0, me.z));

            if (Cooldown > 0)
            {
                Cooldown -= Time.deltaTime;
                Actor.gameObject.SetActive(Mathf.Repeat(Cooldown, 0.2f) > 0.1f || Cooldown <= 0);
                return;
            }

            if (IsBoss)
            {
                Actor.FacingRight = lp.x > me.x;
                if (dist < 3.4f) Game.I.TriggerBoss(this);
                return;
            }

            if (dist < 0.85f)
            {
                Game.I.TriggerEncounter(this);
                return;
            }

            if (!Wanders)
            {
                Actor.FacingRight = lp.x > me.x;
                return;
            }

            Vector3 target;
            float speed;
            if (dist < ChaseRange)
            {
                target = lp;
                speed = 2.5f;
            }
            else
            {
                wanderTimer -= Time.deltaTime;
                if (wanderTimer <= 0)
                {
                    wanderTimer = Random.Range(1.5f, 3f);
                    wanderTarget = home + new Vector3(Random.Range(-WanderRadius, WanderRadius), 0, Random.Range(-WanderRadius, WanderRadius) * 0.8f);
                }
                target = wanderTarget;
                speed = 1.1f;
            }
            Vector3 d = target - me;
            d.y = 0;
            if (d.magnitude > 0.05f)
            {
                Vector3 step = d.normalized * speed * Time.deltaTime;
                Vector3 p = me;
                if (WorldCollision.CanStand(p + new Vector3(step.x, 0, 0), 0.3f)) p.x += step.x;
                if (WorldCollision.CanStand(p + new Vector3(0, 0, step.z), 0.3f)) p.z += step.z;
                transform.position = p;
                if (Mathf.Abs(d.x) > 0.05f) Actor.FacingRight = d.x > 0;
            }
        }
    }
}
