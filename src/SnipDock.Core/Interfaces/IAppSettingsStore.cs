using SnipDock.Core.Models;

namespace SnipDock.Core.Interfaces
{
    public interface IAppSettingsStore
    {
        AppSettings Load();
        void Save(AppSettings settings);
    }
}
