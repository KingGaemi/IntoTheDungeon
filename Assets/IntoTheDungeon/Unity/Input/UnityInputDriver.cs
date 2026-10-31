using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.World;
using UnityEngine;

/// <summary>
/// 키보드 → IInputService. 숫자키 1~4 = 파티원 필살기, R = 오크 소환, T = 동료 소환(둘 다 테스트용).
/// </summary>
[DefaultExecutionOrder(-10001)]
public sealed class UnityInputDriver : MonoBehaviour, IWorldInjectable
{
    static readonly KeyCode[] SkillKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4 };

    private IInputService _svc;

    public void Init(IWorld w) => w.TryGet(out _svc);

    void Update()
    {
        if (_svc == null) return;

        int slot = -1;
        for (int i = 0; i < SkillKeys.Length; i++)
            if (Input.GetKeyDown(SkillKeys[i])) { slot = i; break; }

        bool rDown = Input.GetKeyDown(KeyCode.R);
        bool allyDown = Input.GetKeyDown(KeyCode.T);

        if (slot >= 0 || rDown || allyDown)
            _svc.SetKeys(slot, rDown, allyDown);
    }
}
