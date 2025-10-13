#if false
using IntoTheDungeon.Features.State;

namespace IntoTheDungeon.Features.Event
{
    public class EventNotifier
    {
        public event System.Action<StateSnapshot, ChangeMask> OnStateChanged;
        public event System.Action<int> OnHpUp;
        public event System.Action<int> OnHpDown;
        public event System.Action<float> OnMSChange;
        public event System.Action<float> OnASChange;
        public void NotifyStateChange(StateSnapshot curr, ChangeMask mask)
        {
            OnStateChanged?.Invoke(curr, mask);
        }
        public void NotifyHpUp(int amount)
        {
            OnHpUp?.Invoke(amount);
        }
        public void NotifyHpDown(int amount)
        {
            OnHpDown?.Invoke(amount);
        }

        public void NotifyASChange(float speed)
        {
            OnASChange?.Invoke(speed);
        }
        public void NotifyMSChange(float speed)
        {
            OnMSChange?.Invoke(speed);
        }
    }
}
#endif