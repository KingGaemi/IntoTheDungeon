namespace IntoTheDungeon.Core.World.Abstractions
{
    public interface IWorldDependent
    {
        IWorld World { get; set; }
    }
}