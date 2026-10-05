using System.Collections.Generic;
using UnityEngine;

namespace HD2DRPG
{
    /// <summary>Builds the explorable dark castle diorama from <see cref="CastleMap"/>.</summary>
    public static class CastleBuilder
    {
        public const float TallWall = 5.2f;
        public const float LowWall = 0.55f;

        public class Result
        {
            public Transform Root;
            public CastleMap Map;
            public List<Chest> Chests = new List<Chest>();
            public List<SaveCrystal> Crystals = new List<SaveCrystal>();
            public List<EnemySymbol> Symbols = new List<EnemySymbol>();
            public EnemySymbol Boss;
            public Vector3 Start;
        }

        public static Result Build(Transform parent, PartyState state)
        {
            var map = new CastleMap();
            var res = new Result { Map = map };
            var root = new GameObject("Castle").transform;
            root.SetParent(parent, false);
            res.Root = root;

            BuildFloor(map, root);
            BuildWalls(map, root);
            BuildProps(map, root, res, state);
            BuildAmbience(map, root);
            res.Start = map.TileToWorld(map.Find('@'));
            return res;
        }

        static bool RenderWall(CastleMap map, int x, int row)
        {
            if (!map.IsWall(x, row)) return false;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (map.IsFloor(x + dx, row + dy)) return true;
            return false;
        }

        /// <summary>A wall that faces north into a room (i.e. the camera-side wall): always low.</summary>
        static bool IsCameraSideWall(CastleMap map, int x, int row) =>
            map.IsFloor(x, row - 1) && !map.IsFloor(x, row + 1);

        static void BuildFloor(CastleMap map, Transform root)
        {
            const int chunk = 8;
            for (int cy = 0; cy < map.Height; cy += chunk)
                for (int cx = 0; cx < map.Width; cx += chunk)
                {
                    var mb = new MeshBuilder();
                    for (int row = cy; row < Mathf.Min(cy + chunk, map.Height); row++)
                        for (int x = cx; x < Mathf.Min(cx + chunk, map.Width); x++)
                        {
                            if (!map.IsFloor(x, row)) continue;
                            Vector3 c = map.TileToWorld(x, row);
                            mb.Face(c + new Vector3(-0.5f, 0, -0.5f), Vector3.right, Vector3.forward, 0.5f);
                        }
                    if (mb.VertexCount > 0) mb.Create("Floor_" + cx + "_" + cy, root, EnvKit.FloorMat, false);
                }

            // Royal carpet: merge vertical runs of identical horizontal spans into long strips.
            var carpet = new MeshBuilder();
            int runStart = -1, runX0 = -1, runX1 = -1;
            for (int row = map.Height - 1; row >= -1; row--)
            {
                int x0 = -1, x1 = -1;
                if (row >= 0)
                    for (int x = 0; x < map.Width; x++)
                        if (IsCarpet(map, x, row)) { if (x0 < 0) x0 = x; x1 = x; }
                bool same = x0 == runX0 && x1 == runX1 && x0 >= 0;
                if (!same)
                {
                    if (runStart >= 0 && runX0 >= 0)
                    {
                        Vector3 a = map.TileToWorld(runX0, runStart) + new Vector3(-0.5f, 0.012f, -0.5f);
                        Vector3 b = map.TileToWorld(runX1, row + 1) + new Vector3(0.5f, 0.012f, 0.5f);
                        AddCarpetStrip(carpet, a, b);
                    }
                    runStart = row; runX0 = x0; runX1 = x1;
                }
            }
            if (carpet.VertexCount > 0) carpet.Create("Carpet", root, EnvKit.CarpetMat, false);
        }

        static bool IsCarpet(CastleMap map, int x, int row)
        {
            char c = map.At(x, row);
            if (c == '=') return true;
            return x >= 14 && x <= 16 && (c == 'B' || c == 'G' || c == 'g' || c == 'e' || c == '@' || c == 'T');
        }

