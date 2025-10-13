

namespace IntoTheDungeon.Core.Abstractions.Messages.Combat
{
    public interface IStateEventListener
    {
        void OnStateChanged(in StateChangedEvent evt);
    }

    public interface IStatusEventListener
    {
        void OnStatusChanged(in StatusChangedEvent evt);
    }

}
namespace IntoTheDungeon.Core.Abstractions.Messages.Animation
{
    public interface IAnimationEventListener
    {
        void OnAnimationPhaseChanged(in AnimationPhaseChangedEvent evt);
    }
}