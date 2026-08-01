using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GAS.Runtime;
using NUnit.Framework;
using MoreMountains.Feedbacks;
using Unity.Collections;
using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using DotEntity = global::Unity.Entities.Entity;
using DotEntityManager = global::Unity.Entities.EntityManager;
using DotEntityQuery = global::Unity.Entities.EntityQuery;
using DotComponentType = global::Unity.Entities.ComponentType;

namespace FantasyWord.GameCore.Tests
{
    public sealed class MeleeAttackAbilityEditModeTests
    {
        private const int BasicAttackAbilityCode = XAbility.ABILITY_Attack;
        private const int TransformReplaceAbilityCode = XAbility.ABILITY_TransformReplaceSmoke;
        private const int ChargedAttackReleaseAbilityCode = XAbility.ABILITY_ChargedAttackRelease;
        private const float EditModeTickDeltaTime = 1.0f / 30.0f;

        [Serializable]
        private sealed class NullAnimationStrategy : IAnimationStrategy
        {
            public void AddDeathAnimationStartedListener(UnityEngine.Events.UnityAction listener) { }
            public void RemoveDeathAnimationStartedListener(UnityEngine.Events.UnityAction listener) { }
            public void AddDeathAnimationEndedListener(UnityEngine.Events.UnityAction listener) { }
            public void RemoveDeathAnimationEndedListener(UnityEngine.Events.UnityAction listener) { }
            public void Initialize() { }
            public void Pause() { }
            public void Resume() { }
            public void OnInvincibleAnimationStart() { }
            public void OnInvincibleAnimationStop() { }
            public void OnDeathAnimationStart() { }
            public void OnDeathAnimationStop() { }
            public void SetLookAtDirection(Vector2 direction) { }
            public void SetTargetDirection(Vector2 direction) { }
            public void SetMovement(Vector2 speed) { }
            public bool PlayHitAnimation() => false;
            public bool PlayDeathAnimation() => false;
            public bool PlayInvincibleAnimation() => false;
            public bool IsInvincibleAnimationPlaying() => false;
        }

        [Serializable]
        private sealed class TestMeleeFeedbackProbe : MonoBehaviour
        {
            public int playCount;

            public void HandlePlay()
            {
                playCount++;
            }
        }

        private readonly System.Collections.Generic.List<UnityEngine.Object> m_createdObjects = new();

        [SetUp]
        public void SetUp()
        {
            GasEditModeTestHelper.ResetWorld();
            CreateGameManagerWithMinimalConfig();
        }

        [TearDown]
        public void TearDown()
        {
            SetStaticField(typeof(GameManager), "_instance", null);

            for (int i = m_createdObjects.Count - 1; i >= 0; i--)
            {
                if (m_createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(m_createdObjects[i]);
                }
            }

            m_createdObjects.Clear();
            GasEditModeTestHelper.ShutdownWorld();

        }

        [Test]
        public void FormalGasAttackDescription_UsesGasTableWithoutRuntimeGameManager()
        {
            SetStaticField(typeof(GameManager), "_instance", null);
            RegisterFormalGasAbilityDescriptionGeneratedRuntime();
            List<AbilityDescriptionLine> lines = new();

            Assert.DoesNotThrow(
                () => FormalGasAbilityDescriptionResolver.TryAppendFormalDamageLines(BasicAttackAbilityCode, lines),
                "基础攻击 GAS 描述应能在编辑器资产检查时生成，不能依赖运行中的 GameManager。");

            string joinedLines = string.Join(" | ", lines.ConvertAll(line => $"{line.header}:{line.content}"));
            Assert.That(joinedLines, Does.Contain("造成伤害:4 固定伤害+1 属性缩放伤害 物理"), "基础攻击描述应读取 EX-GAS GameplayEffect 里的正式基础伤害。");
            Assert.That(joinedLines, Does.Contain("造成伤害:3 固定伤害 物理"), "基础攻击描述应读取 EX-GAS GameplayEffect 里的正式背刺附加伤害。");
            Assert.That(joinedLines, Does.Not.Contain("[INVALID_SHORTNAME]"), "策划可见描述不能显示内部术语占位符。");
        }


        [Test]
        public void FormalGasQuickSlotSave_WritesGasCodeWithoutLegacyAbilityReference()
        {
            CharacterActor owner = CreateCharacter("owner", Vector2.zero, CreateStats(health: 30));
            GrantBasicAttack(owner);

            Assert.IsTrue(owner.TryEquipFormalGasAbilityCodeToSlot(BasicAttackAbilityCode, 0), "测试前应能只凭 EX-GAS Ability Code 把基础攻击装入快捷槽。");

            CharacterAbilitySlotData[] slots = owner.CreateEquippedAbilitySlotDataSnapshot(GameManager.Database);

            Assert.IsNotEmpty(slots, "快捷槽保存快照不能为空。");
            Assert.AreEqual(0, slots[0].slotIndex, "第一个快捷槽索引不符合预期。");
            Assert.AreEqual(BasicAttackAbilityCode, slots[0].formalGasAbilityCode, "已迁移基础攻击快捷槽保存必须写 EX-GAS Ability Code。");
            Assert.IsNull(
                typeof(CharacterAbilitySlotData).GetField("ability"),
                "已迁移基础攻击快捷槽保存不应再保留旧 旧主动能力表 引用字段，否则读档仍有第二套身份真相。");
        }

        [Test]
        public void FormalGasQuickSlotEquip_UsesGasCodeAsSlotTruth()
        {
            CharacterActor owner = CreateCharacter("owner", Vector2.zero, CreateStats(health: 30));
            GrantBasicAttack(owner);

            Assert.IsTrue(owner.TryEquipFormalGasAbilityCodeToSlot(BasicAttackAbilityCode, 0), "已迁移基础攻击应能直接按 EX-GAS Ability Code 装入快捷槽。");

            CharacterEquippedAbilitySlotView[] slots = owner.GetEquippedAbilitySlotViewSnapshots();

            Assert.IsNotEmpty(slots, "快捷槽展示快照不能为空。");
            Assert.AreEqual(BasicAttackAbilityCode, slots[0].FormalGasAbilityCode, "已迁移基础攻击快捷槽展示真相必须是 EX-GAS Ability Code。");
            Assert.IsNull(typeof(CharacterEquippedAbilitySlotView).GetProperty("LegacySheet"), "按 EX-GAS Ability Code 装槽时不应把旧 旧主动能力表 继续塞进槽位展示真相。");
            Assert.AreEqual("Attack", slots[0].DisplayName, "按 EX-GAS Ability Code 装槽后仍应从 EX-GAS Ability 表读取显示名。");
        }

        [Test]
        public void CharacterActorRuntimeLoad_MissingQuickSlots_ClearsExistingQuickSlot()
        {
            CharacterActor owner = CreateCharacter("quick-slot-restore-owner", Vector2.zero, CreateStats(health: 30));
            GrantBasicAttack(owner);
            Assert.IsTrue(owner.TryEquipFormalGasAbilityCodeToSlot(BasicAttackAbilityCode, 0), "测试前必须先构造一个读档前残留快捷槽。");

            CharacterActorRuntimeStateData runtimeState = owner.CreateActorRuntimeState();
            runtimeState.quickAbilitySlots = null;

            RegisterPlayerSystemForAlterationTest(owner);
            owner.LoadActorRuntimeState(runtimeState);

            CharacterEquippedAbilitySlotView[] slots = owner.GetEquippedAbilitySlotViewSnapshots();
            Assert.IsNotEmpty(slots, "角色快捷槽视图数量应保持配置长度。");
            Assert.AreEqual(
                0,
                slots[0].FormalGasAbilityCode,
                "存档没有快捷槽数据时，读档结果必须清空读档前槽位，不能沿用旧运行时状态。");
            Assert.IsTrue(owner.HasFormalGasAbility(BasicAttackAbilityCode), "读档只应清空快捷槽布局，不能移除角色仍然拥有的正式能力。");
        }


        [Test]
        public void FormalGasQuickSlotCooldownSnapshot_UsesGasCodeWithoutLegacySheetReference()
        {
            CharacterActor owner = CreateCharacter("owner", Vector2.zero, CreateStats(health: 30));
            GrantBasicAttack(owner);

            Assert.IsTrue(owner.TryEquipFormalGasAbilityCodeToSlot(BasicAttackAbilityCode, 0), "测试前应能只凭 EX-GAS Ability Code 把基础攻击装入快捷槽。");
            CharacterEquippedAbilitySlotView[] slots = owner.GetEquippedAbilitySlotViewSnapshots();

            Assert.IsNotEmpty(slots, "快捷槽展示快照不能为空。");
            Assert.IsTrue(
                owner.TryGetActiveAbilityCooldownSnapshot(slots[0], out CharacterAbilityCooldownSnapshot snapshot),
                "已迁移基础攻击的冷却展示应能直接按 EX-GAS Ability Code 查询。");
            Assert.AreEqual(BasicAttackAbilityCode, snapshot.FormalGasAbilityCode, "已迁移基础攻击的冷却展示身份必须是 EX-GAS Ability Code。");
            Assert.IsNull(typeof(CharacterAbilityCooldownSnapshot).GetProperty("LegacySheet"), "已迁移基础攻击的冷却展示快照不应继续夹带旧 旧主动能力表。");
            Assert.IsTrue(snapshot.HasFormalGasAbility, "冷却展示快照应明确标记为正式 GAS 能力。");
            Assert.IsNull(typeof(CharacterAbilityCooldownSnapshot).GetProperty("HasLegacySheet"), "冷却展示快照不能同时标记为旧 旧能力表。");
        }

