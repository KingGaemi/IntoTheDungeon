
namespace IntoTheDungeon.Features.State
{
    public struct StateSnapshot
    {
        public ActionState Action;
        public MovementState Movement;
        public ControlState Control;
        public Core.Facing2D Facing;
        public uint Version;
    }
}
