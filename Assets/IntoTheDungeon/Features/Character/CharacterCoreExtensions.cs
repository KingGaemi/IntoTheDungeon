
namespace IntoTheDungeon.Features.Status
{
    public static class CharacterCoreExtensions
    {
        public static ref StatusComponent GetStatus(this CharacterCore core)
        {
            if (!core.IsValid)
            {
                throw new System.InvalidOperationException(
                    $"Cannot get status - Entity is not valid. " +
                    $"Entity: {core.Entity}, World: {core.World != null}, " +
                    $"EntityRoot: {core.EntityRoot != null}");
            }
            return ref core.World.EntityManager.GetComponent<StatusComponent>(core.Entity);
        }

        public static int GetCurrentHp(this CharacterCore core)
        {
            return core.GetStatus().CurrentHp;
        }

        public static float GetAttackSpeed(this CharacterCore core)
        {
            return core.GetStatus().AttackSpeed;
        }
    }
}