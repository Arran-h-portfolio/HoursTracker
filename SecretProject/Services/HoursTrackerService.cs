using Data.Models;
using Microsoft.EntityFrameworkCore;

/* This is the only place that talks directly to AppDbContext,
 pages will call this service, never EF Core directly. */

 namespace SecretProject.Services;

 public record EarningsSummary(double TotalHours, double GrossEarnings, double NetEarnings);

 public class HoursTrackerService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<AppSettings>
    GetSettingsAsync()
    {
        using var db = await dbFactory.CreateDbContextAsync();
        var settings = await db.Settings.FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new AppSettings();
            db.Settings.Add(settings);
            await db.SaveChangesAsync();
        }
        return settings;
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        using var db = await dbFactory.CreateDbContextAsync();
        db.Settings.Update(settings);
        await db.SaveChangesAsync();
    }

    public async Task<Dictionary<DateOnly, double>> GetHoursLoggedAsync()
    {
        using var db = await dbFactory.CreateDbContextAsync();
        return await db.HoursEntries.ToDictionaryAsync(h => h.Date, h => h.HoursWorked);
    }

    public async Task LogHoursAsync(DateOnly date, double hours)
    {
        using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.HoursEntries.FirstOrDefaultAsync(h => h.Date == date);
        if (existing is null)
        {
            db.HoursEntries.Add(new HoursEntry{ Date = date, HoursWorked = hours });
        }
        else
        {
            existing.HoursWorked = hours;
        }
        await db.SaveChangesAsync();
    }

    public async Task<EarningsSummary>GetEarningsSummaryAsync()
    {
        using var db = await dbFactory.CreateDbContextAsync();
        var totalHours = await db.HoursEntries.SumAsync(h => h.HoursWorked);
        var settings = await db.Settings.FirstOrDefaultAsync() ?? new AppSettings();
        var gross = totalHours * settings.HourlyWage;
        var net = gross - (gross * settings.TaxRate);
        return new EarningsSummary(totalHours, gross, net);
    }


}
/* Gross/Net aren't stored anywhere, they are always
   derived, Computing them on read means they can never go stale
   
   'LogHoursAsync is an "upsert" (update or insert) it checks is today
   already has a row before deciding whether to add a new one or update the existing one, 
   which is what keeps the unique index happy */