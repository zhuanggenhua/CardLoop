using System;
using System.Collections.Generic;
using GAS.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// GameCore 可由 EX-GAS Cue 触发的反馈事件类型。
    /// </summary>
    public enum EGameCoreFeedbackCueKind
    {
        AbilityStart = 0,
        AbilityStop = 1,
        WeaponStart = 2,
        WeaponUse = 3,
        WeaponStop = 4,
        Interrupted = 5,
        HitDamageable = 6,
        HitNonDamageable = 7,
        HitAnything = 8,
        DamageTaken = 9,
        Death = 10,
        ReloadNeeded = 11,
        ReloadStart = 12,
        ReloadComplete = 13
    }

    /// <summary>
    /// Cue 播放反馈时解析角色的目标侧，决定使用效果目标还是效果来源。
    /// </summary>
    public enum EGameCoreFeedbackCueTarget
    {
        Target = 0,
        Source = 1
    }

    /// <summary>
    /// EX-GAS Cue 到 GameCore 反馈闭包的唯一桥。
    /// 技能时间轴只配置 Cue；MMFeedbacks 仍只藏在 GameCore 的 GameplayFeedbackSet 边界内。
    /// </summary>
    public sealed class CuePlayGameCoreFeedback : GameplayCueBase<XParamGameCoreFeedback>
    {
        public override void OnActivate(float time)
        {
            base.OnActivate(time);
            CharacterBase character = ResolveCharacter();
            if (character == null)
            {
                return;
            }

            Play(character.characterSheet.feedbacks, character.transform.position);
        }

        private CharacterBase ResolveCharacter()
        {
            AbilitySystemCell cell = Parameter.Target == EGameCoreFeedbackCueTarget.Source
                ? GetEffectSpec()?.Source ?? _abilitySystemCell
                : _abilitySystemCell;

            return cell?.GameObject != null
                ? cell.GameObject.GetComponent<CharacterBase>()
                : null;
        }

        private void Play(GameplayFeedbackSet feedbacks, Vector3 position)
        {
            if (feedbacks == null)
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!feedbacks.HasFeedback(Parameter.Kind))
            {
                Debug.LogWarning(
                    $"EX-GAS Cue 已触发 GameCore 表现事件 {Parameter.Kind}，但目标 {Parameter.Target} 的 GameplayFeedbackSet 未配置对应 MMFeedbacks。请在角色/实体的正式反馈槽位补表现资产，而不是回到技能旧执行资产补第二套反馈。",
                    ResolveCharacter());
            }
#endif

            switch (Parameter.Kind)
            {
                case EGameCoreFeedbackCueKind.AbilityStart:
                    feedbacks.PlayAbilityStart(position);
                    break;
                case EGameCoreFeedbackCueKind.AbilityStop:
                    feedbacks.PlayAbilityStop(position);
                    break;
                case EGameCoreFeedbackCueKind.WeaponStart:
                    feedbacks.PlayWeaponStart(position);
                    break;
                case EGameCoreFeedbackCueKind.WeaponUse:
                    feedbacks.PlayWeaponUse(position);
                    break;
                case EGameCoreFeedbackCueKind.WeaponStop:
                    feedbacks.PlayWeaponStop(position);
                    break;
                case EGameCoreFeedbackCueKind.Interrupted:
                    feedbacks.PlayInterrupted(position);
                    break;
                case EGameCoreFeedbackCueKind.HitDamageable:
                    feedbacks.PlayHitDamageable(position);
                    break;
                case EGameCoreFeedbackCueKind.HitNonDamageable:
                    feedbacks.PlayHitNonDamageable(position);
                    break;
                case EGameCoreFeedbackCueKind.HitAnything:
                    feedbacks.PlayHitAnything(position);
                    break;
                case EGameCoreFeedbackCueKind.DamageTaken:
                    feedbacks.PlayDamageTaken(position);
                    break;
                case EGameCoreFeedbackCueKind.Death:
                    feedbacks.PlayDeath(position);
                    break;
                case EGameCoreFeedbackCueKind.ReloadNeeded:
                    feedbacks.PlayReloadNeeded(position);
                    break;
                case EGameCoreFeedbackCueKind.ReloadStart:
                    feedbacks.PlayReloadStart(position);
                    break;
                case EGameCoreFeedbackCueKind.ReloadComplete:
                    feedbacks.PlayReloadComplete(position);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    /// <summary>
    /// EX-GAS Cue 的参数载荷，把表格中的整数配置解码成 GameCore 反馈事件和目标侧。
    /// </summary>
    [Serializable]
    public sealed class XParamGameCoreFeedback : XParam
    {
        [ShowInInspector]
        [LabelText("反馈事件")]
        [BeanField(nameof(SetKind), LubanType = "int", Comment = "GameCore反馈事件", Order = 1)]
        public EGameCoreFeedbackCueKind Kind { get; private set; } = EGameCoreFeedbackCueKind.WeaponUse;

        [ShowInInspector]
        [LabelText("反馈目标")]
        [BeanField(nameof(SetTarget), LubanType = "int", Comment = "GameCore反馈目标", Order = 2)]
        public EGameCoreFeedbackCueTarget Target { get; private set; } = EGameCoreFeedbackCueTarget.Target;

        public void SetKind(int kind)
        {
            Kind = Enum.IsDefined(typeof(EGameCoreFeedbackCueKind), kind)
                ? (EGameCoreFeedbackCueKind)kind
                : EGameCoreFeedbackCueKind.WeaponUse;
        }

        public void SetTarget(int target)
        {
            Target = Enum.IsDefined(typeof(EGameCoreFeedbackCueTarget), target)
                ? (EGameCoreFeedbackCueTarget)target
                : EGameCoreFeedbackCueTarget.Target;
        }

#if UNITY_EDITOR
        public void DecodeExcelData(List<object> paramData)
        {
            if (paramData == null || paramData.Count == 0)
            {
                Kind = EGameCoreFeedbackCueKind.WeaponUse;
                Target = EGameCoreFeedbackCueTarget.Target;
                return;
            }

            if (paramData.Count > 0 && int.TryParse(paramData[0]?.ToString(), out int kind))
            {
                SetKind(kind);
            }

            if (paramData.Count > 1 && int.TryParse(paramData[1]?.ToString(), out int target))
            {
                SetTarget(target);
            }
        }

        public List<object> EncodeExcelData()
        {
            return new List<object>
            {
                (int)Kind,
                (int)Target
            };
        }
#endif
    }
}
