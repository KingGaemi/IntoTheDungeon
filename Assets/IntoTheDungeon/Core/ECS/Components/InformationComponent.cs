using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.ECS.Components
{
    public struct InformationComponent : IComponentData
    {
        public NameId NameId; // 문자열은 테이블에서 조회
        public RecipeId RecipeId;
    }

}