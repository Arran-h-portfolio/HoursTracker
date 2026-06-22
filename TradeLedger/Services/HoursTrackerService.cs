using Data.Models;
using Microsoft.EntityFrameworkCore;

/* This is the only place that talks directly to AppDbContext,
 pages will call this service, never EF Core directly. */

namespace TradeLedger.Services;

// TaxableProfit = max(0, GrossEarnings - TotalExpenses)
// TaxAmount     = TaxableProfit * taxRate
// NetEarnings   = TaxableProfit - TaxAmount
public record EarningsSummary(
    double TotalHours,
    double GrossEarnings,
    double TotalExpenses,
    double TaxableProfit,
    double TaxAmount,
    double NetEarnings);

public record MonthSummary(DateOnly Month, EarningsSummary Earnings);
public record TaxRundownResult(EarningsSummary Total, List<MonthSummary> Monthly);

public class HoursTrackerService(IDbContextFactory<AppDbContext> dbFactory)
{
    // ── Settings ────────────────────────────────────────────────────────────

    public async Task<AppSettings> GetSettingsAsync()
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

    // ── Hours ────────────────────────────────────────────────────────────────

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
            db.HoursEntries.Add(new HoursEntry { Date = date, HoursWorked = hours });
        else
            existing.HoursWorked = hours;
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

    // ── Expenses ─────────────────────────────────────────────────────────────

    public async Task LogExpenseAsync(Expense expense)
    {
        using var db = await dbFactory.CreateDbContextAsync();
        db.Expenses.Add(expense);
        await db.SaveChangesAsync();
    }

    public async Task DeleteExpenseAsync(int id)
    {
        using var db = await dbFactory.CreateDbContextAsync();
        var expense = await db.Expenses.FindAsync(id);
        if (expense is not null)
        {
            db.Expenses.Remove(expense);
            await db.SaveChangesAsync();
        }
    }

    public async Task<List<Expense>> GetExpensesInPeriodAsync(DateOnly from, DateOnly to)
    {
        using var db = await dbFactory.CreateDbContextAsync();
        return await db.Expenses
            .Where(e => e.Date >= from && e.Date <= to)
            .OrderByDescending(e => e.Date)
            .ToListAsync();
    }

    // ── Pay period bounds ────────────────────────────────────────────────────

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
                periodStart = new DateOnly(today.Year, today.Month, startDay);
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

    // ── Earnings summaries ───────────────────────────────────────────────────

    // Central formula: all EarningsSummary construction goes through here.
    private static EarningsSummary BuildSummary(double hours, double hourlyWage, double taxRate, double expenses)
    {
        var gross         = hours * hourlyWage;
        var taxableProfit = Math.Max(0, gross - expenses);
        var taxAmount     = taxableProfit * taxRate;
        var net           = taxableProfit - taxAmount;
        return new EarningsSummary(hours, gross, expenses, taxableProfit, taxAmount, net);
    }

    public async Task<EarningsSummary> GetPeriodEarningsSummaryAsync(DateOnly from, DateOnly to)
    {
        using var db = await dbFactory.CreateDbContextAsync();
        var settings = await db.Settings.FirstOrDefaultAsync() ?? new AppSettings();

        var totalHours = await db.HoursEntries
            .Where(h => h.Date >= from && h.Date <= to)
            .SumAsync(h => (double?)h.HoursWorked) ?? 0;
        var totalExpenses = await db.Expenses
            .Where(e => e.Date >= from && e.Date <= to)
            .SumAsync(e => (double?)e.Amount) ?? 0;

        return BuildSummary(totalHours, settings.HourlyWage, settings.TaxRate, totalExpenses);
    }

    public async Task<EarningsSummary> GetEarningsSummaryAsync()
    {
        using var db = await dbFactory.CreateDbContextAsync();
        var settings      = await db.Settings.FirstOrDefaultAsync() ?? new AppSettings();
        var totalHours    = await db.HoursEntries.SumAsync(h => (double?)h.HoursWorked) ?? 0;
        var totalExpenses = await db.Expenses.SumAsync(e => (double?)e.Amount) ?? 0;
        return BuildSummary(totalHours, settings.HourlyWage, settings.TaxRate, totalExpenses);
    }

