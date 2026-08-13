using System;
using System.Collections.Generic;
using GameCore;
using UnityEngine;
using JSON = SimpleJSON.JSON;
using JSONNode = SimpleJSON.JSONNode;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GAS.Runtime
{
    public static class GasGeneratedConfigIntegration
    {
        private const int GasTimelineFrameRate = 30;

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void RegisterForEditor()
        {
            Register();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterForRuntime()
        {
            Register();
        }

        public static void Register()
        {
            CharacterAttributes.Configure(
                new CharacterAttributeCodes(
                    XAttrSet.FightUnit,
                    XAttrSet.AS_FightUnit.Health,
                    XAttrSet.AS_FightUnit.Mana,
                    XAttrSet.AS_FightUnit.MoveSpeed,
                    XAttrSet.AS_FightUnit.Attack,
                    XAttrSet.AS_FightUnit.Defense,
                    XAttrSet.AS_FightUnit.Stamina,
                    XAttrSet.AS_FightUnit.MaxHealth,
                    XAttrSet.AS_FightUnit.MaxMana,
                    XAttrSet.AS_FightUnit.MaxStamina,
                    XAttrSet.AS_FightUnit.AttackSpeed,
                    XAttrSet.AS_FightUnit.Accuracy,
                    XAttrSet.AS_FightUnit.Dodge,
                    XAttrSet.AS_FightUnit.CriticalChance,
                    XAttrSet.AS_FightUnit.CriticalMultiplier),
                () => XAttrSet.AttributeSetMap[XAttrSet.FightUnit]);

            FormalGasAbilityIdentityResolver.RegisterTryResolveAbilityIdentityHandler(TryResolveAbilityIdentity);
            FormalGasAbilityRuntimeConfigResolver.RegisterTryResolveRuntimeConfigHandler(TryResolveRuntimeConfig);
            FormalGasAbilityDescriptionResolver.RegisterAppendFormalDamageLinesHandler(TryAppendFormalDamageLines);
            FormalGasAbilityTimelineExecutionResolver.RegisterTryResolveTimelineExecutionSettingsHandler(
                TryResolveTimelineExecutionSettings);
            GameplayEffectHelper.RegisterGetConfigByIDFunc(GetGameplayEffectConfigWithFormalDamage);
        }

        /// <summary>
        /// 将生成的 EX-GAS 配置表接入当前项目的资源生命周期。
        /// 编辑器未启动资源系统时允许读取作者 JSON；运行时必须通过 ResourceSystem / YooAsset。
        /// </summary>
        public static bool InitializeConfigTables()
        {
#if UNITY_EDITOR
            if (!ResourceSystem.Initialized)
            {
                XLuban.LoadTablesForEditor();
                Register();
                return true;
            }
#endif

            XLauncher.InitConfigTables(LoadConfigTable);
            // XLuban.Init 会重设 GameplayEffect 的基础解析器，随后恢复项目已登记的伤害扩展。
            Register();
            return true;
        }

        private static JSONNode LoadConfigTable(string tableName)
        {
            TextAsset tableAsset = FormalGasAbilityResourceLoader.LoadRuntimeAddressSync<TextAsset>(tableName);
            if (tableAsset == null)
            {
                throw new InvalidOperationException(
                    $"无法从 YooAsset 加载 EX-GAS 配置表：{tableName}。"
                    + "请检查 DefaultPackage 的 gas-config 收集组和资源地址。");
            }

            return JSON.Parse(tableAsset.text);
        }

        private static bool TryResolveAbilityIdentity(
            int abilityCode,
            out FormalGasAbilityIdentity identity)
        {
#if UNITY_EDITOR
            XLuban.LoadTablesForEditor();
#endif

            cfg.exgas.ability ability = XLuban.Tables?.Tbability.GetOrDefault(abilityCode);
            if (ability == null)
            {
                identity = default;
                return false;
            }

            identity = new FormalGasAbilityIdentity(ability.Name, ability.Desc);
            return true;
        }

        private static bool TryResolveRuntimeConfig(
            int abilityCode,
            out FormalGasAbilityRuntimeConfig config)
        {
#if UNITY_EDITOR
            XLuban.LoadTablesForEditor();
#endif

            cfg.exgas.abilityGameCore abilityConfig = XLuban.Tables?.TbabilityGameCore.GetOrDefault(abilityCode);
            if (abilityConfig == null)
            {
                config = default;
                return false;
            }

            config = new FormalGasAbilityRuntimeConfig(
                abilityConfig.PrefabGuid,
                abilityConfig.PrefabPath,
                abilityConfig.IconGuid,
                abilityConfig.IconPath,
                ResolveAbilityRootMode(abilityConfig.AbilityRootMode),
                new FormalAbilityInputGateConfig(
                    ResolveTriggerMode(abilityConfig.InputTriggerMode),
                    abilityConfig.BufferInput,
                    abilityConfig.NewInputExtendsBuffer,
                    abilityConfig.MaximumBufferDuration,
                    abilityConfig.DelayBeforeUseReleaseInterruption,
                    abilityConfig.TimeBetweenUsesReleaseInterruption,
                    abilityConfig.UpdateLookAtDirectionOnFire));
            return true;
        }

        private static EFormalGasAbilityRootMode ResolveAbilityRootMode(int value)
        {
            return value switch
            {
                0 => EFormalGasAbilityRootMode.Static,
                2 => EFormalGasAbilityRootMode.Horizontal,
                _ => EFormalGasAbilityRootMode.Polydirectional
            };
        }

        private static EFormalAbilityInputTriggerMode ResolveTriggerMode(int value)
        {
            return value switch
            {
                (int)EFormalAbilityInputTriggerMode.Auto => EFormalAbilityInputTriggerMode.Auto,
                (int)EFormalAbilityInputTriggerMode.HoldRelease => EFormalAbilityInputTriggerMode.HoldRelease,
                _ => EFormalAbilityInputTriggerMode.SemiAuto
            };
        }

        private static GameplayEffectConfig GetGameplayEffectConfigWithFormalDamage(int id)
        {
#if UNITY_EDITOR
            XLuban.LoadTablesForEditor();
#endif

            GameplayEffectConfig baseConfig = XLuban.GetGameplayEffectConfig(id);
            cfg.exgas.gameplayEffect effect = XLuban.Tables?.TbgameplayEffect.GetOrDefault(id);
            if (baseConfig == null || effect == null)
            {
                return baseConfig;
            }

            List<GameplayEffectComponentConfig> configs = new();
            if (baseConfig.ComponentConfigs != null)
            {
                configs.AddRange(baseConfig.ComponentConfigs);
            }

            if (effect.FormalDamage != null)
            {
                configs.Add(new MCConfGameplayEffectDamage(CreatePayload(effect.FormalDamage.Value)));
            }

            if (effect.FormalConditionalDamage != null)
            {
                configs.Add(new MCConfGameplayEffectConditionalDamage(
                    CreateConditionalPayload(effect.FormalConditionalDamage.Value)));
            }

            return new GameplayEffectConfig(configs.ToArray());
        }

        private static bool TryResolveTimelineExecutionSettings(
            int abilityCode,
            out FormalGasTimelineExecutionSettings settings)
        {
#if UNITY_EDITOR
            XLuban.LoadTablesForEditor();
#endif

            settings = default;
            cfg.exgas.ability ability = XLuban.Tables?.Tbability.GetOrDefault(abilityCode);
            if (ability?.AbilityLogic is not cfg.ALTimeline timelineLogic)
            {
                return false;
            }

            cfg.exgas.timelineAbility timeline = XLuban.Tables.TbtimelineAbility.GetOrDefault(timelineLogic.Param.ID);
            if (timeline == null)
            {
                return false;
            }

            float delayBeforeUse = Mathf.Max(0, FindFirstGameplayTaskStartFrame(timeline)) / (float)GasTimelineFrameRate;
            float timeBetweenUses = Mathf.Max(0, timeline.LifeTime) / (float)GasTimelineFrameRate;
            settings = new FormalGasTimelineExecutionSettings(delayBeforeUse, timeBetweenUses);
            return true;
        }

        private static int FindFirstGameplayTaskStartFrame(cfg.exgas.timelineAbility timeline)
        {
            int firstFrame = int.MaxValue;
            if (timeline.Tracks == null)
            {
                return 0;
            }

            foreach (cfg.Track track in timeline.Tracks)
            {
                if (track.TaskClips == null)
                {
                    continue;
                }

                foreach (cfg.TaskClip taskClip in track.TaskClips)
                {
                    if (taskClip.Task is cfg.TaskDoCost or cfg.TaskApplyEffects)
                    {
                        firstFrame = Mathf.Min(firstFrame, taskClip.StartTime);
                    }
                }
            }

            return firstFrame == int.MaxValue ? 0 : firstFrame;
        }

        private static bool TryAppendFormalDamageLines(int abilityCode, List<AbilityDescriptionLine> lines)
        {
#if UNITY_EDITOR
            XLuban.LoadTablesForEditor();
#endif

            cfg.exgas.ability ability = XLuban.Tables?.Tbability.GetOrDefault(abilityCode);
            if (ability?.AbilityLogic is not cfg.ALTimeline timelineLogic)
            {
                return false;
            }

            cfg.exgas.timelineAbility timeline = XLuban.Tables.TbtimelineAbility.GetOrDefault(timelineLogic.Param.ID);
            if (timeline?.Tracks == null)
            {
                return false;
            }

            bool appended = false;
            foreach (cfg.Track track in timeline.Tracks)
            {
                if (track.TaskClips == null)
                {
                    continue;
                }

                foreach (cfg.TaskClip taskClip in track.TaskClips)
                {
                    if (taskClip.Task is not cfg.TaskApplyEffects applyEffects ||
                        applyEffects.Param?.IDs == null)
                    {
                        continue;
                    }

                    foreach (int effectId in applyEffects.Param.IDs)
                    {
                        cfg.exgas.gameplayEffect effect = XLuban.Tables.TbgameplayEffect.GetOrDefault(effectId);
                        if (effect == null)
                        {
                            continue;
                        }

                        appended |= AppendFormalDamage(effect.FormalDamage, lines);
                        appended |= AppendFormalConditionalDamage(effect.FormalConditionalDamage, lines);
                    }
                }
            }

            return appended;
        }

        private static bool AppendFormalDamage(cfg.FormalDamage? damage, List<AbilityDescriptionLine> lines)
        {
            if (damage == null)
            {
                return false;
            }

            return AppendPayload(CreatePayload(damage.Value), lines);
        }

        private static bool AppendFormalConditionalDamage(cfg.FormalConditionalDamage? damage, List<AbilityDescriptionLine> lines)
        {
            if (damage == null)
            {
                return false;
            }

            return AppendPayload(CreatePayload(damage.Value), lines);
        }

        private static bool AppendPayload(GameplayEffectDamagePayload payload, List<AbilityDescriptionLine> lines)
        {
            if (!payload.TryGenerateDescription(out AbilityDescriptionLine description))
            {
                return false;
            }

            lines.Add(description);
            return true;
        }

        private static GameplayEffectDamagePayload CreatePayload(cfg.FormalDamage damage)
        {
            return new GameplayEffectDamagePayload(
                new DamageDescriptor(
                    (EDamageType)damage.DamageType,
                    damage.FlatDamage,
                    damage.ScalingFactor,
                    damage.IgnoreDefense),
                (EEffectVisualFlags)damage.VisualFlags,
                new DamageImpactSettings
                {
                    pushMode = (EDamagePushMode)damage.PushMode,
                    pushIntensity = damage.PushIntensity,
                    pushResistance = damage.PushResistance,
                    invincibilityDuration = damage.InvincibilityDuration
                },
                (EEffectImpactDataType)damage.ImpactDataType,
                new Vector2(damage.ImpactData.X, damage.ImpactData.Y));
        }

        private static GameplayEffectDamagePayload CreatePayload(cfg.FormalConditionalDamage damage)
        {
            return new GameplayEffectDamagePayload(
                new DamageDescriptor(
                    (EDamageType)damage.DamageType,
                    damage.FlatDamage,
                    damage.ScalingFactor,
                    damage.IgnoreDefense),
                (EEffectVisualFlags)damage.VisualFlags,
                new DamageImpactSettings
                {
                    pushMode = (EDamagePushMode)damage.PushMode,
                    pushIntensity = damage.PushIntensity,
                    pushResistance = damage.PushResistance,
                    invincibilityDuration = damage.InvincibilityDuration
                },
                (EEffectImpactDataType)damage.ImpactDataType,
                new Vector2(damage.ImpactData.X, damage.ImpactData.Y));
        }

        private static GameplayEffectConditionalDamagePayload CreateConditionalPayload(cfg.FormalConditionalDamage damage)
        {
            return new GameplayEffectConditionalDamagePayload(
                new GameplayEffectDamageCondition(
                    (EDamageConditionKind)damage.ConditionKind,
                    damage.FacingDotThreshold),
                CreatePayload(damage));
        }
    }
}

