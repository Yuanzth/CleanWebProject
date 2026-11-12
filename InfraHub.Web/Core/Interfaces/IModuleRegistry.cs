namespace InfraHub.Core.Interfaces;

public interface IModuleRegistry
{
    bool IsModuleEnabled(string moduleName);
    IEnumerable<string> GetEnabledModules();
}
