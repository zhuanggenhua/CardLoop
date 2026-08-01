using System;  
using System.Collections.Generic;  
using UnityEngine;  
  
namespace GAS.Runtime  
{  
    public class TaskApplyEffects : AbilityTaskBase<XParamApplyEffects>  
    {  
        private TargetCatcherBase _catcher;  
        private List<AbilitySystemCell> _catchResults = new List<AbilitySystemCell>();  
  
        public TaskApplyEffects(AbilityLogicBase logic) : base(logic)  
        {  
        }  
  
        public override void InitParameters(XParam parameter)  
        {  
            base.InitParameters(parameter);  
            if (Parameter == null) return;

            if (string.IsNullOrEmpty(Parameter.CatcherType)) return;
            
            _catcher = TargetCatcherHelper.TryCreateTargetCatcher(Parameter.CatcherType);  
            if (_catcher != null)
            {
                if (Parameter.Param != null)  
                    _catcher.InitParameters(Parameter.Param);  
            }
        }  
  
        protected override void OnBegin(int startFrame)  
        {  
            if (_catcher == null || Parameter?.IDs == null) return;  
            
            // 每次激活都刷新上下文；同一个 AbilitySpec 会被重复使用。
            AbilityActivationContext activationContext = _logic.ActivationContext;
            _catcher.Init(Owner, activationContext);
            
            AbilitySystemCell mainTarget = activationContext?.MainTarget ?? Owner;
            _catcher.CatchTargetsNonAllocSafe(mainTarget, ref _catchResults);
            foreach (var target in _catchResults)  
            {  
                foreach (var id in Parameter.IDs)  
                {  
                    var effectCfg = GameplayEffectHelper.GetConfigByID(id);  
                    var geEntity = GameplayEffectHelper.CreateGameplayEffectEntity(effectCfg.ComponentConfigs);  
                    GameplayEffectHelper.ApplyGameplayEffectTo(geEntity, target.Entity, Owner.Entity);  
                }  
            }  
        }

        public override void OnEditorPreview(GameObject target, int frame, int startFrame, int endFrame)
        {
            base.OnEditorPreview(target, frame, startFrame, endFrame);
            if (_catcher == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"TaskApplyEffects preview skipped: target catcher is not initialized. CatcherType={Parameter?.CatcherType}");
#endif
                return;
            }

            _catcher.OnEditorPreview(target);
        }
    }  
}
