using StockBot.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace StockBot.Services;

public class MemoryService
{
    private readonly BotDbContext _db;

    public MemoryService(BotDbContext db) => _db = db;

    public async Task<UserPreference?> GetUserPreferenceAsync(ulong userId)
    {
        return await _db.UserPreferences.FirstOrDefaultAsync(u => u.UserId == userId);
    }

    public async Task SaveUserPreferenceAsync(ulong userId, string sectors)
    {
        var pref = await _db.UserPreferences.FindAsync(userId);
        if (pref == null)
        {
            _db.UserPreferences.Add(new UserPreference { UserId = userId, PreferredSectors = sectors });
        }
        else
        {
            pref.PreferredSectors = sectors;
        }
        await _db.SaveChangesAsync();
    }
}
