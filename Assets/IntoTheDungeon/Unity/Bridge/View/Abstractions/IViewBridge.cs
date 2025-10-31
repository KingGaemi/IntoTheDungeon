using UnityEngine;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Unity.Bridge.View.Abstractions
{
    /// <summary>
    /// 뷰 브릿지 핵심 인터페이스 - Entity와 GameObject 간 매핑 제공
    /// </summary>
    public interface IViewBridge
    {
        /// <summary>
        /// Entity에 대응하는 GameObject를 조회
        /// </summary>
        bool TryGetGameObject(Entity entity, out GameObject go);

        /// <summary>
        /// GameObject에 대응하는 Entity를 조회
        /// </summary>
        Entity GetEntity(GameObject go);

        /// <summary>
        /// Entity의 뷰 루트 Transform을 조회
        /// </summary>
        bool TryGetViewRoot(Entity entity, out Transform root);
    }
}