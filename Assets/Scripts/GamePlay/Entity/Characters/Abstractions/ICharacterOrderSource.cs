
namespace MyGame.GamePlay.Entity.Characters.Abstractions
{
    public interface ICharacterOrderSource
    {
        event System.Action<CharacterOrder> Order;
    }

}