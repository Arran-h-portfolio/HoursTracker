# TradeLedger — Project Context

## Overview

TradeLedger is a mobile-first earnings and tax tracking application built for the self-employed. The target audience is people who work for themselves and need a simple, reliable way to know what they have earned and what tax they owe — tradespeople (plumbers, electricians, decorators), freelancers, tattoo artists, hair stylists, market traders, and small business owners.

The core promise: log your hours and expenses, and TradeLedger tells you your true taxable profit and exactly what tax you owe — not just a rough estimate.

---

## Platform

**Mobile only — Android and iOS.**

Built with .NET MAUI Blazor (C#), targeting Android and iPhone iOS. The UI is a Blazor WebView hosted inside MAUI, using SQLite (via EF Core) for all local data persistence. There is no cloud backend or server component at this stage — all data lives on the device.

---

## Business Model

SaaS subscription model. The app is free to download with access to Standard features. Premium features require an active subscription purchased through the Google Play Store or Apple App Store (in-app purchase). The subscription toggle is currently a manual flag (`IsPremium` in Settings) as a placeholder until real store billing is wired up.

---

## Tech Stack

- **Framework:** .NET MAUI Blazor (C#), .NET 10
- **UI:** Blazor WebView, component-scoped CSS, CSS custom properties for theming
- **Database:** SQLite via EF Core (`AppDbContext`), schema managed with `EnsureCreated` + manual `ALTER TABLE / CREATE TABLE IF NOT EXISTS` migrations on startup in `MauiProgram.cs`
- **Notifications:** Plugin.LocalNotification (daily reminder scheduling)
- **Export:** MAUI Share API (CSV file via native share sheet)
- **Key services:** `HoursTrackerService` — the single point of contact with the database; pages never call EF Core directly

---

## Standard Features (Free)

These are available to all users with no subscription required.

- **Shift logging** — log a shift by date, start time, and end time; hours are calculated automatically
- **Edit and delete shifts** — tap to edit hours inline or delete an entry entirely
- **Hourly wage** — set an adjustable hourly rate (capped at £999/hr)
- **Tax mode** — choose between Simple (flat % applied to taxable profit) or UK Self-Employed (accurate 2024/25 banded rates: Personal Allowance, Income Tax, Class 4 NI). Toggled in Settings.
- **Pay period** — choose weekly or monthly pay periods with a configurable start day
- **Earnings card** — displays Hours, Gross Earnings, Tax This Period, and Net Earnings for the selected pay period
- **Period navigation** — ← → arrow buttons plus horizontal swipe gesture on the earnings card to move between periods
- **Period comparison indicator** — shows percentage increase or decrease in gross earnings vs the previous period (e.g. ↑ 14% vs last period)
- **3-period history** — summary card showing the last three pay periods side by side (hours, gross, tax, net)
- **Year-to-date profit chip** — compact card on Home showing taxable profit since 6 Apr (current tax year), with a link to Tax Rundown
- **Daily log** — list of all shifts within the currently viewed pay period, with edit and delete actions
- **Illustrated empty states** — clock SVG shown when no shifts are logged for the period; receipt SVG shown when no expenses are logged
- **Onboarding wizard** — first-run setup (4 steps: Welcome → Hourly Rate → Tax Rate → Pay Period) using bare `EmptyLayout` with no tab bar
- **Dark mode** — System / Light / Dark theme toggle in Settings; preference applied before first paint to prevent flash
- **Daily reminders** — opt-in push notification at a configurable time to prompt the user to log their hours

---

## Premium Features (Subscription Required)

All standard features are included. The following are locked behind a subscription. Premium pages show a paywall card with a lock icon when accessed by a free user.

- **Expense tracking** — log business expenses by category (Materials, Travel, Equipment, Other) with date, amount, and optional description. Expenses are deducted from gross earnings before tax. The earnings card shows the full chain: Gross → Less Expenses → Taxable Profit → Tax → Net. Colour-coded category chips on all expense lists.
- **Period earnings goal** — set a net earnings target (£) per pay period in Settings. The Home screen shows a labelled progress bar beneath the Net Earnings stat: indigo fill while in progress, turns green with "✓ Achieved" when the target is met. Set to 0 to hide. `EarningsGoal` stored in the Settings table.
- **Yearly Dashboard** — a dedicated analytics page (`/dashboard`) showing:
  - Year navigation (browse any past year)
  - Yearly summary: total hours, gross earnings, total expenses, tax owed, net earnings
  - Insights chips: best performing month, average monthly earnings, effective tax rate
  - CSS bar chart of 12 months' gross earnings with the current month highlighted
  - Monthly breakdown table (Month / Hrs / Gross / Expenses / Net)
- **Tax Rundown** — custom date range tax breakdown (`/tax-rundown`):
  - "This Tax Year" quick button (auto-fills 6 Apr → today)
  - Accurate UK banded tax breakdown (Personal Allowance / Income Tax / Class 4 NI) or flat rate, depending on tax mode
  - Month-by-month breakdown table for multi-month ranges
  - Designed to be useful when completing a Self Assessment tax return
- **CSV Export** — export complete earnings and expenses history to a CSV file via the native share sheet; compatible with Excel, Google Sheets, and Numbers

---

## Data Model

| Table | Key fields |
|---|---|
| `Settings` | `HourlyWage`, `TaxRate`, `PayPeriodType`, `PayPeriodStartDay`, `IsPremium`, `IsOnboarded`, `ThemePreference`, `NotificationsEnabled`, `NotificationHour`, `NotificationMinute`, `UseUKTax`, `EarningsGoal` |
| `HoursEntries` | `Date` (unique), `HoursWorked` |
| `Expenses` | `Date`, `Amount`, `Category` (enum: Materials/Travel/Equipment/Other), `Description` |

### Earnings formula

All financial figures are derived at read time — nothing is stored — so they can never go stale:

```
GrossEarnings  = TotalHours × HourlyWage
TaxableProfit  = max(0, GrossEarnings − TotalExpenses)
TaxAmount      = TaxableProfit × TaxRate          ← Simple mode
NetEarnings    = TaxableProfit − TaxAmount
```

### UK Self-Employed tax bands (2024/25)

Applied to the full period total in Tax Rundown only. Period earnings card always uses the flat rate for quick reference.

| Band | Income | Income Tax | Class 4 NI |
|---|---|---|---|
| Personal Allowance | Up to £12,570 | 0% | 0% |
| Basic Rate | £12,571 – £50,270 | 20% | 6% |
| Higher Rate | £50,271 – £125,140 | 40% | 2% |
| Additional Rate | Above £125,140 | 45% | 2% |

Class 2 NI abolished April 2024 — not included. Personal allowance taper above £100k not yet modelled.

---

## Pages and Navigation

| Route | Page | Access |
|---|---|---|
| `/` | Home — shift logging, earnings card, expense logging, period goal, daily log, period history | Free |
| `/dashboard` | Dashboard — yearly chart, breakdown table, insights, CSV export | Premium |
| `/tax-rundown` | Tax Rundown — custom date range tax breakdown | Premium |
| `/settings` | Settings — wage, tax mode, pay period, earnings goal, appearance, notifications, subscription | Free |
| `/onboarding` | Onboarding wizard — first-run setup, no tab bar | Free |

**Bottom tab bar** (fixed, replaces original sidebar) — four tabs: Home / Dashboard / Tax / Settings. Inline SVG icons coloured with `currentColor`; active tab shows brand indigo with a pill indicator at the top edge and a subtle icon scale-up. Respects iOS `env(safe-area-inset-bottom)`. Tab bar CSS lives in global `app.css` (not scoped) because `NavLink` is a child component and Blazor CSS isolation does not apply scope attributes to child component output.

---

## Styling and UI

### Design language

- Clean, professional SaaS aesthetic — white cards on a light grey background (`#F8FAFC`)
- Brand colour: `#4F46E5` (indigo); hover: `#4338CA`
- Green (`#10B981`) for positive / net figures; red (`#EF4444`) for tax and expenses
- Full dark mode via CSS custom properties (`--color-bg`, `--color-surface`, `--color-brand`, etc.); theme applied from `localStorage` before first paint to prevent flash
- Entrance animations on cards (`card-enter` keyframe, staggered delay per card)
- `AnimatedNumber.razor` shared component: smooth count-up animation on all earnings figures

### Mobile UX

- **Haptic feedback** — `HapticFeedback.Default.Perform()` fires on every mutating action: Log Shift / Log Expense / Save Edit (`Click`), Delete shift/expense (`LongPress`). Wrapped in try/catch for platform safety via a private `Haptic()` helper in `Home.razor`.
- **Swipe-to-navigate periods** — `@ontouchstart` / `@ontouchend` on the earnings card. Fires only when horizontal delta > 48 px and is the dominant axis, so vertical page scroll is unaffected. Handlers: `OnEarningsSwipeStart` / `OnEarningsSwipeEnd`.
- **Illustrated empty states** — inline SVG illustrations (clock for no shifts, receipt for no expenses) with a title and subtitle, replacing blank list areas. Use `currentColor` so they respond to dark mode automatically.

### Branding

- **App icon** — indigo gradient background (`#6366F1` → `#3730A3`); white coin/badge circle foreground with a bold `£` symbol in brand indigo. Pure SVG stroke paths, no text elements, readable at all sizes. Android adaptive icon safe zone respected.
- **Splash screen** — same `£` badge mark centred above the "TradeLedger" wordmark and tagline "Track your hours. Know your profit." on `#4F46E5` indigo background. `BaseSize="360,360"`.

---

## Key Files

| File | Purpose |
|---|---|
| `Components/Models/AppSettings.cs` | All settings fields including `IsPremium`, `UseUKTax`, `EarningsGoal` |
| `Components/Pages/Home.razor` | Main page: earnings card, goal bar, expense log, daily log, empty states, swipe + haptic handlers |
| `Components/Pages/Settings.razor` | All user settings including premium goal input |
| `Components/Pages/Dashboard.razor` | Premium yearly analytics page |
| `Components/Pages/TaxRundown.razor` | Premium tax breakdown page |
| `Components/Shared/AnimatedNumber.razor` | Reusable count-up number animation component |
| `Components/Layout/NavMenu.razor` | Bottom tab bar (four tabs, inline SVG icons) |
| `Components/Layout/EmptyLayout.razor` | Bare layout for onboarding (no tab bar) |
| `Services/HoursTrackerService.cs` | All DB queries; `CalculateUKTax()`, `GetYearToDateSummaryAsync()`, `GetTaxRundownAsync()` |
| `Services/NotificationService.cs` | Wraps Plugin.LocalNotification for daily reminders |
| `MauiProgram.cs` | App bootstrap, DI registration, DB migrations |
| `wwwroot/app.css` | Global CSS: variables, dark mode, animations, bottom tab bar styles |
| `wwwroot/index.html` | Theme init script (prevents flash), loads `TradeLedger.styles.css` bundle |
| `Platforms/Android/MainActivity.cs` | Passes `null` to `base.OnCreate` to prevent stale fragment state crash |
| `docs/GOOGLE_PLAY_PUBLISHING.md` | Full Play Store publishing guide including billing wiring |
| `docs/APPLE_APP_STORE_PUBLISHING.md` | Full App Store publishing guide including StoreKit wiring |

---

## What Is Not Yet Built (Planned Next Steps)

1. **Real in-app purchase billing** — replace the manual `IsPremium` toggle with Google Play Billing and Apple StoreKit. Tutorial docs are already written and ready in `docs/`.
2. **Cloud backup / sync** — local-only storage means data is lost if the device is replaced; iCloud or Google Drive backup would significantly increase user trust and retention.
3. **UK tax personal allowance taper above £100k** — the £100k–£125,140 taper (effective 60% rate) is not yet modelled; low priority for v1.