        static void AddCarpetStrip(MeshBuilder mb, Vector3 a, Vector3 b)
        {
            // u spans the carpet width 0..1 (borders on both sides), v tiles once per unit of length.
            float w = b.x - a.x, len = b.z - a.z;
            mb.FaceUV(new Vector3(a.x, a.y, a.z), new Vector3(w, 0, 0), new Vector3(0, 0, len), new Vector2(1f, len));
        }

        static void BuildWalls(CastleMap map, Transform root)
        {
            var wallsRoot = new GameObject("Walls").transform;
            wallsRoot.SetParent(root, false);

            // camera-side (low) walls in one mesh
            var lowSides = new MeshBuilder();
            var lowTops = new MeshBuilder();

            for (int row = 0; row < map.Height; row++)
            {
                var tallSides = new MeshBuilder();
                var tallTops = new MeshBuilder();
                var lowRowSides = new MeshBuilder();
                var lowRowTops = new MeshBuilder();
                var decorTiles = new List<Vector2Int>();

                for (int x = 0; x < map.Width; x++)
                {
                    if (!RenderWall(map, x, row)) continue;
                    Vector3 c = map.TileToWorld(x, row);
                    Vector3 min = c + new Vector3(-0.5f, 0, -0.5f);
                    bool cameraSide = IsCameraSideWall(map, x, row);
                    var faces = MeshBuilder.Faces.Sides;
                    if (RenderWall(map, x - 1, row) && cameraSide == IsCameraSideWall(map, x - 1, row)) faces &= ~MeshBuilder.Faces.West;
                    if (RenderWall(map, x + 1, row) && cameraSide == IsCameraSideWall(map, x + 1, row)) faces &= ~MeshBuilder.Faces.East;

                    if (cameraSide)
                    {
                        lowSides.Box(min, min + new Vector3(1, LowWall, 1), faces);
                        lowTops.Box(min, min + new Vector3(1, LowWall, 1), MeshBuilder.Faces.Top);
                    }
                    else
                    {
                        tallSides.Box(min, min + new Vector3(1, TallWall, 1), faces);
                        tallTops.Box(min, min + new Vector3(1, TallWall, 1), MeshBuilder.Faces.Top);
                        lowRowSides.Box(min, min + new Vector3(1, LowWall, 1), faces);
                        lowRowTops.Box(min, min + new Vector3(1, LowWall, 1), MeshBuilder.Faces.Top);
                        char ch = map.At(x, row);
                        if (ch != '#') decorTiles.Add(new Vector2Int(x, row));
                    }
                }

                if (tallSides.VertexCount == 0) continue;
                var rowRoot = new GameObject("WallRow_" + row).transform;
                rowRoot.SetParent(wallsRoot, false);
                var tall = new GameObject("Tall");
                tall.transform.SetParent(rowRoot, false);
                tallSides.Create("Sides", tall.transform, EnvKit.WallMat);
                tallTops.Create("Tops", tall.transform, EnvKit.TrimMat);
                var low = new GameObject("Low");
                low.transform.SetParent(rowRoot, false);
                lowRowSides.Create("Sides", low.transform, EnvKit.WallMat);
                lowRowTops.Create("Tops", low.transform, EnvKit.TrimMat);
                low.SetActive(false);

                var lights = new List<Light>();
                foreach (var t in decorTiles)
                {
                    var l = BuildDecor(map, tall.transform, t.x, t.y);
                    if (l != null) lights.Add(l);
                }

                var cut = rowRoot.gameObject.AddComponent<Cutaway>();
                cut.Z = map.TileToWorld(0, row).z;
                cut.Margin = 0.9f;
                cut.LowScale = LowWall / TallWall;
                cut.Tall = tall;
                cut.Low = low;
                cut.Lights = lights.ToArray();
            }

            if (lowSides.VertexCount > 0)
            {
                lowSides.Create("CameraSideWalls", wallsRoot, EnvKit.WallMat);
                lowTops.Create("CameraSideWallTops", wallsRoot, EnvKit.TrimMat);
            }
        }

