namespace InfraHub.Core.Helpers;

/// <summary>
/// Helper statis untuk mapping role names ke hak akses modul.
/// Setara dengan app/Helpers/RoleHelper.php di Laravel.
/// </summary>
public static class RoleHelper
{
    // Role Constants
    public const string Administrator = "Administrator";
    public const string NOC = "NOC";
    public const string Tracking = "Tracking";
    public const string Viewer = "Viewer";

    /// <summary>
    /// Menentukan modul apa yang boleh diakses berdasarkan role name.
    /// </summary>
    public static string[] GetAllowedModules(string? roleName) => roleName switch
    {
        Administrator => new[] { "Bts", "Ups", "Tracking", "Shared" },
        NOC => new[] { "Ups", "Shared" },
        Tracking => new[] { "Tracking", "Shared" },
        Viewer => new[] { "Bts", "Shared" },
        _ => new[] { "Shared" }
    };

    /// <summary>
    /// Menentukan default redirect URL setelah login berdasarkan role.
    /// </summary>
    public static string GetDefaultRedirectUrl(string? roleName) => roleName switch
    {
        Administrator or Viewer => "/Bts/Dashboard",
        NOC => "/Ups/Dashboard",
        Tracking => "/Tracking/Live",
        _ => "/Shared/Profile"
    };

    /// <summary>
    /// Cek apakah role tertentu boleh mengakses suatu modul.
    /// </summary>
    public static bool CanAccessModule(string? roleName, string moduleName)
    {
        return GetAllowedModules(roleName).Contains(moduleName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Cek apakah role tertentu adalah admin (bisa manage).
    /// </summary>
    public static bool IsAdmin(string? roleName)
    {
        return roleName is Administrator or NOC;
    }
}
