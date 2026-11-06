using System.Collections.Generic;

namespace IntoTheDungeon.Features.Run
{
    /// <summary>이벤트(events 트랙) 저장 필드. 없던 옛 저장은 빈 값으로 읽힌다.</summary>
    public sealed partial class RunSave
    {
        public string PendingEvent = "";          // 고르는 중인 이벤트 Id("" = 없음)
        public List<string> SeenEvents = new();   // 이번 판에 나온 이벤트 Id
    }
}
