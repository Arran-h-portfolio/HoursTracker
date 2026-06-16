# Tutorial: Adjustable Tax Rate + Local SQLite Persistence

A step-by-step build guide for adding a persisted tax rate + hourly wage, and showing gross/net earnings on the Home page, backed by a real local SQLite database via EF Core. Written for someone who hasn't used SQLite/EF Core before — each step explains *why*, not just *what*.

## Why this is bigger than "add a field"

Right now `TrackingData` is registered as a DI singleton in `MauiProgram.cs`, but `Home.razor` and `Settings.razor` each do `new TrackingData()` instead of injecting it — so the two pages don't even share data with each other today, and nothing survives an app restart. SQLite + EF Core gives real persistence, which is also a great portfolio talking point ("I designed a local relational schema and used an ORM"), so this guide replaces the single in-memory blob with a small proper data layer.

## Concepts you'll use (read once, refer back as needed)

- **SQLite**: a relational database that lives in a single file on disk — no server process needed. Perfect for a single-user mobile/desktop app.
- **EF Core**: Microsoft's ORM (Object-Relational Mapper). You write plain C# classes ("entities"); EF Core turns them into tables and turns your C# code into SQL.
- **DbContext**: the EF Core class representing one working session with the database — it's what you call `.Add()`, `.Update()`, and run queries against.
- **DbContextFactory** (instead of injecting a `DbContext` directly): in Blazor, a single long-lived `DbContext` isn't safe to share across concurrent operations. The recommended pattern is to inject a *factory* and create a short-lived `DbContext` per operation: create it, use it, dispose it.
- **`EnsureCreated()` vs Migrations**: Migrations are how you evolve a schema over time without losing existing users' data — proper tooling, but more ceremony. `EnsureCreated()` just builds the schema from your current model the first time the DB doesn't exist. Since there's no real user data yet, `EnsureCreated()` is the right tool for now. (Switching to Migrations later is a natural "next step" if you want to demonstrate that skill too.)
- **Dependency Injection (DI) recap**: `MauiProgram.cs` registers services into a container; `@inject SomeService Foo` in a `.razor` file asks that container for an instance.

---

## Step 1 — Add the EF Core SQLite package

```bash
dotnet add SecretProject/SecretProject.csproj package Microsoft.EntityFrameworkCore.Sqlite
```

This adds a `<PackageReference>` to `SecretProject.csproj` and pulls in the EF Core SQLite provider, including the native SQLite engine binaries (via `SQLitePCLRaw`) bundled automatically for each platform you target.

**Checkpoint:** open `SecretProject.csproj` and confirm a new `Microsoft.EntityFrameworkCore.Sqlite` line appeared in the `<ItemGroup>`.

---

## Step 2 — Create the entity classes

These are plain C# classes that EF Core will turn into tables. Create two new files in `Components/Models/`.

