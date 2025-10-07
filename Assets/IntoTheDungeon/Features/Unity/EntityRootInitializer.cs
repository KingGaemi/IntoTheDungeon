using UnityEngine;
namespace IntoTheDungeon.Features.Unity
{
    public static class EntityRootInitializer
    {
        /// <summary>
        /// MonoBehaviour에 EntityRoot 설정 (없으면 생성)
        /// </summary>
        public static EntityRootBehaviour InitializeEntityRoot(this IEntityRootDependent dependent)
        {
            var mono = dependent as MonoBehaviour;
            if (mono == null)
            {
                Debug.LogError("IEntityRootDependent must be a MonoBehaviour");
                return null;
            }

            // 이미 설정됐으면 반환
            if (dependent.EntityRoot != null)
                return dependent.EntityRoot;

            // 부모에서 찾기
            var root = mono.GetComponentInParent<EntityRootBehaviour>();

            if (root == null)
            {

#if UNITY_EDITOR
                Debug.LogWarning(
                    $"[EntityRoot] No EntityRoot found for {mono.name}. " +
                    $"Created on {mono.transform.root.name}");
#endif
                return null;
            }

            dependent.EntityRoot = root;
            return root;
        }

        
        public static Transform GetRootTransform(this IEntityRootDependent dependent)
        {
            return dependent.EntityRoot?.Transform;
        }
        

        public static bool InitializeEntity(this IEntityRootDependent component)
        {
            // EntityRoot 초기화
            var root = component.InitializeEntityRoot();
            if (root == null)
                return false;

            // Entity 캐싱 시도
            if (component is MonoBehaviour mono)
            {
                // Start에서 호출하거나 이벤트로 지연 설정
                mono.StartCoroutine(SetEntityWhenReady(component, root));
            }

            return true;
        }

        private static System.Collections.IEnumerator SetEntityWhenReady(
            IEntityRootDependent component, EntityRootBehaviour root)
        {
            // Entity가 설정될 때까지 대기
            yield return new WaitUntil(() => !root.Entity.Equals(default));

            // Reflection으로 Entity 설정 (IEntityComponent에 setter가 있다면)
            var property = component.GetType().GetProperty("Entity");
            if (property != null && property.CanWrite)
            {
                property.SetValue(component, root.Entity);
            }
        }
    }
}