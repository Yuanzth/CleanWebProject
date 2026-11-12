using InfraHub.Core.Interfaces;
using InfraHub.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InfraHub.Infrastructure.Services;

public class ModuleRegistry : IModuleRegistry
{
    private readonly IConfiguration _configuration;

    public ModuleRegistry(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IEnumerable<string> GetEnabledModules()
    {
        var modules = _configuration.GetSection("Modules:Enabled").GetChildren();
        return modules.Select(m => m.Value ?? string.Empty).ToList();
    }

    public void RegisterModules(IServiceCollection services)
    {
        services.AddScoped<IStoredProcedureExecutor, StoredProcedureExecutor>();
        services.AddScoped<ReportService>();
    }

    public bool IsModuleEnabled(string moduleName)
    {
        var enabledModules = GetEnabledModules();
        return enabledModules.Contains(moduleName, StringComparer.OrdinalIgnoreCase);
    }
}
