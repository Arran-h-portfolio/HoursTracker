using Data.Models;
using Microsoft.EntityFrameworkCore;

/* This is the only place that talks directly to AppDbContext,
 pages will call this service, never EF Core directly. */

namespace TradeLedger.Services;

// TaxableProfit = max(0, GrossEarnings - TotalExpenses)
// TaxAmount     = TaxableProfit * taxRate  (simple mode)
//               = IncomeTax + ClassFourNI  (UK mode — calculated on period total only)
// NetEarnings   = TaxableProfit - TaxAmount
public record EarningsSummary(
    double TotalHours,
    double GrossEarnings,
    double TotalExpenses,
    double TaxableProfit,
    double TaxAmount,
    double NetEarnings);

// Detailed UK self-employed tax breakdown (2024/25 rates)
public record UKTaxBreakdown(
    double PersonalAllowanceUsed,
    double IncomeTax,
    double ClassFourNI,
    double TotalTax);

public record MonthSummary(DateOnly Month, EarningsSummary Earnings);
public record TaxRundownResult(EarningsSummary Total, List<MonthSummary> Monthly, UKTaxBreakdown? UKBreakdown = null);

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

    public async Task<List<HoursEntry>> GetEntriesAsync()
    {
        using var db = await dbFactory.CreateDbContextAsync();
        return await db.HoursEntries
            .OrderByDescending(h => h.Date)
            .ThenBy(h => h.StartTime)
            .ToListAsync();
    }

    public async Task AddEntryAsync(HoursEntry entry)
    {
        using var db = await dbFactory.CreateDbContextAsync();
        db.HoursEntries.Add(entry);
        await db.SaveChangesAsync();
    }

    public async Task UpdateEntryAsync(HoursEntry entry)
    {
        using var db = await dbFactory.CreateDbContextAsync();
        db.HoursEntries.Update(entry);
        await db.SaveChangesAsync();
    }

    public async Task DeleteEntryAsync(int id)
    {
        using var db = await dbFactory.CreateDbContextAsync();
        var entry = await db.HoursEntries.FindAsync(id);
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

    // 2024/25 UK self-employed tax: income tax bands + Class 4 NI.
    // Class 2 NI abolished April 2024. Personal allowance taper above £100k not modelled.
    public static UKTaxBreakdown CalculateUKTax(double profit)
    {
        const double personalAllowance = 12_570;
        const double basicRateLimit    = 50_270;
        const double higherRateLimit   = 125_140;

        double incomeTax = 0;
        if (profit > personalAllowance)
        {
            incomeTax += (Math.Min(profit, basicRateLimit) - personalAllowance) * 0.20;
        }
        if (profit > basicRateLimit)
        {
            incomeTax += (Math.Min(profit, higherRateLimit) - basicRateLimit) * 0.40;
        }
        if (profit > higherRateLimit)
        {
            incomeTax += (profit - higherRateLimit) * 0.45;
        }

        double classNI = 0;
        if (profit > personalAllowance)
        {
            classNI += (Math.Min(profit, basicRateLimit) - personalAllowance) * 0.06;
        }
        if (profit > basicRateLimit)
        {
            classNI += (profit - basicRateLimit) * 0.02;
        }

        var paUsed = Math.Min(profit, personalAllowance);
        return new UKTaxBreakdown(paUsed, incomeTax, classNI, incomeTax + classNI);
    }

    // Total gross, expenses and taxable profit from 6 April to today (current UK tax year).
    public async Task<EarningsSummary> GetYearToDateSummaryAsync()
    {
        var today        = DateOnly.FromDateTime(DateTime.Today);
        var startYear    = (today.Month > 4 || (today.Month == 4 && today.Day >= 6))
                           ? today.Year : today.Year - 1;
        var taxYearStart = new DateOnly(startYear, 4, 6);
        return await GetPeriodEarningsSummaryAsync(taxYearStart, today);
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
            // UK mode: monthly rows carry 0 tax — tax is only meaningful on the period total
            var rate = settings.UseUKTax ? 0.0 : settings.TaxRate;
            monthly.Add(new MonthSummary(cursor, BuildSummary(hours, settings.HourlyWage, rate, expAmt)));
            cursor = cursor.AddMonths(1);
        }

        var totHours = monthly.Sum(m => m.Earnings.TotalHours);
        var totGross = monthly.Sum(m => m.Earnings.GrossEarnings);
        var totExp   = monthly.Sum(m => m.Earnings.TotalExpenses);

        if (settings.UseUKTax)
        {
            // UK: calculate tax on the period total (not month by month) for accuracy
            var profit      = Math.Max(0, totGross - totExp);
            var ukBreakdown = CalculateUKTax(profit);
            return new TaxRundownResult(
                new EarningsSummary(totHours, totGross, totExp, profit, ukBreakdown.TotalTax, profit - ukBreakdown.TotalTax),
                monthly,
                ukBreakdown);
        }

        var totProfit = monthly.Sum(m => m.Earnings.TaxableProfit);
        var totTax    = monthly.Sum(m => m.Earnings.TaxAmount);
        var totNet    = monthly.Sum(m => m.Earnings.NetEarnings);
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
        sb.AppendLine("Date,Start,End,Hours,Job,Gross (GBP),Tax (GBP),Net (GBP)");
        foreach (var entry in entries)
        {
            var gross  = entry.HoursWorked * settings.HourlyWage;
            var tax    = gross * settings.TaxRate;
            var net    = gross - tax;
            var start  = entry.StartTime.HasValue ? entry.StartTime.Value.ToString("HH:mm") : "";
            var end    = entry.EndTime.HasValue ? entry.EndTime.Value.ToString("HH:mm") : "";
            sb.AppendLine($"{entry.Date:yyyy-MM-dd},{start},{end},{entry.HoursWorked:0.##},{entry.Label},{gross:0.00},{tax:0.00},{net:0.00}");
        }

        sb.AppendLine();
        sb.AppendLine("EXPENSES");
        sb.AppendLine("Date,Category,Description,Amount (GBP)");
        foreach (var exp in expenses)
            sb.AppendLine($"{exp.Date:yyyy-MM-dd},{exp.Category},{exp.Description},{exp.Amount:0.00}");

        return sb.ToString();
    }
}
