using System.Collections.Generic;
using UnityEngine;

namespace HD2DRPG
{
    /// <summary>
    /// The dark castle layout. North is up (+Z). Legend:
    ///  #  wall            .  floor          =  royal carpet     P  pillar
    ///  t  wall torch      W  stained window H  banner           R  rose window
    ///  T  throne          b  brazier        c  treasure chest   S  save crystal
    ///  e/g/G  enemy       B  dark lord      @  party start
    /// </summary>
    public class CastleMap
    {
        public static readonly string[] Rows =
        {
            "###############################",
            "#####W##t##H##WRW##H##t##W#####",
            "#####c...P...b=T=b...P...c#####",
            "#####.........===.........#####",
            "####t....P....=B=....P....t####",
            "#####.........===.........#####",
            "#####....P....===....P....#####",
            "####t.........===.........t####",
            "#####....P....===....P....#####",
            "#####.........===.........#####",
            "#####....P....===....P....#####",
            "#####.........===.........#####",
            "###########...=G=...###########",
            "##########t.S.===...t##########",
            "###########.P.===.P.###########",
            "##########W...===...W##########",
            "###########...===..c###########",
            "###########.P.===.P.###########",
            "##########t...=g=...t##########",
            "###########...===...###########",
            "###########.P.===.P.###########",
            "##########W...===...W##########",
            "###########c..===...###########",
            "###########.P.===.P.###########",
            "##########t...=e=...t##########",
            "###########...===...###########",
            "###########.P.===.P.###########",
            "##########W...===..c###########",
            "###########...===...###########",
            "#####W##t##H#.===.#H##t##W#####",
            "#####c........===.........#####",
            "#####..P.....P===.P......P#####",
            "#####.........===.........#####",
            "####t....e....===.........t####",
            "#####.........===.....e...#####",
            "#####c........===.........#####",
            "#####..P.....P===.P......P#####",
            "####t.........===........ct####",
            "#####...S.....===.........#####",
            "#####.........=@=.........#####",
            "###############################",
        };

        public class Chest
        {
            public Vector2Int Tile;
            public string[] Items;   // "item:id", "equip:id", "gold:n"
        }

        public class Encounter
        {
            public Vector2Int Tile;
            public string Id;        // Database.Encounters key
            public string Sprite;
            public bool Wanders = true;
            public float Scale = 1f;
            public Color Tint = Color.white;
        }

        /// <summary>Chest contents keyed by tile (column,row).</summary>
        public static readonly Dictionary<Vector2Int, string[]> ChestContents = new Dictionary<Vector2Int, string[]>
        {
            { new Vector2Int(5, 2), new[] { "equip:aegis_plate", "item:ambrosia" } },
            { new Vector2Int(25, 2), new[] { "equip:dawn_ward", "item:elixir" } },
            { new Vector2Int(19, 16), new[] { "equip:caduceus", "equip:circlet_dawn" } },
            { new Vector2Int(11, 22), new[] { "equip:titans_cleaver" } },
            { new Vector2Int(19, 27), new[] { "equip:starfire_saber", "equip:selene_amulet" } },
            { new Vector2Int(5, 30), new[] { "equip:ring_vigor", "gold:300" } },
            { new Vector2Int(5, 35), new[] { "item:hi_potion", "item:hi_potion", "item:storm_shard" } },
            { new Vector2Int(25, 37), new[] { "equip:peplos_hera", "item:phoenix_feather" } },
        };

        public static readonly Dictionary<Vector2Int, Encounter> EncounterSpots = new Dictionary<Vector2Int, Encounter>
        {
            { new Vector2Int(9, 33), new Encounter { Id = "hall_a", Sprite = "skeleton" } },
            { new Vector2Int(22, 34), new Encounter { Id = "hall_b", Sprite = "wraith" } },
            { new Vector2Int(15, 18), new Encounter { Id = "gallery_a", Sprite = "gargoyle" } },
            { new Vector2Int(15, 24), new Encounter { Id = "gallery_b", Sprite = "wraith" } },
            { new Vector2Int(15, 12), new Encounter { Id = "guard", Sprite = "skeleton", Wanders = false, Scale = 1.3f, Tint = new Color(1f, 0.55f, 0.55f) } },
        };

        public int Width => Rows[0].Length;
        public int Height => Rows.Length;

        public char At(int x, int row)
        {
            if (row < 0 || row >= Height || x < 0 || x >= Width) return '#';
            return Rows[row][x];
        }

        public static bool IsWallChar(char c) => c == '#' || c == 'W' || c == 'H' || c == 't' || c == 'R';
        public static bool IsFloorChar(char c) => !IsWallChar(c);
        public static bool IsWalkableChar(char c) => c == '.' || c == '=' || c == 'e' || c == 'g' || c == 'G' || c == 'B' || c == '@';

        public bool IsFloor(int x, int row) => IsFloorChar(At(x, row));
        public bool IsWall(int x, int row) => IsWallChar(At(x, row));
        public bool IsWalkable(int x, int row) => IsWalkableChar(At(x, row));

        public Vector3 TileToWorld(int x, int row) => new Vector3(x, 0, Height - 1 - row);
        public Vector3 TileToWorld(Vector2Int t) => TileToWorld(t.x, t.y);

        public Vector2Int WorldToTile(Vector3 p) =>
            new Vector2Int(Mathf.RoundToInt(p.x), Height - 1 - Mathf.RoundToInt(p.z));

        /// <summary>True if a circle of radius r at world position p overlaps no solid tile.</summary>
        public bool CanStand(Vector3 p, float r)
        {
            int x0 = Mathf.FloorToInt(p.x - r + 0.5f), x1 = Mathf.FloorToInt(p.x + r + 0.5f);
            int z0 = Mathf.FloorToInt(p.z - r + 0.5f), z1 = Mathf.FloorToInt(p.z + r + 0.5f);
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    int row = Height - 1 - z;
                    if (IsWalkable(x, row)) continue;
                    // circle vs tile AABB
                    float cx = Mathf.Clamp(p.x, x - 0.5f, x + 0.5f);
                    float cz = Mathf.Clamp(p.z, z - 0.5f, z + 0.5f);
                    float dx = p.x - cx, dz = p.z - cz;
                    float rr = At(x, row) == 'P' ? r * 0.7f : r; // pillars are round-ish
                    if (dx * dx + dz * dz < rr * rr) return false;
                }
            return true;
        }

        public Vector2Int Find(char c)
        {
            for (int row = 0; row < Height; row++)
            {
                int x = Rows[row].IndexOf(c);
                if (x >= 0) return new Vector2Int(x, row);
            }
            return Vector2Int.zero;
        }

        public List<Vector2Int> FindAll(char c)
        {
            var list = new List<Vector2Int>();
            for (int row = 0; row < Height; row++)
                for (int x = 0; x < Width; x++)
                    if (Rows[row][x] == c) list.Add(new Vector2Int(x, row));
            return list;
        }

        /// <summary>Name of the area at a world position (for the menu's location label).</summary>
        public string AreaName(Vector3 p)
        {
            var t = WorldToTile(p);
            if (t.y <= 11) return "Throne of Eternal Night";
            if (t.y <= 28) return "Moonlit Gallery";
            return "Hall of Ashes";
        }
    }
}
