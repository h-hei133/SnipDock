using System;

namespace SnipDock.Core.Interfaces
{
    public interface IAppPathProvider
    {
        string GetNewBootstrapFolderPath();   // %APPDATA%\SnipDock
        string GetLegacyBootstrapFolderPath(); // %APPDATA%\PromptShelf
        string GetBootstrapLogFolderPath();    // %LOCALAPPDATA%\SnipDock\logs
    }
}
