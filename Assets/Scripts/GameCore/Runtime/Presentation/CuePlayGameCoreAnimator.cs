using GAS.Runtime;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// EX-GAS 动画 Cue 只提交动作键。
    /// 角色 Animator、武器序列和武器自带特效由角色表现驱动统一解析。
    /// </summary>
    public sealed class CuePlayGameCoreAnimator : GameplayCueBase<XParamAnimator>
    {
        private ICharacterAnimationDriver m_animationDriver;
        private string m_activeAnimationKey;

        public override void OnAdd(float time)
        {
            base.OnAdd(time);
            m_animationDriver = ResolveAnimationDriver(_abilitySystemCell?.GameObject);
            m_activeAnimationKey = null;
        }

        public override void OnActivate(float time)
        {
            base.OnActivate(time);
            string animationKey = Parameter?.AnimationName?.Trim();
            if (string.IsNullOrWhiteSpace(animationKey))
            {
                return;
            }

            if (m_animationDriver != null && m_animationDriver.TryPlayAnimation(animationKey))
            {
                m_activeAnimationKey = animationKey;
                return;
            }

            // 纯 EditMode 逻辑测试不会构建角色表现层；运行时 Prefab 缺配置仍必须直接报错。
            if (!Application.isPlaying)
            {
                return;
            }

            Debug.LogError(
                $"EX-GAS 动画 Cue 无法播放动作“{animationKey}”。"
                + "请确认角色 Prefab 已配置 ICharacterAnimationDriver，"
                + "且 Animator 与动作数据库包含该动作。",
                _abilitySystemCell?.GameObject);
        }

        public override void OnRemove(float time)
        {
            base.OnRemove(time);
            if (m_animationDriver != null
                && !string.IsNullOrWhiteSpace(m_activeAnimationKey)
                && !m_animationDriver.TryRestoreDefaultAnimation(m_activeAnimationKey)
                && Application.isPlaying)
            {
                Debug.LogError(
                    $"EX-GAS 动画 Cue 结束后无法从动作“{m_activeAnimationKey}”恢复默认动作。"
                    + "请检查角色动画驱动、动作数据库与 Animator 状态。",
                    _abilitySystemCell?.GameObject);
            }

            m_activeAnimationKey = null;
            m_animationDriver = null;
        }

#if UNITY_EDITOR
        public override void OnPreview(GameObject target, int frame, int startFrame, int endFrame)
        {
            base.OnPreview(target, frame, startFrame, endFrame);

            string animationKey = Parameter?.AnimationName?.Trim();
            if (target == null || string.IsNullOrWhiteSpace(animationKey))
            {
                return;
            }

            float normalizedTime = endFrame > startFrame
                ? Mathf.Clamp01((float)(frame - startFrame) / (endFrame - startFrame))
                : 0f;

            ResolveAnimationDriver(target)?.TryPreviewAnimation(animationKey, normalizedTime);
            UnityEditor.SceneView.RepaintAll();
        }
#endif

        private ICharacterAnimationDriver ResolveAnimationDriver(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            MonoBehaviour[] behaviours = target.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICharacterAnimationDriver driver)
                {
                    return driver;
                }
            }

            return null;
        }
    }
}
