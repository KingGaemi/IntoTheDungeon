using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Core.Abstractions.World;
using UnityEngine;
using IntoTheDungeon.Core.Runtime.Services;

[DefaultExecutionOrder(-10001)]
public sealed class UnityInputDriver : MonoBehaviour, IWorldInjectable
{
    IWorld _world; IInputService _svc;
    public void Init(IWorld w) { _world = w; _world.TryGet(out _svc); }

    void Update()
    {
        if (_svc == null) return;
        var axis = new Vec2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        bool atk = Input.GetKey(KeyCode.Space);

        ((UnityInputService)_svc).Set(axis, atk);
    }
}