namespace InfraHub.Core.DTOs.Dashboard;

public class StatCardDto
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public int? Total { get; set; }
    public string Id { get; set; } = string.Empty;
    public string TotalId { get; set; } = string.Empty;
    public string IconClass { get; set; } = string.Empty;
    public string BorderColor { get; set; } = string.Empty;
    public string IconBg { get; set; } = string.Empty;
    public bool Clickable { get; set; }
    public string DetailTarget { get; set; } = string.Empty;
}
