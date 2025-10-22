using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Abstractions.Spawn;
using IntoTheDungeon.Features.Status;

namespace IntoTheDungeon.Features.Character
{
    public struct CharacterStatsInit : ISpawnInit
    {
        public int MaxHp; public float MoveSpeed; public float AttackSpeed;

        public void Apply(IWorld world, Entity e)
        {
            var em = world.EntityManager;

            // StatusComponent add/replace
            if (em.HasComponent<StatusComponent>(e))
            {
                var s = em.GetComponent<StatusComponent>(e);
                s.MaxHp = MaxHp;
                s.CurrentHp = MaxHp;          // 초기 HP = MaxHp
                s.MovementSpeed = MoveSpeed;
                s.AttackSpeed = AttackSpeed;
                s.IsAlive = true;
                em.SetComponent(e, s);
            }
            else
            {
                em.AddComponent(e, new StatusComponent
                {
                    MaxHp = MaxHp,
                    CurrentHp = MaxHp,
                    MovementSpeed = MoveSpeed,
                    AttackSpeed = AttackSpeed,
                    IsAlive = true
                });
            }
        }
    }
}