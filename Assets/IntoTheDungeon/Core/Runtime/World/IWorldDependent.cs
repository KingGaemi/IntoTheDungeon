namespace IntoTheDungeon.Core.Runtime.World
{
    public interface IWorldDependent
    {
        GameWorld World { get; set; }
    }
}