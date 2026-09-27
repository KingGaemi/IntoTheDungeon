using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Features.Character;
using IntoTheDungeon.Runtime.Abstractions;
using IntoTheDungeon.Core.Runtime.World;
using IntoTheDungeon.Core.ECS.Abstractions;
namespace IntoTheDungeon.Runtime.Installers
{
    public sealed class EntityFactoryInstaller : IGameInstaller
    {
        private readonly CharacterCoreFactory _characterFactory;
        private readonly List<string> _characterNames; // JSON에서 파싱하거나 인스펙터에서 넘겨받은 이름들

        public EntityFactoryInstaller(CharacterCoreFactory factory, List<string> names)
        {
            _characterFactory = factory;
            _characterNames = names;
        }

        public void Install(IWorld world)
        {
            // 월드에서 팩토리 레지스트리 서비스 꺼내기
            if (!world.TryGet(out IEntityRecipeRegistry registry))
            {
                throw new System.Exception("IEntityRecipeRegistry가 월드에 없습니다.");
            }
            if (!world.TryGet(out IRecipeTable recipeTable))
            {
                throw new System.Exception("IRecipeTable이 월드에 없습니다.");
            }

            // "Orc1", "Catherine" 등의 이름을 모두 하나의 CharacterCoreFactory에 연결
            foreach (var name in _characterNames)
            {
                var recipeId = recipeTable.GetId(name);
                registry.Register(recipeId, _characterFactory);
            }
        }

        public void Install(GameWorld world)
        {
            throw new System.NotImplementedException();
        }
    }
}