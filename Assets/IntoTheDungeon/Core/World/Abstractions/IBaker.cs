namespace IntoTheDungeon.Core.World.Abstractions
{
  public interface IBaker
  {
    void Execute(IAuthoring authoring, IWorld world, ECS.Abstractions.Entity entity);

  }
}