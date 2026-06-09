using System;
using System.IO;
using Serilog;

namespace SnipDock.Infrastructure.Logging
{
    public static class LoggingConfigurator
    {
        public static void InitializeBootstrapLogging()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logDirectory = Path.Combine(localAppData, "SnipDock", "logs");
            
            try
            {
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }
            }
            catch
            {
                // Suppress and continue
            }

            // NOTE: With RollingInterval.Day, Serilog appends the date directly after the underscore, e.g. bootstrap_20260605.log
            var logPath = Path.Combine(logDirectory, "bootstrap_.log");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    path: logPath,
                    rollingInterval: RollingInterval.Day,
                    shared: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();

            Log.Information("Bootstrap logging initialized. Stage 1 started.");
        }

        public static void InitializeAppLogging(string storagePath)
        {
            if (string.IsNullOrWhiteSpace(storagePath))
                throw new ArgumentException("Storage path cannot be null or empty.", nameof(storagePath));

            var logDirectory = Path.Combine(storagePath, "logs");

            try
            {
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create application log directory at {LogDirectory}", logDirectory);
                throw;
            }

            // NOTE: With RollingInterval.Day, Serilog appends the date directly after the underscore, e.g. snipdock_20260605.log
            var logPath = Path.Combine(logDirectory, "snipdock_.log");

            // Close the existing bootstrap logger safely before opening the new one
            Log.CloseAndFlush();

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    path: logPath,
                    rollingInterval: RollingInterval.Day,
                    shared: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();

            Log.Information("Application logging initialized. Stage 2 started. Storage path: {StoragePath}", storagePath);
            Log.Information("Product naming migration: Log files renamed from promptshelf to snipdock.");
        }
    }
}
