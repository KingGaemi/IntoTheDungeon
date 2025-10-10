
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Features.Attack
{
    public struct PiercingComponent : IComponentData
    {
        public int Hit0, Hit1, Hit2, Hit3, Hit4, Hit5, Hit6, Hit7;
        public int HitCount;
        public int MaxPierceCount;

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