using UnityEngine;
using System;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Unity.Bridge.View.Abstractions
{
    // 모든 뷰 엔티티가 반드시 갖는 것 — 절대 안 바뀌는 최소 계약
    // IViewRecipe.cs
    public interface IViewRecipe
    {
        ViewId ViewId { get; }
        GameObject Prefab { get; }
        string DisplayName { get; }
        Type[] GetScriptContainerBehaviours();
        Type[] GetVisualContainerBehaviours();
        // InstanceTag 제거 — 레시피 소관 아님

        // ViewId는 레시피 자신이 계산하지 않는다 — ViewRecipeRegistry가 유일한 발급처.
        void AssignViewId(ViewId id);
    }

    // "공격 페이즈 시스템"을 쓰는 몹만 구현 — CharacterAnimator가 요구하는 계약
    public interface IAttackAnimatable
    {
        AnimationClip AttackWindupClip { get; }
        AnimationClip AttackRecoveryClip { get; }
    }
}