using System.ComponentModel.DataAnnotations;

namespace StockBot.Data;

public class UserPreference
{
    [Key]
    public ulong UserId { get; set; }
    public string? Username { get; set; }
    public string? PreferredSectors { get; set; }  // 逗号分隔的偏好行业
    public DateTime? LastInteraction { get; set; }
    public int InteractionCount { get; set; }
}