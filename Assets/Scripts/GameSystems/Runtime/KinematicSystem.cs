using System.Collections.Generic;

using IntoTheDungeon.Core.ECS;
using IntoTheDungeon.Core.Abstractions.Physics;
using IntoTheDungeon.Core.Abstractions.Messages;

public class KinematicSystem : GameSystemBase
{
    public static KinematicSystem Instance { get; private set; }
    private readonly List<(KinematicComponent kc, IPhysicsBody body)> _agents = new();
    private readonly ILogger _log;
    private IRegistry<KinematicComponent> _reg;

    public KinematicSystem(ILogger log) => _log = log;

    public void Register(KinematicComponent kc, IPhysicsBody body)
    {
        if (kc == null || body == null) return;
        _agents.Add((kc, body));
        _log.Log($"Registered {kc}");
    }


    public void Tick(float dt)
    {
        for (int i = 0; i < _agents.Count; i++)
        {
            var (kc, body) = _agents[i];
            body.SetLinearVelocity(kc.Velocity.X, body.GetLinearVelocity().y);
        }
    }
}
