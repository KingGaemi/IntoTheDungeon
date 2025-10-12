namespace IntoTheDungeon.Core.Abstractions.World
{
  public interface IBaker
  {
    void Execute(IAuthoring authoring, IWorld world, ECS.Abstractions.Entity entity);

  }
}