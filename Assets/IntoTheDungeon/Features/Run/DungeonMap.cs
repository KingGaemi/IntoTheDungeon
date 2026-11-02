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
    /// 첫 층은 전부 일반 전투, 정예는 3층부터, 휴식은 4층부터, 상점은 3층부터(지도마다 최소 하나), 보스 바로 앞 층은 전부 휴식.
    /// </summary>
    public sealed class DungeonMap
    {
        public readonly List<List<MapNode>> Layers = new();
        public int LayerCount => Layers.Count;
        public MapNode Boss => Layers[^1][0];

        public static DungeonMap Generate(int seed, int floors = 8)
        {
            var rng = new System.Random(seed);
            var map = new DungeonMap();

            for (int layer = 0; layer < floors; layer++)
            {
                bool isBoss = layer == floors - 1;
                int width = isBoss ? 1 : rng.Next(2, 5);
                var row = new List<MapNode>(width);
                for (int i = 0; i < width; i++)
                    row.Add(new MapNode { Layer = layer, Index = i, Type = PickType(rng, layer, floors) });
                map.Layers.Add(row);
            }

            EnsureShop(map, rng, floors);

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

        static RoomType PickType(System.Random rng, int layer, int floors)
        {
            if (layer == floors - 1) return RoomType.Boss;
            if (layer == floors - 2) return RoomType.Rest;   // 보스 전 정비
            if (layer == 0) return RoomType.Battle;
            double r = rng.NextDouble();
            if (layer >= 2 && r < 0.2) return RoomType.Elite;
            if (layer >= 3 && r < 0.38) return RoomType.Rest;
            if (layer >= 2 && r < 0.48) return RoomType.Shop;
            return RoomType.Battle;
        }

        // 상점이 하나도 안 나왔으면 가운데 층의 전투 방 하나를 상점으로
        static void EnsureShop(DungeonMap map, System.Random rng, int floors)
        {
            foreach (var row in map.Layers)
                foreach (var n in row)
                    if (n.Type == RoomType.Shop) return;
            int mid = System.Math.Clamp(floors / 2, 2, floors - 3);
            for (int l = mid; l < floors - 2; l++)
            {
                var battles = map.Layers[l].FindAll(n => n.Type == RoomType.Battle);
                if (battles.Count == 0) continue;
                battles[rng.Next(battles.Count)].Type = RoomType.Shop;
                return;
            }
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
