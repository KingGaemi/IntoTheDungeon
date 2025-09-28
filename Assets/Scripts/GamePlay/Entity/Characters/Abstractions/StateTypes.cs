namespace MyGame.GamePlay.Entity.Characters.Abstractions
{
    public enum ControlState { Normal, Stunned, Rooted, Silenced, Dead }
    public enum ActionState { Idle, Attack }
    public enum MovementState { Idle, Move, Walk, Run, Jump }
    public enum DenyReason { None, Dead, Stunned, Rooted, Silenced, InRecovery, Cooldown }
    [System.Flags] public enum ChangeMask : byte { None = 0, Action = 1, Movement = 2, Control = 4, Facing = 8, All = 0xFF }
}
