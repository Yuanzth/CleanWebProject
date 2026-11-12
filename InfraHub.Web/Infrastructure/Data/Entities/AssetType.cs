namespace InfraHub.Infrastructure.Data.Entities;

/// <summary>
/// Tabel: tbl_r_asset_types
/// Laravel Model: AssetType
/// </summary>
public class AssetType
{
    public Guid Id { get; set; }
    public string TypeName { get; set; } = string.Empty;

    // Navigation
    public ICollection<Device> Devices { get; set; } = new List<Device>();
}
