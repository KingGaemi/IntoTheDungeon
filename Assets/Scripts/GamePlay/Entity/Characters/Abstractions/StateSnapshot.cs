
namespace MyGame.GamePlay.Entity.Characters.Abstractions
{
    public struct StateSnapshot
    {
        public ActionState Action;
        public MovementState Movement;
        public ControlState Control;
        public Entity.Abstractions.Facing2D Facing;
        public uint Version;
    }
}
