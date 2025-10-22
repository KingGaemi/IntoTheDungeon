
using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Runtime.World;
using IntoTheDungeon.Runtime.Abstractions;
using UnityEngine;

namespace IntoTheDungeon.Unity
{
    sealed class UnitySceneGraph : ISceneGraph
    {
        public IEnumerable<IWorldInjectable> EnumerateInjectables()
        {

            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb is IWorldInjectable inj) yield return inj;
        }

    }

    // Unity 구현체들
    sealed class UnityClock : IClock
    {
        public float TimeSinceStartup => UnityEngine.Time.realtimeSinceStartup;
        public float DeltaTime => UnityEngine.Time.deltaTime;
        public float FixedDeltaTime => UnityEngine.Time.fixedDeltaTime;
        public float Time => UnityEngine.Time.time;
        public float UnscaledTime => UnityEngine.Time.unscaledTime;
        public float UnscaledDeltaTime => UnityEngine.Time.unscaledDeltaTime;
    }

    sealed class UnityLogger : Core.Abstractions.Messages.ILogger
    {
        public void Log(string msg) => Debug.Log(msg);
        public void Warn(string msg) => Debug.LogWarning(msg);
        public void Error(string msg) => Debug.LogError(msg);
    }

    // 설치자(MonoBehaviour) → IGameInstaller 브릿지
    public abstract class MonoGameInstaller : MonoBehaviour, IGameInstaller
    {
        public abstract void Install(GameWorld world);
    }
}
