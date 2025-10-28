using System.Collections.Generic;
using IntoTheDungeon.Unity.Bridge.Core.Abstractions;
using UnityEngine;

namespace IntoTheDungeon.Unity.Bridge.Core
{
    public class SceneViewRegistry : ISceneViewRegistry
    {
        private readonly Dictionary<int, GameObject> _id2goMap = new(512);

        public void Register(int sceneLinkId, GameObject go)
        {
            if (go == null) return;
            _id2goMap[sceneLinkId] = go; // 덮어쓰기 허용
        }

        // 조회 후 맵에서 제거 → 1회성 회수
        public bool TryTake(int sceneLinkId, out GameObject go)
        {
            if (_id2goMap.TryGetValue(sceneLinkId, out go))
            {
                _id2goMap.Remove(sceneLinkId);
                return true;
            }
            return false;
        }

        public void Clear() => _id2goMap.Clear();
    }
}