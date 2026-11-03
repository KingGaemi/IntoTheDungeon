using System.Collections.Generic;

namespace IntoTheDungeon.Features.Run
{
    public enum RoomType { Battle, Elite, Rest, Boss, Shop }

    public sealed class MapNode
    {
        public int Layer;
        public int Index;                       // 층 안에서 왼쪽부터 순서
        public RoomType Type;
        public readonly List<int> Next = new(); // 다음 층에서 갈 수 있는 방들의 Index
        public bool Visited;
    }

    /// <summary>
    /// 갈림길 지도: 아래층(0)에서 위로 올라가며 층마다 이어진 방 하나를 고른다. 마지막 층은 보스 하나.
    /// 생성 규칙(임시값): 층마다 2~4칸, 각 방은 다음 층의 가까운 방 1~2개로 이어지고, 모든 방은 들어오는 길이 있다.
    /// 방 종류는 완전 무작위가 아니라 굵직하게 정해 둔다: 첫 두 층은 일반 전투, 보스 바로 앞 층은 전부 휴식,
    /// 그 사이(3층~보스 2층 전)에서 어느 길로 가든 정예 딱 1번·상점 1~2번(연달아 나오지 않게, 2번인 길이 많음)을 만나고 나머지는 전투.
    /// </summary>
    public sealed class DungeonMap
    {
        public readonly List<List<MapNode>> Layers = new();
        public int LayerCount => Layers.Count;
        public MapNode Boss => Layers[^1][0];

        // 길 하나마다 만나는 횟수(최소~최대). 최소를 먼저 맞추고, 최대까지는 확률로 더 놓아 길마다 차이가 나게 한다
        const int EliteMin = 1, EliteMax = 1, ShopMin = 1, ShopMax = 2;
        const double ExtraShopChance = 0.6;
        const int FirstSpecialLayer = 2;   // 정예·상점이 나올 수 있는 첫 층(0부터): 처음 두 층은 전투로 몸풀기

        public static DungeonMap Generate(int seed, int floors = 8)
        {
            var rng = new System.Random(seed);
            DungeonMap map = null;
            // 모양에 따라 규칙을 못 맞출 수 있어 새 모양으로 몇 번 다시 만든다(대부분 첫 번째에 맞음)
            for (int attempt = 0; attempt < 40; attempt++)
            {
                map = Build(rng, floors);
                if (AssignRooms(map, rng, floors)) return map;
            }
            return map;   // 끝까지 못 맞추면 마지막 것(최대치는 지켜져 있음)
        }

        static DungeonMap Build(System.Random rng, int floors)
        {
            var map = new DungeonMap();
            for (int layer = 0; layer < floors; layer++)
            {
                bool isBoss = layer == floors - 1;
                int width = isBoss ? 1 : rng.Next(2, 5);
                var row = new List<MapNode>(width);
                var type = isBoss ? RoomType.Boss : layer == floors - 2 ? RoomType.Rest : RoomType.Battle;   // 보스 전 정비
                for (int i = 0; i < width; i++)
                    row.Add(new MapNode { Layer = layer, Index = i, Type = type });
                map.Layers.Add(row);
            }

            // 길 잇기: 각 방 → 다음 층에서 비슷한 위치(비율)의 방 1~2개
            for (int layer = 0; layer < floors - 1; layer++)
            {
                var cur = map.Layers[layer];
                var next = map.Layers[layer + 1];
                foreach (var node in cur)
                {
                    int target = Nearest(node.Index, cur.Count, next.Count);
                    AddEdge(node, target);
                    if (next.Count > 1 && rng.NextDouble() < 0.5)
                    {
                        int side = target + (rng.NextDouble() < 0.5 ? -1 : 1);
                        if (side >= 0 && side < next.Count) AddEdge(node, side);
                    }
                }
                // 들어오는 길이 없는 방은 가장 가까운 아래 방에서 잇는다
                for (int j = 0; j < next.Count; j++)
                {
                    bool reached = false;
                    foreach (var node in cur) if (node.Next.Contains(j)) { reached = true; break; }
                    if (!reached) AddEdge(cur[Nearest(j, next.Count, cur.Count)], j);
                }
            }
            return map;
        }

        // ───────── 방 종류 배정(길마다 정예·상점 횟수 맞추기) ─────────

