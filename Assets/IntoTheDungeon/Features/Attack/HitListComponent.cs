
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Features.Attack
{
    /// <summary>
    /// 히트박스 하나가 이미 맞힌 대상(Entity.Index). 같은 대상을 두 번 때리지 않게 한다.
    /// 최대 8명까지 기억하고, 그 이상은 IsFull로 판단해 더 때리지 않는다.
    /// </summary>
    public struct HitListComponent : IComponentData
    {
        public int Hit0, Hit1, Hit2, Hit3, Hit4, Hit5, Hit6, Hit7;
        public int HitCount;

        public readonly bool IsFull => HitCount >= 8;

        public readonly bool AlreadyHit(int entityId)
        {
            if (HitCount > 0 && Hit0 == entityId) return true;
            if (HitCount > 1 && Hit1 == entityId) return true;
            if (HitCount > 2 && Hit2 == entityId) return true;
            if (HitCount > 3 && Hit3 == entityId) return true;
            if (HitCount > 4 && Hit4 == entityId) return true;
            if (HitCount > 5 && Hit5 == entityId) return true;
            if (HitCount > 6 && Hit6 == entityId) return true;
            if (HitCount > 7 && Hit7 == entityId) return true;
            return false;
        }

        public void AddHit(int entityId)
        {
            switch (HitCount)
            {
                case 0: Hit0 = entityId; break;
                case 1: Hit1 = entityId; break;
                case 2: Hit2 = entityId; break;
                case 3: Hit3 = entityId; break;
                case 4: Hit4 = entityId; break;
                case 5: Hit5 = entityId; break;
                case 6: Hit6 = entityId; break;
                case 7: Hit7 = entityId; break;
            }
            if (HitCount < 8) HitCount++;
        }
    }
}