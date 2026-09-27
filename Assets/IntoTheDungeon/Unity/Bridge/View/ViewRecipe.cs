using UnityEngine;
using System;
using System.Linq;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Unity.Bridge.View.Abstractions;

namespace IntoTheDungeon.Unity.Bridge.View
{
    [CreateAssetMenu(fileName = "ViewRecipe", menuName = "IntoTheDungeon/View/Recipe")]
    public sealed class ViewRecipe : ScriptableObject, IViewRecipe, IAttackAnimatable
    {
        [SerializeField] private GameObject prefab;

        [Header("Attack Phase Clips (공격 페이즈를 쓰는 몹만, 비우면 배속 보정 없음)")]
        [SerializeField] private AnimationClip attackWindupClip;
        [SerializeField] private AnimationClip attackRecoveryClip;

        [Header("Auto-Collected Behaviours")]
        [SerializeField] private string[] visualBehaviourTypeNames;
        [SerializeField] private string[] scriptBehaviourTypeNames;

        [Header("Debug/Display")]
        [SerializeField] private string displayName;

        private ViewId _viewId;
        private Type[] _cachedScriptTypes;
        private Type[] _cachedVisualTypes;

        public GameObject Prefab => prefab;
        public AnimationClip AttackWindupClip => attackWindupClip;
        public AnimationClip AttackRecoveryClip => attackRecoveryClip;

        string IViewRecipe.DisplayName => displayName;

        // ViewRecipeRegistry가 Initialize() 시점에 배정하는 순번. 레시피 스스로 계산하지 않는다.
        public ViewId ViewId => _viewId;

        public void AssignViewId(ViewId id) => _viewId = id;

        public Type[] GetScriptContainerBehaviours()
        {
            if (_cachedScriptTypes == null)
                _cachedScriptTypes = ResolveTypes(scriptBehaviourTypeNames);
            return _cachedScriptTypes;
        }

        public Type[] GetVisualContainerBehaviours()
        {
            if (_cachedVisualTypes == null)
                _cachedVisualTypes = ResolveTypes(visualBehaviourTypeNames);
            return _cachedVisualTypes;
        }

        private Type[] ResolveTypes(string[] typeNames)
        {
            if (typeNames == null || typeNames.Length == 0)
                return Array.Empty<Type>();

            var types = new Type[typeNames.Length];
            for (int i = 0; i < typeNames.Length; i++)
            {
                if (string.IsNullOrEmpty(typeNames[i])) continue;

                types[i] = Type.GetType(typeNames[i]);
                if (types[i] == null)
                    Debug.LogError($"[ViewRecipe] Failed to resolve type: {typeNames[i]}");
            }

            return types;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Prefab 변경 감지 → 자동 분석
            if (prefab != null)
            {
                AutoCollectBehavioursFromPrefab();
            }
            ValidateDisplayName();   // ← 추가
            // 캐시 무효화 (ViewId는 레지스트리가 소유하므로 여기서 건드리지 않음)
            _cachedScriptTypes = null;
            _cachedVisualTypes = null;
        }
        private void ValidateDisplayName()
        {
            if (string.IsNullOrEmpty(displayName))
            {
                Debug.LogWarning($"[ViewRecipe] '{name}' 에셋의 DisplayName이 비어있습니다. " +
                                  $"디버그 씬에서는 Prefab 이름({(prefab ? prefab.name : "없음")})으로 대체 표시됩니다.");
            }

            if (prefab != null && !string.IsNullOrEmpty(displayName)
                && !prefab.name.Contains(displayName, StringComparison.OrdinalIgnoreCase)
                && !displayName.Contains(prefab.name, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"[ViewRecipe] '{name}': DisplayName('{displayName}')과 " +
                                  $"연결된 Prefab 이름('{prefab.name}')이 서로 다릅니다. " +
                                  $"프리팹을 잘못 연결한 건 아닌지 확인해주세요.");
            }
        }

        private void AutoCollectBehavioursFromPrefab()
        {
            if (prefab == null) return;

            if (!prefab.TryGetComponent<EntityRootBehaviour>(out var root))
            {
                Debug.LogWarning($"[ViewRecipe] Prefab {prefab.name} missing EntityRootBehaviour");
                scriptBehaviourTypeNames = Array.Empty<string>();
                visualBehaviourTypeNames = Array.Empty<string>();
                return;
            }

            // Script container behaviours 수집
            var scriptBehaviours = root.Script != null
                ? root.Script.GetComponents<MonoBehaviour>()
                    .Where(b => b != null && b.GetType() != typeof(EntityRootBehaviour)
                                          && b.GetType() != typeof(ScriptContainer))
                    .Select(b => b.GetType().AssemblyQualifiedName)
                    .ToArray()
                : Array.Empty<string>();

            // Visual container behaviours 수집
            var spriteRoot = root.Visual != null
                ? root.Visual.transform.Find("SpriteRoot")
                : null;

            var visualBehaviours = spriteRoot != null
                ? spriteRoot.GetComponents<Component>()
                    .Where(b => b != null && b.GetType() != typeof(Transform))
                    .Select(b => b.GetType().AssemblyQualifiedName)
                    .ToArray()
                : Array.Empty<string>();

            // 변경사항이 있을 때만 업데이트
            bool changed = false;

            if (!ArraysEqual(scriptBehaviourTypeNames, scriptBehaviours))
            {
                scriptBehaviourTypeNames = scriptBehaviours;
                changed = true;
            }

            if (!ArraysEqual(visualBehaviourTypeNames, visualBehaviours))
            {
                visualBehaviourTypeNames = visualBehaviours;
                changed = true;
            }

            if (changed)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                Debug.Log($"[ViewRecipe] Auto-collected behaviours from {prefab.name}: " +
                         $"{scriptBehaviours.Length} script, {visualBehaviours.Length} visual");
            }
        }

        private bool ArraysEqual(string[] a, string[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;

            return true;
        }

        [ContextMenu("Debug: Show ViewId")]
        private void DebugShowViewId()
        {
            Debug.Log($"[ViewRecipe] {name}\n" +
                     $"  ViewId: {ViewId.Value}\n" +
                     $"  Script: {string.Join(", ", scriptBehaviourTypeNames?.Select(t => t.Split('.').Last()) ?? Array.Empty<string>())}\n" +
                     $"  Visual: {string.Join(", ", visualBehaviourTypeNames?.Select(t => t.Split('.').Last()) ?? Array.Empty<string>())}");
        }
#endif
    }
}