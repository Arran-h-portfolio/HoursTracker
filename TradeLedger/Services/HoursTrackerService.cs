using Data.Models;
using Microsoft.EntityFrameworkCore;

/* This is the only place that talks directly to AppDbContext,
 pages will call this service, never EF Core directly. */

 namespace TradeLedger.Services;

 public record EarningsSummary(double TotalHours, double GrossEarnings, double NetEarnings);
 public record MonthSummary(DateOnly Month, EarningsSummary Earnings);
 public record TaxRundownResult(EarningsSummary Total, List<MonthSummary> Monthly);

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

    public async Task DeleteHoursAsync(DateOnly date)
    {
        using var db = await dbFactory.CreateDbContextAsync();
        var entry = await db.HoursEntries.FirstOrDefaultAsync(h => h.Date == date);
        if (entry is not null)
        {
            db.HoursEntries.Remove(entry);
            await db.SaveChangesAsync();
        }
    }

    // Returns the start and end dates of the pay period that is `offset` periods away
    // from today (0 = current, -1 = previous, +1 = next).
    public static (DateOnly Start, DateOnly End) GetPayPeriodBounds(AppSettings settings, int offset)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        if (settings.PayPeriodType == PayPeriodType.Weekly)
        {
            var startDow = (DayOfWeek)settings.PayPeriodStartDay;
            int daysSinceStart = ((int)today.DayOfWeek - (int)startDow + 7) % 7;
            var start = today.AddDays(-daysSinceStart + offset * 7);
            return (start, start.AddDays(6));
        }
        else
        {
            int startDay = Math.Clamp(settings.PayPeriodStartDay, 1, 28);
            DateOnly periodStart;
            if (today.Day >= startDay)
            {
                periodStart = new DateOnly(today.Year, today.Month, startDay);
            }
            else
            {
                var prev = today.AddMonths(-1);
                periodStart = new DateOnly(prev.Year, prev.Month,
                    Math.Min(startDay, DateTime.DaysInMonth(prev.Year, prev.Month)));
            }
            periodStart = periodStart.AddMonths(offset);
            return (periodStart, periodStart.AddMonths(1).AddDays(-1));
        }
    }

    public async Task<EarningsSummary> GetPeriodEarningsSummaryAsync(DateOnly from, DateOnly to)
    {
        using var db = await dbFactory.CreateDbContextAsync();
        var totalHours = await db.HoursEntries
            .Where(h => h.Date >= from && h.Date <= to)
            .SumAsync(h => (double?)h.HoursWorked) ?? 0;
        var settings = await db.Settings.FirstOrDefaultAsync() ?? new AppSettings();
        var gross = totalHours * settings.HourlyWage;
        var net = gross - (gross * settings.TaxRate);
        return new EarningsSummary(totalHours, gross, net);
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

    // Fetches all 12 months for a given year in a single DB round-trip.
    public async Task<List<MonthSummary>> GetYearlyHistoryAsync(int year)
    {
        using var db = await dbFactory.CreateDbContextAsync();
        var settings = await db.Settings.FirstOrDefaultAsync() ?? new AppSettings();

        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd   = new DateOnly(year, 12, 31);
        var entries   = await db.HoursEntries
            .Where(h => h.Date >= yearStart && h.Date <= yearEnd)
            .ToListAsync();

        var result = new List<MonthSummary>();
        for (int month = 1; month <= 12; month++)
        {
            var hours = entries.Where(h => h.Date.Month == month).Sum(h => h.HoursWorked);
            var gross = hours * settings.HourlyWage;
            var net   = gross - (gross * settings.TaxRate);
            result.Add(new MonthSummary(new DateOnly(year, month, 1), new EarningsSummary(hours, gross, net)));
        }
        return result;
    }

    // Breaks a custom date range down month-by-month for the tax rundown page.
    public async Task<TaxRundownResult> GetTaxRundownAsync(DateOnly from, DateOnly to)
    {
        using var db = await dbFactory.CreateDbContextAsync();
        var settings = await db.Settings.FirstOrDefaultAsync() ?? new AppSettings();

        var entries = await db.HoursEntries
            .Where(h => h.Date >= from && h.Date <= to)
            .ToListAsync();

        var monthly = new List<MonthSummary>();
        var cursor   = new DateOnly(from.Year, from.Month, 1);
        var endMonth = new DateOnly(to.Year, to.Month, 1);

        while (cursor <= endMonth)
        {
            var hours = entries
                .Where(h => h.Date.Year == cursor.Year && h.Date.Month == cursor.Month)
                .Sum(h => h.HoursWorked);
            var gross = hours * settings.HourlyWage;
            var net   = gross - (gross * settings.TaxRate);
            monthly.Add(new MonthSummary(cursor, new EarningsSummary(hours, gross, net)));
            cursor = cursor.AddMonths(1);
        }

        var totalHours = monthly.Sum(m => m.Earnings.TotalHours);
        var totalGross = monthly.Sum(m => m.Earnings.GrossEarnings);
        var totalNet   = monthly.Sum(m => m.Earnings.NetEarnings);
        return new TaxRundownResult(new EarningsSummary(totalHours, totalGross, totalNet), monthly);
    }

    // Generates a CSV string of every logged shift with computed financials.
    public async Task<string> GenerateCsvAsync()
    {
        using var db = await dbFactory.CreateDbContextAsync();
        var settings = await db.Settings.FirstOrDefaultAsync() ?? new AppSettings();
        var entries  = await db.HoursEntries.OrderBy(h => h.Date).ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Date,Hours Worked,Gross (GBP),Tax (GBP),Net (GBP)");
        foreach (var entry in entries)
        {
            var gross = entry.HoursWorked * settings.HourlyWage;
            var tax   = gross * settings.TaxRate;
            var net   = gross - tax;
            sb.AppendLine($"{entry.Date:yyyy-MM-dd},{entry.HoursWorked:0.##},{gross:0.00},{tax:0.00},{net:0.00}");
        }
        return sb.ToString();
    }

}
/* Gross/Net aren't stored anywhere, they are always
   derived, Computing them on read means they can never go stale
   
   'LogHoursAsync is an "upsert" (update or insert) it checks is today
   already has a row before deciding whether to add a new one or update the existing one, 
   which is what keeps the unique index happy */