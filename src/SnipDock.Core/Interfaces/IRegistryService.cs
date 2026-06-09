namespace SnipDock.Core.Interfaces
{
    public interface IRegistryService
    {
        string? GetValue(string keyPath, string valueName);
        void SetValue(string keyPath, string valueName, object value);
        void DeleteValue(string keyPath, string valueName, bool throwOnMissingSubKey);
    }
}
