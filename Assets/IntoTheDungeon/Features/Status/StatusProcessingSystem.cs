using IntoTheDungeon.Core.ECS;
using IntoTheDungeon.Features.Character;
using IntoTheDungeon.Core.ECS.Entities;
using IntoTheDungeon.Core.Util.Maths;
using System.Diagnostics;

namespace IntoTheDungeon.Features.Status
{
    public class StatusProcessingSystem : GameSystem, ITick
    {
        public override int Priority => 500; // 다른 시스템들 이후 실행
        
        public void Tick(float dt)
        {
            foreach (var chunk in _world.EntityManager.GetChunks(
                typeof(StatusComponent)))
            {
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var entities = chunk.GetEntities();
                
                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var status = ref statuses[i];
                    var queue = _world.EntityManager.GetManagedComponent<StatusModificationQueue>(entities[i]);
                    if (!status.IsAlive) continue;
                    if (queue.Count == 0)
                        continue;

                    // 수정 전 상태 저장
                    var prevHp = status.CurrentHp;

                    // 모든 수정사항 일괄 처리
                    foreach (var mod in queue.Modifications)
                    {
                        ApplyModification(ref status, mod);
                    }

                    // 이벤트 발행
                    NotifyChanges(entities[i], ref status, prevHp);

                    // 큐 비우기
                    queue.Clear();
                }
            }
        }
        
        private void ApplyModification(ref StatusComponent status, StatusModification mod)
        {
            switch (mod.ModType)
            {
                case StatusModification.Type.Damage:
                    int damageAfterArmor = Mathx.Max(0, (int)mod.Value - status.Armor);
                    status.CurrentHp = Mathx.Max(0, status.CurrentHp - damageAfterArmor);
                    break;

                case StatusModification.Type.Heal:
                    status.CurrentHp = Mathx.Min(status.MaxHp, status.CurrentHp + (int)mod.Value);
                    break;

                case StatusModification.Type.SetHp:
                    status.CurrentHp = Mathx.Clamp((int)mod.Value, 0, status.MaxHp);
                    break;

                case StatusModification.Type.ModifyDamage:
                    status.Damage = Mathx.Max(0, status.Damage + (int)mod.Value);
                    break;

                case StatusModification.Type.Armor:
                    status.Armor = Mathx.Max(0, status.Armor + (int)mod.Value);
                    break;

                case StatusModification.Type.AttackSpeed:
                    status.AttackSpeed = Mathx.Max(0.1f, status.AttackSpeed + mod.Value);
                    break;

                case StatusModification.Type.MovementSpeed:
                    status.MovementSpeed = Mathx.Max(0, status.MovementSpeed + mod.Value);
                    break;

                case StatusModification.Type.ModifyArmor:
                    status.Armor = Mathx.Max(0, (int)mod.Value);
                    break;

                case StatusModification.Type.ModifyAttackSpeed:
                    status.AttackSpeed = Mathx.Max(0.1f, mod.Value);
                    break;

                case StatusModification.Type.ModifyMovementSpeed:
                    status.MovementSpeed = Mathx.Max(0, mod.Value);
                    break;
                case StatusModification.Type.Init:
                    break;
            }
        }

        private void NotifyChanges(Entity entity, ref StatusComponent status, int prevHp)
        {
            var receiver = _world.EntityManager.GetManagedComponent<EventReceiver>(entity);
            if (receiver == null)
                return;

            // HP 변경 이벤트
            if (status.CurrentHp != prevHp)
            {
                receiver.NotifyHpChange(status.CurrentHp, prevHp);

                // 사망 체크
                if (status.CurrentHp <= 0 && prevHp > 0)
                {
                    status.IsAlive = false;
                }
            }
            receiver.NotifyASChange(status.AttackSpeed);
            receiver.NotifyMSChange(status.MovementSpeed);
        }
    }
}