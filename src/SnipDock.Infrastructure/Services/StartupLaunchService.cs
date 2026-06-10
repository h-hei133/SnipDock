using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SnipDock.Core.Interfaces;

namespace SnipDock.Infrastructure.Services
{
    public class StartupLaunchService : IStartupLaunchService
    {
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "SnipDock";
        private readonly IRegistryService _registryService;
        private readonly ILogger<StartupLaunchService> _logger;
        private readonly Func<string?> _getExecutablePath;

        public StartupLaunchService(IRegistryService registryService, ILogger<StartupLaunchService> logger)
            : this(registryService, logger, () => Environment.ProcessPath)
        {
        }

        public StartupLaunchService(IRegistryService registryService, ILogger<StartupLaunchService> logger, Func<string?> getExecutablePath)
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _getExecutablePath = getExecutablePath ?? throw new ArgumentNullException(nameof(getExecutablePath));
        }

        public bool IsDevelopmentMode()
        {
            var path = GetCurrentExecutablePath();
            if (string.IsNullOrEmpty(path)) return true;
            return path.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains(@"\obj\", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(Path.GetFileName(path), "dotnet.exe", StringComparison.OrdinalIgnoreCase);
        }

        public string GetCurrentExecutablePath()
        {
            return _getExecutablePath() ?? string.Empty;
        }

        public Task<bool> IsEnabledAsync()
        {
            _logger.LogInformation("Startup launch state checked");
            try
            {
                // 1. Check if new SnipDock exists
                var value = _registryService.GetValue(RunRegistryKey, AppName);
                if (!string.IsNullOrEmpty(value))
                {
                    var expectedPath = $"\"{GetCurrentExecutablePath()}\" --startup";
                    return Task.FromResult(value.Equals(expectedPath, StringComparison.OrdinalIgnoreCase));
                }

                // 2. If new SnipDock doesn't exist, check legacy PromptShelf
                var legacyValue = _registryService.GetValue(RunRegistryKey, "PromptShelf");
                if (!string.IsNullOrEmpty(legacyValue))
                {
                    _logger.LogInformation("Legacy PromptShelf startup key detected. Migrating to SnipDock safely...");
                    try
                    {
                        var exePath = GetCurrentExecutablePath();
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            var expectedNewValue = $"\"{exePath}\" --startup";
                            
                            // 1. Write new key
                            _registryService.SetValue(RunRegistryKey, AppName, expectedNewValue);
                            
                            // 2. Verify new key
                            var verifyValue = _registryService.GetValue(RunRegistryKey, AppName);
                            if (verifyValue != null && verifyValue.Equals(expectedNewValue, StringComparison.OrdinalIgnoreCase))
                            {
                                // 3. Delete legacy key
                                _registryService.DeleteValue(RunRegistryKey, "PromptShelf", false);
                                _logger.LogInformation("Migrated startup key safely from PromptShelf to SnipDock.");
                                return Task.FromResult(true);
                            }
                            else
                            {
                                _logger.LogWarning("Failed to verify written SnipDock startup key. Retaining legacy key.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to migrate startup registry key safely. Retaining legacy key.");
                    }
                }

                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check startup launch state");
                return Task.FromResult(false);
            }
        }

        public Task<bool> EnableAsync()
        {
            try
            {
                var exePath = GetCurrentExecutablePath();
                if (string.IsNullOrEmpty(exePath))
                {
                    throw new InvalidOperationException("Could not detect executable path.");
                }

                var value = $"\"{exePath}\" --startup";
                _registryService.SetValue(RunRegistryKey, AppName, value);
                _logger.LogInformation("Startup launch enabled");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Startup launch failed");
                throw;
            }
        }

        public Task<bool> DisableAsync()
        {
            try
            {
                _registryService.DeleteValue(RunRegistryKey, AppName, false);
                _logger.LogInformation("Startup launch disabled");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Startup launch failed");
                throw;
            }
        }
    }
}
