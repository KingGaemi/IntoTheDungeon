using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.Abstractions.Types
{
    public struct BehaviourRecipe
    {
        public BehaviourTypeId TypeId;
        public string PayloadJson; // 선택 사항: JSON 직렬화된 파라미터

    }
}