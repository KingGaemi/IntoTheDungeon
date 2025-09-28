
namespace MyGame.GamePlay.Entity.Characters.Abstractions
{
    public enum ActionKind { None, BasicAttack }
    public enum ActionPhase { None, Queued, Startup, Active, Recovery, Cooldown }

    public struct PhaseDurations
    {
        public float Startup, Active, Recovery, Cooldown;
    }
}