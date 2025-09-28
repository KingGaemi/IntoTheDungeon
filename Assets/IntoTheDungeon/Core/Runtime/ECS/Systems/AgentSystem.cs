using System.Collections.Generic;
using UnityEngine;


namespace IntoTheDungeon.Core.Runtime.ECS.Systems
{
    public abstract class AgentSystem<TComp, TDep> : MonoBehaviour
        where TComp : Component
        where TDep : class
    {
        // Packed arrays
        protected readonly List<TComp> comps = new();
        protected readonly List<TDep> deps = new();
        protected readonly Dictionary<TComp, int> indexOf = new();

        // 컴포넌트가 자동 호출
        public void Register(TComp c)
        {
            if (!c || indexOf.ContainsKey(c)) return;
            var dep = FetchDependency(c);
            if (dep == null) { OnDependencyMissing(c); return; }

            indexOf[c] = comps.Count;
            comps.Add(c);
            deps.Add(dep);
            OnRegistered(c, dep);
        }

        public void Unregister(TComp c)
        {
            if (!c || !indexOf.TryGetValue(c, out int i)) return;
            int last = comps.Count - 1;

            // swap-remove
            (comps[i], comps[last]) = (comps[last], comps[i]);
            (deps[i], deps[last]) = (deps[last], deps[i]);

            indexOf[comps[i]] = i;
            comps.RemoveAt(last);
            deps.RemoveAt(last);
            indexOf.Remove(c);

            OnUnregistered(c);
        }

        protected virtual void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            for (int i = 0; i < comps.Count; i++)
                OnFixedStep(comps[i], deps[i], dt);
        }

        // 훅
        protected abstract TDep FetchDependency(TComp c);
        protected virtual void OnRegistered(TComp c, TDep d) { }
        protected virtual void OnUnregistered(TComp c) { }
        protected virtual void OnDependencyMissing(TComp c)
            => Debug.LogWarning($"[{GetType().Name}] Missing dependency for {c.name}");
        protected abstract void OnFixedStep(TComp c, TDep d, float dt);
    }
}