**`Components/Models/AppSettings.cs`** (one row — the user's settings):
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

**`Components/Models/HoursEntry.cs`** (one row per day worked — replaces `Dictionary<DateOnly, double>`):
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

**Checkpoint:** project should still build (these classes aren't used by anything yet).

---

## Step 3 — Create the DbContext

This is the class that represents "a connection to the database" and exposes your tables. Create a new `Services/` folder, then `Services/AppDbContext.cs`:

```csharp
using Data.Models;
using Microsoft.EntityFrameworkCore;

namespace SecretProject.Services;

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

The unique index on `Date` enforces "one row per day" at the database level — it'll reject (or rather, we'll prevent via code in Step 5) two entries for the same date.

---

## Step 4 — Wire it into MauiProgram.cs and create the DB file

Replace the contents of `MauiProgram.cs` with:

```csharp
using Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecretProject.Services;

namespace SecretProject;

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

Note: `TrackingData` is no longer registered here — it'll be deleted in Step 6. `HoursTrackerService` doesn't exist yet either — that's Step 5. This file references both ahead of time so you can see the full picture; it just won't compile until you finish Steps 5 and 6.

`FileSystem.Current.AppDataDirectory` is MAUI's cross-platform "writable app data folder" API — it resolves to the right sandboxed location on Android, iOS, MacCatalyst, and Windows automatically.

**Tip for later:** if you want to *see* the actual database file (great for a portfolio demo), temporarily add `System.Diagnostics.Debug.WriteLine(dbPath);` next to where `dbPath` is built, run the app once, find the path in your debug output, then open that `.db` file with a free tool like "DB Browser for SQLite" to browse the tables visually.

---

## Step 5 — Build the service layer (the bridge between UI and database)

This is the only place that talks to `AppDbContext` directly — pages will call this service, never EF Core directly. Create `Services/HoursTrackerService.cs`:

```csharp
using Data.Models;
using Microsoft.EntityFrameworkCore;

namespace SecretProject.Services;

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

Why Gross/Net aren't stored anywhere: they're always *derived* from hours + wage + tax rate. Computing them on read means they can never go stale, no matter which page last changed something.

`LogHoursAsync` is an "upsert" (update-or-insert) — it checks if today already has a row before deciding whether to add a new one or update the existing one, which is what keeps the unique index from Step 3 happy.

**Checkpoint:** the project should now build cleanly (MauiProgram.cs's references all resolve).

---

## Step 6 — Delete the old model

Delete `Components/Models/Data.cs` (the `TrackingData` class). It's fully superseded by `AppSettings` + `HoursEntry`.

**Checkpoint:** `dotnet build` will now fail — but on purpose. The compiler will point you at exactly the two files left to update: `Home.razor` and `Settings.razor`, both of which still reference `TrackingData`. That's a useful signal, not a problem.

---

## Step 7 — Rewire Settings.razor

Replace the entire contents of `Components/Pages/Settings.razor`:

```razor
@page "/settings"
@using Data.Models
@using SecretProject.Services
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

`OnInitializedAsync` is a Blazor lifecycle hook that runs once when the page first loads — the right place to pull data from the database. The percentage-to-fraction conversion happens here in the UI layer, keeping the stored `TaxRate` a clean 0–1 fraction for the earnings math.

---

## Step 8 — Rewire Home.razor

Replace the entire contents of `Components/Pages/Home.razor`:

```razor
@page "/"
@using Data.Models
@using SecretProject.Services
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

Note there's no manual `StateHasChanged()` call anymore — Blazor automatically re-renders a component after an `async` event handler's `Task` completes, so it's redundant once `AddHours` is `async`.

The MoM/arrow indicator (`isUp`) is left exactly as it was — still a placeholder, out of scope for this feature.

---

## Step 9 — Build, run, and verify end-to-end

1. `dotnet build -f net10.0-maccatalyst` (buildable target on macOS) — confirm it compiles clean.
2. Run the app (however you've been running this MAUI app so far — Visual Studio's Run button, or `dotnet build -t:Run -f net10.0-maccatalyst`).
3. Go to **Settings**, enter a tax rate (e.g. `20`) and hourly wage (e.g. `15`), tap **Save**.
4. Go to **Home**, log a shift. Confirm:
   - **Gross** = total hours × hourly wage
   - **Net** = Gross − (Gross × tax rate) — e.g. with 20% tax, Net should be 80% of Gross.
5. **Fully quit** the app (not just navigate away) and relaunch it. Confirm the tax rate, hourly wage, and the daily log are all still there — this proves SQLite persistence is actually working, not just in-memory state.
6. Go back to Settings — confirm it shows the same tax rate/wage you set, proving both pages are now reading from the same database instead of separate disconnected objects.

## Common pitfalls to watch for

- If you change an entity's properties (e.g. add a new field to `AppSettings`) *after* you've already run the app once, `EnsureCreated()` will **not** alter the existing table — delete the `.db` file (find its path via the `Debug.WriteLine` tip in Step 4) to force it to be recreated with the new schema. This is exactly the problem Migrations solve, if you want to explore that next.
- Don't forget `async`/`await` on the Razor side — if `AddHours` or `OnInitializedAsync` aren't `async Task`, the UI won't wait for the database call and may render before the data arrives.
- Test persistence by **fully closing** the app, not just hot-reloading — hot reload can make in-memory bugs look like they're persisting when they aren't.
