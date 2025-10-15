namespace IntoTheDungeon.Core.Abstractions.Types
{
    public enum ControlState { Normal, Stunned, Rooted, Silenced, Dead }
    public enum ActionState { Idle, Attack, Hit, Death }
    public enum MovementState { Idle, Move, Walk, Run, Jump }
    public enum DenyReason { None, Dead, Stunned, Rooted, Silenced, InRecovery, Cooldown }
    [System.Flags] public enum ChangeMask : byte { None = 0, Action = 1, Movement = 2, Control = 4, Facing = 8, All = 0xFF }
    public static class MaskX
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static bool Has(this ChangeMask m, ChangeMask f) => (m & f) != 0;
    }
}