        [Test]
        public void FormalGasActiveAbilitySnapshot_DoesNotExposeLegacySheetForMigratedAbility()
        {
            CharacterActor owner = CreateCharacter("owner", Vector2.zero, CreateStats(health: 30));
            GrantBasicAttack(owner);

            Assert.IsNull(
                typeof(CharacterBase).GetMethod("CreateOwnedLegacy旧能力表Snapshot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                "彻底迁到 EX-GAS 后，不应继续保留旧 旧能力表 快照入口。");
            Assert.IsTrue(
                Array.Exists(owner.GetActiveAbilityMenuEntrySnapshots(), entry => entry.FormalGasAbilityCode == BasicAttackAbilityCode),
                "已迁移基础攻击仍应通过 GAS 菜单投影暴露，且菜单条目不能夹带旧 旧能力表。");
        }

        [Test]
        public void CharacterSheet_LegacyAbilityMap_IsRemovedFromFormalAbilityFlow()
        {
            Assert.IsNull(
                typeof(CharacterSheet).GetField("m_legacyAbilitiesPerLevel", BindingFlags.Instance | BindingFlags.NonPublic),
                "角色出生能力正式真相应只剩 EX-GAS code 表，不应继续保留旧 旧能力表 解锁表。");
            Assert.IsNull(
                typeof(CharacterSheet).GetMethod("GetAvailableLegacyAbilitiesAtLevel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                "角色出生能力不应继续暴露旧 旧能力表 查询入口。");
            Assert.IsNull(
                typeof(CharacterSheet).GetMethod("GetLegacyAbilitiesUnlockedAtLevel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                "角色升级解锁不应继续暴露旧 旧能力表 查询入口。");
        }

        [Test]
        public void LegacyBonusAbilityApi_IsRemovedFromCharacterRuntime()
        {
            Assert.IsFalse(
                Array.Exists(typeof(CharacterBase).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), method =>
                    (method.Name is "AddBonusAbility" or "RemoveBonusAbility" or "HasAbility" or "IsAbilitySuppressed") &&
                    Array.Exists(method.GetParameters(), HasLegacyAbilitySheetParameter)),
                "角色运行时不应继续暴露以 旧能力表 为参数的授予、移除、查询或压制入口。");
            Assert.IsNull(typeof(CharacterAbilityMenuEntry).GetProperty("LegacySheet"), "菜单投影不应再暴露旧 旧能力表。");
        }

        [Test]
        public void CharacterAlterationRule_LegacyAbilityFields_AreRemoved()
        {
            Assert.IsNull(
                typeof(CharacterAlterationRule).GetField("m_legacyGrantedAbilities", BindingFlags.Instance | BindingFlags.NonPublic),
                "变形/感染规则授予能力应只保存 EX-GAS code，不应继续保留旧 旧能力表 授予字段。");
            Assert.IsNull(
                typeof(CharacterAlterationRule).GetField("m_legacySuppressedAbilities", BindingFlags.Instance | BindingFlags.NonPublic),
                "变形/感染规则压制能力应只保存 EX-GAS code，不应继续保留旧 旧能力表 压制字段。");
        }

        [Test]
        public void CharacterAlterationRule_InvalidFormalGasAbilityCode_ThrowsBeforeStateChange()
        {
            CharacterActor owner = CreateCharacter("alteration-invalid-code-owner", Vector2.zero, CreateStats(health: 30));
            CharacterAlterationRule rule = CreateRegisteredCharacterAlterationRule(
                "invalid-formal-gas-alteration",
                "test-invalid-formal-gas-alteration");
            SetInstanceField(rule, "m_grantedFormalGasAbilityCodes", new[] { 0 });
            SetInstanceField(rule, "m_lockedActions", EActionFlags.Move);

            Assert.IsTrue(owner.Can(EActionFlags.Move), "测试前角色必须仍可移动。");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => owner.ApplyCharacterAlterationRule(rule, GameManager.Database),
                "变形/感染规则里真实填出的 Formal GAS 技能编号小于等于 0 时，不能被过滤成成功状态变化。");

            Assert.That(exception.Message, Does.Contain("必须大于 0"));
            Assert.IsTrue(
                owner.Can(EActionFlags.Move),
                "坏能力编号必须在动作锁等非能力状态改变前被拦住。");
        }

        [Test]
        public void CharacterAlterationRule_EmptyFormalGasAbilityLists_AllowNonAbilityStateRule()
        {
            CharacterActor owner = CreateCharacter("alteration-empty-ability-owner", Vector2.zero, CreateStats(health: 30));
            RegisterPlayerSystemForAlterationTest(owner);
            CharacterAlterationRule rule = CreateRegisteredCharacterAlterationRule(
                "empty-formal-gas-alteration",
                "test-empty-formal-gas-alteration");
            SetInstanceField(rule, "m_grantedFormalGasAbilityCodes", Array.Empty<int>());
            SetInstanceField(rule, "m_suppressedFormalGasAbilityCodes", Array.Empty<int>());
            SetInstanceField(rule, "m_lockedActions", EActionFlags.Move);

            Assert.IsTrue(
                owner.ApplyCharacterAlterationRule(rule, GameManager.Database),
                "空能力列表表示该规则不改能力，仍应允许动作锁、AI 接管、阵营覆盖等非能力状态生效。");
            Assert.IsFalse(owner.Can(EActionFlags.Move), "非能力状态规则应成功锁定移动。");

            Assert.IsTrue(owner.RemoveCharacterAlterationRule(rule, GameManager.Database));
            Assert.IsTrue(owner.Can(EActionFlags.Move), "撤回规则后移动动作锁应解除。");
        }

        [Test]
        public void CharacterAlterationRuleRuntimeLoad_RestoresNonAbilityStateAndCanRevoke()
        {
            const string ruleKey = "test-non-ability-alteration-runtime-restore";
            CharacterActor owner = CreateCharacter("alteration-non-ability-save-owner", Vector2.zero, CreateStats(health: 30));
            RegisterPlayerSystemForAlterationTest(owner);
            CharacterAlterationRule rule = CreateRegisteredCharacterAlterationRule(
                "non-ability-alteration-runtime-restore",
                ruleKey);
            SetInstanceField(rule, "m_grantedFormalGasAbilityCodes", Array.Empty<int>());
            SetInstanceField(rule, "m_suppressedFormalGasAbilityCodes", Array.Empty<int>());
            SetInstanceField(rule, "m_lockedActions", EActionFlags.Move);
            SetInstanceField(rule, "m_lockPlayerControl", true);

            Assert.IsTrue(owner.ApplyCharacterAlterationRule(rule, GameManager.Database));
            CharacterActorRuntimeStateData runtimeState = owner.CreateActorRuntimeState();
            Assert.AreEqual(1, runtimeState.activeAlterationRules.Length, "活跃变形/感染规则必须保存数据库稳定引用。");
            Assert.AreEqual(0, CountRuntimeAbilitySourceStacks(runtimeState, BasicAttackAbilityCode, ECharacterAbilitySourceKind.Transformation, ruleKey), "纯非能力规则不应伪造能力来源。");

            CharacterActor loadedOwner = CreateCharacter("alteration-non-ability-load-owner", Vector2.zero, CreateStats(health: 30));
            RegisterPlayerSystemForAlterationTest(loadedOwner);
            loadedOwner.LoadActorRuntimeState(runtimeState);

            Assert.IsFalse(loadedOwner.Can(EActionFlags.Move), "读档必须恢复变形/感染规则带来的非能力动作锁。");
            Assert.IsFalse(loadedOwner.CanBePlayerControlled(), "读档必须恢复变形/感染规则带来的玩家控制锁。");

            Assert.IsTrue(loadedOwner.RemoveCharacterAlterationRule(rule, GameManager.Database));
            Assert.IsTrue(loadedOwner.Can(EActionFlags.Move), "撤回读档恢复的规则后，非能力动作锁必须解除。");
            Assert.IsTrue(loadedOwner.CanBePlayerControlled(), "撤回读档恢复的规则后，玩家控制锁必须解除。");
        }

        [Test]
        public void CharacterAlterationRuleRuntimeLoad_DoesNotDuplicateAbilitySourceAndCanRevoke()
        {
            const string ruleKey = "test-ability-alteration-runtime-restore";
            CharacterActor owner = CreateCharacter("alteration-ability-save-owner", Vector2.zero, CreateStats(health: 30));
            RegisterPlayerSystemForAlterationTest(owner);
            CharacterAlterationRule rule = CreateRegisteredCharacterAlterationRule(
                "ability-alteration-runtime-restore",
                ruleKey);
            SetInstanceField(rule, "m_grantedFormalGasAbilityCodes", new[] { BasicAttackAbilityCode });
            SetInstanceField(rule, "m_lockedActions", EActionFlags.Move);

            Assert.IsTrue(owner.ApplyCharacterAlterationRule(rule, GameManager.Database));
            Assert.IsTrue(owner.HasFormalGasAbility(BasicAttackAbilityCode), "测试前规则必须授予 EX-GAS 能力。");
            CharacterActorRuntimeStateData runtimeState = owner.CreateActorRuntimeState();
            Assert.AreEqual(1, CountRuntimeAbilitySourceStacks(runtimeState, BasicAttackAbilityCode, ECharacterAbilitySourceKind.Transformation, ruleKey), "保存时能力来源应只有规则本身一层。");

            CharacterActor loadedOwner = CreateCharacter("alteration-ability-load-owner", Vector2.zero, CreateStats(health: 30));
            RegisterPlayerSystemForAlterationTest(loadedOwner);
            loadedOwner.LoadActorRuntimeState(runtimeState);

            Assert.IsTrue(loadedOwner.HasFormalGasAbility(BasicAttackAbilityCode), "读档必须从能力来源记录恢复规则授予的 EX-GAS 能力。");
            Assert.IsFalse(loadedOwner.Can(EActionFlags.Move), "读档必须同时恢复规则的非能力状态。");
            CharacterActorRuntimeStateData restoredState = loadedOwner.CreateActorRuntimeState();
            Assert.AreEqual(1, CountRuntimeAbilitySourceStacks(restoredState, BasicAttackAbilityCode, ECharacterAbilitySourceKind.Transformation, ruleKey), "读档恢复 activeAlterationRules 只能恢复非能力状态，不能把能力来源重复叠一层。");

            Assert.IsTrue(loadedOwner.RemoveCharacterAlterationRule(rule, GameManager.Database));
            Assert.IsFalse(loadedOwner.HasFormalGasAbility(BasicAttackAbilityCode), "撤回读档恢复的规则后，规则授予的 EX-GAS 能力必须移除。");
            Assert.IsTrue(loadedOwner.Can(EActionFlags.Move), "撤回读档恢复的规则后，非能力动作锁必须解除。");
        }


        [Test]
        public void PlayerAbilityFireFailedEvent_OnlyExposesFormalGasCode()
        {
            PlayerAbilityFireFailedEvent evt = new(BasicAttackAbilityCode, EAbilityFireCheckResult.OnCooldown);

            Assert.AreEqual(BasicAttackAbilityCode, evt.FormalGasAbilityCode, "失败事件只应暴露正式 EX-GAS Ability Code。");
            Assert.IsTrue(evt.HasFormalGasAbility, "失败事件应明确标记正式 EX-GAS 能力。");
            Assert.IsNull(typeof(PlayerAbilityFireFailedEvent).GetProperty("Ability"), "失败事件不应继续携带旧 旧能力表。");
            Assert.AreEqual(EAbilityFireCheckResult.OnCooldown, evt.Reason);
        }

        [Test]
        public void CharacterAbilityInventoryEvents_OnlyExposeFormalGasCode()
        {
            CharacterActor owner = CreateCharacter("owner", Vector2.zero, CreateStats(health: 30));

            CharacterAbilityAddedEvent addedEvent = new(owner, BasicAttackAbilityCode);
            CharacterAbilityRemovedEvent removedEvent = new(owner, BasicAttackAbilityCode);

            Assert.AreEqual(BasicAttackAbilityCode, addedEvent.FormalGasAbilityCode, "角色获得能力事件必须暴露正式 EX-GAS Ability Code。");
            Assert.AreEqual(BasicAttackAbilityCode, removedEvent.FormalGasAbilityCode, "角色失去能力事件必须暴露正式 EX-GAS Ability Code。");
            Assert.IsNull(typeof(CharacterAbilityAddedEvent).GetProperty("Ability"), "角色获得能力事件不应继续携带旧 旧能力表。");
            Assert.IsNull(typeof(CharacterAbilityRemovedEvent).GetProperty("Ability"), "角色失去能力事件不应继续携带旧 旧能力表。");
        }

        [Test]
        public void AddOrRemoveAbility_UsesFormalGasAbilityCodeWithoutLegacySheetReference()
        {
            CharacterActor owner = CreateCharacter("owner", Vector2.zero, CreateStats(health: 30));
            AddOrRemoveAbility command = new();
            SetInstanceField(command, "m_formalGasAbilityCode", BasicAttackAbilityCode);
            Assert.IsNull(FindInstanceField(typeof(AddOrRemoveAbility), "m_abilitySheet"), "脚本授予能力命令不应再保留旧能力表字段。");

            command.Execute(GameCommandContext.Script(owner, "formal-gas-test")).GetAwaiter().GetResult();

            Assert.IsTrue(owner.HasFormalGasAbility(BasicAttackAbilityCode), "脚本授予能力入口应能只凭 EX-GAS Ability Code 授予基础攻击，不应要求旧 旧主动能力表 字段。");
            Assert.IsTrue(
                Array.Exists(owner.GetActiveAbilityMenuEntrySnapshots(), entry =>
                    entry.FormalGasAbilityCode == BasicAttackAbilityCode &&
                    entry.HasFormalGasAbility),
                "脚本授予后的已迁移能力必须通过 GAS code 菜单投影暴露，不能夹带旧 旧能力表。");

            CharacterRuntimeStateData runtimeState = owner.CreateRuntimeState();
            Assert.IsTrue(
                Array.Exists(runtimeState.abilitySources, source =>
                    source != null &&
                    source.formalGasAbilityCode == BasicAttackAbilityCode),
                "脚本授予后的已迁移能力来源保存必须只写 EX-GAS Ability Code。");
            Assert.IsNull(typeof(CharacterAbilitySourceData).GetField("ability"), "能力来源保存模型不应继续保留旧 旧能力表 引用字段。");
            Assert.IsTrue(
                Array.Exists(runtimeState.abilityRuntimeStates, state =>
                    state != null &&
                    state.formalGasAbilityCode == BasicAttackAbilityCode),
                "脚本授予后的已迁移能力运行实例保存必须只写 EX-GAS Ability Code。");
            Assert.IsNull(typeof(CharacterAbilityRuntimeStateData).GetField("sheet"), "能力运行状态保存模型不应继续保留旧 旧能力表 引用字段。");
        }


        [Test]
        public void FormalGasAbilitySourceAndRuntimeState_SaveGasCodeWithoutLegacySheetReference()
        {
            CharacterActor owner = CreateCharacter("owner", Vector2.zero, CreateStats(health: 30));

            Assert.IsTrue(
                owner.AddBonusFormalGasAbility(BasicAttackAbilityCode, CharacterAbilitySourceKey.Script),
                "正式能力来源应能只凭 EX-GAS Ability Code 授予基础攻击。");

            CharacterRuntimeStateData runtimeState = owner.CreateRuntimeState();

            Assert.IsNotNull(runtimeState.abilitySources, "角色运行时状态必须保存能力来源。");
            Assert.IsNotEmpty(runtimeState.abilitySources, "GAS code 授予的能力来源不应丢失。");
            Assert.AreEqual(BasicAttackAbilityCode, runtimeState.abilitySources[0].formalGasAbilityCode, "新能力来源保存必须写 EX-GAS Ability Code。");
            Assert.IsNull(typeof(CharacterAbilitySourceData).GetField("ability"), "新能力来源保存不应再同时写旧 旧能力表 引用。");
            Assert.IsNotNull(runtimeState.abilityRuntimeStates, "角色运行时状态必须保存能力实例状态。");
            Assert.IsTrue(
                Array.Exists(runtimeState.abilityRuntimeStates, state =>
                    state != null &&
                    state.formalGasAbilityCode == BasicAttackAbilityCode),
                "已迁移能力的运行时状态必须按 EX-GAS Ability Code 保存，旧 旧能力表 引用只能服务旧数据兜底。");
            Assert.IsNull(typeof(CharacterAbilityRuntimeStateData).GetField("sheet"), "能力运行状态保存模型不应继续保留旧 旧能力表 引用字段。");
        }

        [Test]
        public void ItemAddAbilityEffect_UsesFormalGasAbilityCodeWithoutLegacySheetReference()
        {
            CharacterActor owner = CreateCharacter("owner", Vector2.zero, CreateStats(health: 30));
            ItemAddAbilityEffect effect = new();
            SetInstanceField(effect, "m_formalGasAbilityCode", BasicAttackAbilityCode);
            Assert.IsNull(FindInstanceField(typeof(ItemAddAbilityEffect), "m_ability"), "道具授予能力效果不应再保留旧能力表字段。");
            Item item = CreateRegisteredItem("formal-gas-ability-item", "test-formal-gas-ability-item");

            ItemUsageResult result = InvokeItemAddAbilityEffect(effect, item, owner, owner, EItemLocation.Bag);

            Assert.IsTrue(result.success, "道具授予能力入口应能只凭 EX-GAS Ability Code 授予基础攻击。");
            Assert.IsTrue(owner.HasFormalGasAbility(BasicAttackAbilityCode), "道具授予后角色应持有对应 EX-GAS Ability。");
        }


        [Test]
        public void EquipmentBonusAbility_UsesFormalGasAbilityCodeWithoutLegacySheetReference()
        {
            CharacterActor owner = CreateCharacter("owner", Vector2.zero, CreateStats(health: 30));
            CharacterEquipment characterEquipment = owner.gameObject.AddComponent<CharacterEquipment>();
            SetInstanceField(characterEquipment, "m_character", owner);
            InvokeLifecycle(characterEquipment, "Awake");

            Equipment equipment = ScriptableObject.CreateInstance<Equipment>();
            equipment.name = "formal-gas-equipment";
            m_createdObjects.Add(equipment);
            SetInstanceField(equipment, "m_type", EEquipmentType.Weapon);
            SetInstanceField(equipment, "m_formalGasAbilityCodes", new[] { BasicAttackAbilityCode });
            Assert.IsNull(FindInstanceField(typeof(Equipment), "m_legacyBonusAbilities"), "装备不应再保留旧能力表授予字段。");
            RegisterRuntimeDatabaseEntry(equipment, "test-equipment-formal-gas");

            EEquipmentOperationResult result = characterEquipment.TryEquip(equipment, out Equipment previousEquipment);

            Assert.AreEqual(EEquipmentOperationResult.Valid, result, "装备授予能力入口应接受只配置 EX-GAS Ability Code 的装备。");
            Assert.IsNull(previousEquipment, "第一次装备武器不应替换掉旧装备。");
            Assert.IsTrue(owner.HasFormalGasAbility(BasicAttackAbilityCode), "装备授予后角色应持有对应 EX-GAS Ability。");

            CharacterRuntimeStateData runtimeState = owner.CreateRuntimeState();
            Assert.IsTrue(
                Array.Exists(runtimeState.abilitySources, source =>
                    source != null &&
                    source.formalGasAbilityCode == BasicAttackAbilityCode),
                "装备授予的已迁移能力保存时必须写 EX-GAS Ability Code，不能再同时写旧 旧能力表 引用。");
            Assert.IsNull(typeof(CharacterAbilitySourceData).GetField("ability"), "装备授予能力来源保存模型不应继续保留旧 旧能力表 引用字段。");
        }

        [Test]
        public void CharacterActorRuntimeLoad_MissingEquipmentSlots_ClearsExistingEquipment()
        {
            CharacterActor owner = CreateCharacter("equipment-restore-owner", Vector2.zero, CreateStats(health: 30));
            CharacterEquipment characterEquipment = owner.gameObject.AddComponent<CharacterEquipment>();
            SetInstanceField(characterEquipment, "m_character", owner);
            InvokeLifecycle(characterEquipment, "Awake");

            Equipment equipment = ScriptableObject.CreateInstance<Equipment>();
            equipment.name = "restore-cleared-equipment";
            m_createdObjects.Add(equipment);
            SetInstanceField(equipment, "m_type", EEquipmentType.Weapon);
            RegisterRuntimeDatabaseEntry(equipment, "test-restore-cleared-equipment");
            Assert.AreEqual(
                EEquipmentOperationResult.Valid,
                characterEquipment.TryEquip(equipment, out _),
                "测试前必须先构造一个读档前残留装备。");
            Assert.IsTrue(characterEquipment.TryGetEquipment(EEquipmentType.Weapon, out _), "测试前武器槽必须已穿装备。");

            CharacterActorRuntimeStateData runtimeState = owner.CreateActorRuntimeState();
            runtimeState.equipmentSlots = null;
            runtimeState.abilitySources = Array.Empty<CharacterAbilitySourceData>();
            runtimeState.abilitySuppressions = Array.Empty<CharacterAbilitySourceData>();

            RegisterPlayerSystemForAlterationTest(owner);
            owner.LoadActorRuntimeState(runtimeState);

            Assert.IsFalse(
                characterEquipment.TryGetEquipment(EEquipmentType.Weapon, out _),
                "存档没有装备槽数据时，读档结果必须清空读档前装备，不能沿用 Prefab 或旧运行时槽位。");
        }


        [Test]
        public void CharacterSheetInitialAbility_UsesFormalGasAbilityCodeWithoutLegacySheetReference()
        {
            CharacterActor owner = CreateCharacter(
                "owner",
                Vector2.zero,
                CreateStats(health: 30),
                initializeAbilities: false);
            SetCharacterFormalGasAbilityUnlock(owner.characterSheet, BasicAttackAbilityCode, level: 1);

            InvokeStaticLifecycle(owner, "InitializeAbilities");

            Assert.IsTrue(owner.HasFormalGasAbility(BasicAttackAbilityCode), "角色出生能力入口应能只凭 EX-GAS Ability Code 授予基础攻击。");
            CharacterRuntimeStateData runtimeState = owner.CreateRuntimeState();
            Assert.IsTrue(
                Array.Exists(runtimeState.abilityRuntimeStates, state =>
                    state != null &&
                    state.formalGasAbilityCode == BasicAttackAbilityCode),
                "出生授予的已迁移能力运行时状态必须按 EX-GAS Ability Code 保存，不能再写旧 旧能力表 引用。");
            Assert.IsNull(typeof(CharacterAbilityRuntimeStateData).GetField("sheet"), "出生授予能力运行状态保存模型不应继续保留旧 旧能力表 引用字段。");
        }

        [Test]
        public void CharacterAbilitySetAdditionalAbility_UsesFormalGasAbilityCodeWithoutLegacySheetReference()
        {
            CharacterActor owner = CreateCharacter(
                "owner",
                Vector2.zero,
                CreateStats(health: 30),
                initializeAbilities: false);
            CharacterAbilitySet abilitySet = owner.GetComponent<CharacterAbilitySet>();
            SetInstanceField(abilitySet, "m_additionalFormalGasAbilityCodes", new[] { BasicAttackAbilityCode });

            InvokeStaticLifecycle(owner, "InitializeAbilities");

            Assert.IsTrue(owner.HasFormalGasAbility(BasicAttackAbilityCode), "角色组件附加能力入口应能只凭 EX-GAS Ability Code 授予基础攻击。");
            CharacterRuntimeStateData runtimeState = owner.CreateRuntimeState();
            Assert.IsTrue(
                Array.Exists(runtimeState.abilityRuntimeStates, state =>
                    state != null &&
                    state.formalGasAbilityCode == BasicAttackAbilityCode),
                "角色组件附加能力的已迁移能力运行时状态必须按 EX-GAS Ability Code 保存，不能再写旧 旧能力表 引用。");
            Assert.IsNull(typeof(CharacterAbilityRuntimeStateData).GetField("sheet"), "角色组件附加能力运行状态保存模型不应继续保留旧 旧能力表 引用字段。");
        }

        [Test]
        public void FormalGasAttackDescription_DoesNotUseLegacy旧能力表CostOrCooldown()
        {
            SetStaticField(typeof(GameManager), "_instance", null);
            RegisterFormalGasAbilityDescriptionGeneratedRuntime();
            List<AbilityDescriptionLine> lines = new();

            Assert.IsTrue(
                FormalGasAbilityDescriptionResolver.TryAppendFormalDamageLines(BasicAttackAbilityCode, lines),
                "基础攻击正式描述必须直接读取 EX-GAS GameplayEffect 表。");

            string joinedLines = string.Join(" | ", lines.ConvertAll(line => $"{line.header}:{line.content}"));
            Assert.That(joinedLines, Does.Contain("造成伤害:4 固定伤害+1 属性缩放伤害 物理"), "基础攻击描述仍应读取 EX-GAS GameplayEffect 里的正式基础伤害。");
            Assert.IsFalse(lines.Exists(line => line.content == "5"), "已绑定 EX-GAS 的基础攻击不应再把 项目侧旧能力表 manaCost 显示成正式消耗描述。");
            Assert.IsFalse(lines.Exists(line => line.content == "9s"), "已绑定 EX-GAS 的基础攻击不应再把 项目侧旧能力表 cooldown 显示成正式冷却描述。");
        }

        [Test]
        public void FormalGasBasicAttackGameplayEffect_DisablesDefaultPush()
        {
            string gameplayEffectJson =
                File.ReadAllText("Assets/DataGenerated/Luban/Json/GAS/exgas_tbgameplayeffect.json");
            int effectStart = gameplayEffectJson.IndexOf("\"ID\": 2003", StringComparison.Ordinal);
            int nextEffectStart = gameplayEffectJson.IndexOf("\"ID\": 2004", effectStart + 1, StringComparison.Ordinal);

            Assert.GreaterOrEqual(effectStart, 0, "基础攻击必须保留正式 GameplayEffect 2003。");
            Assert.Greater(nextEffectStart, effectStart, "基础攻击 GameplayEffect 2003 必须能与下一条效果配置明确分隔。");

            string basicAttackEffectJson = gameplayEffectJson.Substring(effectStart, nextEffectStart - effectStart);
            Assert.AreEqual(
                2,
                System.Text.RegularExpressions.Regex.Matches(basicAttackEffectJson, "\"PushMode\": 1").Count,
                "基础攻击普通伤害和背刺附加伤害都必须显式禁用默认击退。");
            Assert.That(
                basicAttackEffectJson,
                Does.Not.Contain("\"PushMode\": 0"),
                "基础攻击不能再把 PushMode=0 误当成无击退。");
        }

        [Test]
        public void FormalGasAttackCueValidation_DoesNotRequireIndependentPrefabCue()
        {
            const string timelineJson = "[{\"ID\":101,\"Tracks\":[]}]";
            const string gameplayCueJson = "[]";

            string extractedCueJson = ExtractFormalAbilityCueJsonForTest(timelineJson, gameplayCueJson, BasicAttackAbilityCode);

            Assert.IsFalse(ContainsResolvableGameCoreAudioCueForTest(extractedCueJson), "基础攻击缺少正式音效 Cue 时必须留在 EX-GAS Cue 路径补齐，不能回到旧 旧能力表.fireAudio。");
            Assert.IsFalse(ContainsResolvableMountPrefabCueForTest(extractedCueJson), "基础攻击当前不要求独立特效 Prefab Cue；角色动作和武器层分离，武器攻击与武器特效由装备/武器动作一起承载。");
        }

        [Test]
        public void FormalGasAttackValidation_AcceptsGameCoreAudioCueAsFormalSoundCue()
        {
            string timelineJson =
                "[{\"ID\":101,\"Tracks\":[{\"TaskClips\":[{\"Task\":{\"$type\":\"TaskPlayCue\",\"Param\":{\"CueLogic\":{\"$type\":\"CuePlayGameCoreAudio\",\"Param\":{\"AudioResolverGuid\":\"test-guid\"}}}}}]}]}]";
            string gameplayCueJson = "[]";

            string extractedCueJson = ExtractFormalAbilityCueJsonForTest(timelineJson, gameplayCueJson, BasicAttackAbilityCode);

            Assert.That(extractedCueJson, Does.Contain("\"$type\":\"CuePlayGameCoreAudio\""), "校验器必须把项目正式 GameCore 音频 Cue 识别为正式音效入口。");
            Assert.That(extractedCueJson, Does.Not.Contain("\"$type\":\"CuePlaySound\""), "该测试只覆盖项目正式音频桥，不能靠 EX-GAS 自带 CuePlaySound 误判通过。");
        }

        [Test]
        public void FormalGasAttackValidation_RejectsMissingMountPrefabResourceKey()
        {
            string missingCueJson =
                "{\"$type\":\"CueMountPrefab\",\"Param\":{\"PrefabPath\":\"Assets/Prefabs/Missing/NoSuchPrefab.prefab\"}}";
            string emptyCueJson =
                "{\"$type\":\"CueMountPrefab\",\"Param\":{\"PrefabPath\":\"\"}}";

            Assert.IsFalse(ContainsResolvableMountPrefabCueForTest(missingCueJson), "校验器不能把失效 PrefabPath 误判为正式特效入口已完成。");
            Assert.IsFalse(ContainsResolvableMountPrefabCueForTest(emptyCueJson), "校验器不能把空 PrefabPath 误判为正式特效入口已完成。");
        }


        [Test]
        public void FormalGasAttackEditor_DoesNotProvideProjectSide旧能力表Authoring()
        {
            Type editorType = Type.GetType("FantasyWord.GameCore.旧能力表Editor, FantasyWord.GameCore.Editor");

            Assert.IsNull(
                editorType,
                "项目侧不应再注册 旧能力表 自定义作者面。技能身份、图标、输入、消耗、冷却、命中和表现应回到 EX-GAS Ability / exgas.abilityGameCore / Timeline / Cue。");
        }

        [Test]
        public void LegacyAbilityExecutionAssetEditors_DoNotExistAsProjectSideAuthoring()
        {
            string[] legacyEditorTypeNames =
            {
                "FantasyWord.GameCore.MeleeAbilityExecutionAssetEditor, FantasyWord.GameCore.Editor",
                "FantasyWord.GameCore.DashAbilityExecutionAssetEditor, FantasyWord.GameCore.Editor",
                "FantasyWord.GameCore.ProjectileAbilityExecutionAssetEditor, FantasyWord.GameCore.Editor",
                "FantasyWord.GameCore.SummoningAbilityExecutionAssetEditor, FantasyWord.GameCore.Editor"
            };

            foreach (string editorTypeName in legacyEditorTypeNames)
            {
                Type editorType = Type.GetType(editorTypeName);
                Assert.IsNull(
                    editorType,
                    $"{editorTypeName} 不应作为项目侧技能作者面存在。能力身份、执行、规则和表现必须回到 EX-GAS Ability / Timeline / GameplayEffect / Cue；旧执行资产只允许作为未迁移兼容结构。");
            }
        }

        [Test]
        public void CuePlayGameCoreAudio_RequestsGameCoreAudioPlayback()
        {
            const string audioResolverGuid = "formal-gas-audio-cue-test-guid";
            AudioClipResolver audioClipResolver = ScriptableObject.CreateInstance<AudioClipResolver>();
            audioClipResolver.name = "formal-gas-audio-cue-probe";
            m_createdObjects.Add(audioClipResolver);
            RegisterRuntimeDatabaseEntry(audioClipResolver, audioResolverGuid);

            int audioRequestCount = 0;
            AudioClipResolver requestedResolver = null;
            void HandleAudioRequest(AudioPlaybackRequestedEvent audioPlaybackRequestedEvent)
            {
                audioRequestCount++;
                requestedResolver = audioPlaybackRequestedEvent.AudioClipResolver;
            }

            XParamGameCoreAudio audioParam = new();
            audioParam.SetAudioResolverGuid(audioResolverGuid);
            CuePlayGameCoreAudio cue = new();
            cue.InitParameters(audioParam);

            RegisterRuntimeEvent<AudioPlaybackRequestedEvent>(HandleAudioRequest);
            try
            {
                cue.OnActivate(0.0f);
            }
            finally
            {
                UnregisterRuntimeEvent<AudioPlaybackRequestedEvent>(HandleAudioRequest);
            }

            Assert.AreEqual(1, audioRequestCount, "EX-GAS GameCore 音频 Cue 应通过 GameCore 音频事件请求播放，而不是回到技能旧执行资产或 EX-GAS 资源路径。");
            Assert.AreSame(audioClipResolver, requestedResolver, "EX-GAS GameCore 音频 Cue 应按数据库 GUID 解析并转发 AudioClipResolver。");
        }

        [Test]
        public void CuePlayGameCoreAudio_WarnsWhenAudioResolverGuidIsMissing()
        {
            XParamGameCoreAudio audioParam = new();
            audioParam.SetAudioResolverGuid(string.Empty);
            CuePlayGameCoreAudio cue = new();
            cue.InitParameters(audioParam);

            LogAssert.Expect(
                LogType.Warning,
                "EX-GAS Cue 已触发 GameCore 音频事件，但 CuePlayGameCoreAudio 未配置 AudioClipResolver GUID。");

            cue.OnActivate(0.0f);
        }

        [Test]
        public void CuePlayGameCoreAudio_WarnsWhenAudioResolverGuidCannotResolve()
        {
            const string missingAudioResolverGuid = "missing-formal-gas-audio-cue-guid";
            XParamGameCoreAudio audioParam = new();
            audioParam.SetAudioResolverGuid(missingAudioResolverGuid);
            CuePlayGameCoreAudio cue = new();
            cue.InitParameters(audioParam);

            LogAssert.Expect(
                LogType.Warning,
                $"EX-GAS Cue 已触发 GameCore 音频事件，但找不到 AudioClipResolver GUID：{missingAudioResolverGuid}。");

            cue.OnActivate(0.0f);
        }

        [Test]
        public void Fire_HitsTargetInsideHitbox_AndUpdatesFormalHealth()
        {
            CharacterActor attacker = CreateCharacter("attacker", new Vector2(0.0f, 0.0f), CreateStats(health: 30, physicalAttack: 10));
            CharacterActor defender = CreateCharacter("defender", new Vector2(0.6f, 0.2f), CreateStats(health: 40, physicalDefense: 2));
            GrantBasicAttack(attacker);
AssertFormalAttackAbilityReady(attacker);
            attacker.SetLookAtDirection(Vector2.right);
            attacker.SetTargetDirection(Vector2.right);
            defender.SetLookAtDirection(Vector2.left);
            defender.SetTargetDirection(Vector2.left);

            int previousHealth = defender.GetCurrentHealth();
            int previousMaxHealth = defender.GetMaxHealth();
            int expectedDamage = CalculateExpectedResolvedDamage(attacker, defender, CreateBasicAttackBaseDamagePayload());

            EAbilityFireCheckResult fireResult = attacker.FireFormalGasAbility(BasicAttackAbilityCode, GameCommandContext.ResolveForActor(attacker));

            Assert.AreEqual(EAbilityFireCheckResult.Valid, fireResult);
            GasEditModeTestHelper.AdvanceWorldUntil(
                () => defender.GetCurrentHealth() == previousHealth - expectedDamage,
                CalculateExpectedGasTimelineHitTicks());
            Assert.AreEqual(previousHealth - expectedDamage, defender.GetCurrentHealth());
            Assert.AreEqual(previousMaxHealth, defender.GetMaxHealth());
            Assert.IsTrue(defender.TryGetFormalAbilitySystem(out AbilitySystemComponent defenderAsc));
            Assert.AreEqual(
                previousHealth - expectedDamage,
                Mathf.RoundToInt(defenderAsc.GetAttrCurrentValue(FormalGameplayAttributeSet.SetCode, FormalGameplayAttributeSet.Health)));
            Assert.AreEqual(
                previousMaxHealth,
                Mathf.RoundToInt(defenderAsc.GetAttrBaseValue(FormalGameplayAttributeSet.SetCode, FormalGameplayAttributeSet.Health)));
        }

        [Test]
        public void Fire_DoesNotBakeFacingDirectionIntoGasActivationContext()
        {
            CharacterActor attacker = CreateCharacter("attacker", new Vector2(0.0f, 0.0f), CreateStats(health: 30, physicalAttack: 10));
            CharacterActor defender = CreateCharacter("defender", new Vector2(0.6f, 0.2f), CreateStats(health: 40, physicalDefense: 2));
            GrantBasicAttack(attacker);
            CharacterAbilitySet abilitySet = attacker.GetComponent<CharacterAbilitySet>();
            AssertFormalAttackAbilityReady(attacker);
            Assert.IsTrue(TryGetFormalAbilitySpec(abilitySet, BasicAttackAbilityCode, out AbilitySpec abilitySpec), "技能 EX-GAS Ability 20001 未注册正式 GAS AbilitySpec。");
            Assert.IsTrue(TryGetFormalGasAbilityInstance(abilitySet, BasicAttackAbilityCode, out AbilityBase abilityBase), "未找到正式近战能力实例。");
            ActiveAbilityBase activeAbility = abilityBase as ActiveAbilityBase;
            Assert.IsNotNull(activeAbility, "近战能力实例不是主动能力。");

            attacker.SetLookAtDirection(Vector2.right);
            attacker.SetTargetDirection(Vector2.right);
            defender.SetLookAtDirection(Vector2.left);
            defender.SetTargetDirection(Vector2.left);

            int previousHealth = defender.GetCurrentHealth();
            int expectedDamage = CalculateExpectedResolvedDamage(attacker, defender, CreateBasicAttackBaseDamagePayload());

            EAbilityFireCheckResult fireResult = attacker.FireFormalGasAbility(BasicAttackAbilityCode, GameCommandContext.ResolveForActor(attacker));

            Assert.AreEqual(EAbilityFireCheckResult.Valid, fireResult);
            GasEditModeTestHelper.AdvanceWorldUntil(
                () => abilitySpec.IsActive && abilitySpec.GetActivationContext() != null,
                CalculateExpectedGasTimelineHitTicks());

            AbilityActivationContext activationContext = abilitySpec.GetActivationContext();
            Assert.IsNotNull(activationContext, "正式 GAS Ability 激活后必须持有本次攻击上下文。");
            Assert.IsFalse(
                activationContext.TryGetAimDirection(out _),
                "通用主动技能入口不应把角色起手朝向写成最终执行方向；闪现、突进等技能会在命中前改变姿态。");
            Assert.IsInstanceOf<XParamALTimelineID>(
                abilitySpec.GetParamRaw(),
                "运行时激活上下文不能覆盖 ALTimeline 的作者配置参数。");

            Assert.IsFalse(
                attacker.CanUpdateTargetDirection(),
                "正式攻击状态仍应通过 Event.Attacking 阻止普通目标方向更新；激活快照不应改变该动作规则。");
            GasEditModeTestHelper.AdvanceWorldUntil(
                () => defender.GetCurrentHealth() == previousHealth - expectedDamage,
                CalculateExpectedGasTimelineHitTicks());
            Assert.AreEqual(previousHealth - expectedDamage, defender.GetCurrentHealth(), "普通攻击期间正式攻击状态会锁住转向，命中帧读取当前朝向仍应稳定命中。");

            GasEditModeTestHelper.AdvanceWorldUntil(
                () => activeAbility.inputGateState == EFormalAbilityInputGateState.Idle && !abilitySpec.IsActive,
                40);
            Assert.IsNull(
                abilitySpec.GetActivationContext(),
                "正式 GAS Ability 结束后必须清理本次激活上下文，后续攻击不能复用上一轮目标或输入意图。");
        }

        [Test]
        public void AbilityActivationContext_AllowsDirectionlessAbilities()
        {
            AbilityActivationContext activationContext = new(Vector3.one);

            Assert.IsFalse(
                activationContext.TryGetAimDirection(out Vector3 aimDirection),
                "自疗、全屏 Buff 等无方向能力不应被强制要求攻击方向。");
            Assert.AreEqual(Vector3.zero, aimDirection);
        }

        [Test]
        public void Fire_WhenTargetBackFacesAttacker_AppliesFormalGasBackstabBonus()
        {
            CharacterActor attacker = CreateCharacter("attacker", new Vector2(0.0f, 0.0f), CreateStats(health: 30, physicalAttack: 10));
            CharacterActor defender = CreateCharacter("defender", new Vector2(0.6f, 0.2f), CreateStats(health: 40, physicalDefense: 2));
            GrantBasicAttack(attacker);
AssertFormalAttackAbilityReady(attacker);
            attacker.SetLookAtDirection(Vector2.right);
            attacker.SetTargetDirection(Vector2.right);
            defender.SetLookAtDirection(Vector2.right);
            defender.SetTargetDirection(Vector2.right);

            int previousHealth = defender.GetCurrentHealth();
            int expectedBaseDamage = CalculateExpectedResolvedDamage(attacker, defender, CreateBasicAttackBaseDamagePayload());
            int expectedBackstabDamage = CalculateExpectedResolvedDamage(
                attacker,
                defender,
                CreateBackstabBonusDamagePayload());

            EAbilityFireCheckResult fireResult = attacker.FireFormalGasAbility(BasicAttackAbilityCode, GameCommandContext.ResolveForActor(attacker));

            Assert.AreEqual(EAbilityFireCheckResult.Valid, fireResult);
            GasEditModeTestHelper.AdvanceWorldUntil(
                () => defender.GetCurrentHealth() == previousHealth - expectedBaseDamage - expectedBackstabDamage,
                CalculateExpectedGasTimelineHitTicks());
            Assert.AreEqual(previousHealth - expectedBaseDamage - expectedBackstabDamage, defender.GetCurrentHealth());
        }

        [Test]
        public void Fire_WhenTargetFacesAttacker_DoesNotApplyFormalGasBackstabBonus()
        {
            CharacterActor attacker = CreateCharacter("attacker", new Vector2(0.0f, 0.0f), CreateStats(health: 30, physicalAttack: 10));
            CharacterActor defender = CreateCharacter("defender", new Vector2(0.6f, 0.2f), CreateStats(health: 40, physicalDefense: 2));
            GrantBasicAttack(attacker);
AssertFormalAttackAbilityReady(attacker);
            attacker.SetLookAtDirection(Vector2.right);
            attacker.SetTargetDirection(Vector2.right);
            defender.SetLookAtDirection(Vector2.left);
            defender.SetTargetDirection(Vector2.left);

            int previousHealth = defender.GetCurrentHealth();
            int expectedBaseDamage = CalculateExpectedResolvedDamage(attacker, defender, CreateBasicAttackBaseDamagePayload());

            EAbilityFireCheckResult fireResult = attacker.FireFormalGasAbility(BasicAttackAbilityCode, GameCommandContext.ResolveForActor(attacker));

            Assert.AreEqual(EAbilityFireCheckResult.Valid, fireResult);
            GasEditModeTestHelper.AdvanceWorldUntil(
                () => defender.GetCurrentHealth() == previousHealth - expectedBaseDamage,
                CalculateExpectedGasTimelineHitTicks());
            Assert.AreEqual(previousHealth - expectedBaseDamage, defender.GetCurrentHealth());
        }

        [Test]
        public void Fire_DoesNotHitTargetOutsideHitbox()
        {
            CharacterActor attacker = CreateCharacter("attacker", new Vector2(0.0f, 0.0f), CreateStats(health: 30, physicalAttack: 10));
            CharacterActor defender = CreateCharacter("defender", new Vector2(2.5f, 0.0f), CreateStats(health: 40, physicalDefense: 2));
            GrantBasicAttack(attacker);
AssertFormalAttackAbilityReady(attacker);
            attacker.SetLookAtDirection(Vector2.right);
            attacker.SetTargetDirection(Vector2.right);

            int previousHealth = defender.GetCurrentHealth();
            int previousMaxHealth = defender.GetMaxHealth();

            EAbilityFireCheckResult fireResult = attacker.FireFormalGasAbility(BasicAttackAbilityCode, GameCommandContext.ResolveForActor(attacker));

            Assert.AreEqual(EAbilityFireCheckResult.Valid, fireResult);
            Assert.AreEqual(previousHealth, defender.GetCurrentHealth());
            Assert.AreEqual(previousMaxHealth, defender.GetMaxHealth());
            Assert.IsTrue(defender.TryGetFormalAbilitySystem(out AbilitySystemComponent defenderAsc));
            Assert.AreEqual(
                previousHealth,
                Mathf.RoundToInt(defenderAsc.GetAttrCurrentValue(FormalGameplayAttributeSet.SetCode, FormalGameplayAttributeSet.Health)));
            Assert.AreEqual(
                previousMaxHealth,
                Mathf.RoundToInt(defenderAsc.GetAttrBaseValue(FormalGameplayAttributeSet.SetCode, FormalGameplayAttributeSet.Health)));
        }

        [Test]
        public void CatchAreaBox2D_CatchesChildHitboxAbilitySystemComponent_AndFiltersOwner()
        {
            CharacterActor attacker = CreateCharacter("attacker", new Vector2(0.0f, 0.0f), CreateStats(health: 30, physicalAttack: 10));
            CharacterActor defender = CreateCharacter("defender", new Vector2(0.6f, 0.2f), CreateStats(health: 40, physicalDefense: 2));
            Assert.IsTrue(attacker.TryGetFormalAbilitySystem(out AbilitySystemComponent attackerAsc), "攻击者缺少正式 GAS ASC。");
            Assert.IsTrue(defender.TryGetFormalAbilitySystem(out AbilitySystemComponent defenderAsc), "目标缺少正式 GAS ASC。");

            int hitboxLayer = LayerMask.NameToLayer("Hitbox");
            Assert.GreaterOrEqual(hitboxLayer, 0, "测试项目必须存在 Hitbox 层。");

            XParamCatchAreaBox2D parameter = new();
            parameter.SetIsWorldSpace(false);
            parameter.SetOffset(new Vector2(0.65f, 0.15f));
            parameter.SetSize(new Vector2(0.95f, 0.8f));
            parameter.SetRotation(0.0f);
            parameter.SetLayer(1 << hitboxLayer);

            CatchAreaBox2D catcher = new();
            attacker.SetLookAtDirection(Vector2.right);
            attacker.SetTargetDirection(Vector2.right);
            catcher.Init(attackerAsc.Cell, new AbilityActivationContext(attacker.transform.position));
            catcher.InitParameters(parameter);

            List<AbilitySystemCell> results = new();
            catcher.CatchTargetsNonAllocSafe(attackerAsc.Cell, ref results);

            Assert.Contains(defenderAsc.Cell, results, "CatchAreaBox2D 应能通过子物体 Hitbox Collider 找到父物体上的 ASC。");
            Assert.IsFalse(results.Contains(attackerAsc.Cell), "CatchAreaBox2D 默认不应把施放者自己的 Hitbox 返回为目标。");
            Assert.AreEqual(1, results.Count, "同一目标多个 Collider 命中时不应重复返回同一个 ASC。");
        }

        [Test]
        public void CatchAreaBox2D_UsesOwnerFacingAtHitFrameWithoutTransformRotation()
        {
            CharacterActor attacker = CreateCharacter("attacker", new Vector2(0.0f, 0.0f), CreateStats(health: 30, physicalAttack: 10));
            CharacterActor defenderAbove = CreateCharacter("defender-above", new Vector2(-0.15f, 0.65f), CreateStats(health: 40, physicalDefense: 2));
            CharacterActor defenderRight = CreateCharacter("defender-right", new Vector2(0.65f, -0.6f), CreateStats(health: 40, physicalDefense: 2));
            Assert.IsTrue(attacker.TryGetFormalAbilitySystem(out AbilitySystemComponent attackerAsc), "攻击者缺少正式 GAS ASC。");
            Assert.IsTrue(defenderAbove.TryGetFormalAbilitySystem(out AbilitySystemComponent defenderAboveAsc), "上方目标缺少正式 GAS ASC。");
            Assert.IsTrue(defenderRight.TryGetFormalAbilitySystem(out AbilitySystemComponent defenderRightAsc), "右侧目标缺少正式 GAS ASC。");

            attacker.transform.rotation = Quaternion.identity;
            attacker.SetLookAtDirection(Vector2.up);
            attacker.SetTargetDirection(Vector2.up);

            int hitboxLayer = LayerMask.NameToLayer("Hitbox");
            Assert.GreaterOrEqual(hitboxLayer, 0, "测试项目必须存在 Hitbox 层。");

            XParamCatchAreaBox2D parameter = new();
            parameter.SetIsWorldSpace(false);
            parameter.SetOffset(new Vector2(0.65f, 0.15f));
            parameter.SetSize(new Vector2(0.95f, 0.8f));
            parameter.SetRotation(0.0f);
            parameter.SetLayer(1 << hitboxLayer);

            CatchAreaBox2D catcher = new();
            catcher.Init(
                attackerAsc.Cell,
                new AbilityActivationContext(attacker.transform.position, Vector3.right));
            catcher.InitParameters(parameter);

            List<AbilitySystemCell> results = new();
            catcher.CatchTargetsNonAllocSafe(attackerAsc.Cell, ref results);

            Assert.Contains(defenderAboveAsc.Cell, results, "CatchAreaBox2D 应读取施法者命中帧的当前朝向，而不是只看 Transform Z 旋转。");
            Assert.IsFalse(results.Contains(defenderRightAsc.Cell), "激活上下文中的旧输入方向不能覆盖施法者命中帧的实际朝向。");
        }

        [Test]
        public void CatchAreaPolygon2D_CatchesOnlyTargetsInsideAuthoredPolygon()
        {
            CharacterActor attacker = CreateCharacter("attacker", new Vector2(0.0f, 0.0f), CreateStats(health: 30, physicalAttack: 10));
            CharacterActor defenderInside = CreateCharacter("defender-inside", new Vector2(0.65f, 0.05f), CreateStats(health: 40, physicalDefense: 2));
            CharacterActor defenderOutside = CreateCharacter("defender-outside", new Vector2(0.95f, 0.35f), CreateStats(health: 40, physicalDefense: 2));
            Assert.IsTrue(attacker.TryGetFormalAbilitySystem(out AbilitySystemComponent attackerAsc), "攻击者缺少正式 GAS ASC。");
            Assert.IsTrue(defenderInside.TryGetFormalAbilitySystem(out AbilitySystemComponent defenderInsideAsc), "多边形内目标缺少正式 GAS ASC。");
            Assert.IsTrue(defenderOutside.TryGetFormalAbilitySystem(out AbilitySystemComponent defenderOutsideAsc), "多边形外目标缺少正式 GAS ASC。");

            BoxCollider2D outsideHitbox = GetDamageHitbox(defenderOutside);
            outsideHitbox.size = new Vector2(0.06f, 0.06f);

            int hitboxLayer = LayerMask.NameToLayer("Hitbox");
            Assert.GreaterOrEqual(hitboxLayer, 0, "测试项目必须存在 Hitbox 层。");

            XParamCatchAreaPolygon2D parameter = new();
            parameter.SetIsWorldSpace(false);
            parameter.SetPoints("0.2,-0.25;1.0,-0.2;0.85,0.35;0.25,0.4");
            parameter.SetLayer(1 << hitboxLayer);

            CatchAreaPolygon2D catcher = new();
            attacker.SetLookAtDirection(Vector2.right);
            attacker.SetTargetDirection(Vector2.right);
            catcher.Init(attackerAsc.Cell, new AbilityActivationContext(attacker.transform.position));
            catcher.InitParameters(parameter);

            List<AbilitySystemCell> results = new();
            catcher.CatchTargetsNonAllocSafe(attackerAsc.Cell, ref results);

            Assert.Contains(defenderInsideAsc.Cell, results, "CatchAreaPolygon2D 应按编辑出的多边形真实命中范围返回目标。");
            Assert.IsFalse(results.Contains(defenderOutsideAsc.Cell), "多边形外目标不应被外接盒粗筛误判为命中。");
            Assert.IsFalse(results.Contains(attackerAsc.Cell), "CatchAreaPolygon2D 默认不应把施放者自己的 Hitbox 返回为目标。");
        }

        [Test]
        public void CatchAreaPolygon2D_UsesOwnerPoseAtHitFrame_AfterTeleport()
        {
            CharacterActor attacker = CreateCharacter("attacker", new Vector2(0.0f, 0.0f), CreateStats(health: 30, physicalAttack: 10));
            CharacterActor targetAtDestination = CreateCharacter("target-at-destination", new Vector2(1.35f, 0.0f), CreateStats(health: 40, physicalDefense: 2));
            CharacterActor targetAtCastOrigin = CreateCharacter("target-at-cast-origin", new Vector2(0.65f, 0.0f), CreateStats(health: 40, physicalDefense: 2));
            Assert.IsTrue(attacker.TryGetFormalAbilitySystem(out AbilitySystemComponent attackerAsc), "攻击者缺少正式 GAS ASC。");
            Assert.IsTrue(targetAtDestination.TryGetFormalAbilitySystem(out AbilitySystemComponent destinationAsc), "闪现落点目标缺少正式 GAS ASC。");
            Assert.IsTrue(targetAtCastOrigin.TryGetFormalAbilitySystem(out AbilitySystemComponent castOriginAsc), "起手位置目标缺少正式 GAS ASC。");
            GetDamageHitbox(targetAtDestination).size = new Vector2(0.06f, 0.06f);
            GetDamageHitbox(targetAtCastOrigin).size = new Vector2(0.06f, 0.06f);

            int hitboxLayer = LayerMask.NameToLayer("Hitbox");
            Assert.GreaterOrEqual(hitboxLayer, 0, "测试项目必须存在 Hitbox 层。");

            XParamCatchAreaPolygon2D parameter = new();
            parameter.SetIsWorldSpace(false);
            parameter.SetPoints("0.2,-0.25;1.0,-0.2;0.85,0.35;0.25,0.4");
            parameter.SetLayer(1 << hitboxLayer);

            AbilityActivationContext activationContext = new(
                attacker.transform.position,
                Vector3.right,
                destinationAsc.Cell);
            CatchAreaPolygon2D catcher = new();
            catcher.Init(attackerAsc.Cell, activationContext);
            catcher.InitParameters(parameter);

            attacker.transform.position = new Vector3(2.0f, 0.0f, 0.0f);
            attacker.SetLookAtDirection(Vector2.left);
            attacker.SetTargetDirection(Vector2.right);

            List<AbilitySystemCell> results = new();
            catcher.CatchTargetsNonAllocSafe(destinationAsc.Cell, ref results);

            Assert.Contains(destinationAsc.Cell, results, "闪现后攻击应以命中帧的施法者位置和朝向计算范围。");
            Assert.IsFalse(results.Contains(castOriginAsc.Cell), "闪现后攻击不能继续使用起手位置、起手方向或输入目标方向计算命中范围。");
        }

        [Test]
        public void FormalGasChargedAttackRelease_UsesIndependentGasAbilityCodeAndTimeline()
        {
            RegisterFormalGasAbilityDescriptionGeneratedRuntime();

            Assert.AreEqual(
                ChargedAttackReleaseAbilityCode,
                XAbility.ABILITY_ChargedAttackRelease,
                "蓄力释放必须使用 EX-GAS 生成的 Ability Code，不能覆盖基础攻击或变形替换 smoke。");
            Assert.IsTrue(
                FormalGasAbilityRuntimeConfigResolver.TryResolveRuntimeConfig(
                    ChargedAttackReleaseAbilityCode,
                    out FormalGasAbilityRuntimeConfig config),
                "蓄力释放占位能力必须从 exgas.abilityGameCore 解析运行配置。");
            Assert.IsTrue(config.TryLoadPrefab(out GameObject prefab), "蓄力释放占位能力必须能加载正式 Ability Prefab。");
            Assert.IsNotNull(prefab.GetComponent<AbilityBase>(), "蓄力释放占位能力 Prefab 根节点必须包含 AbilityBase。");
            Assert.IsTrue(config.TryLoadIcon(out Sprite icon), "蓄力释放占位能力必须能从 exgas.abilityGameCore 加载蓄力图标。");
            Assert.IsNotNull(icon, "蓄力释放占位能力图标不能为空。");
            Assert.AreEqual(
                EFormalAbilityInputTriggerMode.HoldRelease,
                config.InputGate.triggerMode,
                "蓄力释放必须由 exgas.abilityGameCore.InputTriggerMode=2 表达为按住蓄力、松手释放，不能继续按下即释放。");

            string abilityJson = File.ReadAllText("Assets/DataGenerated/Luban/Json/GAS/exgas_tbability.json");
            string abilityGameCoreJson = File.ReadAllText("Assets/DataGenerated/Luban/Json/GAS/exgas_tbabilitygamecore.json");
            string timelineJson = File.ReadAllText("Assets/DataGenerated/Luban/Json/GAS/exgas_tbtimelineability.json");
            string gameplayEffectJson = File.ReadAllText("Assets/DataGenerated/Luban/Json/GAS/exgas_tbgameplayeffect.json");
            string gameplayCueJson = File.ReadAllText("Assets/DataGenerated/Luban/Json/GAS/exgas_tbgameplaycue.json");

            StringAssert.Contains("\"ID\": 20004", abilityJson, "EX-GAS Ability 表必须包含蓄力释放占位能力 20004。");
            StringAssert.Contains("\"Name\": \"ChargedAttackRelease\"", abilityJson, "蓄力释放占位能力不能复用基础攻击身份。");
            StringAssert.Contains("\"InputTriggerMode\": 2", abilityGameCoreJson, "蓄力释放必须在 exgas.abilityGameCore 配置为 HoldRelease 输入触发模式。");
            StringAssert.Contains("\"ID\": 20004", timelineJson, "蓄力释放占位能力必须有独立 Timeline 20004。");
            StringAssert.Contains("\"$type\": \"CuePlayGameCoreAnimator\"", timelineJson, "攻击动画必须显式配置项目侧 CuePlayGameCoreAnimator，不能通过重注册覆盖 EX-GAS 内置 CuePlayAnimator。");
            StringAssert.Contains("\"AnimationName\": \"ChargedAttack\"", timelineJson, "蓄力释放 Timeline 必须触发装备系统蓄力攻击动作键。");
            Assert.That(timelineJson, Does.Not.Contain("\"$type\": \"CuePlayAnimator\""), "GAS Timeline 不应再把内置 CuePlayAnimator 当项目侧装备动画桥使用。");
            Assert.That(timelineJson, Does.Not.Contain("\"AnimationName\": \"Skill_Attack\""), "GAS Timeline 不应再填写旧角色动画状态名；普攻表现应触发装备系统 Attack 动作键。");
            Assert.That(timelineJson, Does.Not.Contain("\"AnimationName\": \"Skill_ChargedAttack\""), "GAS Timeline 不应再填写旧角色动画状态名；蓄力表现应触发装备系统 ChargedAttack 动作键。");
            StringAssert.Contains("\"ID\": 2004", gameplayEffectJson, "蓄力释放必须有独立 GameplayEffect 2004。");
            Assert.That(gameplayEffectJson, Does.Not.Contain("      20003"), "基础攻击当前不应把临时独立特效 Cue 当作完成条件；武器攻击和特效由武器动作承载。");
            Assert.That(gameplayEffectJson, Does.Not.Contain("      20004"), "蓄力释放当前不应把临时独立特效 Cue 当作完成条件；武器攻击和特效由武器动作承载。");
        }

        [Test]
        public void FormalGasAttackEquipmentAnimationCue_RequiresEquipmentSystemForWeaponActions()
        {
            string cueSource = File.ReadAllText("Assets/Scripts/GameCore/Runtime/Presentation/CuePlayGameCoreAnimator.cs");

            Assert.That(cueSource, Does.Not.Contain("NormalizeAnimationKey"), "项目侧动画 Cue 不应再把旧角色动画名静默转成装备动作键。");
            Assert.That(cueSource, Does.Not.Contain("Skill_Attack"), "普攻正式配置必须直接使用装备系统 Attack 动作键，不能保留旧 Skill_Attack 兼容口。");
            Assert.That(cueSource, Does.Not.Contain("Skill_ChargedAttack"), "蓄力释放正式配置必须直接使用装备系统 ChargedAttack 动作键，不能保留旧 Skill_ChargedAttack 兼容口。");
            StringAssert.Contains("ICharacterAnimationDriver", cueSource, "项目侧动画 Cue 必须通过正式角色动作驱动合同进入装备表现层。");
            StringAssert.Contains("TryPlayAnimation(animationKey)", cueSource, "GAS 动画 Cue 必须只提交动作键，由角色表现驱动同步身体、武器和武器自带特效。");
            StringAssert.Contains("Debug.LogError", cueSource, "运行时角色 Prefab 缺少动作驱动或动作配置时必须直接报错。");
            Assert.That(cueSource, Does.Not.Contain("Type.GetType"), "正式动画链不得再通过字符串反射猜测装备动画组件。");
            Assert.That(cueSource, Does.Not.Contain("TryPlayAnimatorFallback"), "装备动作失败时不得回退普通 Animator，避免角色动作和武器攻击/特效脱节。");
        }

        [Test]
        public void Fire_ChargedAttackRelease_HoldsUntilInputReleaseThenHitsThroughGasTimeline()
        {
            CharacterActor attacker = CreateCharacter("charged-attacker", new Vector2(0.0f, 0.0f), CreateStats(health: 30, physicalAttack: 10));
            CharacterActor defender = CreateCharacter("charged-defender", new Vector2(0.75f, 0.2f), CreateStats(health: 50, physicalDefense: 2));
            GrantFormalGasAbility(attacker, ChargedAttackReleaseAbilityCode);
            AssertFormalAbilityReady(attacker, expectFormalCost: false, EAbilityFireCheckResult.Valid, ChargedAttackReleaseAbilityCode);
            attacker.SetLookAtDirection(Vector2.right);
            attacker.SetTargetDirection(Vector2.right);
            defender.SetLookAtDirection(Vector2.left);
            defender.SetTargetDirection(Vector2.left);

            int previousHealth = defender.GetCurrentHealth();
            int expectedDamage = CalculateExpectedResolvedDamage(attacker, defender, CreateChargedAttackReleaseDamagePayload());

            EAbilityFireCheckResult fireResult = attacker.FireFormalGasAbility(
                ChargedAttackReleaseAbilityCode,
                GameCommandContext.ResolveForActor(attacker));

            Assert.AreEqual(EAbilityFireCheckResult.Valid, fireResult);
            AssertFormalAbilityInputGateState(attacker, ChargedAttackReleaseAbilityCode, EFormalAbilityInputGateState.Charging);
            GasEditModeTestHelper.AdvanceWorld(CalculateExpectedGasTimelineHitTicks(12));
            Assert.AreEqual(
                previousHealth,
                defender.GetCurrentHealth(),
                "蓄力释放在按住阶段不能提前启动 EX-GAS Timeline 命中；必须等松手释放。");

            Assert.IsTrue(
                attacker.StopFireFormalGasAbility(ChargedAttackReleaseAbilityCode),
                "松开输入应通过正式 StopFireFormalGasAbility 触发蓄力释放。");
            GasEditModeTestHelper.AdvanceWorldUntil(
                () => defender.GetCurrentHealth() == previousHealth - expectedDamage,
                CalculateExpectedGasTimelineHitTicks(12));
            Assert.AreEqual(
                previousHealth - expectedDamage,
                defender.GetCurrentHealth(),
                "蓄力释放应在松手后通过 EX-GAS Timeline 20004 的 TaskApplyEffects 和 GameplayEffect 2004 命中目标。");
        }

        [Test]
        public void TaskApplyEffects_OnEditorPreviewWithoutCatcher_SkipsPreviewWithoutThrowing()
        {
            TaskApplyEffects task = new(null);

            LogAssert.Expect(
                LogType.Warning,
                "TaskApplyEffects preview skipped: target catcher is not initialized. CatcherType=");

            Assert.DoesNotThrow(
                () => task.OnEditorPreview(null, 0, 0, 0),
                "EX-GAS TaskApplyEffects 编辑器预览没有初始化 TargetCatcher 时，应跳过预览并给出警告，不能再空引用打断时间轴预览。");
        }

        [Test]
        public void FormalGasAttack_UsesGasTimelineExecutionGate_NotExecutionAssetTiming()
        {
            CharacterActor attacker = CreateCharacter("attacker", new Vector2(0.0f, 0.0f), CreateStats(health: 30, physicalAttack: 10));
            SerializedObject prefabAbilitySerializedObject = new SerializedObject(LoadBasicAttackAbilityPrefabComponent());
            Assert.IsNull(prefabAbilitySerializedObject.FindProperty("m_inputGate"), "近战技能资产不应再保留 legacy m_inputGate。");


            GrantBasicAttack(attacker);
            AssertFormalAttackAbilityReady(attacker);

            EAbilityFireCheckResult fireResult = attacker.FireFormalGasAbility(BasicAttackAbilityCode, GameCommandContext.ResolveForActor(attacker));
            Assert.AreEqual(EAbilityFireCheckResult.Valid, fireResult);

            CharacterAbilitySet abilitySet = attacker.GetComponent<CharacterAbilitySet>();
            Assert.IsNotNull(abilitySet, "角色缺少正式 CharacterAbilitySet。");
            Assert.IsTrue(TryGetFormalGasAbilityInstance(abilitySet, BasicAttackAbilityCode, out AbilityBase abilityBase), "未找到正式近战能力实例。");
            Assert.IsInstanceOf<MeleeAttackAbility>(abilityBase, "技能未实例化成正式近战能力组件。");

            EFormalAbilityInputGateState inputGateState = (EFormalAbilityInputGateState)GetInstanceFieldValue(abilityBase, "m_inputGate", typeof(FormalAbilityInputGateRuntime), "m_state");
            Assert.AreEqual(EFormalAbilityInputGateState.DelayBeforeUse, inputGateState, "正式 GAS 基础攻击应按 EX-GAS Timeline 的首个玩法帧进入本地输入门控，不能再被旧执行资产的 0 前摇改成直接出手。");
        }


        [Test]
        public void FormalGasAttackRuntimeInstance_UsesGasContextNotMigrationSheetFlag()
        {
            RegisterFormalGasAbilityDescriptionGeneratedRuntime();
            CharacterActor attacker = CreateCharacter("attacker", new Vector2(0.0f, 0.0f), CreateStats(health: 30, physicalAttack: 10));
            Assert.IsTrue(
                FormalGasAbilityRuntimeConfigResolver.TryResolveRuntimeConfig(BasicAttackAbilityCode, out FormalGasAbilityRuntimeConfig config),
                "测试前应能从 exgas.abilityGameCore 解析基础攻击运行配置。");
            Assert.IsFalse(
                config.InputGate.updateLookAtDirectionOnFire,
                "四方向俯视角普攻必须按角色当前朝向执行；开火瞬间不能用鼠标、点击目标或输入请求方向改写朝向。");
            Assert.IsTrue(config.TryLoadPrefab(out GameObject prefab), "测试前应能从 exgas.abilityGameCore 加载基础攻击 Prefab。");

            GameObject instance = UnityEngine.Object.Instantiate(prefab, attacker.transform);
            instance.name = "FormalGasAttackRuntimeInstance";
            m_createdObjects.Add(instance);
            AbilityBase abilityBase = instance.GetComponent<AbilityBase>();
            Assert.IsNotNull(abilityBase, "基础攻击 Prefab 缺少 AbilityBase 组件。");

            abilityBase.InitFormalGasAbility(attacker, BasicAttackAbilityCode);

            Assert.AreEqual(
                BasicAttackAbilityCode,
                (int)GetInstanceFieldValue(abilityBase, "m_formalGasAbilityCode"),
                "已迁移能力实例的正式身份必须来自 InitFormalGasAbility 传入的 GAS Ability Code，不应再依赖迁移壳 旧能力表 的 m_formalGasAbilityCode。");
            object runtimeSettings = GetInstanceFieldValue(abilityBase, "m_inputGate", typeof(FormalAbilityInputGateRuntime), "m_settings");
            Assert.AreEqual(EFormalAbilityInputTriggerMode.SemiAuto, ((FormalAbilityInputGateSettings)runtimeSettings).triggerMode, "迁移壳不再带 GAS 标记时，正式能力实例仍应按 GAS context 使用 exgas.abilityGameCore 输入门控。");
            Assert.IsTrue(((FormalAbilityInputGateSettings)runtimeSettings).bufferInput, "正式能力实例的输入缓冲应来自 GAS 运行配置，而不是旧执行资产。");
        }

        [Test]
        public void FormalGasAttackRuntimeInstance_DoesNotInheritLegacy旧主动能力表RuntimeShell()
        {
            RegisterFormalGasAbilityDescriptionGeneratedRuntime();
            CharacterActor attacker = CreateCharacter("attacker", new Vector2(0.0f, 0.0f), CreateStats(health: 30, physicalAttack: 10));
            Assert.IsTrue(
                FormalGasAbilityRuntimeConfigResolver.TryResolveRuntimeConfig(BasicAttackAbilityCode, out FormalGasAbilityRuntimeConfig config),
                "测试前应能从 exgas.abilityGameCore 解析基础攻击运行配置。");
            Assert.IsTrue(config.TryLoadPrefab(out GameObject prefab), "测试前应能从 exgas.abilityGameCore 加载基础攻击 Prefab。");

            GameObject instance = UnityEngine.Object.Instantiate(prefab, attacker.transform);
            instance.name = "FormalGasAttackRuntimeShellProbe";
            m_createdObjects.Add(instance);
            AbilityBase abilityBase = instance.GetComponent<AbilityBase>();
            Assert.IsNotNull(abilityBase, "基础攻击 Prefab 缺少 AbilityBase 组件。");

            abilityBase.InitFormalGasAbility(attacker, BasicAttackAbilityCode);

            Assert.IsInstanceOf<MeleeAttackAbility>(abilityBase, "基础攻击正式 Prefab 应挂载项目侧 EX-GAS 近战适配组件。");
            Assert.IsNull(
                typeof(AbilityBase).GetProperty("legacy旧能力表", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                "正式能力实例不应再暴露旧 旧能力表 身份属性；运行身份必须来自 EX-GAS Ability Code。");
            Assert.IsFalse(
                typeof(ActiveAbilityBase).IsGenericType,
                "主动能力运行基类不应再是 ActiveAbility<TSheet> 旧表泛型壳。");
        }

        [Test]
        public void FormalGasAttack_DoesNotRequireLegacyExecutionAssetForRuntime()
        {
            CharacterActor attacker = CreateCharacter("attacker", new Vector2(0.0f, 0.0f), CreateStats(health: 30, physicalAttack: 10));
            CharacterActor defender = CreateCharacter("defender", new Vector2(0.6f, 0.2f), CreateStats(health: 40, physicalDefense: 2));
            GrantBasicAttack(attacker);
            AssertFormalAttackAbilityReady(attacker);

            int previousHealth = defender.GetCurrentHealth();
            EAbilityFireCheckResult fireResult = attacker.FireFormalGasAbility(BasicAttackAbilityCode, GameCommandContext.ResolveForActor(attacker));
            Assert.AreEqual(EAbilityFireCheckResult.Valid, fireResult);

            GasEditModeTestHelper.AdvanceWorld(CalculateExpectedGasTimelineHitTicks());
            Assert.Less(defender.GetCurrentHealth(), previousHealth, "正式 GAS 基础攻击应只依赖 Ability/Timeline/GameplayEffect 数据运行，不应要求旧执行资产存在。");
        }

        [Test]
        public void FormalGasAttack_DoesNotUseLegacyExecutionFeedbacks()
        {
            CharacterActor attacker = CreateCharacter("attacker", new Vector2(0.0f, 0.0f), CreateStats(health: 30, physicalAttack: 10));
            GrantBasicAttack(attacker);
            AssertFormalAttackAbilityReady(attacker);

            SerializedObject prefabAbilitySerializedObject = new SerializedObject(LoadBasicAttackAbilityPrefabComponent());
            Assert.IsNull(prefabAbilitySerializedObject.FindProperty("m_feedbacks"), "近战技能资产不应再保留 legacy m_feedbacks。");

            EAbilityFireCheckResult fireResult = attacker.FireFormalGasAbility(BasicAttackAbilityCode, GameCommandContext.ResolveForActor(attacker));
            Assert.AreEqual(EAbilityFireCheckResult.Valid, fireResult);

            GasEditModeTestHelper.AdvanceWorld(CalculateExpectedGasTimelineHitTicks());
        }

        [Test]
        public void FormalGasAttack_DoesNotDeclareLegacyHitWindowRuntime()
        {
            CharacterActor attacker = CreateCharacter("attacker", new Vector2(0.0f, 0.0f), CreateStats(health: 30, physicalAttack: 10));
GrantBasicAttack(attacker);
            AssertFormalAttackAbilityReady(attacker);

            CharacterAbilitySet abilitySet = attacker.GetComponent<CharacterAbilitySet>();
            Assert.IsTrue(TryGetFormalGasAbilityInstance(abilitySet, BasicAttackAbilityCode, out AbilityBase abilityBase), "未找到基础攻击能力实例。");
            Assert.IsInstanceOf<MeleeAttackAbility>(abilityBase, "基础攻击未实例化成近战能力。");

            FieldInfo legacyHitWindowRuntimeField = FindInstanceField(typeof(MeleeAttackAbility), "m_hitWindowRuntime");
            Assert.IsNull(legacyHitWindowRuntimeField, "MeleeAttackAbility 不应再声明项目侧旧命中窗口运行态字段；命中窗口应只由 EX-GAS Timeline/TaskApplyEffects 承担。");
        }

        [Test]
        public void FormalGasAttack_DoesNotUseLegacyExecutionInterruptOrReloadFeedbacks()
        {
            CharacterActor attacker = CreateCharacter("attacker", new Vector2(0.0f, 0.0f), CreateStats(health: 30, physicalAttack: 10));
GrantBasicAttack(attacker);
            AssertFormalAttackAbilityReady(attacker);


            CharacterAbilitySet abilitySet = attacker.GetComponent<CharacterAbilitySet>();
            Assert.IsTrue(TryGetFormalGasAbilityInstance(abilitySet, BasicAttackAbilityCode, out AbilityBase abilityBase), "未找到基础攻击能力实例。");
            ITriggerableAbility triggerableAbility = abilityBase as ITriggerableAbility;
            Assert.IsNotNull(triggerableAbility, "未找到基础攻击触发实例。");
            Assert.IsNull(
                typeof(ITriggerableAbility).GetMethod("Reload"),
                "正式触发接口不应继续暴露旧项目侧 Reload；换弹若需要存在，应由 EX-GAS Timeline / Cue 或正式项目配置承担。");

            EAbilityFireCheckResult fireResult = attacker.FireFormalGasAbility(BasicAttackAbilityCode, GameCommandContext.ResolveForActor(attacker));
            Assert.AreEqual(EAbilityFireCheckResult.Valid, fireResult);
            abilityBase.Interrupt();
            GasEditModeTestHelper.AdvanceWorld(4);

        }

        [Test]
        public void FormalGasAttack_TriggersTargetHitFeedbackThroughGameplayCue()
        {
            CharacterActor attacker = CreateCharacter("attacker", new Vector2(0.0f, 0.0f), CreateStats(health: 30, physicalAttack: 10));
            CharacterActor defender = CreateCharacter("defender", new Vector2(0.6f, 0.2f), CreateStats(health: 40, physicalDefense: 2));
GrantBasicAttack(attacker);
            AssertFormalAttackAbilityReady(attacker);
            attacker.SetLookAtDirection(Vector2.right);
            attacker.SetTargetDirection(Vector2.right);
            defender.SetLookAtDirection(Vector2.left);
            defender.SetTargetDirection(Vector2.left);

            MMFeedbacks targetCueFeedback = CreateProbeFeedback("target-gas-cue-hit-feedback");
            TestMeleeFeedbackProbe targetCueProbe = targetCueFeedback.GetComponent<TestMeleeFeedbackProbe>();
            SetInstanceField(defender.characterSheet.feedbacks, "m_hitDamageableFeedbacks", targetCueFeedback);

            int previousHealth = defender.GetCurrentHealth();
            int expectedDamage = CalculateExpectedResolvedDamage(attacker, defender, CreateBasicAttackBaseDamagePayload());

            EAbilityFireCheckResult fireResult = attacker.FireFormalGasAbility(BasicAttackAbilityCode, GameCommandContext.ResolveForActor(attacker));

            Assert.AreEqual(EAbilityFireCheckResult.Valid, fireResult);
            GasEditModeTestHelper.AdvanceWorldUntil(
                () => targetCueProbe.playCount > 0 && defender.GetCurrentHealth() == previousHealth - expectedDamage,
                CalculateExpectedGasTimelineHitTicks() + 12,
                () => CreateGasCueDiagnostic(defender, targetCueProbe, previousHealth, expectedDamage));
            Assert.AreEqual(previousHealth - expectedDamage, defender.GetCurrentHealth());
            Assert.GreaterOrEqual(targetCueProbe.playCount, 1, "基础攻击命中反馈必须通过 EX-GAS CueOnApply 触发目标角色的 GameplayFeedbackSet。");
        }


        [Test]
        public void FormalGasAttack_UsesActivationOwnedAttackingTag_NotLegacyDisabledActions()
        {
            CharacterActor attacker = CreateCharacter("attacker", new Vector2(0.0f, 0.0f), CreateStats(health: 30, mana: 12, physicalAttack: 10));
            GrantBasicAttack(attacker);

            CharacterAbilitySet abilitySet = attacker.GetComponent<CharacterAbilitySet>();
            AssertFormalAttackAbilityReady(attacker);
            Assert.IsTrue(TryGetFormalAbilitySpec(abilitySet, BasicAttackAbilityCode, out AbilitySpec abilitySpec), "技能 EX-GAS Ability 20001 未注册正式 GAS AbilitySpec。");
            Assert.Contains(FormalGameplayTagCatalog.AttackingEvent.TagCode, abilitySpec.GetActivationOwnedTags(), "基础攻击 EX-GAS Ability 必须通过 ActivationOwnedTags 持有正在攻击标签。");

            EAbilityFireCheckResult fireResult = attacker.FireFormalGasAbility(BasicAttackAbilityCode, GameCommandContext.ResolveForActor(attacker));

            Assert.AreEqual(EAbilityFireCheckResult.Valid, fireResult);
            GasEditModeTestHelper.AdvanceWorldUntil(
                () => abilitySpec.IsActive,
                CalculateExpectedFormalGasInputGateUseTicks());
            Assert.IsTrue(abilitySpec.IsActive, "测试必须进入正式 GAS Active 生命周期，才能验证攻击标签动作锁。");
            Assert.IsFalse(attacker.Can(EActionFlags.Move), "正式 GAS 基础攻击活动期应由 Event.Attacking 标签阻止移动，而不是旧 disabledActionsWhileCasting。");
            Assert.IsFalse(attacker.Can(EActionFlags.UseAbility), "正式 GAS 基础攻击活动期应由 Event.Attacking 标签阻止再次出手，而不是旧 disabledActionsWhileCasting。");
            Assert.IsFalse(attacker.Can(EActionFlags.UpdateTargetDirection), "正式 GAS 基础攻击活动期应由 Event.Attacking 标签阻止更新瞄准方向，而不是旧 disabledActionsWhileCasting。");
        }

        [Test]
        public void FormalGasAttack_DoesNotUseLegacyCanInterruptAsActionInterruptGate()
        {
            CharacterActor attacker = CreateCharacter("attacker", new Vector2(0.0f, 0.0f), CreateStats(health: 30, mana: 12, physicalAttack: 10));
            GrantBasicAttack(attacker);

            CharacterAbilitySet abilitySet = attacker.GetComponent<CharacterAbilitySet>();
            AssertFormalAttackAbilityReady(attacker);
            Assert.IsTrue(TryGetFormalGasAbilityInstance(abilitySet, BasicAttackAbilityCode, out AbilityBase abilityBase), "未找到正式近战能力实例。");
            Assert.IsInstanceOf<IActionInterruptReceiver>(abilityBase, "正式近战能力实例必须能接收动作打断。");
            Assert.IsTrue(TryGetFormalAbilitySpec(abilitySet, BasicAttackAbilityCode, out AbilitySpec abilitySpec), "技能 EX-GAS Ability 20001 未注册正式 GAS AbilitySpec。");

            EAbilityFireCheckResult fireResult = attacker.FireFormalGasAbility(BasicAttackAbilityCode, GameCommandContext.ResolveForActor(attacker));
            Assert.AreEqual(EAbilityFireCheckResult.Valid, fireResult);
            GasEditModeTestHelper.AdvanceWorldUntil(
                () => abilitySpec.IsActive,
                CalculateExpectedFormalGasInputGateUseTicks());
            Assert.IsTrue(abilitySpec.IsActive, "测试必须先进入正式 GAS Active 生命周期。");

            ((IActionInterruptReceiver)abilityBase).OnActionInterrupted();

            Assert.IsTrue(HasPendingFormalAbilityCancel(abilitySpec), "已绑定 EX-GAS 的基础攻击接收动作打断时应向 EX-GAS 提交取消请求，不应由旧 旧主动能力表.canInterupt=false 阻断。");
        }


        [Test]
        public void Fire_WithWindup_ActivatesFormalGasAbilityDuringWeaponSequenceAndEndsAfterStop()
        {
            CharacterActor attacker = CreateCharacter("attacker", new Vector2(0.0f, 0.0f), CreateStats(health: 30, physicalAttack: 10));
GrantBasicAttack(attacker);
            CharacterAbilitySet abilitySet = attacker.GetComponent<CharacterAbilitySet>();
            AssertFormalAttackAbilityReady(attacker);
            Assert.IsTrue(TryGetFormalAbilitySpec(abilitySet, BasicAttackAbilityCode, out AbilitySpec abilitySpec), "技能 EX-GAS Ability 20001 未注册正式 GAS AbilitySpec。");
            Assert.IsTrue(TryGetFormalGasAbilityInstance(abilitySet, BasicAttackAbilityCode, out AbilityBase abilityBase), "未找到正式近战能力实例。");
            ActiveAbilityBase activeAbility = abilityBase as ActiveAbilityBase;
            Assert.IsNotNull(activeAbility, "近战能力实例不是主动能力。");

            EAbilityFireCheckResult fireResult = attacker.FireFormalGasAbility(BasicAttackAbilityCode, GameCommandContext.ResolveForActor(attacker));

            Assert.AreEqual(EAbilityFireCheckResult.Valid, fireResult);
            GasEditModeTestHelper.AdvanceWorldUntil(
                () => abilitySpec.IsActive,
                CalculateExpectedFormalGasInputGateUseTicks());
            Assert.IsTrue(abilitySpec.IsActive, "近战基础攻击前摇期间应已经进入 GAS Active 生命周期。");

            GasEditModeTestHelper.AdvanceWorldUntil(
                () => activeAbility.inputGateState == EFormalAbilityInputGateState.Idle && !abilitySpec.IsActive,
                40);
            Assert.IsFalse(abilitySpec.IsActive, "武器序列结束后应结束 GAS Active 生命周期。");
        }

        [Test]
        public void Fire_WhenBlockedTagAppearsDuringWindup_DoesNotApplyHitOrCooldown()
        {
            CharacterActor attacker = CreateCharacter("attacker", new Vector2(0.0f, 0.0f), CreateStats(health: 30, mana: 10, physicalAttack: 10));
            CharacterActor defender = CreateCharacter("defender", new Vector2(0.6f, 0.2f), CreateStats(health: 40, physicalDefense: 2));
GrantBasicAttack(attacker);
            CharacterAbilitySet abilitySet = attacker.GetComponent<CharacterAbilitySet>();
            AssertFormalAttackAbilityReady(attacker);
            Assert.IsTrue(TryGetFormalAbilitySpec(abilitySet, BasicAttackAbilityCode, out AbilitySpec abilitySpec), "技能 EX-GAS Ability 20001 未注册正式 GAS AbilitySpec。");
            Assert.IsTrue(attacker.TryGetFormalAbilitySystem(out AbilitySystemComponent attackerAsc), "攻击者未绑定正式 GAS AbilitySystemComponent。");
            Assert.IsTrue(TryGetFormalGasAbilityInstance(abilitySet, BasicAttackAbilityCode, out AbilityBase abilityBase), "未找到正式近战能力实例。");
            ActiveAbilityBase activeAbility = abilityBase as ActiveAbilityBase;
            Assert.IsNotNull(activeAbility, "近战能力实例不是主动能力。");

            int blockedTag = GetFirstConfiguredActivationBlockedTag(abilitySpec);
            attacker.SetLookAtDirection(Vector2.right);
            attacker.SetTargetDirection(Vector2.right);
            int previousHealth = defender.GetCurrentHealth();
            int previousMana = attacker.GetCurrentMana();
            EAbilityFireCheckResult fireResult = attacker.FireFormalGasAbility(BasicAttackAbilityCode, GameCommandContext.ResolveForActor(attacker));

            Assert.AreEqual(EAbilityFireCheckResult.Valid, fireResult);
            Assert.IsTrue(attackerAsc.Cell.AddFixedTag(blockedTag), "测试应能在前摇期间添加 GAS 阻断标签。");
            AssertFormalAbilityStartResult(
                abilitySet,
                BasicAttackAbilityCode,
                "EX-GAS Ability 20001",
                EAbilityFireCheckResult.Incapacitated,
                $"前摇期间添加 GAS 阻断标签后，项目侧正式入口应拒绝真正出手。{CreateFormalGateDiagnostic(abilitySet, BasicAttackAbilityCode, attackerAsc.Cell, blockedTag)}");

            GasEditModeTestHelper.AdvanceWorldUntil(
                () => activeAbility.inputGateState == EFormalAbilityInputGateState.Idle && !abilitySpec.IsActive,
                40);

            Assert.AreEqual(previousHealth, defender.GetCurrentHealth(), "GAS 阻断标签在真正出手前出现时，不应结算基础攻击命中。");
            Assert.AreEqual(previousMana, attacker.GetCurrentMana(), "GAS 阻断标签在真正出手前出现时，不应扣蓝。");
            Assert.IsTrue(abilitySpec.IsCooldownReady, "GAS 阻断标签提交失败时不应启动冷却。");
        }

        [Test]
        public void PlayerControl_WhenTargetDirectionUpdateIsLocked_PreservesFacingUntilUnlocked()
        {
            CharacterActor player = CreateCharacter(
                "player-facing-lock",
                Vector2.zero,
                CreateStats(health: 30));

            CharacterMovement movement = player.gameObject.AddComponent<CharacterMovement>();
            SetInstanceField(movement, "m_character", player);

            CharacterCommandExecutor commandExecutor = player.gameObject.AddComponent<CharacterCommandExecutor>();
            SetInstanceField(commandExecutor, "m_character", player);
            InvokeLifecycle(commandExecutor, "Awake");

            CharacterPlayerControl playerControl = player.gameObject.AddComponent<CharacterPlayerControl>();
            SetInstanceField(playerControl, "m_character", player);
            SetInstanceField(playerControl, "m_commandExecutor", commandExecutor);
            InvokeLifecycle(playerControl, "Awake");

            GameObject playerSystemObject = new("PlayerSystemFacingLock");
            m_createdObjects.Add(playerSystemObject);
            PlayerSystem playerSystem = playerSystemObject.AddComponent<PlayerSystem>();
            SetInstanceField(playerSystem, "m_primaryPlayerCharacter", player);

            IDictionary systems = GetInstanceFieldValue(GameManager.Instance, "m_systems") as IDictionary;
            Assert.IsNotNull(systems, "测试 GameManager.m_systems 未初始化或类型不兼容。");
            systems[typeof(PlayerSystem)] = playerSystem;
            playerSystem.SetCurrentControlledCharacter(player);

            player.SetLookAtDirection(Vector2.down);
            player.SetTargetDirection(Vector2.right);
            player.DisableActions(EActionFlags.UpdateTargetDirection);

            Assert.IsFalse(player.CanUpdateTargetDirection(), "测试必须先进入禁止更新朝向的状态。");
            InvokeLifecycle(playerControl, "Update");
            Assert.AreEqual(
                Vector2.right,
                player.GetTargetDirection(),
                "攻击前摇等禁止转向状态必须保留起手方向，不能退回上一段移动方向。");

            player.EnableActions(EActionFlags.UpdateTargetDirection);
            InvokeLifecycle(playerControl, "Update");
            Assert.AreEqual(
                Vector2.down,
                player.GetTargetDirection(),
                "解除朝向锁后，无指针目标时仍应恢复原有的移动朝向回退行为。");
        }

        [Test]
        public void PlayerCommand_FireAbility_PreservesRequestedDirectionUntilGasActivation()
        {
            CharacterActor player = CreateCharacter(
                "player-command-facing-lock",
                Vector2.zero,
                CreateStats(health: 30, physicalAttack: 10));
            CharacterActor target = CreateCharacter(
                "player-command-facing-target",
                Vector2.down * 0.8f,
                CreateStats(health: 40, physicalDefense: 2));
            GrantBasicAttack(player);

            CharacterMovement movement = player.gameObject.AddComponent<CharacterMovement>();
            SetInstanceField(movement, "m_character", player);

            CharacterCommandExecutor commandExecutor = player.gameObject.AddComponent<CharacterCommandExecutor>();
            SetInstanceField(commandExecutor, "m_character", player);
            InvokeLifecycle(commandExecutor, "Awake");

            CharacterPlayerControl playerControl = player.gameObject.AddComponent<CharacterPlayerControl>();
            SetInstanceField(playerControl, "m_character", player);
            SetInstanceField(playerControl, "m_commandExecutor", commandExecutor);
            InvokeLifecycle(playerControl, "Awake");

            GameObject playerSystemObject = new("PlayerSystemCommandFacingLock");
            m_createdObjects.Add(playerSystemObject);
            PlayerSystem playerSystem = playerSystemObject.AddComponent<PlayerSystem>();
            SetInstanceField(playerSystem, "m_primaryPlayerCharacter", player);

            IDictionary systems = GetInstanceFieldValue(GameManager.Instance, "m_systems") as IDictionary;
            Assert.IsNotNull(systems, "测试 GameManager.m_systems 未初始化或类型不兼容。");
            systems[typeof(PlayerSystem)] = playerSystem;
            playerSystem.SetCurrentControlledCharacter(player);

            CharacterAbilitySet abilitySet = player.GetComponent<CharacterAbilitySet>();
            AssertFormalAttackAbilityReady(player);
            Assert.IsTrue(
                TryGetFormalAbilitySpec(abilitySet, BasicAttackAbilityCode, out AbilitySpec abilitySpec),
                "技能 EX-GAS Ability 20001 未注册正式 GAS AbilitySpec。");
            Assert.IsTrue(
                target.TryGetFormalAbilitySystem(out AbilitySystemComponent targetAbilitySystem),
                "目标未绑定正式 GAS AbilitySystemComponent。");

            player.SetLookAtDirection(Vector2.right);
            player.SetTargetDirection(Vector2.right);

            PlayerCommandResult fireResult = commandExecutor.Execute(new PlayerCommandRequest(
                GameCommandContext.ResolveForActor(player),
                EPlayerCommandKind.FireAbility,
                abilityIndex: 0,
                targetCharacter: target));

            Assert.IsTrue(fireResult.Succeeded, $"玩家普攻命令应被接受，实际失败原因：{fireResult.FailureReason}。");
            Assert.IsFalse(abilitySpec.IsActive, "该回归测试必须覆盖本地前摇已开始、GAS 尚未激活的帧间隙。");
            Assert.IsFalse(player.CanUpdateTargetDirection(), "本地前摇门控期间应锁住本次攻击方向。");
            Assert.That(
                Vector2.Dot(player.GetTargetDirection().normalized, Vector2.down),
                Is.GreaterThan(0.999f),
                "带目标的玩家技能命令应在前摇开始时朝向目标。");

            InvokeLifecycle(playerControl, "Update");

            Assert.That(
                Vector2.Dot(player.GetTargetDirection().normalized, Vector2.down),
                Is.GreaterThan(0.999f),
                "GAS 激活前的同帧玩家控制更新不能把本次攻击方向重置成旧移动方向。");

            GasEditModeTestHelper.AdvanceWorldUntil(
                () => abilitySpec.IsActive,
                CalculateExpectedFormalGasInputGateUseTicks());
            Assert.IsTrue(abilitySpec.IsActive, "正式 GAS Ability 应在本地前摇后激活。");

            AbilityActivationContext activationContext = abilitySpec.GetActivationContext();
            Assert.IsNotNull(activationContext, "正式 GAS 激活必须保留玩家命令创建的激活上下文。");
            Assert.IsTrue(
                activationContext.TryGetAimDirection(out Vector3 aimDirection),
                "玩家技能命令的目标方向必须进入 GAS 激活上下文。");
            Assert.That(
                Vector2.Dot(new Vector2(aimDirection.x, aimDirection.y).normalized, Vector2.down),
                Is.GreaterThan(0.999f),
                "GAS 激活上下文中的瞄准方向必须与玩家命令目标一致。");
            Assert.AreSame(
                targetAbilitySystem.Cell,
                activationContext.MainTarget,
                "玩家技能命令的主目标必须进入 GAS 激活上下文。");
        }

        [Test]
        public void PlayerSystem_RevalidateTransientControlLoss_RestoresPrimaryPlayerInputTarget()
        {
            CharacterActor player = CreateCharacter(
                "player-control-recovery",
                Vector2.zero,
                CreateStats(health: 30));
            CharacterCommandExecutor commandExecutor = player.gameObject.AddComponent<CharacterCommandExecutor>();
            SetInstanceField(commandExecutor, "m_character", player);
            InvokeLifecycle(commandExecutor, "Awake");

            CharacterPlayerControl playerControl = player.gameObject.AddComponent<CharacterPlayerControl>();
            SetInstanceField(playerControl, "m_character", player);
            SetInstanceField(playerControl, "m_commandExecutor", commandExecutor);
            InvokeLifecycle(playerControl, "Awake");

            GameObject playerSystemObject = new("PlayerSystemControlRecovery");
            m_createdObjects.Add(playerSystemObject);
            PlayerSystem playerSystem = playerSystemObject.AddComponent<PlayerSystem>();
            SetInstanceField(playerSystem, "m_primaryPlayerCharacter", player);

            IDictionary systems = GetInstanceFieldValue(GameManager.Instance, "m_systems") as IDictionary;
            Assert.IsNotNull(systems, "测试 GameManager.m_systems 未初始化或类型不兼容。");
            systems[typeof(PlayerSystem)] = playerSystem;

            playerSystem.SetCurrentControlledCharacter(player);
            Assert.IsTrue(
                playerSystem.TryGetCurrentInputTarget(out IPlayerInputTarget initialTarget),
                "测试前必须建立主角色输入目标。");
            Assert.AreSame(playerControl, initialTarget);

            playerControl.enabled = false;
            playerSystem.RevalidateCurrentControlledCharacter();

            Assert.IsFalse(
                playerSystem.TryGetCurrentInputTarget(out _),
                "控制组件短暂失效时应清空当前输入目标。");
            Assert.IsTrue(
                (bool)GetInstanceFieldValue(playerSystem, "m_pendingPlayerControlRestore"),
                "清空主角色输入目标后必须保留恢复请求，不能永久卡在 MissingInputTarget。");

            playerControl.enabled = true;
            playerSystem.OnMapLoaded();

            Assert.IsTrue(
                playerSystem.TryGetCurrentInputTarget(out IPlayerInputTarget restoredTarget),
                "控制组件恢复后必须重新建立主角色输入目标。");
            Assert.AreSame(playerControl, restoredTarget);
        }


        [Test]
        public void LegacyExecutionAssetTypes_DoNotExistAsAbilityAuthoringModel()
        {
            Assert.IsNull(
                Type.GetType("FantasyWord.GameCore.AbilityExecutionAsset, FantasyWord.GameCore"),
                "旧 AbilityExecutionAsset 类型不应继续存在；能力执行、规则和表现必须由 EX-GAS Ability / Timeline / GameplayEffect / Cue 表达。");
            Assert.IsNull(
                Type.GetType("FantasyWord.GameCore.MeleeAbilityExecutionAsset, FantasyWord.GameCore"),
                "旧 MeleeAbilityExecutionAsset 类型不应继续存在；普攻命中、伤害、反馈和时间轴不能再有项目侧第二套执行资产。");
        }

        private void CreateGameManagerWithMinimalConfig()
        {
            GameObject gameManagerObject = new("EditModeGameManager");
            GameManager gameManager = gameManagerObject.AddComponent<GameManager>();
            GameConfig config = ScriptableObject.CreateInstance<GameConfig>();
            DatabaseRegistry database = ScriptableObject.CreateInstance<DatabaseRegistry>();

            m_createdObjects.Add(config);
            m_createdObjects.Add(database);
            m_createdObjects.Add(gameManagerObject);

            SetInstanceField(config, "m_databaseRegistry", database);
            SetInstanceField(config, "m_canCriticalHit", false);
            SetInstanceField(config, "m_canMissHit", false);
            SetInstanceField(gameManager, "m_config", config);
            SetInstanceField(
                gameManager,
                "m_systems",
                Activator.CreateInstance(GetRequiredFieldType(typeof(GameManager), "m_systems")));
            SetStaticField(typeof(GameManager), "_instance", gameManager);
        }

        private static void RegisterPlayerSystemForConditionTests(CharacterActor player)
        {
            Assert.IsTrue(GameManager.Exists(), "注册测试 PlayerSystem 前必须已有 GameManager。");
            GameObject playerSystemObject = new("EditModePlayerSystem");
            PlayerSystem playerSystem = playerSystemObject.AddComponent<PlayerSystem>();
            SetInstanceField(playerSystem, "m_primaryPlayerCharacter", player);

            IDictionary systems = GetInstanceFieldValue(GameManager.Instance, "m_systems") as IDictionary;
            Assert.IsNotNull(systems, "测试 GameManager.m_systems 未初始化或类型不兼容。");
            systems[typeof(PlayerSystem)] = playerSystem;
        }

        private void RegisterPlayerSystemForAlterationTest(CharacterActor player)
        {
            Assert.IsTrue(GameManager.Exists(), "注册测试 PlayerSystem 前必须已有 GameManager。");
            GameObject playerSystemObject = new("EditModeAlterationPlayerSystem");
            m_createdObjects.Add(playerSystemObject);
            PlayerSystem playerSystem = playerSystemObject.AddComponent<PlayerSystem>();
            SetInstanceField(playerSystem, "m_primaryPlayerCharacter", player);

            IDictionary systems = GetInstanceFieldValue(GameManager.Instance, "m_systems") as IDictionary;
            Assert.IsNotNull(systems, "测试 GameManager.m_systems 未初始化或类型不兼容。");
            systems[typeof(PlayerSystem)] = playerSystem;
        }

        private CharacterActor CreateCharacter(string name, Vector2 position, Stats baseStats, bool initializeAbilities = true)
        {
            GameObject characterObject = new(name)
            {
                layer = 0
            };
            characterObject.transform.position = position;
            m_createdObjects.Add(characterObject);

            Rigidbody2D rigidbody2D = characterObject.AddComponent<Rigidbody2D>();
            rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            BoxCollider2D bodyCollider = characterObject.AddComponent<BoxCollider2D>();
            CreateDamageHitbox(characterObject.transform, bodyCollider);
            characterObject.AddComponent<Animator>();

            CharacterActor character = characterObject.AddComponent<CharacterActor>();
            AbilitySystemComponent abilitySystemComponent = characterObject.GetComponent<AbilitySystemComponent>();
            CharacterAbilitySet abilitySet = characterObject.GetComponent<CharacterAbilitySet>();

            CharacterSheet sheet = ScriptableObject.CreateInstance<CharacterSheet>();
            m_createdObjects.Add(sheet);
            SetInstanceField(sheet, "m_baseStats", baseStats.Clone());
            SetInstanceField(
                sheet,
                "m_formalGasAbilitiesPerLevel",
                Activator.CreateInstance(GetRequiredFieldType(typeof(CharacterSheet), "m_formalGasAbilitiesPerLevel")));
            SetInstanceField(character, "m_sheet", sheet);
            SetInstanceField(character, "m_rigidbody", rigidbody2D);
            SetInstanceField(character, "m_animationStrategy", new NullAnimationStrategy());

            ConfigureAbilityRoots(characterObject.transform, abilitySet);

            InvokeLifecycle(abilitySystemComponent, "Awake");
            SetInstanceField(abilitySet, "m_character", character);
            InvokeLifecycle(abilitySet, "Awake");
            InvokeLifecycle(character, "Awake");
            InvokeLifecycle(abilitySystemComponent, "OnEnable");
            InvokeLifecycle(abilitySet, "OnEnable");
            InvokeLifecycle(character, "OnEnable");
            if (initializeAbilities)
            {
                InvokeStaticLifecycle(character, "InitializeAbilities");
            }

            character.SetLookAtDirection(Vector2.right);
            character.SetTargetDirection(Vector2.right);

            return character;
        }

        private static void CreateDamageHitbox(Transform owner, BoxCollider2D bodyCollider)
        {
            GameObject hitboxObject = new("Hitbox");
            int hitboxLayer = LayerMask.NameToLayer("Hitbox");
            hitboxObject.layer = hitboxLayer >= 0 ? hitboxLayer : owner.gameObject.layer;
            hitboxObject.transform.SetParent(owner, false);

            BoxCollider2D hitboxCollider = hitboxObject.AddComponent<BoxCollider2D>();
            hitboxCollider.isTrigger = true;
            if (bodyCollider != null)
            {
                hitboxCollider.offset = bodyCollider.offset;
                hitboxCollider.size = bodyCollider.size;
            }
        }

        private static BoxCollider2D GetDamageHitbox(CharacterActor owner)
        {
            int hitboxLayer = LayerMask.NameToLayer("Hitbox");
            foreach (BoxCollider2D collider in owner.GetComponentsInChildren<BoxCollider2D>())
            {
                if (collider.isTrigger && (hitboxLayer < 0 || collider.gameObject.layer == hitboxLayer))
                {
                    return collider;
                }
            }

            Assert.Fail("测试角色缺少用于受击检测的 Hitbox 子碰撞体。");
            return null;
        }

        private MMFeedbacks CreateProbeFeedback(string name)
        {
            GameObject feedbackObject = new(name);
            m_createdObjects.Add(feedbackObject);

            MMFeedbacks feedbacks = feedbackObject.AddComponent<MMFeedbacks>();
            TestMeleeFeedbackProbe probe = feedbackObject.AddComponent<TestMeleeFeedbackProbe>();
            MMFeedbacksEvents feedbackEvents = new MMFeedbacksEvents
            {
                TriggerUnityEvents = true,
                OnPlay = new UnityEngine.Events.UnityEvent()
            };
            feedbackEvents.OnPlay.AddListener(probe.HandlePlay);
            feedbackEvents.Initialization();
            feedbacks.Events = feedbackEvents;
            feedbacks.Initialization(feedbackObject);
            return feedbacks;
        }


        private static void RegisterRuntimeDatabaseEntry(DatabaseEntry entry, string key)
        {
            Assert.IsNotNull(entry, "测试数据库注册对象不能为空。");
            Assert.IsTrue(GameManager.Exists(), "测试数据库注册必须在 GameManager 初始化后执行。");
            Assert.IsFalse(string.IsNullOrWhiteSpace(key), "测试数据库注册 key 不能为空。");

            DatabaseRegistry database = GameManager.Database;
            Assert.IsNotNull(database, "测试 GameConfig 缺少 DatabaseRegistry。");
            database.GetEntries();

            IDictionary entries = GetInstanceFieldValue(database, "m_entries") as IDictionary;
            Assert.IsNotNull(entries, "DatabaseRegistry.m_entries 未初始化或类型不兼容。");
            entries[key] = entry;
        }

        private CharacterAlterationRule CreateRegisteredCharacterAlterationRule(string name, string key)
        {
            CharacterAlterationRule rule = ScriptableObject.CreateInstance<CharacterAlterationRule>();
            rule.name = name;
            m_createdObjects.Add(rule);
            RegisterRuntimeDatabaseEntry(rule, key);
            return rule;
        }

        private static int CountRuntimeAbilitySourceStacks(
            CharacterRuntimeStateData runtimeState,
            int formalGasAbilityCode,
            ECharacterAbilitySourceKind sourceKind,
            string sourceId)
        {
            if (runtimeState?.abilitySources == null)
            {
                return 0;
            }

            int totalStacks = 0;
            foreach (CharacterAbilitySourceData sourceData in runtimeState.abilitySources)
            {
                if (sourceData != null &&
                    sourceData.formalGasAbilityCode == formalGasAbilityCode &&
                    sourceData.sourceKind == sourceKind &&
                    string.Equals(sourceData.sourceId, sourceId, StringComparison.Ordinal))
                {
                    totalStacks += sourceData.stackCount;
                }
            }

            return totalStacks;
        }

        private Item CreateRegisteredItem(string name, string key)
        {
            Item item = ScriptableObject.CreateInstance<Item>();
            item.name = name;
            m_createdObjects.Add(item);
            RegisterRuntimeDatabaseEntry(item, key);
            return item;
        }

        private static void SetCharacterFormalGasAbilityUnlock(CharacterSheet sheet, int formalGasAbilityCode, int level)
        {
            Assert.IsNotNull(sheet, "角色表不能为空。");
            IDictionary unlocks = GetInstanceFieldValue(sheet, "m_formalGasAbilitiesPerLevel") as IDictionary;
            Assert.IsNotNull(unlocks, "CharacterSheet.m_formalGasAbilitiesPerLevel 未初始化或类型不兼容。");
            unlocks[formalGasAbilityCode] = level;
        }

        private static string ExtractFormalAbilityCueJsonForTest(string timelineJson, string gameplayCueJson, int abilityCode)
        {
            MethodInfo method = typeof(FormalAbilityAssetValidation).GetMethod(
                "ExtractAbilityCueJson",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "FormalAbilityAssetValidation 缺少 EX-GAS Cue 证据提取入口。");

            return method.Invoke(null, new object[] { timelineJson, gameplayCueJson, "[]", abilityCode }) as string
                ?? string.Empty;
        }

        private static bool ContainsResolvableMountPrefabCueForTest(string abilityCueJson)
        {
            MethodInfo method = typeof(FormalAbilityAssetValidation).GetMethod(
                "ContainsResolvableMountPrefabCue",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "FormalAbilityAssetValidation 缺少 CueMountPrefab 可解析校验入口。");

            return method.Invoke(null, new object[] { abilityCueJson }) is bool value && value;
        }

        private static bool ContainsResolvableGameCoreAudioCueForTest(string abilityCueJson)
        {
            MethodInfo method = typeof(FormalAbilityAssetValidation).GetMethod(
                "ContainsResolvableGameCoreAudioCue",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "FormalAbilityAssetValidation 缺少 CuePlayGameCoreAudio 可解析校验入口。");

            return method.Invoke(null, new object[] { abilityCueJson }) is bool value && value;
        }

        private static void GrantBasicAttack(CharacterActor owner)
        {
            GrantFormalGasAbility(owner, BasicAttackAbilityCode);
        }

        private static void GrantFormalGasAbility(CharacterActor owner, int formalGasAbilityCode)
        {
            owner.AddBonusFormalGasAbility(formalGasAbilityCode, CharacterAbilitySourceKey.Script);
            Assert.IsTrue(owner.HasFormalGasAbility(formalGasAbilityCode), $"角色未正式持有 EX-GAS Ability：{formalGasAbilityCode}");
        }

        private static bool HasLegacyAbilitySheetParameter(ParameterInfo parameter)
        {
            string parameterTypeName = parameter.ParameterType.FullName ?? parameter.ParameterType.Name;
            return parameterTypeName.Contains("旧能力表", StringComparison.Ordinal) ||
                parameterTypeName.Contains("LegacyAbility", StringComparison.Ordinal);
        }

        private static AbilityBase LoadBasicAttackAbilityPrefabComponent()
        {
            RegisterFormalGasAbilityDescriptionGeneratedRuntime();
            Assert.IsTrue(
                FormalGasAbilityRuntimeConfigResolver.TryResolveRuntimeConfig(
                    BasicAttackAbilityCode,
                    out FormalGasAbilityRuntimeConfig config),
                "测试前应能从 exgas.abilityGameCore 解析基础攻击运行配置。");
            Assert.IsTrue(config.TryLoadPrefab(out GameObject prefab), "测试前应能从 exgas.abilityGameCore 加载基础攻击 Prefab。");
            AbilityBase abilityBase = prefab.GetComponent<AbilityBase>();
            Assert.IsNotNull(abilityBase, "基础攻击 Prefab 缺少 AbilityBase 组件。");
            return abilityBase;
        }

        private static void AssertFormalAttackAbilityReady(
            CharacterActor owner,
            bool expectFormalCost = false)
        {
            AssertFormalAbilityReady(owner, expectFormalCost, EAbilityFireCheckResult.Valid, BasicAttackAbilityCode);
        }
        private static void AssertFormalAbilityReady(
            CharacterActor owner,
            bool expectFormalCost,
            EAbilityFireCheckResult expectedResult,
            int formalGasAbilityCode = BasicAttackAbilityCode)
        {
            string abilityName = $"EX-GAS Ability {formalGasAbilityCode}";
            CharacterAbilitySet abilitySet = owner.GetComponent<CharacterAbilitySet>();
            Assert.IsNotNull(abilitySet, "角色缺少正式 CharacterAbilitySet。");
            Assert.IsTrue(TryGetFormalGasAbilityInstance(abilitySet, formalGasAbilityCode, out AbilityBase abilityBase), $"角色没有为 {abilityName} 创建正式能力实例。");
            Assert.IsInstanceOf<MeleeAttackAbility>(abilityBase, $"技能 {abilityName} 没有实例化成正式近战能力组件。");

            bool usesFormalCost;
            EAbilityFireCheckResult formalResult;
            bool registered = TryEvaluateFormalGasAbilityRuleActivation(abilitySet, formalGasAbilityCode, out formalResult, out usesFormalCost);
            Assert.IsTrue(registered, $"技能 {abilityName} 没有注册正式 GAS 能力规则。");
            Assert.AreEqual(expectedResult, formalResult, $"技能 {abilityName} 的正式 GAS 规则结果不符合预期。");
            Assert.AreEqual(expectFormalCost, usesFormalCost, $"技能 {abilityName} 的正式 GAS 消耗标记不符合预期。");
        }

        private static void AssertFormalAbilityInputGateState(
            CharacterActor owner,
            int formalGasAbilityCode,
            EFormalAbilityInputGateState expectedState)
        {
            CharacterAbilitySet abilitySet = owner.GetComponent<CharacterAbilitySet>();
            Assert.IsNotNull(abilitySet, "角色缺少正式 CharacterAbilitySet。");
            Assert.IsTrue(
                TryGetFormalGasAbilityInstance(abilitySet, formalGasAbilityCode, out AbilityBase abilityBase),
                $"角色没有为 EX-GAS Ability {formalGasAbilityCode} 创建正式能力实例。");
            EFormalAbilityInputGateState inputGateState = (EFormalAbilityInputGateState)GetInstanceFieldValue(
                abilityBase,
                "m_inputGate",
                typeof(FormalAbilityInputGateRuntime),
                "m_state");
            Assert.AreEqual(expectedState, inputGateState, $"EX-GAS Ability {formalGasAbilityCode} 的本地输入门控状态不符合预期。");
        }

        private static bool TryGetFormalGasAbilityInstance(CharacterAbilitySet abilitySet, int formalGasAbilityCode, out AbilityBase abilityBase)
        {
            abilityBase = null;
            object runtime = GetInstanceFieldValue(abilitySet, "m_runtime");
            Assert.IsNotNull(runtime, "CharacterAbilitySet 运行时能力容器缺失。");

            MethodInfo method = runtime.GetType().GetMethod(
                "TryGetFormalGasAbilityInstance",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(int), typeof(AbilityBase).MakeByRefType() },
                null);
            Assert.IsNotNull(method, "找不到 CharacterAbilitySetRuntime.TryGetFormalGasAbilityInstance。");

            object[] args = { formalGasAbilityCode, null };
            bool resolved = method.Invoke(runtime, args) is bool value && value;
            if (resolved)
            {
                abilityBase = args[1] as AbilityBase;
            }

            return resolved;
        }

        private static bool TryEvaluateFormalGasAbilityRuleActivation(
            CharacterAbilitySet abilitySet,
            int formalGasAbilityCode,
            out EAbilityFireCheckResult result,
            out bool usesFormalCost)
        {
            MethodInfo method = FindInstanceMethod(
                typeof(CharacterAbilitySet),
                "TryEvaluateFormalGasAbilityRuleActivation",
                typeof(int),
                typeof(EAbilityFireCheckResult).MakeByRefType(),
                typeof(bool).MakeByRefType());
            Assert.IsNotNull(method, "找不到 CharacterAbilitySet.TryEvaluateFormalGasAbilityRuleActivation。");

            object[] args = { formalGasAbilityCode, EAbilityFireCheckResult.Unknown, false };
            bool registered = method.Invoke(abilitySet, args) is bool value && value;
            result = args[1] is EAbilityFireCheckResult fireCheckResult
                ? fireCheckResult
                : EAbilityFireCheckResult.Unknown;
            usesFormalCost = args[2] is bool boolValue && boolValue;
            return registered;
        }

        private static void AssertFormalAbilityCostNotConfigured(CharacterAbilitySet abilitySet, int formalGasAbilityCode, string abilityName)
        {
            Assert.IsTrue(TryGetFormalAbilitySpec(abilitySet, formalGasAbilityCode, out AbilitySpec abilitySpec), $"技能 {abilityName} 未注册正式 GAS AbilitySpec。");
            Assert.IsFalse(abilitySpec.CheckCostExist(), $"技能 {abilityName} 当前 GAS 表未配置 Cost，不应由 项目侧旧能力表 manaCost 合成 CAbilityCost。");
            Assert.AreNotEqual(AbilityActivationResult.FailCost, abilitySpec.CheckActivation(), $"技能 {abilityName} 不应被旧 项目侧旧能力表 manaCost 判成 GAS 消耗不足。");
        }

        private static void AssertFormalAbilityCooldownNotConfigured(CharacterAbilitySet abilitySet, int formalGasAbilityCode, string abilityName)
        {
            Assert.IsTrue(TryGetFormalAbilitySpec(abilitySet, formalGasAbilityCode, out AbilitySpec abilitySpec), $"技能 {abilityName} 未注册正式 GAS AbilitySpec。");
            Assert.IsFalse(abilitySpec.CheckCooldownExist(), $"技能 {abilityName} 当前 GAS 表未配置 Cooldown，不应由 项目侧旧能力表 cooldown 合成 CAbilityCooldown。");
            Assert.IsTrue(abilitySpec.IsCooldownReady, $"技能 {abilityName} 未配置 GAS Cooldown 时应保持冷却可用。");
        }

        private static void AssertFormalAbilityStartResult(
            CharacterAbilitySet abilitySet,
            int formalGasAbilityCode,
            string abilityName,
            EAbilityFireCheckResult expectedResult,
            string message)
        {
            MethodInfo method = FindInstanceMethod(
                typeof(CharacterAbilitySet),
                "TryEvaluateFormalAbilityActivation",
                typeof(int),
                typeof(EAbilityFireCheckResult).MakeByRefType(),
                typeof(bool).MakeByRefType());
            Assert.IsNotNull(method, "找不到 CharacterAbilitySet.TryEvaluateFormalAbilityActivation。");

            object[] args = { formalGasAbilityCode, null, null };
            bool evaluated = method.Invoke(abilitySet, args) is bool value && value;
            Assert.IsTrue(evaluated, $"技能 {abilityName} 未能通过项目侧正式规则入口完成激活检查。");
            Assert.AreEqual(expectedResult, (EAbilityFireCheckResult)args[1], message);
        }

        private static string CreateFormalGateDiagnostic(
            CharacterAbilitySet abilitySet,
            int formalGasAbilityCode,
            AbilitySystemCell abilitySystemCell,
            int expectedTag)
        {
            bool hasSpec = TryGetFormalAbilitySpec(abilitySet, formalGasAbilityCode, out AbilitySpec abilitySpec);
            string fixedTags = string.Join(",", abilitySystemCell.FixedTags());
            string blockedTags = hasSpec ? string.Join(",", abilitySpec.GetActivationBlockedTags()) : "<no-spec>";
            string activation = hasSpec ? abilitySpec.CheckActivation().ToString() : "<no-spec>";
            bool hasExact = HasExactFixedTag(abilitySystemCell, new[] { expectedTag });
            return $" fixedTags=[{fixedTags}] blockedTags=[{blockedTags}] expectedTag={expectedTag} hasExact={hasExact} rawActivation={activation}";
        }

        private static int GetFirstConfiguredActivationBlockedTag(AbilitySpec abilitySpec)
        {
            Assert.IsNotNull(abilitySpec, "技能未注册正式 GAS AbilitySpec。");
            int[] blockedTags = abilitySpec.GetActivationBlockedTags();
            Assert.IsNotEmpty(blockedTags, "正式 GAS AbilitySpec 缺少 ActivationBlockedTags 配置。");

            foreach (int blockedTag in blockedTags)
            {
                if (blockedTag != 0)
                {
                    return blockedTag;
                }
            }

            Assert.Fail("正式 GAS AbilitySpec 的 ActivationBlockedTags 只有占位 0，没有可用于阻断的正式标签。");
            return 0;
        }

        private static bool HasExactFixedTag(AbilitySystemCell abilitySystemCell, IEnumerable<int> expectedTags)
        {
            Assert.IsNotNull(abilitySystemCell, "角色未绑定正式 GAS AbilitySystemCell。");
            foreach (int fixedTag in abilitySystemCell.FixedTags())
            {
                foreach (int expectedTag in expectedTags)
                {
                    if (fixedTag == expectedTag)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryGetFormalAbilitySpec(CharacterAbilitySet abilitySet, int formalGasAbilityCode, out AbilitySpec abilitySpec)
        {
            abilitySpec = null;
            MethodInfo method = FindInstanceMethod(
                typeof(CharacterAbilitySet),
                "TryGetFormalAbilitySpec",
                typeof(int),
                typeof(AbilitySpec).MakeByRefType(),
                typeof(object).MakeByRefType());
            method ??= FindInstanceMethodByParameterPrefix(
                typeof(CharacterAbilitySet),
                "TryGetFormalAbilitySpec",
                typeof(int),
                typeof(AbilitySpec).MakeByRefType());
            Assert.IsNotNull(method, "找不到 CharacterAbilitySet.TryGetFormalAbilitySpec。");

            object[] args = { formalGasAbilityCode, null, null };
            bool resolved = method.Invoke(abilitySet, args) is bool value && value;
            if (resolved)
            {
                abilitySpec = args[1] as AbilitySpec;
            }

            return resolved;
        }

        private static bool HasPendingFormalAbilityCancel(AbilitySpec abilitySpec)
        {
            Assert.IsNotNull(abilitySpec, "技能未注册正式 GAS AbilitySpec。");
            DotEntity abilityEntity = GetFormalAbilityEntity(abilitySpec);
            Assert.AreNotEqual(DotEntity.Null, abilityEntity, "正式 GAS AbilitySpec 缺少有效 ECS Entity。");
            return GASManager.EntityManager.HasComponent<CAbilityInTryCancel>(abilityEntity);
        }

        private static DotEntity GetFormalAbilityEntity(AbilitySpec abilitySpec)
        {
            PropertyInfo entityProperty = typeof(AbilitySpec).GetProperty(
                "Entity",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(entityProperty, "找不到 AbilitySpec.Entity 内部属性。");
            object value = entityProperty.GetValue(abilitySpec);
            return value is DotEntity entity ? entity : DotEntity.Null;
        }

        private static void ConfigureAbilityRoots(Transform characterTransform, CharacterAbilitySet abilitySet)
        {
            GameObject staticRoot = new("StaticAbilityRoot");
            GameObject polydirectionalRoot = new("PolydirectionalAbilityRoot");
            GameObject horizontalRoot = new("HorizontalAbilityRoot");

            staticRoot.transform.SetParent(characterTransform, false);
            polydirectionalRoot.transform.SetParent(characterTransform, false);
            horizontalRoot.transform.SetParent(characterTransform, false);

            SetInstanceField(abilitySet, "m_staticAbilityRoot", staticRoot.transform);
            SetInstanceField(abilitySet, "m_polydirectionalAbilityRoot", polydirectionalRoot.transform);
            SetInstanceField(abilitySet, "m_horizontalAbilityRoot", horizontalRoot.transform);
        }

        private static Stats CreateStats(
            int health = 0,
            int mana = 0,
            int physicalAttack = 0,
            int magicalAttack = 0,
            int physicalDefense = 0,
            int magicalDefense = 0,
            int agility = 0,
            int luck = 0,
            int attackSpeed = 0)
        {
            Stats stats = new();
            stats[EStat.Health] = health;
            stats[EStat.Mana] = mana;
            stats[EStat.PhysicalAttack] = physicalAttack;
            stats[EStat.MagicalAttack] = magicalAttack;
            stats[EStat.PhysicalDefense] = physicalDefense;
            stats[EStat.MagicalDefense] = magicalDefense;
            stats[EStat.Agility] = agility;
            stats[EStat.Luck] = luck;
            stats[EStat.AttackSpeed] = attackSpeed;
            return stats;
        }

        private static int CalculateExpectedResolvedDamage(CharacterBase attacker, CharacterBase defender, FormalDamageEffectPayload damageRule)
        {
            DamageDescriptor descriptor = damageRule.damageDescriptor;
            CombatStatSnapshot attackerStats = attacker.CreateCombatStatSnapshot();
            CombatStatSnapshot defenderStats = defender.CreateCombatStatSnapshot();
            int outgoingDamage = DamageSolver.CalculateDamageOut(
                descriptor.flatDamages,
                descriptor.scalingFactor,
                attackerStats.GetOffensiveStat(descriptor.damageType));
            int incomingDamage = DamageSolver.CalculateDamageIn(
                outgoingDamage,
                descriptor.ignoreDefense ? 0 : defenderStats.GetDefensiveStat(descriptor.damageType));
            return incomingDamage;
        }

        private static FormalDamageEffectPayload CreateBasicAttackBaseDamagePayload()
        {
            return new FormalDamageEffectPayload(
                new DamageDescriptor
                {
                    damageType = EDamageType.Physical,
                    flatDamages = 4,
                    scalingFactor = 1.0f,
                    ignoreDefense = false
                },
                EEffectVisualFlags.None,
                default,
                EEffectImpactDataType.Velocity,
                Vector2.zero);
        }

        private static FormalDamageEffectPayload CreateBackstabBonusDamagePayload()
        {
            return new FormalDamageEffectPayload(
                new DamageDescriptor
                {
                    damageType = EDamageType.Physical,
                    flatDamages = 3,
                    scalingFactor = 0.0f,
                    ignoreDefense = false
                },
                EEffectVisualFlags.None,
                default,
                EEffectImpactDataType.Velocity,
                Vector2.zero);
        }

        private static FormalDamageEffectPayload CreateChargedAttackReleaseDamagePayload()
        {
            return new FormalDamageEffectPayload(
                new DamageDescriptor
                {
                    damageType = EDamageType.Physical,
                    flatDamages = 7,
                    scalingFactor = 1.5f,
                    ignoreDefense = false
                },
                EEffectVisualFlags.None,
                default,
                EEffectImpactDataType.Velocity,
                Vector2.zero);
        }

        private static int CalculateExpectedFormalGasInputGateUseTicks()
        {
            const int firstFormalGameplayFrame = 1;
            float totalTime = firstFormalGameplayFrame / 30.0f + EditModeTickDeltaTime;
            return Mathf.Max(4, Mathf.CeilToInt(totalTime / EditModeTickDeltaTime) + 1);
        }

        private static int CalculateExpectedGasTimelineHitTicks()
        {
            return CalculateExpectedGasTimelineHitTicks(8);
        }

        private static int CalculateExpectedGasTimelineHitTicks(int timelineHitFrame)
        {
            return CalculateExpectedFormalGasInputGateUseTicks() + timelineHitFrame + 4;
        }

        private static string CreateGasCueDiagnostic(
            CharacterBase defender,
            TestMeleeFeedbackProbe targetCueProbe,
            int previousHealth,
            int expectedDamage)
        {
            int cueCount = 0;
            int gameCoreCueCount = 0;
            int playableCueCount = 0;
            int playingCueCount = 0;

            if (GASManager.ExWorld != null && GASManager.ExWorld.IsCreated)
            {
                DotEntityManager entityManager = GASManager.EntityManager;
                using DotEntityQuery query = entityManager.CreateEntityQuery(DotComponentType.ReadOnly<MCCue>());
                using NativeArray<DotEntity> cueEntities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
                foreach (DotEntity cueEntity in cueEntities)
                {
                    cueCount++;
                    MCCue cue = EntityHelper.GetManagedComponentData<MCCue>(cueEntity);
                    if (cue?.cue != null && cue.cue.GetType() == typeof(CuePlayGameCoreFeedback))
                    {
                        gameCoreCueCount++;
                    }

                    if (entityManager.HasComponent<ECCuePlayable>(cueEntity) && entityManager.IsComponentEnabled<ECCuePlayable>(cueEntity))
                    {
                        playableCueCount++;
                    }

                    if (entityManager.HasComponent<ECCuePlaying>(cueEntity) && entityManager.IsComponentEnabled<ECCuePlaying>(cueEntity))
                    {
                        playingCueCount++;
                    }
                }
            }

            return $"defenderHealth={defender.GetCurrentHealth()}, expectedHealth={previousHealth - expectedDamage}, " +
                   $"targetCuePlayCount={targetCueProbe.playCount}, cueCount={cueCount}, gameCoreCueCount={gameCoreCueCount}, " +
                   $"playableCueCount={playableCueCount}, playingCueCount={playingCueCount}";
        }

        private static void InvokeLifecycle(Component component, string methodName)
        {
            MethodInfo method = FindInstanceMethod(component.GetType(), methodName);
            Assert.IsNotNull(method, $"找不到生命周期方法 {component.GetType().Name}.{methodName}");
            method.Invoke(component, null);
        }

        private static void InvokeStaticLifecycle(object target, string methodName)
        {
            MethodInfo method = FindInstanceMethod(target.GetType(), methodName);
            Assert.IsNotNull(method, $"找不到生命周期方法 {target.GetType().Name}.{methodName}");
            method.Invoke(target, null);
        }

        private static void SetInstanceField(object target, string fieldName, object value)
        {
            Assert.IsNotNull(target, $"目标对象为空，无法写入字段 {fieldName}");
            FieldInfo field = FindInstanceField(target.GetType(), fieldName);
            Assert.IsNotNull(field, $"找不到字段 {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }

        private static void SetStaticField(Type type, string fieldName, object value)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"找不到静态字段 {type.Name}.{fieldName}");
            field.SetValue(null, value);
        }

        private static void InvokeStaticMethod(Type type, string methodName)
        {
            Assert.IsNotNull(type, $"找不到类型 {methodName} 的宿主。");
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"找不到静态方法 {type.Name}.{methodName}");
            method.Invoke(null, null);
        }

        private static void RegisterFormalGasAbilityDescriptionGeneratedRuntime()
        {
            Type type = Type.GetType("GAS.Runtime.FormalGasAbilityDescriptionGeneratedRuntime, FantasyWord.GAS.GeneratedRuntime");
            Assert.IsNotNull(type, "找不到 GAS 生成运行时的基础攻击描述注册入口。");
            InvokeStaticMethod(type, "Register");
        }

        private static Type GetRequiredFieldType(Type type, string fieldName)
        {
            FieldInfo field = FindInstanceField(type, fieldName);
            Assert.IsNotNull(field, $"找不到字段 {type.Name}.{fieldName}");
            return field.FieldType;
        }

        private static object GetInstanceFieldValue(object target, string fieldName)
        {
            Assert.IsNotNull(target, $"目标对象为空，无法读取字段 {fieldName}");
            FieldInfo field = FindInstanceField(target.GetType(), fieldName);
            Assert.IsNotNull(field, $"找不到字段 {target.GetType().Name}.{fieldName}");
            return field.GetValue(target);
        }

        private static ItemUsageResult InvokeItemAddAbilityEffect(
            ItemAddAbilityEffect effect,
            Item item,
            CharacterBase sourceOwner,
            CharacterBase target,
            EItemLocation location)
        {
            MethodInfo method = FindInstanceMethod(typeof(ItemAddAbilityEffect), "OnUse");
            Assert.IsNotNull(method, "找不到 ItemAddAbilityEffect.OnUse。");
            object result = method.Invoke(effect, new object[] { item, sourceOwner, target, location });
            Assert.IsInstanceOf<ItemUsageResult>(result, "ItemAddAbilityEffect.OnUse 必须返回 ItemUsageResult。");
            return (ItemUsageResult)result;
        }

        private static object GetInstanceFieldValue(object owner, string ownerFieldName, Type nestedType, string nestedFieldName)
        {
            object nestedObject = GetInstanceFieldValue(owner, ownerFieldName);
            Assert.IsNotNull(nestedObject, $"字段 {ownerFieldName} 为空，无法读取内部字段 {nestedFieldName}");
            FieldInfo nestedField = FindInstanceField(nestedType, nestedFieldName);
            Assert.IsNotNull(nestedField, $"找不到字段 {nestedType.Name}.{nestedFieldName}");
            return nestedField.GetValue(nestedObject);
        }

        private static FieldInfo FindInstanceField(Type type, string fieldName)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static MethodInfo FindInstanceMethod(Type type, string methodName)
        {
            return FindInstanceMethod(type, methodName, Array.Empty<Type>());
        }

        private static MethodInfo FindInstanceMethod(Type type, string methodName, params Type[] parameterTypes)
        {
            while (type != null)
            {
                MethodInfo method = parameterTypes == null || parameterTypes.Length == 0
                    ? FindFirstInstanceMethod(type, methodName)
                    : type.GetMethod(
                        methodName,
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                        null,
                        parameterTypes,
                        null);
                if (method != null)
                {
                    return method;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static MethodInfo FindInstanceMethodByParameterPrefix(Type type, string methodName, params Type[] parameterPrefixTypes)
        {
            while (type != null)
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (method.Name != methodName)
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length < parameterPrefixTypes.Length)
                    {
                        continue;
                    }

                    bool matches = true;
                    for (int i = 0; i < parameterPrefixTypes.Length; i++)
                    {
                        if (parameters[i].ParameterType != parameterPrefixTypes[i])
                        {
                            matches = false;
                            break;
                        }
                    }

                    if (matches)
                    {
                        return method;
                    }
                }

                type = type.BaseType;
            }

            return null;
        }

        private static MethodInfo FindFirstInstanceMethod(Type type, string methodName)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (method.Name == methodName)
                {
                    return method;
                }
            }

            return null;
        }

        private static void RegisterRuntimeEvent<TEvent>(Action<TEvent> handler)
        {
            InvokeRuntimeEventMethod(nameof(RegisterRuntimeEvent), "Register", handler);
        }

        private static void UnregisterRuntimeEvent<TEvent>(Action<TEvent> handler)
        {
            InvokeRuntimeEventMethod(nameof(UnregisterRuntimeEvent), "UnRegister", handler);
        }

        private static void InvokeRuntimeEventMethod<TEvent>(string caller, string methodName, Action<TEvent> handler)
        {
            Type eventKitType = Type.GetType("YokiFrame.EventKit, YokiFrame");
            Assert.IsNotNull(eventKitType, $"{caller} 找不到 YokiFrame.EventKit。");

            FieldInfo typeField = eventKitType.GetField("Type", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(typeField, $"{caller} 找不到 EventKit.Type。");

            object typeEvent = typeField.GetValue(null);
            Assert.IsNotNull(typeEvent, $"{caller} 读取到的 EventKit.Type 为空。");

            MethodInfo method = typeEvent.GetType()
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)?
                .MakeGenericMethod(typeof(TEvent));
            Assert.IsNotNull(method, $"{caller} 找不到 EventKit.Type.{methodName}<{typeof(TEvent).Name}>。");
            method.Invoke(typeEvent, new object[] { handler });
        }

        private static void SetFloat(SerializedProperty root, string relativePath, float value)
        {
            SerializedProperty property = root.FindPropertyRelative(relativePath);
            Assert.IsNotNull(property, $"找不到序列化字段 {relativePath}");
            property.floatValue = value;
        }

        private static void SetBool(SerializedProperty root, string relativePath, bool value)
        {
            SerializedProperty property = root.FindPropertyRelative(relativePath);
            Assert.IsNotNull(property, $"找不到序列化字段 {relativePath}");
            property.boolValue = value;
        }

        private static void SetEnum<TEnum>(SerializedProperty root, string relativePath, TEnum value) where TEnum : Enum
        {
            SerializedProperty property = root.FindPropertyRelative(relativePath);
            Assert.IsNotNull(property, $"找不到序列化字段 {relativePath}");
            property.enumValueIndex = Convert.ToInt32(value);
        }

        private static void SetObjectReference(SerializedProperty root, string relativePath, UnityEngine.Object value)
        {
            SerializedProperty property = root.FindPropertyRelative(relativePath);
            Assert.IsNotNull(property, $"找不到序列化字段 {relativePath}");
            property.objectReferenceValue = value;
        }

        private static void UnloadAssetAtPath<T>(string assetPath) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
            {
                Resources.UnloadAsset(asset);
            }
        }
    }
}

