using UnityEngine;

namespace HD2DRPG
{
    /// <summary>
    /// Battle arenas (a castle hall and the throne room), built once off to the side of the
    /// dungeon. Party stands on the right, enemies on the left, in the HD-2D tradition.
    /// </summary>
    public class BattleStage : MonoBehaviour
    {
        public Vector3 Origin => transform.position;
        public bool IsBoss;

        public static readonly Vector3[] PartySlots =
        {
            new Vector3(3.0f, 0, 1.9f), new Vector3(3.9f, 0, 0.55f), new Vector3(4.8f, 0, -0.8f)
        };

        public static Vector3[] EnemySlots(int count, bool boss)
        {
            if (boss) return new[] { new Vector3(-3.6f, 0, 1.2f) };
            switch (count)
            {
                case 1: return new[] { new Vector3(-3.4f, 0, 0.6f) };
                case 2: return new[] { new Vector3(-2.9f, 0, 1.5f), new Vector3(-3.9f, 0, -0.3f) };
                default: return new[] { new Vector3(-2.6f, 0, 2.0f), new Vector3(-4.3f, 0, 0.7f), new Vector3(-2.8f, 0, -0.7f) };
            }
        }

        public Vector3 CameraPos => Origin + new Vector3(0.2f, 5.0f, -10.8f);
        public Vector3 CameraLook => Origin + new Vector3(0.2f, 1.25f, 0.7f);

        public static BattleStage Build(Transform parent, Vector3 origin, bool boss)
        {
            var go = new GameObject(boss ? "BossStage" : "BattleStage");
            go.transform.SetParent(parent, false);
            go.transform.position = origin;
            var st = go.AddComponent<BattleStage>();
            st.IsBoss = boss;
            st.Construct();
            return st;
        }