        static bool AssignRooms(DungeonMap map, System.Random rng, int floors)
        {
            var paths = new List<MapNode[]>();
            var walk = new MapNode[floors];
            foreach (var start in map.Layers[0]) Walk(map, start, walk, paths);

            int last = floors - 3;   // 정예·상점이 나올 수 있는 마지막 층(보스 전 휴식 바로 아래)
            if (!Place(map, paths, RoomType.Elite, EliteMin, EliteMax, rng, last)) return false;
            if (!Place(map, paths, RoomType.Shop, ShopMin, ShopMax, rng, last)) return false;
            Extra(map, paths, RoomType.Shop, ShopMax, ExtraShopChance, rng, last);
            return true;
        }

        static void Walk(DungeonMap map, MapNode node, MapNode[] walk, List<MapNode[]> paths)
        {
            walk[node.Layer] = node;
            if (node.Layer == map.LayerCount - 1) { paths.Add((MapNode[])walk.Clone()); return; }
            foreach (int j in node.Next) Walk(map, map.Layers[node.Layer + 1][j], walk, paths);
        }

        // 모든 길이 그 종류를 최소 min번 만나게 전투 방을 바꾼다.
        // 아직 모자란 길을 가장 많이 지나는 전투 방부터(같으면 무작위) 바꾸되, 어떤 길도 max를 넘지 않게,
        // 상점은 한 길에서 연달아 붙지 않게 한다.
        static bool Place(DungeonMap map, List<MapNode[]> paths, RoomType type, int min, int max, System.Random rng, int last)
        {
            var through = new List<MapNode[]>();
            while (true)
            {
                bool lacking = false;
                foreach (var p in paths) if (Count(p, type) < min) { lacking = true; break; }
                if (!lacking) return true;

                MapNode best = null;
                int bestScore = 0, ties = 0;
                for (int l = FirstSpecialLayer; l <= last; l++)
                {
                    foreach (var n in map.Layers[l])
                    {
                        if (n.Type != RoomType.Battle) continue;
                        through.Clear();
                        foreach (var p in paths) if (p[l] == n) through.Add(p);
                        if (!CanPlace(through, type, max, l, last)) continue;
                        int score = 0;
                        foreach (var p in through) if (Count(p, type) < min) score++;
                        if (score == 0) continue;
                        if (score > bestScore) { best = n; bestScore = score; ties = 1; }
                        else if (score == bestScore && rng.Next(++ties) == 0) best = n;
                    }
                }
                if (best == null) return false;
                best.Type = type;
            }
        }

        // 최소를 맞춘 뒤, 남은 전투 방을 무작위 순서로 돌며 chance 확률로 더 바꾼다(최대·규칙은 그대로)
        static void Extra(DungeonMap map, List<MapNode[]> paths, RoomType type, int max, double chance, System.Random rng, int last)
        {
            var nodes = new List<MapNode>();
            for (int l = FirstSpecialLayer; l <= last; l++)
                foreach (var n in map.Layers[l]) if (n.Type == RoomType.Battle) nodes.Add(n);
            for (int i = nodes.Count - 1; i > 0; i--) { int k = rng.Next(i + 1); (nodes[i], nodes[k]) = (nodes[k], nodes[i]); }
            var through = new List<MapNode[]>();
            foreach (var n in nodes)
            {
                if (rng.NextDouble() >= chance) continue;
                through.Clear();
                foreach (var p in paths) if (p[n.Layer] == n) through.Add(p);
                if (through.Count > 0 && CanPlace(through, type, max, n.Layer, last)) n.Type = type;
            }
        }

        // 이 층의 방을 type으로 바꿔도 지나는 모든 길이 규칙을 지키는가
        static bool CanPlace(List<MapNode[]> through, RoomType type, int max, int l, int last)
        {
            foreach (var p in through)
            {
                if (Count(p, type) >= max) return false;
                if (BattlesLeft(p, last) <= 1) return false;   // 길마다 중간 전투 한 번은 남긴다
                if (type == RoomType.Shop && (p[l - 1].Type == RoomType.Shop || p[l + 1].Type == RoomType.Shop)) return false;
            }
            return true;
        }

        static int Count(MapNode[] path, RoomType type)
        {
            int c = 0;
            foreach (var n in path) if (n.Type == type) c++;
            return c;
        }

        static int BattlesLeft(MapNode[] path, int last)
        {
            int c = 0;
            for (int l = FirstSpecialLayer; l <= last; l++) if (path[l].Type == RoomType.Battle) c++;
            return c;
        }

        static int Nearest(int index, int fromCount, int toCount)
        {
            if (toCount <= 1) return 0;
            float t = fromCount <= 1 ? 0.5f : (float)index / (fromCount - 1);
            return (int)System.MathF.Round(t * (toCount - 1));
        }

        static void AddEdge(MapNode node, int next)
        {
            if (!node.Next.Contains(next)) node.Next.Add(next);
        }
    }
}
