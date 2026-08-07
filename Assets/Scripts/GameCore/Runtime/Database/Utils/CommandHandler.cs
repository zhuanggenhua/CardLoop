using System.Threading.Tasks;
using System;
using UnityEngine;
using MackySoft.SerializeReferenceExtensions;

namespace GameCore
{
    [CreateAssetMenu(menuName = AssetMenuIndexer.Utils + nameof(CommandHandler))]
    public class CommandHandler : DatabaseEntry
    {
        [SerializeReference, SubclassSelector]
        private ICommand m_command = null;

        public Task Execute()
        {
            return Execute(GameCommandContext.Script());
        }

        public Task Execute(GameCommandContext context)
        {
            if (m_command == null)
            {
                throw new InvalidOperationException($"{nameof(CommandHandler)} '{name}' 缺少要执行的命令。");
            }

            return m_command.Execute(context);
        }
    }
}

