// Core.Runtime/Unity/Components
using UnityEngine;
using IntoTheDungeon.Core.Abstractions.Physics;
using IntoTheDungeon.Core.ECS;
using IntoTheDungeon.Core.Runtime.Physics;
using IntoTheDungeon.Core.Runtime.World;

[DisallowMultipleComponent]
public sealed class KinematicBehaviour : MonoBehaviour, IWorldInjectable
{
    private IRegistry<KinematicAgent> _reg;
    private IPhysicsBody _body;

    public void Init(GameWorld world)
    {
        _reg = world.Get<IRegistry<KinematicAgent>>();
        var rb = GetComponentInParent<Rigidbody2D>();
        if (!rb) { enabled = false; return; }
        _body = new UnityPhysicsBody(rb);
    }

    void OnEnable()  => _reg?.Register(new KinematicAgent(this, _body));
    void OnDisable() => _reg?.Unregister(new KinematicAgent(this, _body));
}


public readonly struct KinematicAgent : IComponent
{
    public readonly KinematicBehaviour View; // Unity 쪽
    public readonly IPhysicsBody Body;       // 물리 어댑터
    public KinematicAgent(KinematicBehaviour v, IPhysicsBody b) { View = v; Body = b; }

    // 값 비교를 위해 Equals/GetHashCode 구현 권장 (뷰 참조 기준)
    public override int GetHashCode() => View ? View.GetHashCode() : 0;
    public override bool Equals(object o) => o is KinematicAgent k && k.View == View;
}
