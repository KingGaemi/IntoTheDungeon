namespace IntoTheDungeon.Core.Abstractions.World
{
    public interface IWorldDependent
    {
        IWorld World { get; set; }
    }
}