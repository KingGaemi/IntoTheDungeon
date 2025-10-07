using System;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Features.Command;

namespace IntoTheDungeon.Features.Character
{
    public class CharacterCoreBundle : ComponentBundle
    {
        public static readonly Type[] CoreBundle = new[]
        {
            typeof(StatusComponent),
            typeof(StatusModificationQueue),  // 필수
            typeof(StateComponent),
            typeof(CharacterIntentBuffer),
            typeof(EventReceiver)
        };
    }
}