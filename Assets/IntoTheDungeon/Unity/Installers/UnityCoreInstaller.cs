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
using IntoTheDungeon.Features.Input;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Unity.World;
using IntoTheDungeon.Core.Runtime.ECS;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Entities;
using IntoTheDungeon.Features.Unity.Abstractions;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Features.Character;
using IntoTheDungeon.Unity.Bridge.View.Abstractions;
using IntoTheDungeon.Unity.Bridge.Core.Abstractions;
using IntoTheDungeon.Unity.Bridge.Core;
using IntoTheDungeon.Unity.Bridge.View;
using IntoTheDungeon.Core.Abstractions.Types;
namespace IntoTheDungeon.Unity
{
    public class UnityCoreInstaller : MonoGameInstaller
    {
        [SerializeField] ViewRecipeRegistry viewRecipeRegistry;
        [SerializeField] EntityViewMappingTable mappingTable;

        [Header("Entity Data")]
        [Tooltip("플레이어 입력을 받는 RecipeId 이름. PlayerInputSystem 쿼리에 걸려야 하는 유일한 이름.")]
        [SerializeField] string playerCharacterName = "Character";
        [Tooltip("몹으로 등록할 이름들 (예: Catherine, Orc1, Slime). PlayerTag가 붙지 않는다.")]
        [SerializeField] string[] monsterNames = { "Catherine", "Orc1" };
        public override void Install(GameWorld world)
        {
            Debug.Log("[Installer] EventHub");
            world.SetOnce<IEventHub>(new EventHub());

            Debug.Log("[Installer] INameTable");
            world.SetOnce<INameTable>(new NameTable());

            Debug.Log("[Installer] IRecipeTable");
            var recipeTable = new RecipeTable();
            world.SetOnce<IRecipeTable>(recipeTable);

            Debug.Log("[Installer] INameToRecipeRegistry");
            world.SetOnce<INameToRecipeRegistry>(new NameToRecipeRegistry());

            Debug.Log("[Installer] EntityRecipeRegistry");
            var entityRecipeRegistry = new EntityRecipeRegistry();
            world.SetOnce<IEntityRecipeRegistry>(entityRecipeRegistry);

            // 1. 플레이어 전용 팩토리 / 몹 전용 팩토리를 분리 생성 (PlayerTag는 플레이어만 받는다)
            var characterFactory = new CharacterCoreFactory(isPlayer: true);
            var monsterFactory = new CharacterCoreFactory(isPlayer: false);

            var playerRecipeId = recipeTable.GetId(playerCharacterName);
            entityRecipeRegistry.Register(playerRecipeId, characterFactory);
            Debug.Log($"[Installer] Registered RecipeId: {playerCharacterName} → {playerRecipeId.Value} (player)");

            foreach (var name in monsterNames)
            {
                var recipeId = recipeTable.GetId(name);
                entityRecipeRegistry.Register(recipeId, monsterFactory);
                Debug.Log($"[Installer] Registered RecipeId: {name} → {recipeId.Value} (monster)");
            }

            // ... (기존 ViewRegistry 등록 및 시스템 추가 코드 유지)

            if (!viewRecipeRegistry) { Debug.LogError("viewRecipeRegistry null"); return; }
            viewRecipeRegistry.Initialize();
            world.SetOnce<IViewRecipeRegistry>(viewRecipeRegistry);

            var evMapRegistry = new EntityViewMapRegistry();
            Debug.Log("[Installer] EntityViewMapRegistry");
            world.SetOnce<IEntityViewMapRegistry>(evMapRegistry);
            mappingTable.ApplyMappings(evMapRegistry, viewRecipeRegistry, recipeTable);

            int viewOpQueueCapacity = 512;
            Debug.Log($"[Installer] ViewOpQueue({viewOpQueueCapacity})");
            world.SetOnce<IViewOpQueue>(new ViewOpQueue(viewOpQueueCapacity));

            world.SetOnce<ISceneViewRegistry>(new SceneViewRegistry());






            world.SetOnce<IHandleOwnerIndex>(new HandleOwnerIndex());



            Debug.Log("[Installer] SpawnQueue");
            world.SetOnce<ISystemSpawnQueue>(new SpawnQueue());

            Debug.Log("[Installer] UnityInputService");
            world.SetOnce<IInputService>(new UnityInputService());
            Debug.Log("[Installer] CollisionEvents");
            world.SetOnce<ICollisionEvents>(new CollisionEvents());
            world.SetOnce<IEntityFactory>(new EntityFactory(world, entityRecipeRegistry));





            var physQueue = new PhysicsOpQueue();
            var physStore_in = new PhysicsOpStore();
            var physStore_out = new PhysicsOpStore();
            var physBodyStore = new PhysicsBodyStore();
            var resolveSystem = new PhysicsOpResolveSystem(physQueue, physStore_out);
            world.SetOnce<IPhysicsOpQueue>(physQueue);
            world.SetOnce<IPhysicsCommandStore>(physStore_out);
            world.SetOnce<IPhysicsFeedbackStore>(physStore_in);
            world.SetOnce<IPhysicsBodyStore>(physBodyStore);
            world.SetOnce<IPhysicsOpResolveSystem>(resolveSystem);

            Debug.Log($"[Installer] BodyCreateQueue({viewOpQueueCapacity})");
            world.SetOnce<IBodyCreateQueue>(new BodyCreateQueue());




            Debug.Log("[Installer] E");
            world.SystemManager.AddUnique(new PlayerInputSystem());
            world.SystemManager.AddUnique(new SpawnSystem());
            world.SystemManager.AddUnique(new CharacterIntentApplySystem());
            world.SystemManager.AddUnique(new PhaseControlSystem());

            world.SystemManager.AddUnique(new KinematicPlannerSystem());

            Debug.Log("[Installer] F");
            world.SystemManager.AddUnique(new KinematicSystem());



            Debug.Log("[Installer] H");

            Debug.Log("[Installer] I");
            world.SystemManager.AddUnique(new StatusProcessingSystem());

            Debug.Log("[Installer] J");

            Debug.Log("[Installer] K");

            Debug.Log("[Installer] L");

            Debug.Log("[Installer] M");
            world.SystemManager.AddUnique(new PhysicsSpawnSystem());
            world.SystemManager.AddUnique(new ViewSpawnSystem());
            world.SystemManager.AddUnique(new PhysFeedbackApplySystem());

            world.SystemManager.AddUnique(new TransformProjectionSystem());



            Debug.Log("[Installer] OK");
        }
    }
}
