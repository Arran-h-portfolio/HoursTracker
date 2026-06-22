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

SaaS subscription model. The app is free to download with access to Standard features. Premium features require an active subscription purchased through the Google Play Store or Apple App Store (in-app purchase). The subscription toggle is currently a manual flag (`IsPremium` in settings) as a placeholder until real store billing is wired up.

---

## Tech Stack

- **Framework:** .NET MAUI Blazor (C#)
- **UI:** Blazor WebView, component-scoped CSS, CSS custom properties for theming
- **Database:** SQLite via EF Core (`AppDbContext`), schema managed with `EnsureCreated` + manual `ALTER TABLE / CREATE TABLE IF NOT EXISTS` migrations on startup
- **Notifications:** Plugin.LocalNotification (daily reminder scheduling)
- **Export:** MAUI Share API (CSV file via native share sheet)
- **Key services:** `HoursTrackerService` — the single point of contact with the database; pages never call EF Core directly

---

## Standard Features (Free)

These are available to all users with no subscription required.

- **Shift logging** — log a shift by date, start time, and end time; hours are calculated automatically
- **Edit and delete shifts** — tap to edit hours inline or delete an entry entirely
- **Hourly wage** — set an adjustable hourly rate (capped at £999/hr)
- **Tax rate** — set an adjustable flat tax rate (0–100%)
- **Pay period** — choose weekly or monthly pay periods with a configurable start day
- **Earnings card** — displays Hours, Gross Earnings, Tax This Period, and Net Earnings for the selected pay period
- **Period navigation** — swipe back and forward through previous and future pay periods
- **Period comparison indicator** — shows percentage increase or decrease in gross earnings vs the previous period (e.g. ↑ 14% vs last period)
- **3-period history** — a summary card showing the last three pay periods side by side (hours, gross, tax, net)
- **Daily log** — a filterable list of all shifts logged within the currently viewed pay period
- **Onboarding** — a first-run wizard that guides new users through setting their hourly rate, tax rate, and pay period before reaching the home screen
- **Dark mode** — System / Light / Dark theme toggle in Settings; theme preference persists across sessions
- **Daily reminders** — opt-in push notification at a configurable time to prompt the user to log their hours

---

## Premium Features (Subscription Required)

All standard features are included. The following are locked behind a subscription.

- **Expense tracking** — log business expenses (Materials, Travel, Equipment, Other) with date, amount, and optional description. Expenses are subtracted from gross earnings to produce an accurate taxable profit figure. The earnings card shows the full calculation chain: Gross → Less Expenses → Taxable Profit → Tax → Net
- **Yearly Dashboard** — a dedicated analytics page showing:
  - Year navigation (browse any past year)
  - Yearly summary: total hours, gross earnings, total expenses, tax owed, net earnings
  - Insights: best performing month, average monthly earnings, effective tax rate
  - 12-month bar chart of gross earnings with the current month highlighted
  - Monthly breakdown table (Month / Hrs / Gross / Expenses / Net)
- **Tax Rundown** — select any custom date range and generate a detailed tax breakdown:
  - Total hours and gross earnings for the range
  - Less: business expenses
  - Taxable profit
  - Tax owed (highlighted prominently)
  - Net earnings
  - Month-by-month breakdown table for multi-month ranges
  - Designed to be useful when completing a self-assessment tax return
- **CSV Export** — export the complete earnings and expenses history to a CSV file via the native share sheet; compatible with Excel, Google Sheets, and Numbers

---

## Data Model

| Table | Key fields |
|---|---|
| `Settings` | `HourlyWage`, `TaxRate`, `PayPeriodType`, `PayPeriodStartDay`, `IsPremium`, `ThemePreference`, `NotificationsEnabled`, `NotificationHour`, `NotificationMinute` |
| `HoursEntries` | `Date` (unique), `HoursWorked` |
| `Expenses` | `Date`, `Amount`, `Category` (enum: Materials/Travel/Equipment/Other), `Description` |

### Earnings formula

All financial figures are derived at read time — nothing is stored — so they can never go stale:

```
GrossEarnings  = TotalHours × HourlyWage
TaxableProfit  = max(0, GrossEarnings − TotalExpenses)
TaxAmount      = TaxableProfit × TaxRate
NetEarnings    = TaxableProfit − TaxAmount
```

---

## Pages and Navigation

| Route | Page | Access |
|---|---|---|
| `/` | Home — shift logging, earnings card, expense logging, daily log, period history | Free |
| `/dashboard` | Dashboard — yearly chart, breakdown table, insights, CSV export | Premium |
| `/tax-rundown` | Tax Rundown — custom date range tax breakdown | Premium |
| `/settings` | Settings — wage, tax rate, pay period, appearance, notifications, subscription | Free |
| `/onboarding` | Onboarding wizard — first-run setup (uses bare EmptyLayout, no nav sidebar) | Free |

Premium pages show a paywall card with a lock icon and a link to Settings when accessed by a free user.

---

## Styling and UI Principles

- Clean, professional SaaS aesthetic — white cards on a light grey background, indigo brand colour (`#4F46E5`), green for positive/net figures, red for tax/expenses
- Full dark mode support via CSS custom properties (`--color-bg`, `--color-surface`, `--color-brand`, etc.)
- Tasteful entrance animations on cards (`card-enter` keyframe, staggered per card)
- Animated number count-up on earnings figures (`AnimatedNumber` shared component)
- No unnecessary complexity — the UI should feel immediately obvious to a non-technical user

---

## What Is Not Yet Built (Planned Next Steps)

1. **Real in-app purchase billing** — replace the manual `IsPremium` toggle with Google Play Billing and Apple StoreKit so the subscription is actually charged through the app stores
2. **App icon and splash screen** — current assets are the default MAUI placeholders; needs branded TradeLedger artwork
3. **UK tax band calculation** — replace the flat tax rate with an accurate UK self-assessment calculation (personal allowance, income tax bands, Class 4 NI) so the tax figure is genuinely correct for UK self-employed users
4. **Cloud backup / sync** — local-only storage means data is lost if the device is replaced; iCloud or Google Drive backup would build user trust significantly
