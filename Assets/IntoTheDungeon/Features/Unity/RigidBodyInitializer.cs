using UnityEngine;
namespace IntoTheDungeon.Features.Unity
{ 
    public static class RigidBodyInitializer
    {
        /// <summary>
        /// MonoBehaviour에 EntityRoot 설정 (없으면 생성)
        /// </summary>
        public static Rigidbody2D InitializeRigidBody(this IRigidBodyDependent dependent)
        {
            var mono = dependent as MonoBehaviour;
            if (mono == null)
            {
                Debug.LogError("IRigidbodyDependent must be a MonoBehaviour");
                return null;
            }

            // 이미 설정됐으면 반환
            if (dependent.Rigidbody != null)
                return dependent.Rigidbody;

            // 부모에서 찾기
            var root = mono.GetComponentInParent<EntityRootBehaviour>();
            if (root != null)
            {
                dependent.Rigidbody = root.GetComponent<Rigidbody2D>();
                if (dependent.Rigidbody == null )
                {
                    #if UNITY_EDITOR
                    Debug.LogWarning(
                        $"[EntityRoot] No EntityRoot found for {mono.name}. " +
                        $"Created on {mono.transform.root.name}");
                    #endif
                    return null;
                }
            }
            return dependent.Rigidbody;
        }
    }
}
