using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Features.Battle
{
    public enum FormationRow : byte { Front, Middle, Back }

    /// <summary>파티원의 진형 자리. Active면 열(Row)에 따라 보정이 붙고, 적이 하나도 없을 땐 AI가 Anchor로 돌아가 선다(적이 있으면 맵 어디든 찾아감).</summary>
    public struct FormationComponent : IComponentData
    {
        public bool Active;
        public FormationRow Row;
        public Vec2 Anchor;
    }

    /// <summary>
    /// 전투 전 배치: 3열(전열·중열·후열) × 3줄(위·가운데·아래) 칸. 칸 번호 = 열 × 3 + 줄, 전열이 적 쪽(오른쪽).
    /// 열 보정(임시값): 전열 받는 피해 -20%, 중열 공격 속도 +10%, 후열 공격력 +15%·받는 피해 +15%.
    /// </summary>
    public static class Formation
    {
        public const int Cols = 3, Lanes = 3, Cells = Cols * Lanes;
        public const float ColGap = 1.7f;       // 열 사이 가로 간격
        public const float StartInset = 4f;     // 후열이 방 왼쪽 끝에서 이만큼 안쪽

        public static FormationRow RowOf(int cell) => (FormationRow)System.Math.Clamp(cell / Lanes, 0, Cols - 1);
        public static int LaneOf(int cell) => cell % Lanes;
        public static int Cell(FormationRow row, int lane) => (int)row * Lanes + lane;

        /// <summary>중열 가운데 칸 위치(방 왼쪽 시작 구역).</summary>
        public static Vec2 MiddleOf(IWalkArea area)
            => area is RectWalkArea r ? new Vec2(r.MinX + StartInset + ColGap, (r.MinDepth + r.MaxDepth) * 0.5f) : Vec2.Zero;

        public static Vec2 CellPos(int cell, Vec2 middle, IWalkArea area)
        {
            float spread = area is RectWalkArea r ? System.MathF.Min(0.9f, (r.MaxDepth - r.MinDepth) * 0.5f - 0.35f) : 0.8f;
            var p = new Vec2(middle.X + (1 - cell / Lanes) * ColGap, middle.Y + (1 - LaneOf(cell)) * spread);
            return area != null ? area.Clamp(p) : p;
        }

        public static float DamageTakenMul(FormationRow r) => r switch { FormationRow.Front => 0.8f, FormationRow.Back => 1.15f, _ => 1f };
        public static float DamageMul(FormationRow r) => r == FormationRow.Back ? 1.15f : 1f;
        public static float AttackSpeedMul(FormationRow r) => r == FormationRow.Middle ? 1.1f : 1f;

        public static string RowName(FormationRow r) => r switch { FormationRow.Front => "전열", FormationRow.Middle => "중열", _ => "후열" };
        public static string RowEffect(FormationRow r) => r switch
        {
            FormationRow.Front => "받는 피해 -20%",
            FormationRow.Middle => "공격 속도 +10%",
            _ => "공격력 +15% · 받는 피해 +15%"
        };

        /// <summary>기본 자리 순서: 근접은 전열부터, 원거리는 후열부터(각 열은 가운데·위·아래 줄 순).</summary>
        public static int[] Preference(bool ranged)
        {
            var rows = ranged
                ? new[] { FormationRow.Back, FormationRow.Middle, FormationRow.Front }
                : new[] { FormationRow.Front, FormationRow.Middle, FormationRow.Back };
            var order = new int[Cells];
            int k = 0;
            foreach (var row in rows)
                foreach (var lane in new[] { 1, 0, 2 })
                    order[k++] = Cell(row, lane);
            return order;
        }
    }
}
