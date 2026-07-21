# Tutorial: Adjustable Tax Rate + Local SQLite Persistence

**Status: ✅ Complete — implemented in the codebase.** This was the first step that took
TradeLedger from an in-memory prototype to a real local database. Unlike the other docs
in this folder, this one is a **living document**: it's kept in sync with the codebase
as the persistence layer evolves, rather than a frozen snapshot of a single past
session. Where the current code has grown past what's shown here, a "Since this was
written" note says so and points at `docs/CONTEXT.md` (the authoritative current spec)
instead of duplicating the up-to-date code.

A step-by-step build guide for adding a persisted tax rate + hourly wage, and showing
gross/net earnings on the Home page, backed by a real local SQLite database via EF Core.
Written for someone who hasn't used SQLite/EF Core before — each step explains *why*,
not just *what*.

## Why this was bigger than "add a field"

Before this, `TrackingData` was registered as a DI singleton in `MauiProgram.cs`, but
`Home.razor` and `Settings.razor` each did `new TrackingData()` instead of injecting it —
so the two pages didn't even share data with each other, and nothing survived an app
restart. SQLite + EF Core gave real persistence, which is also a great portfolio talking
point ("I designed a local relational schema and used an ORM"), so this guide replaced
the single in-memory blob with a proper data layer.

## Concepts you'll use (read once, refer back as needed)

