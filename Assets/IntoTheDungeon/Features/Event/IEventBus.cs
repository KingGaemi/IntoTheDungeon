using System.Collections.Generic;

namespace IntoTheDungeon.Features.Event
{
    public interface IEventBus { List<StatusChanged> StatusEvents { get; } void ClearFrame(); }
}