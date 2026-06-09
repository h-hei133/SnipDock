using SnipDock.Core.Models;

namespace SnipDock.Core.Interfaces
{
    public interface IBootstrapSettingsStore
    {
        BootstrapSettings Load();
        void Save(BootstrapSettings settings);
    }
}
