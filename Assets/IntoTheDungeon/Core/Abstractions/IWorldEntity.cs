using IntoTheDungeon.Core.ECS.Entities;
using IntoTheDungeon.Core.Runtime.World;

namespace IntoTheDungeon.Core.Abstractions
{
    public interface IWorldEntity
    {
        GameWorld _gameWorld { set;}
        Entity _entity { set; }
    }
}