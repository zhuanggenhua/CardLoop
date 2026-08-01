using UnityEditor;

namespace FantasyWord.GameCore
{
    internal static class CharacterSheetFeedbackEditorUtility
    {
        public static void DrawFeedbackWarnings(CharacterSheet sheet)
        {
            if (sheet == null)
            {
                return;
            }

            GameplayFeedbackSet feedbacks = sheet.feedbacks;
            if (feedbacks == null)
            {
                EditorGUILayout.HelpBox(
                    "角色反馈配置为空。EX-GAS Cue 触发后没有正式 GameplayFeedbackSet 可以承接表现，请先补角色反馈槽位。",
                    MessageType.Warning);
                return;
            }

            if (!feedbacks.HasFeedback(EGameCoreFeedbackCueKind.HitDamageable))
            {
                EditorGUILayout.HelpBox(
                    "命中可受伤目标反馈未配置。当前基础攻击已经通过 EX-GAS GameplayEffect CueOnApply 触发 HitDamageable；如果这里为空，运行时会有命中结算但没有音效/特效/MMFeedbacks 表现。请在角色正式 GameplayFeedbackSet.HitDamageable 槽位配置表现资产，不要回到旧近战执行资产补第二套反馈。",
                    MessageType.Info);
            }
        }
    }
}
