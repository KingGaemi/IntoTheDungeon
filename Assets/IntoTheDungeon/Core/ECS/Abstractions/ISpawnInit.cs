using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.World;

namespace IntoTheDungeon.Core.ECS.Abstractions.Spawn
{
    public interface ISpawnInit { void Apply(IWorld world, Entity e); }

    public interface ISpawnInitProvider
    {
        // Unity MonoBehaviour가 구현. 에디터 값 → 여러 Init으로 변환
        IEnumerable<ISpawnInit> BuildInits();
    }
}