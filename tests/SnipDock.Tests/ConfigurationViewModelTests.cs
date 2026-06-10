using SnipDock.App.ViewModels;
using SnipDock.Core.Interfaces;
using SnipDock.Core.Models;
using Xunit;

namespace SnipDock.Tests
{
    public class ConfigurationViewModelTests
    {
        private class FakeBootstrapSettingsStore : IBootstrapSettingsStore
        {
            public BootstrapSettings Settings { get; set; } = new();

            public BootstrapSettings Load() => Settings;

            public void Save(BootstrapSettings settings) => Settings = settings;
        }

        [Fact]
        public void FirstRun_UsesChineseCopy()
        {
            var vm = new ConfigurationViewModel(new FakeBootstrapSettingsStore(), ConfigurationMode.FirstRun, "zh-CN");

            Assert.Equal("首次配置 SnipDock", vm.WindowTitle);
            Assert.Contains("数据存储目录", vm.GuideText);
            Assert.Equal("请先选择文件夹", vm.SaveButtonText);

            vm.StoragePath = @"C:\SnipDockData";

            Assert.Equal("确认并开始使用 SnipDock", vm.SaveButtonText);
        }

        [Fact]
        public void ChangeStorage_UsesEnglishCopyAndDoesNotPromiseMigration()
        {
            var vm = new ConfigurationViewModel(new FakeBootstrapSettingsStore(), ConfigurationMode.ChangeStorageLocation, "en-US");

            Assert.Equal("Change storage location", vm.WindowTitle);
            Assert.Contains("will not be moved automatically", vm.GuideText);
            Assert.DoesNotContain("migrate", vm.GuideText, System.StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Choose a folder first", vm.SaveButtonText);

            vm.StoragePath = @"C:\SnipDockData";

            Assert.Equal("Save and switch", vm.SaveButtonText);
        }
    }
}
