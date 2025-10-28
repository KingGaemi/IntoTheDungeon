using UnityEngine;
namespace IntoTheDungeon.Unity.Bridge.Core.Abstractions

{
    public interface ISceneViewRegistry
    {
        void Register(int sceneLinkId, GameObject go);
        bool TryTake(int sceneLinkId, out GameObject go); // 꺼내면 삭제
    }
}
