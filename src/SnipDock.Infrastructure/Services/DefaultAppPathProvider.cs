using System;
using System.IO;
using SnipDock.Core.Interfaces;

namespace SnipDock.Infrastructure.Services
{
    public class DefaultAppPathProvider : IAppPathProvider
    {
        private const string LegacyAppFolderName = "Prompt" + "Shelf";

        public string GetNewBootstrapFolderPath() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SnipDock");

        public string GetLegacyBootstrapFolderPath() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), LegacyAppFolderName);

        public string GetBootstrapLogFolderPath() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnipDock", "logs");
    }
}
