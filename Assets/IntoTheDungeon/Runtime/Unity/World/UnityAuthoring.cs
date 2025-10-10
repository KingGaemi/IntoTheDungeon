// Runtime.Unity.Composition
using UnityEngine;
using IntoTheDungeon.Core.World.Abstractions;

namespace IntoTheDungeon.Runtime.Unity.World
{
    public sealed class UnityAuthoring : IAuthoring
    {
        public MonoBehaviour Behaviour { get; }
        public UnityAuthoring(MonoBehaviour mb) => Behaviour = mb;

        public IBaker CreateBaker()
        {
            if (Behaviour is IAuthoring origin)
                return origin.CreateBaker();
            return null;
        }
    }
}