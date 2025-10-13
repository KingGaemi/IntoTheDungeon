namespace IntoTheDungeon.Core.Abstractions.Types
{
    public struct StateSnapshot
    {
        public ActionState Action;
        public MovementState Movement;
        public ControlState Control;
        public Facing2D Facing;
        public uint Version;
    }
}
