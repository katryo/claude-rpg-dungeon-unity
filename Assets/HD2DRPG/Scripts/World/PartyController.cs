using System.Collections.Generic;
using UnityEngine;

namespace HD2DRPG
{
    /// <summary>
    /// Field movement: the leader walks on the castle grid with sliding collision, the other two
    /// members follow the leader's breadcrumb trail (classic JRPG caravan).
    /// </summary>
    public class PartyController : MonoBehaviour
    {
        public const float WalkSpeed = 3.4f;
        public const float RunSpeed = 5.6f;
        public const float Radius = 0.3f;
        const float FollowSpacing = 0.85f;

        public CastleMap Map;
        public readonly List<SpriteActor> Actors = new List<SpriteActor>();
        public SpriteActor Leader => Actors.Count > 0 ? Actors[0] : null;
        public bool InputEnabled = true;
        public Vector3 LastMoveDir = Vector3.forward;

        readonly List<Vector3> trail = new List<Vector3>();
        float stepTimer;

        public static SpriteActor CreateMemberActor(PartyMember m, Transform parent)
        {
            string p = m.Def.SpritePrefix;
            var a = SpriteActor.Create(m.Name, p + "_stand", parent);
            a.AddAnim("idle", p + "_stand");
            a.AddAnim("walk", p + "_walk1", p + "_stand", p + "_walk2", p + "_stand");
            string act = PixelArt.Has(p + "_attack") ? p + "_attack" : p + "_cast";
            a.AddAnim("attack", act);
            a.AddAnim("cast", PixelArt.Has(p + "_cast") ? p + "_cast" : act);
            a.Play("idle");
            return a;
        }

        public void Init(CastleMap map, PartyState state, Vector3 start)
        {
            Map = map;
            foreach (var a in Actors) if (a) Destroy(a.gameObject);
            Actors.Clear();
            foreach (var m in state.Members)
            {
                var a = CreateMemberActor(m, transform);
                Actors.Add(a);
            }
            Teleport(start, Vector3.forward);
        }

        public void Teleport(Vector3 pos, Vector3 facingDir)
        {
            trail.Clear();
            trail.Add(pos);
            for (int i = 0; i < Actors.Count; i++)
            {
                // try to line up behind the leader, but never inside a wall
                Vector3 p = pos - facingDir * FollowSpacing * i;
                if (Map != null && !Map.CanStand(p, Radius)) p = pos;
                Actors[i].transform.position = p;
                Actors[i].Play("idle");
            }
            LastMoveDir = facingDir;
        }

        void Update()
        {
            if (Leader == null || Map == null) return;
            Vector2 input = InputEnabled ? GameInput.Move : Vector2.zero;
            Vector3 move = new Vector3(input.x, 0, input.y);
            bool moving = move.sqrMagnitude > 0.01f;
            float speed = GameInput.Run ? RunSpeed : WalkSpeed;
            var lt = Leader.transform;

            if (moving)
            {
                Vector3 delta = move * speed * Time.deltaTime;
                Vector3 p = lt.position;
                Vector3 nx = p + new Vector3(delta.x, 0, 0);
                if (Map.CanStand(nx, Radius)) p = nx;
                Vector3 nz = p + new Vector3(0, 0, delta.z);
                if (Map.CanStand(nz, Radius)) p = nz;
                if ((p - lt.position).sqrMagnitude > 1e-6f)
                {
                    lt.position = p;
                    LastMoveDir = move.normalized;
                    RecordTrail(p);
                }
                if (Mathf.Abs(move.x) > 0.1f) Leader.FacingRight = move.x > 0;
                stepTimer -= Time.deltaTime * speed;
                if (stepTimer <= 0) { stepTimer = 1.6f; AudioManager.Play("step", 0.25f, Random.Range(0.85f, 1.15f)); }
            }

            Leader.Play(moving ? "walk" : "idle", GameInput.Run ? 11f : 8f);

            // followers
            for (int i = 1; i < Actors.Count; i++)
            {
                var f = Actors[i];
                Vector3 target = TrailPoint(i * FollowSpacing);
                Vector3 cur = f.transform.position;
                Vector3 d = target - cur;
                bool fMoving = d.sqrMagnitude > 0.0004f;
                f.transform.position = Vector3.MoveTowards(cur, target, speed * 1.25f * Time.deltaTime);
                if (Mathf.Abs(d.x) > 0.01f) f.FacingRight = d.x > 0;
                f.Play(fMoving && moving ? "walk" : "idle", GameInput.Run ? 11f : 8f);
            }
            Cutaway.Focus = lt;
        }

        void RecordTrail(Vector3 p)
        {
            if (trail.Count == 0 || (trail[0] - p).sqrMagnitude > 0.0025f)
            {
                trail.Insert(0, p);
                if (trail.Count > 400) trail.RemoveAt(trail.Count - 1);
            }
        }

        Vector3 TrailPoint(float distance)
        {
            float acc = 0;
            for (int i = 1; i < trail.Count; i++)
            {
                float seg = Vector3.Distance(trail[i - 1], trail[i]);
                if (acc + seg >= distance)
                {
                    float t = (distance - acc) / Mathf.Max(seg, 1e-5f);
                    return Vector3.Lerp(trail[i - 1], trail[i], t);
                }
                acc += seg;
            }
            return trail.Count > 0 ? trail[trail.Count - 1] : Leader.transform.position;
        }

        public void SetVisible(bool v)
        {
            foreach (var a in Actors) if (a) a.gameObject.SetActive(v);
        }
    }

}