        static Vector3 FacingOf(CastleMap map, int x, int row)
        {
            if (map.IsFloor(x, row + 1)) return Vector3.back;   // faces south
            if (map.IsFloor(x + 1, row)) return Vector3.right;
            if (map.IsFloor(x - 1, row)) return Vector3.left;
            return Vector3.forward;
        }

        static Light BuildDecor(CastleMap map, Transform parent, int x, int row)
        {
            char ch = map.At(x, row);
            Vector3 facing = FacingOf(map, x, row);
            Vector3 face = map.TileToWorld(x, row) + facing * 0.5f;
            switch (ch)
            {
                case 't': return EnvKit.Torch(parent, face, facing);
                case 'W': return EnvKit.Window(parent, face, facing);
                case 'H': EnvKit.Banner(parent, face, facing); return null;
                case 'R': EnvKit.RoseWindow(parent, face, facing); return null;
            }
            return null;
        }

        static void BuildProps(CastleMap map, Transform root, Result res, PartyState state)
        {
            var props = new GameObject("Props").transform;
            props.SetParent(root, false);
            for (int row = 0; row < map.Height; row++)
                for (int x = 0; x < map.Width; x++)
                {
                    char ch = map.At(x, row);
                    Vector3 p = map.TileToWorld(x, row);
                    var tile = new Vector2Int(x, row);
                    switch (ch)
                    {
                        case 'P':
                        {
                            // The holder scales the whole pillar down when it hides the party (flutes hide the squash).
                            var holder = new GameObject("PillarCut");
                            holder.transform.SetParent(props, false);
                            var pillar = EnvKit.Pillar(holder.transform, p, 4.4f);
                            var cut = holder.AddComponent<Cutaway>();
                            cut.Z = p.z; cut.X = p.x; cut.XRange = 1.8f; cut.Margin = 0.2f; cut.LowScale = 0.16f;
                            cut.Tall = pillar;
                            break;
                        }
                        case 'T':
                            EnvKit.Throne(props, p + new Vector3(0, 0, 0.1f));
                            break;
                        case 'b':
                            EnvKit.Brazier(props, p, new Color(1f, 0.4f, 0.2f));
                            break;
                        case 'S':
                        {
                            var go = EnvKit.SaveCrystal(props, p);
                            var sc = go.AddComponent<SaveCrystal>();
                            sc.Tile = tile;
                            res.Crystals.Add(sc);
                            break;
                        }
                        case 'c':
                        {
                            var chest = Chest.Create(props, p, tile, CastleMap.ChestContents.TryGetValue(tile, out var items) ? items : new[] { "item:potion" });
                            if (state.Flags.Contains(chest.FlagKey)) chest.SetOpened();
                            res.Chests.Add(chest);
                            break;
                        }
                        case 'B':
                        {
                            var boss = EnemySymbol.Create(props, map, p + new Vector3(0, 0, 0.55f), "boss", "darklord", false, 1.45f, Color.white);
                            boss.IsBoss = true;
                            res.Boss = boss;
                            if (state.Flags.Contains("boss")) boss.Restore(true);
                            break;
                        }
                    }
                    if (CastleMap.EncounterSpots.TryGetValue(tile, out var enc))
                    {
                        var sym = EnemySymbol.Create(props, map, p, enc.Id, enc.Sprite, enc.Wanders, enc.Scale, enc.Tint);
                        res.Symbols.Add(sym);
                        if (state.Flags.Contains("enc_" + enc.Id)) sym.Restore(true);
                    }
                }
        }

        static void BuildAmbience(CastleMap map, Transform root)
        {
            // drifting dust motes over the whole castle
            var center = new Vector3(map.Width / 2f, 2.5f, map.Height / 2f);
            var ps = EnvKit.Ambient(root, center, new Color(0.75f, 0.7f, 1f) * 0.45f, 40f, 0.05f, 9f, new Vector3(0.05f, 0.04f, 0), 1f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(map.Width, 4f, map.Height);
            var main = ps.main;
            main.maxParticles = 500;
            main.prewarm = true;
        }
    }
}
