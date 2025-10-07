namespace MyGame.GamePlay.Entity.Characters.Abstractions
{
    public enum CharacterOrder
    {
        Idle,
        Hold,
        Right,
        Left,
        Move,  // Can be divided Walk & Run later
        Attack,
        Ult,
        Die
    }
}