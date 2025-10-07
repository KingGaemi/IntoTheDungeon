namespace MyGame.GamePlay.Party.Abstractions 
{
    public interface IPartyOrderSource
    {
        event System.Action<PartyOrder> Order;
    }
}