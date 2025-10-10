// Unity/Bridge/ViewBridge.cs
using UnityEngine;
using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.World.Abstractions;

public sealed class ViewBridge : MonoBehaviour, IWorldInjectable
{
    public GameObject[] Prefabs; // index = PrefabId

    IEntityManager _em;
    readonly List<ViewOp> _ops = new();
    readonly Dictionary<Entity, GameObject> _map = new();

    // Behaviour 생성기 레지스트리(옵션)
    readonly Dictionary<string, IBehaviourFactory> _factories = new();

    public void Init(IWorld world)
    {
        _em = world.EntityManager;
    }

    void Awake()
    {
        // _em = /* GameWorld 등에서 획득 */;
        // 필요 시 공장 등록
        _factories["IntoTheDungeon.View.ProjectileView"] = new ProjectileViewFactory();
    }

    void Update()
    {
        _ops.Clear();
        _em.ViewOps.Drain(_ops);    // 1) 큐 비우기

        for (int i = 0; i < _ops.Count; i++)
        {
            var op = _ops[i];
            if (op.Kind == ViewOpKind.Spawn) HandleSpawn(op.Entity, op.Spawn);
            else HandleDespawn(op.Entity);
        }

        // 2) Transform, Sprite 등 동기화는 별도 컴포넌트/스크립트에서 수행
        //    (예: TransformSync, SpriteViewBridge 등)
    }

    void HandleSpawn(Entity e, in ViewSpawnSpec spec)
    {
        if (_map.ContainsKey(e)) return;

        var prefab = (spec.PrefabId >= 0 && spec.PrefabId < Prefabs.Length) ? Prefabs[spec.PrefabId] : null;
        var go = Instantiate(prefab != null ? prefab : new GameObject($"Entity_{e.Index}"));
        _map[e] = go;

        // Behaviour 부착
        var behs = spec.Behaviours;
        if (behs != null)
        {
            for (int k = 0; k < behs.Length; k++)
            {
                if (_factories.TryGetValue(behs[k].TypeName, out var fac))
                    fac.Attach(go, e, behs[k].Payload, _em);
                else
                {
                    var t = System.Type.GetType(behs[k].TypeName);
                    if (t != null && typeof(Component).IsAssignableFrom(t))
                        go.AddComponent(t);
                }
            }
        }

        // 선택: 정렬 계층 적용용 Renderer가 있으면 반영
        if (go.TryGetComponent<Renderer>(out var r))
        {
            r.sortingLayerID = spec.SortingLayerId;
            r.sortingOrder   = spec.OrderInLayer;
        }
    }

    void HandleDespawn(Entity e)
    {
        if (_map.TryGetValue(e, out var go))
        {
            Destroy(go);
            _map.Remove(e);
        }
    }

    // 외부에서 Entity→GO 필요 시
    public bool TryGetGO(Entity e, out GameObject go) => _map.TryGetValue(e, out go);
}

// 선택: Behaviour 팩토리 인터페이스
public interface IBehaviourFactory
{
    Component Attach(GameObject go, Entity e, byte[] payload, IEntityManager em);
}

public sealed class ProjectileViewFactory : IBehaviourFactory
{
    public Component Attach(GameObject go, Entity e, byte[] payload, IEntityManager em)
    {
        // 예시: 전용 뷰 컴포넌트 부착
        return go.AddComponent<ProjectileView>();
    }
}

// 예시 뷰 컴포넌트
public sealed class ProjectileView : MonoBehaviour { }
