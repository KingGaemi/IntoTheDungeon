using System.Collections.Generic;

namespace IntoTheDungeon.Core.Abstractions.World
{
    public interface ISceneGraph
    {
        // 현재 활성 씬의 모든 IWorldInjectable을 열거
        IEnumerable<IWorldInjectable> EnumerateInjectables();
    }
}