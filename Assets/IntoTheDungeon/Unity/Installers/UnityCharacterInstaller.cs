using IntoTheDungeon.Core.Runtime.World;
using IntoTheDungeon.Features.Command;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Features.Status;

namespace IntoTheDungeon.Unity.Character
{
    public class UnityCharacterInstaller : MonoGameInstaller
    {
        public override void Install(GameWorld world)
        {
            world.SystemManager.Add(new StatusProcessingSystem());
            world.SystemManager.Add(new CharacterIntentApplySystem());
            world.SystemManager.Add(new PhaseControlSystem());

        }


    }
}
