using System.Collections.Generic;
using System.Threading.Tasks;
using SnipDock.Core.Models;

namespace SnipDock.Core.Interfaces
{
    public interface IPromptStore
    {
        Task<IReadOnlyList<PromptItem>> LoadAsync();
        Task SaveAsync(IReadOnlyList<PromptItem> prompts);
        
        /// <summary>
        /// 指示数据是否由于主 prompts.json 文件损坏或丢失而成功从备份文件 (prompts.json.bak) 中恢复。
        /// </summary>
        bool WasRecoveredFromBackup { get; }
    }
}
