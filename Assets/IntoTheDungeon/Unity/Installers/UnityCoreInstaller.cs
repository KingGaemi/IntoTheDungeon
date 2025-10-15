using UnityEngine;
using IntoTheDungeon.Core.Runtime.World;
using IntoTheDungeon.Core.Abstractions.Services;

using IntoTheDungeon.Core.Runtime.Services;
using IntoTheDungeon.Core.Runtime.Event;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.Physics.Runtime;
using IntoTheDungeon.Core.Physics.Implementation;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Features.Command;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Features.Physics.Systems;
using IntoTheDungeon.Features.View;
using IntoTheDungeon.Features.Input;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Unity.World;
using IntoTheDungeon.Core.Runtime.ECS;
using IntoTheDungeon.Unity.View;
using IntoTheDungeon.Core.ECS.Abstractions;
namespace IntoTheDungeon.Unity
{
    public class UnityCoreInstaller : MonoGameInstaller
    {
        [SerializeField] EntityRecipeRegistry coreRecipeRegistry;
        [SerializeField] ScriptableObject viewRecipeRegistry;
        public override void Install(GameWorld world)
        {
            Debug.Log("[Installer] A");
            world.SetOnce<IEventHub>(new EventHub());

            Debug.Log("[Installer] B");
            world.SetOnce<IInputService>(new UnityInputService());

            Debug.Log("[Installer] C");
            world.SetOnce<ICollisionEvents>(new CollisionEvents());

            Debug.Log("[Installer] D");
            world.SetOnce<IPhysicsBodyStore>(new PhysicsBodyStore());

            coreRecipeRegistry.ForceRebuild();
            if (!coreRecipeRegistry) { Debug.LogError("coreRecipeRegistry null"); return; }
            world.SetOnce<IRecipeRegistry>(coreRecipeRegistry);
            // world.SetOnce((ViewRecipeRegistry)viewRecipeRegistry);

            Debug.Log("[Installer] E");
            world.SystemManager.AddUnique(new KinematicPlannerSystem());

            Debug.Log("[Installer] F");
            world.SystemManager.AddUnique(new KinematicSystem());

            Debug.Log("[Installer] G");
            world.SystemManager.AddUnique(new PhysicsApplySystem());

            Debug.Log("[Installer] H");
            world.SystemManager.AddUnique(new PhaseControlSystem());

            Debug.Log("[Installer] I");
            world.SystemManager.AddUnique(new StatusProcessingSystem());

            Debug.Log("[Installer] J");
            world.SystemManager.AddUnique(new PlayerInputSystem());

            Debug.Log("[Installer] K");
            world.SystemManager.AddUnique(new CharacterIntentApplySystem());

            Debug.Log("[Installer] L");
            world.SystemManager.AddUnique(new SpawnSystem());

            Debug.Log("[Installer] M");
            world.SystemManager.AddUnique(new CharacterViewSpawnSystem());


            Debug.Log("[Installer] OK");
        }
    }
}
