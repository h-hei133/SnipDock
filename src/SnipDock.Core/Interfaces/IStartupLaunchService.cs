using System.Threading.Tasks;

namespace SnipDock.Core.Interfaces
{
    public interface IStartupLaunchService
    {
        bool IsDevelopmentMode();
        string GetCurrentExecutablePath();
        Task<bool> IsEnabledAsync();
        Task<bool> EnableAsync();
        Task<bool> DisableAsync();
    }
}