- **SQLite**: a relational database that lives in a single file on disk — no server process needed. Perfect for a single-user mobile/desktop app.
- **EF Core**: Microsoft's ORM (Object-Relational Mapper). You write plain C# classes ("entities"); EF Core turns them into tables and turns your C# code into SQL.
- **DbContext**: the EF Core class representing one working session with the database — it's what you call `.Add()`, `.Update()`, and run queries against.
- **DbContextFactory** (instead of injecting a `DbContext` directly): in Blazor, a single long-lived `DbContext` isn't safe to share across concurrent operations. The recommended pattern is to inject a *factory* and create a short-lived `DbContext` per operation: create it, use it, dispose it.
- **`EnsureCreated()` vs Migrations**: Migrations are how you evolve a schema over time without losing existing users' data — proper tooling, but more ceremony. `EnsureCreated()` just builds the schema from your current model the first time the DB doesn't exist. At the time, there was no real user data yet, so `EnsureCreated()` was the right tool. **Since this was written:** the app has real user data now, so schema changes can no longer just delete-and-recreate the database (see Step 4's update and the revised "Common pitfalls" at the bottom) — this is the natural "next step" the original version of this doc flagged, and it's now happened.
- **Dependency Injection (DI) recap**: `MauiProgram.cs` registers services into a container; `@inject SomeService Foo` in a `.razor` file asks that container for an instance.

---

## Step 1 — Add the EF Core SQLite package ✅ Done

```bash
dotnet add TradeLedger/TradeLedger.csproj package Microsoft.EntityFrameworkCore.Sqlite
```

This adds a `<PackageReference>` to `TradeLedger.csproj` and pulls in the EF Core SQLite provider, including the native SQLite engine binaries (via `SQLitePCLRaw`) bundled automatically for each platform you target.

**Checkpoint:** open `TradeLedger.csproj` and confirm a `Microsoft.EntityFrameworkCore.Sqlite` line is present in the `<ItemGroup>`.

---

## Step 2 — Create the entity classes ✅ Done

These are plain C# classes that EF Core turns into tables, in `Components/Models/`.

**`Components/Models/AppSettings.cs`** (one row — the user's settings) — original shape:
```csharp
namespace Data.Models;

public class AppSettings
{
    public int Id { get; set; }

    // Stored as a fraction: 0.2 means 20%. The Settings page converts
    // to/from a 0-100 percentage for display.
    public double TaxRate { get; set; }

    public double HourlyWage { get; set; }

    // Carried over from the original model - not wired up to any UI yet,
    // kept for a future "earnings this pay period" feature.
    public DateTime PayDay { get; set; }
    public TimeSpan PayPeriod { get; set; }
}
```

**`Components/Models/HoursEntry.cs`** (one row per day worked — replaced `Dictionary<DateOnly, double>`) — original shape:
```csharp
namespace Data.Models;

public class HoursEntry
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public double HoursWorked { get; set; }
}
```

`Id` is picked up automatically by EF Core as the primary key by naming convention. `DateOnly` is natively supported as a column type since EF Core 8, so no extra setup needed there.

**Since this was written:** both entities grew well past this original shape, and a third
was added:
- `AppSettings` — the placeholder `PayDay`/`PayPeriod` fields were dropped and replaced by
  `PayPeriodType` (enum: Monthly/Weekly) + `PayPeriodStartDay` (int). It also gained
  `IsPremium`, `IsOnboarded`, `ThemePreference`, `NotificationsEnabled` +
  `NotificationHour`/`NotificationMinute`, `UseUKTax`, `EarningsGoal`, and `WorkingDays`
  (bitmask). Namespace stayed `Data.Models`.
- `HoursEntry` — gained `StartTime`/`EndTime` (`TimeOnly?`) and `Label` (string) once
  multiple shifts per day became possible; the original one-row-per-`Date` unique
  constraint was dropped (see Step 3).
- `Components/Models/Expense.cs` was added later (not part of this tutorial originally)
  for the premium expense-tracking feature: `Date`, `Amount`, `Category` enum, `Description`.

Full current field list: `docs/CONTEXT.md` → Data Model.

**Checkpoint:** project should still build (these classes aren't used by anything yet at this point in the tutorial).

---

## Step 3 — Create the DbContext ✅ Done

This is the class that represents "a connection to the database" and exposes your tables. `Services/AppDbContext.cs` — original shape:

```csharp
using Data.Models;
using Microsoft.EntityFrameworkCore;

namespace TradeLedger.Services;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppSettings> Settings => Set<AppSettings>();
    public DbSet<HoursEntry> HoursEntries => Set<HoursEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HoursEntry>()
            .HasIndex(h => h.Date)
            .IsUnique();
    }
}
```

The unique index on `Date` enforced "one row per day" at the database level.

**Since this was written:** the current `Services/AppDbContext.cs` also exposes
`DbSet<Expense> Expenses`, and the unique index on `HoursEntry.Date` was **dropped**
(via a startup migration, see Step 4) once multiple shifts per day became a supported
feature — `OnModelCreating` no longer configures that index.

---

## Step 4 — Wire it into MauiProgram.cs and create the DB file ✅ Done

```csharp
using Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradeLedger.Services;

namespace TradeLedger;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

		builder.Services.AddDbContextFactory<AppDbContext>(options =>
		{
			var dbPath = Path.Combine(FileSystem.Current.AppDataDirectory, "hourstracker.db");
			options.UseSqlite($"Data Source={dbPath}");
		});
		builder.Services.AddSingleton<HoursTrackerService>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();

		// Make sure the SQLite file + tables exist before any page queries them.
		var dbContextFactory = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
		using (var db = dbContextFactory.CreateDbContext())
		{
			db.Database.EnsureCreated();
		}

		return app;
	}
}
```

`FileSystem.Current.AppDataDirectory` is MAUI's cross-platform "writable app data folder" API — it resolves to the right sandboxed location on Android, iOS, MacCatalyst, and Windows automatically.

**Tip for later:** if you want to *see* the actual database file (great for a portfolio demo), temporarily add `System.Diagnostics.Debug.WriteLine(dbPath);` next to where `dbPath` is built, run the app once, find the path in your debug output, then open that `.db` file with a free tool like "DB Browser for SQLite" to browse the tables visually.

**Since this was written:** `EnsureCreated()` only builds a schema the *first* time the
database doesn't exist — it never alters an existing one. Every field/table added after
the first release (all of Step 2's "Since this was written" list, plus the `Expenses`
table and dropping the `HoursEntries.Date` unique index) needed a real user-data-safe
migration, so `MauiProgram.cs` now runs a block of manual, idempotent schema changes
right after `EnsureCreated()`:

```csharp
using (var db = dbContextFactory.CreateDbContext())
{
	db.Database.EnsureCreated();

	// Add columns introduced after initial schema — SQLite ignores these if they already exist.
	try { db.Database.ExecuteSqlRaw("ALTER TABLE Settings ADD COLUMN PayPeriodType INTEGER NOT NULL DEFAULT 0"); } catch { }
	try { db.Database.ExecuteSqlRaw("ALTER TABLE Settings ADD COLUMN PayPeriodStartDay INTEGER NOT NULL DEFAULT 1"); } catch { }
	try { db.Database.ExecuteSqlRaw("ALTER TABLE Settings ADD COLUMN IsPremium INTEGER NOT NULL DEFAULT 0"); } catch { }
	db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS Expenses (
		Id          INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
		Date        TEXT    NOT NULL,
		Amount      REAL    NOT NULL DEFAULT 0,
		Category    INTEGER NOT NULL DEFAULT 0,
		Description TEXT    NOT NULL DEFAULT ''
	)");
	try { db.Database.ExecuteSqlRaw("ALTER TABLE Settings ADD COLUMN IsOnboarded INTEGER NOT NULL DEFAULT 0"); } catch { }
	try { db.Database.ExecuteSqlRaw("ALTER TABLE Settings ADD COLUMN ThemePreference TEXT NOT NULL DEFAULT 'system'"); } catch { }
	try { db.Database.ExecuteSqlRaw("ALTER TABLE Settings ADD COLUMN NotificationsEnabled INTEGER NOT NULL DEFAULT 0"); } catch { }
	try { db.Database.ExecuteSqlRaw("ALTER TABLE Settings ADD COLUMN NotificationHour INTEGER NOT NULL DEFAULT 18"); } catch { }
	try { db.Database.ExecuteSqlRaw("ALTER TABLE Settings ADD COLUMN NotificationMinute INTEGER NOT NULL DEFAULT 0"); } catch { }
	try { db.Database.ExecuteSqlRaw("ALTER TABLE Settings ADD COLUMN UseUKTax INTEGER NOT NULL DEFAULT 0"); } catch { }
	try { db.Database.ExecuteSqlRaw("ALTER TABLE Settings ADD COLUMN EarningsGoal REAL NOT NULL DEFAULT 0"); } catch { }

	// Allow multiple entries per day (e.g. tradespeople logging separate jobs)
	try { db.Database.ExecuteSqlRaw("DROP INDEX IF EXISTS IX_HoursEntries_Date"); } catch { }
	try { db.Database.ExecuteSqlRaw("ALTER TABLE HoursEntries ADD COLUMN StartTime TEXT"); } catch { }
	try { db.Database.ExecuteSqlRaw("ALTER TABLE HoursEntries ADD COLUMN EndTime TEXT"); } catch { }
	try { db.Database.ExecuteSqlRaw("ALTER TABLE HoursEntries ADD COLUMN Label TEXT NOT NULL DEFAULT ''"); } catch { }

	// Working days bitmask for reminder scheduling (default 62 = Mon–Fri)
	try { db.Database.ExecuteSqlRaw("ALTER TABLE Settings ADD COLUMN WorkingDays INTEGER NOT NULL DEFAULT 62"); } catch { }
}
```

Each `ALTER TABLE` is wrapped in its own `try { } catch { }` because SQLite has no
`ADD COLUMN IF NOT EXISTS` — the first run on a fresh install fails harmlessly (the
column already exists from `EnsureCreated()`'s current model), and the first run on an
upgrading install actually adds it. This pattern is now the standing convention for
every future schema change — see the revised "Common pitfalls" section.

The app also now calls `.UseLocalNotification()` on the builder and registers
`NotificationService` as a singleton, for the daily reminder feature — unrelated to
persistence, mentioned here only because it lives in the same file.

---

## Step 5 — Build the service layer (the bridge between UI and database) ✅ Done

This is the only place that talks to `AppDbContext` directly — pages call this service, never EF Core directly. `Services/HoursTrackerService.cs` — original shape:

```csharp
using Data.Models;
using Microsoft.EntityFrameworkCore;

namespace TradeLedger.Services;

public record EarningsSummary(double TotalHours, double GrossEarnings, double NetEarnings);

public class HoursTrackerService(IDbContextFactory<AppDbContext> dbFactory)
{
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

    public async Task<EarningsSummary> GetEarningsSummaryAsync()
    {
        using var db = await dbFactory.CreateDbContextAsync();
        var totalHours = await db.HoursEntries.SumAsync(h => h.HoursWorked);
        var settings = await db.Settings.FirstOrDefaultAsync() ?? new AppSettings();
        var gross = totalHours * settings.HourlyWage;
        var net = gross - (gross * settings.TaxRate);
        return new EarningsSummary(totalHours, gross, net);
    }
}
```

Why Gross/Net weren't stored anywhere: they're always *derived* from hours + wage + tax rate. Computing them on read means they can never go stale, no matter which page last changed something — this principle still holds today (see below).

`LogHoursAsync` was an "upsert" (update-or-insert) — it checked if today already had a row before deciding whether to add a new one or update the existing one, which is what kept the unique index from Step 3 happy.

**Since this was written:** `HoursTrackerService` grew substantially and the API above no
longer matches it 1:1:
- `LogHoursAsync`'s single-upsert-per-date pattern is **gone** — once multiple shifts per
  day were allowed, it was replaced by `GetEntriesAsync()`, `AddEntryAsync()`,
  `UpdateEntryAsync()`, and `DeleteEntryAsync(int id)`.
- `EarningsSummary` gained `TotalExpenses`, `TaxableProfit`, and `TaxAmount`; `TaxAmount`
  is computed either as a flat rate or via the static `CalculateUKTax()` method
  implementing the UK Self-Employed bands, depending on `AppSettings.UseUKTax`.
- New methods were added for the premium features: `GetYearToDateSummaryAsync()`,
  `GetTaxRundownAsync()` (with UK banded breakdown), CSV export support, and expense CRUD.
- The derive-at-read-time principle from this step is still exactly how the service works
  — nothing financial is ever stored, only recomputed.

Full current method list and formulas: `docs/CONTEXT.md` → Tech Stack / Data Model / Tax system.

**Checkpoint (as of this step, historically):** the project built cleanly once MauiProgram.cs's references all resolved.

---

## Step 6 — Delete the old model ✅ Done

`Components/Models/Data.cs` (the `TrackingData` class) was deleted — fully superseded by `AppSettings` + `HoursEntry` (and later `Expense`). It no longer exists in the codebase.

---

## Step 7 — Rewire Settings.razor ✅ Done (page has since grown far beyond this)

The version below is what `Components/Pages/Settings.razor` looked like immediately after
this tutorial — kept here as the teaching artifact for "wire a page to the service layer."
It is **not** what the file contains today.

```razor
@page "/settings"
@using Data.Models
@using TradeLedger.Services
@inject HoursTrackerService TrackerService

<body>
    <section class="main-background">
        <div class="column-display">
            <h2 class="secondary-headings">Tax Rate</h2>
            <div class="row-display">
                <label class="secondary-headings">
                    Tax Rate (%)
                    <input type="number" min="0" max="100" step="0.1" @bind="taxRatePercent" />
                </label>
            </div>
        </div>
    </section>

    <section class="main-background">
        <div class="column-display">
            <h2 class="secondary-headings">Hourly Wage</h2>
            <div class="row-display">
                <label class="secondary-headings">
                    Hourly Wage (£)
                    <input type="number" min="0" step="0.01" @bind="hourlyWage" />
                </label>
            </div>
        </div>
    </section>

    <button class="btn-main" @onclick="SaveSettings">Save</button>
</body>

@code {
    private double taxRatePercent;
    private double hourlyWage;

    protected override async Task OnInitializedAsync()
    {
        var settings = await TrackerService.GetSettingsAsync();
        taxRatePercent = settings.TaxRate * 100;
        hourlyWage = settings.HourlyWage;
    }

    private async Task SaveSettings()
    {
        var settings = await TrackerService.GetSettingsAsync();
        settings.TaxRate = Math.Clamp(taxRatePercent, 0, 100) / 100.0;
        settings.HourlyWage = hourlyWage;
        await TrackerService.SaveSettingsAsync(settings);
    }
}
```

`OnInitializedAsync` is a Blazor lifecycle hook that runs once when the page first loads — the right place to pull data from the database. The percentage-to-fraction conversion happens in the UI layer, keeping the stored `TaxRate` a clean 0–1 fraction for the earnings math — still true today.

**Since this was written:** `Settings.razor` now also covers tax mode (Simple vs UK
Self-Employed) with the rate-band card, pay period configuration, earnings goal, dark
mode / appearance, notification toggle + time picker + working-days chip row, and the
Free vs Premium subscription section. See `docs/CONTEXT.md` → Pages and Navigation /
Premium Features, rather than reproducing the current file here.

---

## Step 8 — Rewire Home.razor ✅ Done (page has since grown far beyond this)

Same caveat as Step 7 — this is the historical shape, not the current file.

```razor
@page "/"
@using Data.Models
@using TradeLedger.Services
@inject HoursTrackerService TrackerService

<h1 class="main-headings">Salary Tracker</h1>
<body>
@*Card section for displaying hours logged, displayed in weeks*@
<section class="main-background">
    <div class="column-display">
          <h2>Gross Earnings: £@grossEarnings.ToString("N2")</h2>
          <h2>Net Earnings: £@netEarnings.ToString("N2")</h2>
          <p>MoM: £50<span class="@(isUp ? "arrow-up" : "arrow-down")">@(isUp ? "▲" : "▼")</span> </p>
    </div>
</section>
@*Card section displaying input for shift start and end with submit button to log the day*@
<section class="main-background">
    <div>
        <div class="row-display">
        <label class="secondary-headings">Shift Start
            <input type="time" @bind="shiftStart" />
        </label>
        <label class="secondary-headings">Shift End
            <input type="time" @bind="shiftEnd" />
        </label>
        </div>
        <button class="btn-main" @onclick="AddHours">Submit</button>

    </div>
</section>
@*Section for logged hours, displayed as list with days, hours worked*@
<section class="main-background">
    <div>
        <div class="column-display">
            <h2>Daily Log</h2>
            <ul class="log-list">
                @foreach (var entry in hoursLogged.OrderByDescending(e => e.Key))
                {
                    <li>
                        <span class="entry-date">@entry.Key.ToString("dddd, dd, MMM yyyy")</span>
                        <span class="entry-hours">@entry.Value.ToString("0.##") hrs</span>
                    </li>
                }
            </ul>
        </div>
    </div>
</section>
</body>

@code {
    private Dictionary<DateOnly, double> hoursLogged = new();
    private double grossEarnings;
    private double netEarnings;
    private TimeOnly shiftStart = new TimeOnly(9, 0);
    private TimeOnly shiftEnd = TimeOnly.FromDateTime(DateTime.Now);
    private bool isUp = true; // wire this to your real comparison later

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        hoursLogged = await TrackerService.GetHoursLoggedAsync();
        var summary = await TrackerService.GetEarningsSummaryAsync();
        grossEarnings = summary.GrossEarnings;
        netEarnings = summary.NetEarnings;
    }

    // Method for adding in hours to the log for each day
    // Needs to be linked to calendar input, so need calendar setup first
    public async Task AddHours()
    {
        double hours = (shiftEnd - shiftStart).TotalHours;

        await TrackerService.LogHoursAsync(DateOnly.FromDateTime(DateTime.Today), hours);
        await RefreshAsync();
    }
}
```

Note there's no manual `StateHasChanged()` call — Blazor automatically re-renders a component after an `async` event handler's `Task` completes, so it's redundant once a handler is `async`. Still true today.

**Since this was written:** `Home.razor` now covers period navigation and comparison,
3-period history, year-to-date profit chip, expense logging, the earnings goal progress
bar, illustrated empty states, swipe-to-navigate and haptic feedback, and the
`AnimatedNumber` component for count-up figures — the `isUp` placeholder shown above was
replaced by a real period-over-period comparison. See `docs/CONTEXT.md` → Standard
Features / Premium Features / Mobile UX.

---

## Step 9 — Build, run, and verify end-to-end ✅ Done

1. ~~`dotnet build -f net10.0-maccatalyst` (buildable target on macOS) — confirm it compiles clean.~~
2. ~~Run the app (however you've been running this MAUI app so far — Visual Studio's Run button, or `dotnet build -t:Run -f net10.0-maccatalyst`).~~
3. ~~Go to **Settings**, enter a tax rate (e.g. `20`) and hourly wage (e.g. `15`), tap **Save**.~~
4. ~~Go to **Home**, log a shift. Confirm Gross = total hours × hourly wage, Net = Gross − (Gross × tax rate).~~
5. ~~**Fully quit** the app (not just navigate away) and relaunch it. Confirm the tax rate, hourly wage, and the daily log are all still there.~~
6. ~~Go back to Settings — confirm it shows the same tax rate/wage, proving both pages read from the same database.~~

All six checks passed at the time and the feature shipped; the persistence layer has
been extended many times since (Steps 2–5's "Since this was written" notes) without
regressing any of them.

## Common pitfalls to watch for

- **Original pitfall:** if you change an entity's properties *after* you've already run the app once, `EnsureCreated()` will **not** alter the existing table — delete the `.db` file to force it to be recreated with the new schema.
  **Since this was written, this is no longer the answer** — once real user data existed, deleting the `.db` file was no longer an option. The project's actual solution was Step 4's pattern: every new column/table gets a manual `ExecuteSqlRaw` (`ALTER TABLE` / `CREATE TABLE IF NOT EXISTS`) added to the startup block in `MauiProgram.cs`, wrapped in `try { } catch { }`. **When you add a new persisted field going forward, follow that pattern, not the delete-the-file one.**
- Don't forget `async`/`await` on the Razor side — if a handler or `OnInitializedAsync` isn't `async Task`, the UI won't wait for the database call and may render before the data arrives.
- Test persistence by **fully closing** the app, not just hot-reloading — hot reload can make in-memory bugs look like they're persisting when they aren't.

---

## Keeping this document current

This file documents the local persistence layer specifically (SQLite + EF Core +
`HoursTrackerService`), and should be revisited whenever that layer changes materially —
new entities, new migration steps in `MauiProgram.cs`, or a structural change to the
service layer's API (e.g. the planned Supabase sync work in
`docs/CLOUD_ACCOUNTS_AND_SYNC.md`, which will add `UserId`/`RemoteId`/`UpdatedAt`/
`IsDeleted` columns and a `SyncService`). `docs/CONTEXT.md` remains the single
authoritative full spec; this file exists alongside it as the narrative "how the data
layer was built and how it changed" reference, kept accurate rather than archived.
