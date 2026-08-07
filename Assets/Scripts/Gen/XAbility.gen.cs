///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

namespace GAS.Runtime
{
    public static class XAbility
    {
        public const int ABILITY_Attack = 20001;
        public const int ABILITY_TransformReplaceSmoke = 20002;
        public const int ABILITY_ChargedAttackRelease = 20004;

        public static void LoadAbilityCode()
        {
            ///  AbilityLogic
            var ALApplyEffect = typeof(GAS.Runtime.ALApplyEffect);
            GAS.Runtime.AbilityHelper.RegisterAbilityLogic(ALApplyEffect.Name, ALApplyEffect,typeof(GAS.Runtime.XParamEffectIDs));
            var ALDebugLog = typeof(GAS.Runtime.ALDebugLog);
            GAS.Runtime.AbilityHelper.RegisterAbilityLogic(ALDebugLog.Name, ALDebugLog,typeof(GAS.Runtime.XParamString));
            var ALTimeline = typeof(GAS.Runtime.ALTimeline);
            GAS.Runtime.AbilityHelper.RegisterAbilityLogic(ALTimeline.Name, ALTimeline,typeof(GAS.Runtime.XParamALTimelineID));
            var FormalAbilityRuleProxyLogic = typeof(GameCore.FormalAbilityRuleProxyLogic);
            GAS.Runtime.AbilityHelper.RegisterAbilityLogic(FormalAbilityRuleProxyLogic.Name, FormalAbilityRuleProxyLogic,typeof(GAS.Runtime.XParamNone));

            ///  AbilityTask
            var TaskApplyEffects = typeof(GAS.Runtime.TaskApplyEffects);
            GAS.Runtime.AbilityHelper.RegisterAbilityTask(TaskApplyEffects.Name, TaskApplyEffects,typeof(GAS.Runtime.XParamApplyEffects));
            var TaskDebug = typeof(GAS.Runtime.TaskDebug);
            GAS.Runtime.AbilityHelper.RegisterAbilityTask(TaskDebug.Name, TaskDebug,typeof(GAS.Runtime.XParamString));
            var TaskDoCooldown = typeof(GAS.Runtime.TaskDoCooldown);
            GAS.Runtime.AbilityHelper.RegisterAbilityTask(TaskDoCooldown.Name, TaskDoCooldown,typeof(GAS.Runtime.XParamNone));
            var TaskDoCost = typeof(GAS.Runtime.TaskDoCost);
            GAS.Runtime.AbilityHelper.RegisterAbilityTask(TaskDoCost.Name, TaskDoCost,typeof(GAS.Runtime.XParamNone));
            var TaskDoNothing = typeof(GAS.Runtime.TaskDoNothing);
            GAS.Runtime.AbilityHelper.RegisterAbilityTask(TaskDoNothing.Name, TaskDoNothing,typeof(GAS.Runtime.XParamNone));
            var TaskPlayCue = typeof(GAS.Runtime.TaskPlayCue);
            GAS.Runtime.AbilityHelper.RegisterAbilityTask(TaskPlayCue.Name, TaskPlayCue,typeof(GAS.Runtime.XParamCue));

            ///  TargetCatcher
            var CatchAreaBox3D = typeof(GAS.Runtime.CatchAreaBox3D);
            GAS.Runtime.TargetCatcherHelper.RegisterTargetCatcher(CatchAreaBox3D.Name, CatchAreaBox3D,typeof(GAS.Runtime.XParamCatchAreaBox3D));
            var CatchSelf = typeof(GAS.Runtime.CatchSelf);
            GAS.Runtime.TargetCatcherHelper.RegisterTargetCatcher(CatchSelf.Name, CatchSelf,typeof(GAS.Runtime.XParamNone));
            var CatchTarget = typeof(GAS.Runtime.CatchTarget);
            GAS.Runtime.TargetCatcherHelper.RegisterTargetCatcher(CatchTarget.Name, CatchTarget,typeof(GAS.Runtime.XParamNone));
            var CatchAreaBox2D = typeof(GameCore.CatchAreaBox2D);
            GAS.Runtime.TargetCatcherHelper.RegisterTargetCatcher(CatchAreaBox2D.Name, CatchAreaBox2D,typeof(GameCore.XParamCatchAreaBox2D));
            var CatchAreaCircle2D = typeof(GameCore.CatchAreaCircle2D);
            GAS.Runtime.TargetCatcherHelper.RegisterTargetCatcher(CatchAreaCircle2D.Name, CatchAreaCircle2D,typeof(GameCore.XParamCatchAreaCircle2D));
            var CatchAreaPolygon2D = typeof(GameCore.CatchAreaPolygon2D);
            GAS.Runtime.TargetCatcherHelper.RegisterTargetCatcher(CatchAreaPolygon2D.Name, CatchAreaPolygon2D,typeof(GameCore.XParamCatchAreaPolygon2D));
        }
    }
}
