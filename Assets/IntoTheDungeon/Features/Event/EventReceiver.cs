using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Features.State;


namespace IntoTheDungeon.Features.Event
{   
    public sealed class EventReceiver : IManagedComponent
    {
        // 추후에 EventReceiver를 Interface화 하고, 현재 리시버를 CharacterEventReceiver로 만들 수 있음
        public EventNotifier eventNotifier;

        public EventReceiver(EventNotifier eventNoti)
        {
            eventNotifier = eventNoti;
        }
        internal void NotifyStateChange(in StateSnapshot curr, ChangeMask mask)
        {
            eventNotifier.NotifyStateChange(curr, mask);

        }
        internal void NotifyHpChange(int currHp, int prevHp)
        {
            if (currHp > prevHp)
                eventNotifier.NotifyHpUp(currHp - prevHp);
            else
                eventNotifier.NotifyHpDown(prevHp - currHp);

        }
        internal void NotifyMSChange(float speed)
        {
            eventNotifier.NotifyMSChange(speed);
        }
        
        internal void NotifyASChange(float speed)
        {
            eventNotifier.NotifyASChange(speed);
        }

        
        // ManagedComponent 정리
        public void OnDestroy()
        {
            eventNotifier = null;
        }
    }
}