    // Fetches all 12 months for a given year in two DB round-trips (hours + expenses).
    public async Task<List<MonthSummary>> GetYearlyHistoryAsync(int year)
    {
        using var db = await dbFactory.CreateDbContextAsync();
        var settings = await db.Settings.FirstOrDefaultAsync() ?? new AppSettings();

        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd   = new DateOnly(year, 12, 31);
        var entries   = await db.HoursEntries.Where(h => h.Date >= yearStart && h.Date <= yearEnd).ToListAsync();
        var expenses  = await db.Expenses.Where(e => e.Date >= yearStart && e.Date <= yearEnd).ToListAsync();

        var result = new List<MonthSummary>();
        for (int month = 1; month <= 12; month++)
        {
            var hours   = entries.Where(h => h.Date.Month == month).Sum(h => h.HoursWorked);
            var expAmt  = expenses.Where(e => e.Date.Month == month).Sum(e => e.Amount);
            result.Add(new MonthSummary(new DateOnly(year, month, 1),
                BuildSummary(hours, settings.HourlyWage, settings.TaxRate, expAmt)));
        }
        return result;
    }

    // Breaks a custom date range down month-by-month for the tax rundown page.
    public async Task<TaxRundownResult> GetTaxRundownAsync(DateOnly from, DateOnly to)
    {
        using var db = await dbFactory.CreateDbContextAsync();
        var settings = await db.Settings.FirstOrDefaultAsync() ?? new AppSettings();

        var entries  = await db.HoursEntries.Where(h => h.Date >= from && h.Date <= to).ToListAsync();
        var expenses = await db.Expenses.Where(e => e.Date >= from && e.Date <= to).ToListAsync();

        var monthly  = new List<MonthSummary>();
        var cursor   = new DateOnly(from.Year, from.Month, 1);
        var endMonth = new DateOnly(to.Year, to.Month, 1);

        while (cursor <= endMonth)
        {
            var hours  = entries.Where(h => h.Date.Year == cursor.Year && h.Date.Month == cursor.Month).Sum(h => h.HoursWorked);
            var expAmt = expenses.Where(e => e.Date.Year == cursor.Year && e.Date.Month == cursor.Month).Sum(e => e.Amount);
            monthly.Add(new MonthSummary(cursor, BuildSummary(hours, settings.HourlyWage, settings.TaxRate, expAmt)));
            cursor = cursor.AddMonths(1);
        }

        var totHours = monthly.Sum(m => m.Earnings.TotalHours);
        var totGross = monthly.Sum(m => m.Earnings.GrossEarnings);
        var totExp   = monthly.Sum(m => m.Earnings.TotalExpenses);
        var totProfit= monthly.Sum(m => m.Earnings.TaxableProfit);
        var totTax   = monthly.Sum(m => m.Earnings.TaxAmount);
        var totNet   = monthly.Sum(m => m.Earnings.NetEarnings);
        return new TaxRundownResult(
            new EarningsSummary(totHours, totGross, totExp, totProfit, totTax, totNet),
            monthly);
    }

    // Generates a CSV with a shifts section and an expenses section.
    public async Task<string> GenerateCsvAsync()
    {
        using var db = await dbFactory.CreateDbContextAsync();
        var settings = await db.Settings.FirstOrDefaultAsync() ?? new AppSettings();
        var entries  = await db.HoursEntries.OrderBy(h => h.Date).ToListAsync();
        var expenses = await db.Expenses.OrderBy(e => e.Date).ToListAsync();

        var sb = new System.Text.StringBuilder();

        sb.AppendLine("HOURS WORKED");
        sb.AppendLine("Date,Hours,Gross (GBP),Tax (GBP),Net (GBP)");
        foreach (var entry in entries)
        {
            var gross  = entry.HoursWorked * settings.HourlyWage;
            var tax    = gross * settings.TaxRate;
            var net    = gross - tax;
            sb.AppendLine($"{entry.Date:yyyy-MM-dd},{entry.HoursWorked:0.##},{gross:0.00},{tax:0.00},{net:0.00}");
        }

        sb.AppendLine();
        sb.AppendLine("EXPENSES");
        sb.AppendLine("Date,Category,Description,Amount (GBP)");
        foreach (var exp in expenses)
            sb.AppendLine($"{exp.Date:yyyy-MM-dd},{exp.Category},{exp.Description},{exp.Amount:0.00}");

        return sb.ToString();
    }
}
