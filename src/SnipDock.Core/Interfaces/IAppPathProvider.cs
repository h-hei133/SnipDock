using System;

namespace SnipDock.Core.Interfaces
{
    public interface IAppPathProvider
    {
        string GetNewBootstrapFolderPath();   // %APPDATA%\SnipDock
        string GetLegacyBootstrapFolderPath(); // legacy app bootstrap folder
        string GetBootstrapLogFolderPath();    // %LOCALAPPDATA%\SnipDock\logs
    }
}
