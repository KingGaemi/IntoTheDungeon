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
using IntoTheDungeon.Features.Stats;
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
            [Tooltip("화면에 보이는 이름")]
            public string displayName;
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
            [Tooltip("자동으로 쓰는 기본 스킬")]
            public BasicSkillPreset basic;
            [Tooltip("근접 평타가 범위 공격(아니면 한 명만)")]
            public bool cleave;
            [Tooltip("레벨 1 기본 스탯(모두 0이면 5·5·5·5)")]
            public int str, agi, intel, wil;
            [Tooltip("레벨당 오르는 스탯(모두 0이면 1·1·1·1)")]
            public int gStr, gAgi, gInt, gWil;
            [Tooltip("특성 한 줄 설명")]
            public string traitText;
            [Tooltip("스탯 계산 뒤 배율(0이면 1): 체력·공격력·이속·공속")]
            public float hpMul, dmgMul, spdMul, atkSpdMul;
            [Tooltip("넉백 저항 가감(음수면 잘 밀리고 뜸)")]
            public float kbResist;
            [Tooltip("가시: 근접으로 때린 쪽이 돌려받는 비율 / 흡혈: 입힌 피해 중 회복 비율")]
            public float thorns, lifesteal;
            [Tooltip("독: 맞힌 피해의 이 비율을 초마다, poisonTime초 동안")]
            public float poison, poisonTime;
            [Tooltip("근접 평타 넉백·띄우기(0이면 기본 1·2)·경직")]
            public float knockback, launch, hitStun;
            [Tooltip("강타(예고 후 범위 공격, 도발로 끊을 수 있음): 반지름(0이면 없음)·힘 모으기 초·재사용 대기·공격력 배율")]
            public float slamRadius, slamCharge, slamCd, slamDmg;
            [Tooltip("강타 요구 저지력(0이면 10). 필살기 저지력(대부분 10)이 쌓여 이 이상이면 끊김")]
            public int slamStop;
            [Tooltip("되살아남: 횟수(0이면 없음)·무더기를 부수는 데 필요한 저지력")]
            public int revive, reviveStop;
            [Tooltip("보스 잼: 보스로 나오면 확정(bossGem), 아니면 dropGem을 dropGemChance 확률로(GemPool Id, 비우면 없음)")]
            public string bossGem, dropGem;
            public float dropGemChance;
        }

        [Header("캐릭터 종류별 정의(없는 이름은 반지름 0.3·무게 1)")]
        [SerializeField] CharacterDefEntry[] characterDefs =
        {
            new() { recipe = "Character", displayName = "케서린", gStr = 4, gAgi = 2, gInt = 1, gWil = 2, bodyRadius = 0.3f, bodyMass = 1f, headHeight = 1.0f, basic = BasicSkillPreset.Slash, str = 12, agi = 7, intel = 2, wil = 5 },
            new() { recipe = "Catherine", displayName = "케서린", gStr = 4, gAgi = 2, gInt = 1, gWil = 2, bodyRadius = 0.3f, bodyMass = 1f, headHeight = 1.0f, basic = BasicSkillPreset.Slash, str = 12, agi = 7, intel = 2, wil = 5 },
            // 오크: 단단하고(체력·넉백 저항↑) 느리지만, 맞으면 크게 밀려난다(평타 넉백·띄우기)
            new() { recipe = "Orc1", displayName = "오크", gStr = 1, gAgi = 1, gInt = 0, gWil = 1, bodyRadius = 0.35f, bodyMass = 1.2f, headHeight = 1.27f, str = 5, agi = 3, intel = 0, wil = 2,
                    traitText = "단단함 · 묵직한 한 방(크게 밀어냄) · 강타(도발로 끊기)", hpMul = 1.35f, spdMul = 0.9f, kbResist = 0.35f, knockback = 5f, launch = 3f, windup = 0.6f,
                    slamRadius = 1.5f, slamCharge = 1.3f, slamCd = 9f, slamDmg = 2.2f },
            new() { recipe = "Orc2", displayName = "오크 전사", gStr = 2, gAgi = 1, gInt = 0, gWil = 1, bodyRadius = 0.4f, bodyMass = 1.5f, headHeight = 1.36f, str = 7, agi = 4, intel = 0, wil = 3,
                    traitText = "단단함 · 묵직한 한 방(크게 밀어냄) · 강타(도발로 끊기)", hpMul = 1.4f, spdMul = 0.9f, kbResist = 0.4f, knockback = 6f, launch = 3.5f, windup = 0.6f,
                    slamRadius = 1.8f, slamCharge = 1.3f, slamCd = 8f, slamDmg = 2.5f },
            new() { recipe = "Orc3", displayName = "오크 대장", gStr = 2, gAgi = 1, gInt = 0, gWil = 2, bodyRadius = 0.45f, bodyMass = 2f, headHeight = 1.59f, str = 10, agi = 3, intel = 1, wil = 6,
                    traitText = "매우 단단함 · 맞으면 밀려나며 잠깐 굳음", hpMul = 1.5f, spdMul = 0.85f, kbResist = 0.5f, knockback = 7f, launch = 4f, hitStun = 0.25f, windup = 0.7f,
                    slamRadius = 2.1f, slamCharge = 1.4f, slamCd = 7f, slamDmg = 2.8f, slamStop = 15, bossGem = "warlord" },
            // 식인 식물: 체력이 낮은 대신 아프다. 뿌리 때문에 느리다
            new() { recipe = "Plant1", displayName = "식인초", gStr = 1, gAgi = 2, gInt = 0, gWil = 1, bodyRadius = 0.3f, bodyMass = 1f, headHeight = 1.0f, str = 5, agi = 7, intel = 0, wil = 2,
                    traitText = "약하지만 아프게 문다", hpMul = 0.5f, dmgMul = 1.8f, spdMul = 0.8f },
            new() { recipe = "Plant2", displayName = "가시 덩굴", gStr = 2, gAgi = 1, gInt = 0, gWil = 2, bodyRadius = 0.38f, bodyMass = 1.6f, headHeight = 1.05f, str = 8, agi = 3, intel = 0, wil = 5,
                    traitText = "가시: 근접으로 때리면 피해 30%를 돌려받음", hpMul = 0.65f, dmgMul = 1.5f, spdMul = 0.7f, thorns = 0.3f },
            new() { recipe = "Plant3", displayName = "독꽃", gStr = 2, gAgi = 1, gInt = 1, gWil = 1, bodyRadius = 0.4f, bodyMass = 1.8f, headHeight = 1.1f, str = 10, agi = 5, intel = 2, wil = 5,
                    traitText = "독: 맞으면 4초 동안 지속 피해", hpMul = 0.6f, dmgMul = 1.4f, spdMul = 0.75f, poison = 0.2f, poisonTime = 4f, bossGem = "bloom" },
            // 흡혈 박쥐(FreeAssets craftpix Bat01): 떼로 나오는 빠르고 가벼운 몹. 약하지만 문 만큼 회복하고, 맞으면 멀리 날아간다
            new() { recipe = "Bat1", displayName = "흡혈 박쥐", gStr = 1, gAgi = 2, gInt = 0, gWil = 0, bodyRadius = 0.22f, bodyMass = 0.5f, headHeight = 1.45f, str = 3, agi = 10, intel = 0, wil = 0,
                    attackRange = 0.9f, windup = 0.25f, recovery = 0.35f,
                    traitText = "빠른 떼 · 흡혈 · 잘 날아감", hpMul = 0.4f, dmgMul = 0.7f, spdMul = 1.35f, atkSpdMul = 1.3f, kbResist = -0.6f, lifesteal = 0.5f },
            // 해골 기사(FreeAssets craftpix Skeleton01, 정예): 단단하고 아프다. 쓰러지면 뼈 무더기가 됐다가 다시 일어난다(2번).
            // 무더기일 때 저지력 15 이상 필살기를 맞혀야 완전히 부서진다 → "해골 나오면 필살기를 아껴라"
            new() { recipe = "Skeleton1", displayName = "해골 기사", gStr = 2, gAgi = 1, gInt = 0, gWil = 2, bodyRadius = 0.38f, bodyMass = 1.8f, headHeight = 1.5f, str = 9, agi = 5, intel = 0, wil = 6,
                    windup = 0.7f, recovery = 0.5f, traitText = "되살아남(2번) · 뼈 무더기일 때 저지력 15 필살기로 분쇄", hpMul = 1.25f, dmgMul = 1.4f, spdMul = 0.8f, kbResist = 0.45f,
                    knockback = 4f, launch = 2f, hitStun = 0.2f, revive = 2, reviveStop = 15, dropGem = "bonecrown", dropGemChance = 0.4f },
            // 중갑 전사(탱커): 체력·방어·넉백 저항이 높고 무겁다. 대신 휘두르는 게 느려 평타 피해는 낮다. 평타는 범위, 필살기는 도발 + 방패
            new() { recipe = "Warrior", displayName = "중갑 전사", gStr = 4, gAgi = 3, gInt = 1, gWil = 3, bodyRadius = 0.36f, bodyMass = 2.2f, headHeight = 1.3f,
                    windup = 0.7f, recovery = 0.8f, skill = SkillPreset.TauntGuard, basic = BasicSkillPreset.ShieldBash, cleave = true, str = 13, agi = 8, intel = 1, wil = 10 },
            new() { recipe = "Archer", displayName = "궁수", gStr = 3, gAgi = 4, gInt = 2, gWil = 1, bodyRadius = 0.28f, bodyMass = 0.9f, headHeight = 1.05f, ranged = true, attackRange = 5.5f, minRange = 2f,
                    windup = 0.6f, recovery = 0.3f, skill = SkillPreset.PowerShot, basic = BasicSkillPreset.QuickShot, str = 7, agi = 12, intel = 3, wil = 4 },
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
            var catalog = new CharacterCatalog();   // 이름 → 정의(정비 화면 등 캐릭터가 없을 때 정보 표시용)
            foreach (var d in characterDefs)
                if (!string.IsNullOrEmpty(d.recipe))
                {
                    var def = CharacterDefinition.Default;
                    def.DisplayName = string.IsNullOrEmpty(d.displayName) ? d.recipe : d.displayName;
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
                    def.BasicSkill = d.basic;
                    def.Cleave = d.cleave;
                    var stats = new PrimaryStats(d.str, d.agi, d.intel, d.wil);
                    if (!stats.IsZero) def.Stats = stats;
                    var growth = new PrimaryStats(d.gStr, d.gAgi, d.gInt, d.gWil);
                    if (!growth.IsZero) def.Growth = growth;
                    def.TraitText = d.traitText;
                    def.Trait = new TraitComponent
                    {
                        HpMul = d.hpMul > 0f ? d.hpMul : 1f,
                        DamageMul = d.dmgMul > 0f ? d.dmgMul : 1f,
                        MoveSpeedMul = d.spdMul > 0f ? d.spdMul : 1f,
                        AttackSpeedMul = d.atkSpdMul > 0f ? d.atkSpdMul : 1f,
                        KnockbackResistAdd = d.kbResist,
                        Thorns = d.thorns,
                        Lifesteal = d.lifesteal,
                        PoisonPerSec = d.poison,
                        PoisonDuration = d.poisonTime
                    };
                    def.MeleeKnockback = d.knockback;
                    def.MeleeLaunch = d.launch;
                    def.MeleeHitStun = d.hitStun;
                    def.SlamRadius = d.slamRadius;
                    def.SlamCharge = d.slamCharge > 0f ? d.slamCharge : 1.3f;
                    def.SlamCooldown = d.slamCd > 0f ? d.slamCd : 9f;
                    def.SlamDamageMul = d.slamDmg > 0f ? d.slamDmg : 2f;
                    def.SlamStopRequired = d.slamStop > 0 ? d.slamStop : 10;
                    def.ReviveCharges = d.revive;
                    def.ReviveStopRequired = d.reviveStop > 0 ? d.reviveStop : 10;
                    def.BossGem = string.IsNullOrEmpty(d.bossGem) ? 0 : IntoTheDungeon.Features.Run.GemPool.IndexOf(d.bossGem) + 1;   // 번호 + 1(0 = 없음)
                    def.DropGem = string.IsNullOrEmpty(d.dropGem) ? 0 : IntoTheDungeon.Features.Run.GemPool.IndexOf(d.dropGem) + 1;
                    def.DropGemChance = d.dropGemChance;
                    definitions[recipeTable.GetId(d.recipe)] = def;
                    catalog.Set(d.recipe, def);
                }
            var characterFactory = new CharacterCoreFactory(definitions);
            world.SetOnce(catalog);
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
            world.SystemManager.AddUnique(new BasicSkillSystem());     // 기본 스킬: 재사용 대기·마나가 되면 AI가 알아서
            world.SystemManager.AddUnique(new TauntSystem());          // 도발·방패 남은 시간
            world.SystemManager.AddUnique(new CombatMeterSystem());    // 딜 미터 전투 시간
            world.SystemManager.AddUnique(new BossSystem());           // 보스 내려찍기: 힘 모으기(예고) → 범위 공격

            // 전투: 히트박스 생성 → 위치 갱신 → 판정 → HP 적용 → 사망 상태 → 수명 정리
            world.SystemManager.AddUnique(new AttackSystem());
            world.SystemManager.AddUnique(new FollowOwnerSystem());
            world.SystemManager.AddUnique(new ProjectileMotionSystem());   // 검기 등 날아가는 히트박스
            world.SystemManager.AddUnique(new HitDetectSystem());
            world.SystemManager.AddUnique(new PoisonSystem());          // 독 지속 피해(피해 처리 전에 큐에 넣음)
            world.SystemManager.AddUnique(new DamageProcessingSystem());
            world.SystemManager.AddUnique(new DeathStateSystem());
            world.SystemManager.AddUnique(new ReviveSystem());      // 해골 기사: 무너졌다 다시 일어남·필살기로 부숨(보상·드랍·시체 정리보다 먼저)
            world.SystemManager.AddUnique(new LootDropSystem());    // 쓰러진 적이 확률로 아이템을 떨굼(시체가 지워지기 전에)
            world.SystemManager.AddUnique(new KillRewardSystem());  // 처치마다 경험치·골드, 전투 중 레벨 업
            world.SystemManager.AddUnique(new StatRegenSystem());   // 초당 체력·마나 회복(힘·지능)
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
