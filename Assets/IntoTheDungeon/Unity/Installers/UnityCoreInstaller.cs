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
using IntoTheDungeon.Features.Attack;
using IntoTheDungeon.Features.AI;
using IntoTheDungeon.Features.Battle;
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
        [Tooltip("씬에 놓인 파티원 프리팹의 레시피 이름")]
        [SerializeField] string playerCharacterName = "Character";
        [Tooltip("매핑 테이블(EntityViewMappingTable)에 없는데 캐릭터로 등록할 이름. 매핑 테이블에 있는 레시피는 자동 등록된다.")]
        [SerializeField] string[] monsterNames = { };

        [Header("2.5D 걸을 수 있는 영역 (X 가로, Y 깊이)")]
        [Tooltip("씬에 WalkAreaAuthoring이 없을 때만 쓰는 기본값")]
        [SerializeField] Vector2 walkAreaX = new(-100f, 100f);
        [SerializeField] Vector2 walkAreaDepth = new(-100f, 100f);

        [System.Serializable]
        public struct CharacterDefEntry
        {
            public string recipe;
            [Tooltip("몸 충돌 가로 반지름(깊이는 이 절반). 아이템 등 보정은 BodyComponent에서 따로 더해진다")]
            public float bodyRadius;
            [Tooltip("무거울수록 덜 밀린다")]
            public float bodyMass;
            [Tooltip("남을 밀기만 하고 자기는 안 밀림(보스 등)")]
            public bool immovable;
            [Tooltip("발에서 머리 꼭대기까지 높이(체력바 위치). 0이면 기본 1.2")]
            public float headHeight;
            [Tooltip("원거리(화살) 공격")]
            public bool ranged;
            [Tooltip("이 가로 거리 안이면 공격(0이면 기본 1)")]
            public float attackRange;
            [Tooltip("원거리: 이보다 가까우면 물러남")]
            public float minRange;
            [Tooltip("공격 준비·마무리 시간(0이면 기본 0.5·0.7)")]
            public float windup, recovery;
            public SkillPreset skill;
        }

        [Header("캐릭터 종류별 정의(없는 이름은 반지름 0.3·무게 1)")]
        [SerializeField] CharacterDefEntry[] characterDefs =
        {
            new() { recipe = "Character", bodyRadius = 0.3f, bodyMass = 1f, headHeight = 1.0f },
            new() { recipe = "Catherine", bodyRadius = 0.3f, bodyMass = 1f, headHeight = 1.0f },
            new() { recipe = "Orc1", bodyRadius = 0.35f, bodyMass = 1.2f, headHeight = 1.27f },
            new() { recipe = "Orc2", bodyRadius = 0.4f, bodyMass = 1.5f, headHeight = 1.36f },
            new() { recipe = "Orc3", bodyRadius = 0.45f, bodyMass = 2f, headHeight = 1.59f },
            new() { recipe = "Archer", bodyRadius = 0.28f, bodyMass = 0.9f, headHeight = 1.05f, ranged = true, attackRange = 5.5f, minRange = 2f,
                    windup = 0.6f, recovery = 0.3f, skill = SkillPreset.PowerShot },
        };

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

            // 캐릭터는 모두 같은 팩토리(메인 캐릭터 없음). 편은 스폰 때 팀으로 정해진다
            var definitions = new System.Collections.Generic.Dictionary<RecipeId, CharacterDefinition>();
            foreach (var d in characterDefs)
                if (!string.IsNullOrEmpty(d.recipe))
                {
                    var def = CharacterDefinition.Default;
                    def.BodyRadius = d.bodyRadius;
                    def.BodyMass = d.bodyMass;
                    def.Immovable = d.immovable;
                    if (d.headHeight > 0f) def.HeadHeight = d.headHeight;
                    def.Ranged = d.ranged;
                    if (d.attackRange > 0f) def.AttackRange = d.attackRange;
                    def.MinRange = d.minRange;
                    if (d.windup > 0f) def.WindupDuration = d.windup;
                    if (d.recovery > 0f) def.RecoveryDuration = d.recovery;
                    def.Skill = d.skill;
                    definitions[recipeTable.GetId(d.recipe)] = def;
                }
            var characterFactory = new CharacterCoreFactory(definitions);
            var characters = new System.Collections.Generic.HashSet<string>(monsterNames) { playerCharacterName };
            if (mappingTable) characters.UnionWith(mappingTable.RecipeNames);
            foreach (var name in characters)
            {
                var recipeId = recipeTable.GetId(name);
                entityRecipeRegistry.Register(recipeId, characterFactory);
                Debug.Log($"[Installer] Registered RecipeId: {name} → {recipeId.Value}");
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




            // 방(맵)에 WalkAreaAuthoring이 있으면 그 영역, 없으면 설치기 기본값(시스템들이 Initialize에서 가져가므로 먼저 등록)
            var authoring = FindFirstObjectByType<WalkAreaAuthoring>();
            world.SetOnce<IWalkArea>(authoring ? authoring.ToWalkArea()
                                               : new RectWalkArea(walkAreaX.x, walkAreaX.y, walkAreaDepth.x, walkAreaDepth.y));

            world.SetOnce(BuildBattleConfig());
            world.SetOnce(new BattleProgress());

            Debug.Log("[Installer] E");
            world.SystemManager.AddUnique(new PlayerInputSystem());
            world.SystemManager.AddUnique(new CombatAISystem());  // 의도 생성(파티·몹 전원 자동전투): CharacterIntentApplySystem보다 먼저
            world.SystemManager.AddUnique(new BattleFlowSystem());   // 웨이브 진행·승패(스폰 요청은 SpawnSystem이 같은 프레임에 처리)
            world.SystemManager.AddUnique(new SpawnSystem());
            world.SystemManager.AddUnique(new CharacterIntentApplySystem());
            world.SystemManager.AddUnique(new PhaseControlSystem());
            world.SystemManager.AddUnique(new SpecialSkillSystem());   // 필살기: 평타 끊고 시전·검기 발사·종료

            // 전투: 히트박스 생성 → 위치 갱신 → 판정 → HP 적용 → 사망 상태 → 수명 정리
            world.SystemManager.AddUnique(new AttackSystem());
            world.SystemManager.AddUnique(new FollowOwnerSystem());
            world.SystemManager.AddUnique(new ProjectileMotionSystem());   // 검기 등 날아가는 히트박스
            world.SystemManager.AddUnique(new HitDetectSystem());
            world.SystemManager.AddUnique(new DamageProcessingSystem());
            world.SystemManager.AddUnique(new DeathStateSystem());
            world.SystemManager.AddUnique(new HitStunSystem());   // 경직: 맞으면 잠깐 굳고 평타가 끊김(같은 프레임에 죽은 건 건드리지 않게 사망 처리 다음)
            world.SystemManager.AddUnique(new CorpseCleanupSystem());   // 쓰러진 적은 잠시 뒤 사라짐(파티원 시체는 남김)
            world.SystemManager.AddUnique(new LifetimeSystem());

            world.SystemManager.AddUnique(new KinematicPlannerSystem());

            Debug.Log("[Installer] F");
            world.SystemManager.AddUnique(new KinematicSystem());       // 물리 바디가 있는 엔티티용
            world.SystemManager.AddUnique(new MotionSystem());          // 2.5D 캐릭터 이동(TransformProjection보다 먼저)
            world.SystemManager.AddUnique(new HeightMotionSystem());    // 2.5D 높이: 중력·착지
            world.SystemManager.AddUnique(new KnockbackSystem());       // 피격 밀림
            world.SystemManager.AddUnique(new BodySeparationSystem());  // 몸 충돌: 이동이 다 끝난 뒤 겹친 캐릭터를 밀어냄



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

        BattleConfig BuildBattleConfig()
        {
            // 방마다 몹 구성은 Encounters, 파티 구성(동료 수)은 RunState가 정한다
            return new BattleConfig();
        }
    }
}
