using UnityEngine;
using System;
using System.Linq;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Unity.Bridge.View.Abstractions;

namespace IntoTheDungeon.Unity.Bridge.View
{
    [CreateAssetMenu(fileName = "ViewRecipe", menuName = "IntoTheDungeon/View/Recipe")]
    public sealed class ViewRecipe : ScriptableObject, IViewRecipe
    {
        [SerializeField] private GameObject prefab;

        [Header("Auto-Collected Behaviours")]
        [SerializeField] private string[] visualBehaviourTypeNames;
        [SerializeField] private string[] scriptBehaviourTypeNames;

        private ViewId? _cachedViewId;
        private Type[] _cachedScriptTypes;
        private Type[] _cachedVisualTypes;

        public GameObject Prefab => prefab;

        public ViewId ViewId
        {
            get
            {
                if (_cachedViewId == null)
                    _cachedViewId = ComputeViewId();
                return _cachedViewId.Value;
            }
        }

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

        private ViewId ComputeViewId()
        {
            unchecked
            {
                int hash = 17;

                if (scriptBehaviourTypeNames != null)
                {
                    foreach (var typeName in scriptBehaviourTypeNames)
                    {
                        if (string.IsNullOrEmpty(typeName)) continue;
                        hash = hash * 31 + typeName.GetHashCode();
                    }
                }

                if (visualBehaviourTypeNames != null)
                {
                    foreach (var typeName in visualBehaviourTypeNames)
                    {
                        if (string.IsNullOrEmpty(typeName)) continue;
                        hash = hash * 31 + typeName.GetHashCode();
                    }
                }

                return new ViewId(hash);
            }
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

            // 캐시 무효화
            _cachedViewId = null;
            _cachedScriptTypes = null;
            _cachedVisualTypes = null;
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
                         $"{scriptBehaviours.Length} script, {visualBehaviours.Length} visual → " +
                         $"ViewId: {ComputeViewId().Value}");
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