        void Construct()
        {
            var root = transform;
            Vector3 o = Origin;
            const float W = 12f, back = 8.5f, front = -7f, H = 6.5f;

            var floor = new MeshBuilder();
            for (float x = -W; x < W; x += 4)
                floor.Face(new Vector3(x, 0, front), new Vector3(4, 0, 0), new Vector3(0, 0, back - front), 0.5f);
            floor.Create("Floor", root, EnvKit.FloorMat, false);

            var walls = new MeshBuilder();
            walls.Box(new Vector3(-W - 1, 0, back), new Vector3(W + 1, H, back + 1), MeshBuilder.Faces.South | MeshBuilder.Faces.Top);
            walls.Box(new Vector3(-W - 1, 0, front), new Vector3(-W, H, back), MeshBuilder.Faces.East | MeshBuilder.Faces.Top);
            walls.Box(new Vector3(W, 0, front), new Vector3(W + 1, H, back), MeshBuilder.Faces.West | MeshBuilder.Faces.Top);
            walls.Create("Walls", root, EnvKit.WallMat);
            var trim = new MeshBuilder();
            trim.Box(new Vector3(-W, 0, back - 0.35f), new Vector3(W, 0.5f, back), MeshBuilder.Faces.South | MeshBuilder.Faces.Top);
            trim.Box(new Vector3(-W, H - 0.4f, back - 0.25f), new Vector3(W, H - 0.1f, back), MeshBuilder.Faces.South | MeshBuilder.Faces.Bottom);
            trim.Create("Trim", root, EnvKit.TrimMat);

            Vector3 backFace = new Vector3(0, 0, back);
            if (!IsBoss)
            {
                EnvKit.Window(root, o + backFace + new Vector3(-7f, 0, 0), Vector3.back, 1.6f, 3.2f, 1.4f);
                EnvKit.Window(root, o + backFace + new Vector3(7f, 0, 0), Vector3.back, 1.6f, 3.2f, 1.4f);
                EnvKit.Window(root, o + backFace + new Vector3(0f, 0, 0), Vector3.back, 1.6f, 3.2f, 1.4f);
                EnvKit.Torch(root, o + backFace + new Vector3(-3.5f, 0, 0), Vector3.back, 2.4f, 1.2f);
                EnvKit.Torch(root, o + backFace + new Vector3(3.5f, 0, 0), Vector3.back, 2.4f, 1.2f);
                EnvKit.Banner(root, o + backFace + new Vector3(-10f, 0, 0), Vector3.back, 1.1f, 3f, 1.5f);
                EnvKit.Banner(root, o + backFace + new Vector3(10f, 0, 0), Vector3.back, 1.1f, 3f, 1.5f);
                foreach (var px in new[] { -8.6f, 8.6f })
                    EnvKit.Pillar(root, o + new Vector3(px, 0, 5.8f), 5.2f);
                foreach (var px in new[] { -10.4f, 10.4f })
                    EnvKit.Pillar(root, o + new Vector3(px, 0, 0.8f), 5.2f);
                EnvKit.Torch(root, o + new Vector3(-W, 0, 2.5f), Vector3.right, 2.4f, 1.2f);
                EnvKit.Torch(root, o + new Vector3(W, 0, 2.5f), Vector3.left, 2.4f, 1.2f);
                // party-side warm fill light, enemy-side cold light
                EnvKit.PointLight(root, o + new Vector3(4.5f, 3.5f, -2.5f), new Color(1f, 0.75f, 0.55f), 1.6f, 11f, false);
                EnvKit.PointLight(root, o + new Vector3(-4.5f, 3.5f, -2.5f), new Color(0.6f, 0.55f, 1f), 1.4f, 11f, false);
            }
            else
            {
                var carpet = new MeshBuilder();
                carpet.FaceUV(new Vector3(-1.5f, 0.012f, front), new Vector3(3, 0, 0), new Vector3(0, 0, back - front), new Vector2(1, back - front));
                carpet.Create("Carpet", root, EnvKit.CarpetMat, false);
                EnvKit.Throne(root, o + new Vector3(0, 0, back - 1.3f));
                EnvKit.RoseWindow(root, o + backFace, Vector3.back);
                EnvKit.Brazier(root, o + new Vector3(-2.8f, 0, back - 1.6f), new Color(1f, 0.35f, 0.25f), 1.2f);
                EnvKit.Brazier(root, o + new Vector3(2.8f, 0, back - 1.6f), new Color(1f, 0.35f, 0.25f), 1.2f);
                EnvKit.Brazier(root, o + new Vector3(-8.5f, 0, 1.5f), new Color(0.75f, 0.35f, 1f), 1.1f);
                EnvKit.Brazier(root, o + new Vector3(8.5f, 0, 1.5f), new Color(0.75f, 0.35f, 1f), 1.1f);
                EnvKit.Banner(root, o + backFace + new Vector3(-5f, 0, 0), Vector3.back, 1.3f, 3.6f, 1.4f);
                EnvKit.Banner(root, o + backFace + new Vector3(5f, 0, 0), Vector3.back, 1.3f, 3.6f, 1.4f);
                EnvKit.Window(root, o + backFace + new Vector3(-9f, 0, 0), Vector3.back, 1.6f, 3.4f, 1.4f, new Color(0.9f, 0.4f, 0.8f));
                EnvKit.Window(root, o + backFace + new Vector3(9f, 0, 0), Vector3.back, 1.6f, 3.4f, 1.4f, new Color(0.9f, 0.4f, 0.8f));
                foreach (var px in new[] { -6.5f, 6.5f })
                    EnvKit.Pillar(root, o + new Vector3(px, 0, 5.5f), 5.6f);
                foreach (var px in new[] { -10.4f, 10.4f })
                    EnvKit.Pillar(root, o + new Vector3(px, 0, 0.5f), 5.6f);
                EnvKit.PointLight(root, o + new Vector3(4.5f, 3.5f, -2.5f), new Color(1f, 0.75f, 0.6f), 1.5f, 11f, false);
                EnvKit.PointLight(root, o + new Vector3(-4f, 3.0f, -1.5f), new Color(0.75f, 0.3f, 1f), 2.2f, 10f, false);
            }

            var dust = EnvKit.Ambient(root, o + new Vector3(0, 2.5f, 1), IsBoss ? new Color(1f, 0.4f, 0.6f) * 0.5f : new Color(0.75f, 0.7f, 1f) * 0.45f,
                                      IsBoss ? 30f : 18f, 0.06f, 7f, new Vector3(0.05f, IsBoss ? 0.25f : 0.04f, 0), 1f);
            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2 * W, 4f, back - front);
            var main = dust.main;
            main.prewarm = true;
        }
    }
}
