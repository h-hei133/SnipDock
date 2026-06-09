using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using SnipDock.Core.Interfaces;
using SnipDock.Infrastructure.Services;

namespace SnipDock.Tests
{
    public class StartupLaunchTests
    {
        private class FakeRegistryService : IRegistryService
        {
            public readonly Dictionary<string, string> Values = new();
            public bool ThrowOnWrite { get; set; } = false;
            public bool SimulateVerifyFailure { get; set; } = false;

            public string? GetValue(string keyPath, string valueName)
            {
                if (SimulateVerifyFailure && valueName == "SnipDock")
                {
                    return null;
                }
                var fullKey = $"{keyPath}\\{valueName}";
                return Values.TryGetValue(fullKey, out var value) ? value : null;
            }

            public void SetValue(string keyPath, string valueName, object value)
            {
                if (ThrowOnWrite)
                {
                    throw new Exception("Simulated registry write failure");
                }
                var fullKey = $"{keyPath}\\{valueName}";
                Values[fullKey] = value.ToString() ?? string.Empty;
            }

            public void DeleteValue(string keyPath, string valueName, bool throwOnMissingSubKey)
            {
                var fullKey = $"{keyPath}\\{valueName}";
                Values.Remove(fullKey);
            }
        }

        [Fact]
        public async Task StartupLaunchService_EnablesAndDisablesCorrectly()
        {
            var fakeRegistry = new FakeRegistryService();
            var logger = NullLogger<StartupLaunchService>.Instance;
            var service = new StartupLaunchService(fakeRegistry, logger);

            // 1. Initially disabled
            var isEnabled = await service.IsEnabledAsync();
            Assert.False(isEnabled);

            // 2. Enable autostart
            var enabledResult = await service.EnableAsync();
            Assert.True(enabledResult);

            // Verify registry value format (must have quotes and --startup)
            var expectedValue = $"\"{service.GetCurrentExecutablePath()}\" --startup";
            var registryValue = fakeRegistry.GetValue(@"Software\Microsoft\Windows\CurrentVersion\Run", "SnipDock");
            Assert.Equal(expectedValue, registryValue);

            // 3. Confirm is enabled
            isEnabled = await service.IsEnabledAsync();
            Assert.True(isEnabled);

            // 4. Disable autostart
            var disabledResult = await service.DisableAsync();
            Assert.True(disabledResult);

            // Confirm registry key is removed
            registryValue = fakeRegistry.GetValue(@"Software\Microsoft\Windows\CurrentVersion\Run", "SnipDock");
            Assert.Null(registryValue);

            isEnabled = await service.IsEnabledAsync();
            Assert.False(isEnabled);
        }

        [Fact]
        public void StartupLaunchService_DetectsDevelopmentModeCorrectly()
        {
            var fakeRegistry = new FakeRegistryService();
            var logger = NullLogger<StartupLaunchService>.Instance;
            var service = new StartupLaunchService(fakeRegistry, logger);

            // Under test environment, the process path normally contains bin\Debug or similar,
            // so IsDevelopmentMode should return true.
            var isDev = service.IsDevelopmentMode();
            
            var path = service.GetCurrentExecutablePath();
            bool expectedDev = string.IsNullOrEmpty(path) || 
                              path.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase) ||
                              path.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase) ||
                              path.Contains(@"\obj\", StringComparison.OrdinalIgnoreCase) ||
                              path.Contains(@"\dotnet", StringComparison.OrdinalIgnoreCase);

            Assert.Equal(expectedDev, isDev);
        }

        [Fact]
        public async Task StartupLaunchService_MigratesLegacyRegistryKeySafely()
        {
            var fakeRegistry = new FakeRegistryService();
            var logger = NullLogger<StartupLaunchService>.Instance;
            var service = new StartupLaunchService(fakeRegistry, logger);
            
            var runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
            var legacyVal = "\"C:\\Path\\To\\Old\\PromptShelf.exe\" --startup";
            fakeRegistry.SetValue(runKey, "PromptShelf", legacyVal);
            
            var isEnabled = await service.IsEnabledAsync();
            Assert.True(isEnabled);
            
            var expectedNewVal = $"\"{service.GetCurrentExecutablePath()}\" --startup";
            var newVal = fakeRegistry.GetValue(runKey, "SnipDock");
            Assert.Equal(expectedNewVal, newVal);
            
            var oldVal = fakeRegistry.GetValue(runKey, "PromptShelf");
            Assert.Null(oldVal);
        }

        [Fact]
        public async Task StartupLaunchService_SafeMigration_RetainsLegacyKeyOnWriteFailure()
        {
            var fakeRegistry = new FakeRegistryService();
            var logger = NullLogger<StartupLaunchService>.Instance;
            var service = new StartupLaunchService(fakeRegistry, logger);
            
            var runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
            var legacyVal = "\"C:\\Path\\To\\Old\\PromptShelf.exe\" --startup";
            fakeRegistry.SetValue(runKey, "PromptShelf", legacyVal);
            
            fakeRegistry.ThrowOnWrite = true;
            
            var isEnabled = await service.IsEnabledAsync();
            Assert.False(isEnabled);
            
            fakeRegistry.ThrowOnWrite = false;
            
            var oldVal = fakeRegistry.GetValue(runKey, "PromptShelf");
            Assert.Equal(legacyVal, oldVal);
            
            var newVal = fakeRegistry.GetValue(runKey, "SnipDock");
            Assert.Null(newVal);
        }

        [Fact]
        public async Task StartupLaunchService_SafeMigration_RetainsLegacyKeyOnVerificationFailure()
        {
            var fakeRegistry = new FakeRegistryService();
            var logger = NullLogger<StartupLaunchService>.Instance;
            var service = new StartupLaunchService(fakeRegistry, logger);
            
            var runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
            var legacyVal = "\"C:\\Path\\To\\Old\\PromptShelf.exe\" --startup";
            fakeRegistry.SetValue(runKey, "PromptShelf", legacyVal);
            
            fakeRegistry.SimulateVerifyFailure = true;
            
            var isEnabled = await service.IsEnabledAsync();
            Assert.False(isEnabled);
            
            fakeRegistry.SimulateVerifyFailure = false;
            
            var oldVal = fakeRegistry.GetValue(runKey, "PromptShelf");
            Assert.Equal(legacyVal, oldVal);
        }
    }
}
