using UnityEngine;

namespace IntoTheDungeon.Features.Settings
{
    /// <summary>
    /// 플레이어가 바꾸는 게임 설정(PlayerPrefs에 저장, 다음 실행에도 유지).
    /// 연출 수치는 컴포넌트마다 따로 두지 않고 여기 배율로 한 번에 조절한다.
    /// </summary>
    public static class GameSettings
    {
        const string ShakeKey = "settings.shakeScale";
        const string OverlayKey = "settings.debugOverlay";

        public const float MaxShakeScale = 1.5f;

        /// <summary>피격 흔들림 세기 배율(0 = 끔, 1 = 기본, 최대 1.5).</summary>
        public static float ShakeScale
        {
            get => PlayerPrefs.GetFloat(ShakeKey, 1f);
            set { PlayerPrefs.SetFloat(ShakeKey, Mathf.Clamp(value, 0f, MaxShakeScale)); PlayerPrefs.Save(); }
        }

        /// <summary>캐릭터 머리 위 디버그 글자(HP·상태·클립) 표시.</summary>
        public static bool DebugOverlay
        {
            get => PlayerPrefs.GetInt(OverlayKey, 0) != 0;   // 기본은 끔(머리 위엔 체력바만)
            set { PlayerPrefs.SetInt(OverlayKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }
}
