using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Features.Attack;
using IntoTheDungeon.Features.Battle;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 저지력 표시 공용: 망치 아이콘(Resources/StopIcon.png, 코드로 그린 도트)·색(청록)·글자 강조.
    /// 저지력이 나오는 곳(필살기 버튼·예고 표시·아이템 설명·정비 카드·배치 화면·안내)은 모두 이 색과 아이콘을 써서
    /// "청록 망치 = 저지력"으로 자연스럽게 익히게 한다.
    /// </summary>
    public static class StopPowerUi
    {
        public static readonly Color Color = new(0.35f, 0.92f, 0.88f, 1f);
        public static readonly Color ShortColor = new(1f, 0.42f, 0.38f, 1f);   // 저지력 부족(부정 신호라 빨강)
        public const string Hex = "#59EBE0";
        public const string ShortHex = "#FF6B61";

        static Sprite _icon;
        public static Sprite Icon
        {
            get
            {
                if (_icon) return _icon;
                var t = Resources.Load<Texture2D>("StopIcon");
                if (t) _icon = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 16f);
                return _icon;
            }
        }

        static readonly Regex s_word = new("(필살기 )?저지력( [+＋]?\\d+)?");

        /// <summary>설명 글 속 "저지력 +5" 같은 부분을 청록 굵은 글자로(리치 텍스트).</summary>
        public static string Highlight(string text)
            => string.IsNullOrEmpty(text) ? text : s_word.Replace(text, m => $"<color={Hex}><b>{m.Value}</b></color>");

        /// <summary>살아 있는 파티원 필살기 중 가장 높은 저지력(없으면 0).</summary>
        public static int BestPartyStop(IEntityManager em, List<Party.Member> scratch)
        {
            Party.Collect(em, scratch);
            int best = 0;
            foreach (var m in scratch)
                if (m.Alive && em.TryGetComponent<SpecialSkillComponent>(m.Entity, out var sk) && sk.StopPower > best) best = sk.StopPower;
            return best;
        }
    }
}
