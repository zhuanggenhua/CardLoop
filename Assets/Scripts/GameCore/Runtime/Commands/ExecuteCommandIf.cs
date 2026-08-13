using Sirenix.OdinInspector;
using System;
using System.Threading.Tasks;
using UnityEngine;
using MackySoft.SerializeReferenceExtensions;

namespace GameCore
{
    /// <summary>
    /// 条件分支命令，根据条件结果执行 true 或 false 分支。
    /// </summary>
    [Serializable]
    public class ExecuteCommandIf : IContextualCommand
    {
        [LabelText("条件")]
        [Tooltip("为空时按 true 处理。")]
        [SerializeReference, SubclassSelector] private ICondition m_condition = null;

        [LabelText("满足时命令")]
        [Tooltip("条件满足时执行的命令。")]
        [SerializeReference, SubclassSelector] private ICommand m_ifTrue = null;

        [LabelText("不满足时命令")]
        [Tooltip("条件不满足时执行的命令。")]
        [SerializeReference, SubclassSelector] private ICommand m_ifFalse = null;

        public Task Execute()
        {
            return Execute(GameCommandContext.Script());
        }

        public Task Execute(GameCommandContext context)
        {
            if (m_condition?.Evaluate() ?? true)
            {
                return m_ifTrue.Execute(context);
            }
            else
            {
                return m_ifFalse.Execute(context);
            }
        }
    }
}

