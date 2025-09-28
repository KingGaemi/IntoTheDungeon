using IntoTheDungeon.Core.Abstractions.Services;

namespace IntoTheDungeon.Core.Runtime.Services
{
    public sealed class UnityClock : IClock
    {
        public float Time => UnityEngine.Time.time;
        public float DeltaTime => UnityEngine.Time.deltaTime;
        public float UnscaledTime => UnityEngine.Time.unscaledTime;
        public float UnscaledDeltaTime => UnityEngine.Time.unscaledDeltaTime;
        public float FixedDeltaTime => UnityEngine.Time.fixedDeltaTime;
    }
}