using System;
using System.Runtime.Versioning;
using Microsoft.Win32;
using SnipDock.Core.Interfaces;

namespace SnipDock.Infrastructure.Services
{
    [SupportedOSPlatform("windows")]
    public class WindowsRegistryService : IRegistryService
    {
        public string? GetValue(string keyPath, string valueName)
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, false);
            return key?.GetValue(valueName) as string;
        }

        public void SetValue(string keyPath, string valueName, object value)
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, true) 
                ?? Registry.CurrentUser.CreateSubKey(keyPath);
            key.SetValue(valueName, value);
        }

        public void DeleteValue(string keyPath, string valueName, bool throwOnMissingSubKey)
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, true);
            if (key != null)
            {
                key.DeleteValue(valueName, false);
            }
            else if (throwOnMissingSubKey)
            {
                throw new InvalidOperationException($"Registry subkey '{keyPath}' was not found.");
            }
        }
    }
}
