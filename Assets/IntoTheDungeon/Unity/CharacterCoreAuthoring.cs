

using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Physics.Components;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Features.Command;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Core.Runtime.Physics;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Features.Core.Components;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Unity.World;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Features.Character;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;



namespace IntoTheDungeon.Unity.Behaviour
{
    [DisallowMultipleComponent]
    public class CharacterCoreAuthoring : IAuthoring
    {
        public int maxHp;
        public float movementSpeed;
        public float attackSpeed;

        // 베이크용 입력 데이터
        public Rigidbody2D rb;
        public Vector2 pos;
        public Vector2 dir;
        public string displayName;

        sealed class Baker : Baker<CharacterCoreAuthoring>
        {
            protected override void Bake(CharacterCoreAuthoring a)
            {
                var reg = RequireService<IRecipeRegistry>();
                var store = RequireService<IPhysicsBodyStore>();
                int handle = store.Add(new UnityPhysicsBody(a.rb));
                var r = new CharacterCoreRecipe(RecipeIds.Character, handle, a.displayName,
                    new Vec2(a.pos.x, a.pos.y), new Vec2(a.dir.x, a.dir.y),
                    a.maxHp, a.movementSpeed, a.attackSpeed);

                ApplyRecipe(r);
            }
        }
        public IBaker CreateBaker() => new Baker();


    }

}
