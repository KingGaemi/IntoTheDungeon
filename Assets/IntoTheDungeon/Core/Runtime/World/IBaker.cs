namespace IntoTheDungeon.Core.Runtime.World
{
  public interface IBaker
  {
    void Execute(UnityEngine.MonoBehaviour authoring, GameWorld world, Core.ECS.Entities.Entity entity);

  }
}