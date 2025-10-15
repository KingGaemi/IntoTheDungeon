using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Core.Abstractions.World;
using UnityEngine;

[DefaultExecutionOrder(-10001)]
public sealed class UnityInputDriver : MonoBehaviour, IWorldInjectable
{
    private IInputService _svc;
    private Vec2 _lastAxis;
    private bool _lastHeld;

    public void Init(IWorld w)
    {
        if (w.TryGet(out _svc))
        {
            _lastAxis = default;
            _lastHeld = false;
        }
    }

    void Update()
    {
        if (_svc == null) return;

        // Movement Input
        int x = 0;
        int y = 0;

        if (Input.GetKey(KeyCode.LeftArrow))
            x = -1;
        else if (Input.GetKey(KeyCode.RightArrow))
            x = 1;

        if (Input.GetKey(KeyCode.DownArrow))
            y = -1;
        else if (Input.GetKey(KeyCode.UpArrow))
            y = 1;

        var axis = new Vec2(x, y);

        // Attack Input
        bool held = Input.GetKey(KeyCode.Space);
        bool down = Input.GetKeyDown(KeyCode.Space);
        bool up = Input.GetKeyUp(KeyCode.Space);

        // Skill Triggers
        bool qDown = Input.GetKeyDown(KeyCode.Q);
        bool wDown = Input.GetKeyDown(KeyCode.W);
        bool eDown = Input.GetKeyDown(KeyCode.E);
        bool rDown = Input.GetKeyDown(KeyCode.R);

        // State Changes
        bool axisChanged = !axis.Equals(_lastAxis);
        bool heldChanged = held != _lastHeld;

        // 변경사항이 있거나 트리거 입력이 있으면 브로드캐스트
        if (axisChanged || heldChanged || down || up || qDown || wDown || eDown || rDown)
        {
            _svc.SetSnapshot(axis, held, down, up, qDown, wDown, eDown, rDown);
            _svc.SetChanged(axisChanged, heldChanged);
            _lastAxis = axis;
            _lastHeld = held;
        }
    }
}