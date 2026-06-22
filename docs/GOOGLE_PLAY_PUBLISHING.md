# TradeLedger — Google Play Store Publishing Guide

This document covers everything you need to take TradeLedger from a finished build to a live listing on the Google Play Store, including the privacy policy, store listing copy, and wiring in the premium subscription billing.

---

## Before You Start — Prerequisites

- Google Play Developer Account ($25 one-time fee at play.google.com/console)
- A **keystore file** — your app signing identity (generated once, kept forever — losing it means you can never update the app)
- Visual Studio with the MAUI Android workload installed
- The app building cleanly in Release configuration

---

## Step 1 — Create Your Signing Keystore

You only do this once. Store the output file somewhere safe (cloud backup, USB drive) — it is irreplaceable.

Run this in a terminal:

```bash
keytool -genkeypair -v \
  -keystore tradeledger.keystore \
  -alias tradeledger \
  -keyalg RSA \
  -keysize 2048 \
  -validity 10000
```

You will be prompted for a password and some identity details (name, organisation, country). Fill these in — they are embedded in the certificate but are not shown to users.

Save `tradeledger.keystore` somewhere permanent. Also note:
- **Keystore password** — what you set above
- **Key alias** — `tradeledger`
- **Key password** — same as keystore password (or set separately)

---

## Step 2 — Build a Signed Release AAB

Google Play requires an Android App Bundle (`.aab`), not a plain APK.

**Via Visual Studio:**
1. Right-click the `TradeLedger` project → **Archive for Publishing**
2. Once archived, click **Distribute** → **Google Play**
3. Select or import your keystore and fill in the alias/password
4. Click **Save As** to export the `.aab` file

**Via terminal:**

```bash
dotnet publish -f net10.0-android -c Release \
  /p:AndroidKeyStore=true \
  /p:AndroidSigningKeyStore=/path/to/tradeledger.keystore \
  /p:AndroidSigningKeyAlias=tradeledger \
  /p:AndroidSigningKeyPass=YOUR_PASSWORD \
  /p:AndroidSigningStorePass=YOUR_PASSWORD
```

The `.aab` file will appear in:
`bin/Release/net10.0-android/publish/`

---

## Step 3 — Create the App in Play Console

1. Go to [play.google.com/console](https://play.google.com/console)
2. Click **Create app**
3. Fill in:
   - **App name:** TradeLedger
   - **Default language:** English (United Kingdom)
   - **App or game:** App
   - **Free or paid:** Free (your revenue comes from in-app subscriptions)
4. Accept the declarations and click **Create app**

---

## Step 4 — Store Listing

Navigate to **Store presence → Main store listing**. Use the copy below as your starting point — edit freely to match your voice.

---

### App Name
```
TradeLedger
```

### Short Description (max 80 characters)
```
Track hours, expenses & tax — built for the self-employed.
```

### Full Description (max 4000 characters)
```
TradeLedger is the earnings and tax tracker built for people who work for 
themselves — tradespeople, freelancers, tattoo artists, hair stylists, market 
traders, and anyone else who runs their own show.

Stop guessing what you owe in tax. Log your shifts and business expenses, and 
TradeLedger works out your true taxable profit and exactly what you owe — not 
just a rough estimate based on your gross income.

--- FREE FEATURES ---

• Log daily shifts by start and end time — hours calculated automatically
• Set your hourly rate and tax rate
• See your gross earnings, tax owed, and net earnings update in real time
• Choose weekly or monthly pay periods with a custom start day
• Navigate back and forward through pay periods
• See whether you earned more or less than the previous period
• Edit or delete any logged shift
• 3-period earnings history at a glance
• Daily reminder notification so you never forget to log your hours
• Dark mode support

--- PREMIUM FEATURES ---

• Expense tracking — log Materials, Travel, Equipment and Other expenses.
  Your taxable profit is calculated correctly as Gross minus Expenses, so your 
  tax figure is actually accurate.
  
• Yearly Dashboard — a full 12-month earnings chart, monthly breakdown table, 
  and insights showing your best month, monthly average and effective tax rate.
  
• Tax Rundown — pick any date range and get a full breakdown: gross earnings, 
  less expenses, taxable profit, and tax owed. Designed to make your 
  self-assessment straightforward.
  
• CSV Export — export your complete earnings and expenses history to a 
  spreadsheet-compatible file and share it anywhere.

--- DESIGNED FOR ---

Plumbers, electricians, decorators, carpenters, tattoo artists, barbers, 
hairdressers, market traders, delivery drivers, gardeners, cleaners, 
consultants, photographers — anyone who is self-employed and needs to stay on 
top of their income and tax without a complicated accounting package.

No subscriptions needed for the core tracker. Upgrade to Premium when you need 
the full picture.
```

---

### App Category
**Finance**

### Tags (add up to 5)
- Self employed
- Tax calculator
- Earnings tracker
- Freelancer
- Invoice

### Contact Details
- **Email:** your contact email address
- **Website:** your privacy policy URL (see Step 6)
- **Phone:** optional, can leave blank

---

## Step 5 — Graphics and Screenshots

Play Console requires:

| Asset | Size | Notes |
|---|---|---|
| App icon | 512 × 512 px PNG | No alpha/transparency allowed |
| Feature graphic | 1024 × 500 px PNG or JPG | Shown at top of listing |
| Phone screenshots | Min 2, max 8 | At least 320px on shortest side |

**Screenshot tips for TradeLedger:**
- Home screen showing the earnings card with real-looking figures
- The expense logging form
- The Dashboard bar chart with data populated
- The Tax Rundown results screen
- The Settings/subscription page
- Dark mode version of the home screen

Use realistic but fictional numbers (e.g. hourly rate £35, a few months of data). Avoid showing £0 everywhere — it makes the app look empty.

---

## Step 6 — Privacy Policy

Google Play requires a privacy policy URL for any app. TradeLedger collects financial data, so this is non-negotiable.

### What Your Privacy Policy Must Cover

TradeLedger stores all data **locally on the device only**. No data is sent to any server. Your policy should clearly state:

---

**Draft Privacy Policy — copy and adapt this:**

```
Privacy Policy for TradeLedger

Last updated: [DATE]

TradeLedger ("the App") is operated by [YOUR NAME / BUSINESS NAME].

WHAT DATA WE COLLECT
The App stores the following data locally on your device:
- Your configured hourly wage and tax rate
- Shift dates and hours worked
- Business expense dates, amounts, categories, and descriptions
- App preferences (theme, notification settings, pay period settings)

HOW WE USE YOUR DATA
All data is used solely to calculate and display your earnings, tax, and 
expense summaries within the App. No data is used for advertising or profiling.

DATA STORAGE
All data is stored locally on your device using an SQLite database. No data 
is transmitted to external servers, third parties, or the developer.

THIRD-PARTY SERVICES
The App uses Google Play Billing to process subscription purchases. Google's 
privacy policy applies to that transaction: https://policies.google.com/privacy

The App uses local push notifications (scheduled on-device). No notification 
data leaves your device.

DATA DELETION
You can delete all app data by uninstalling the App from your device.

CHILDREN
The App is not directed at children under 13.

CONTACT
If you have questions about this policy, contact: [YOUR EMAIL ADDRESS]
```

---

### Where to Host the Privacy Policy

The easiest free option if you are already using GitHub:

1. In your repository, create a file at `docs/privacy-policy.html` (or `.md`)
2. Go to your repository **Settings → Pages**
3. Set source to **Deploy from branch → main → /docs**
4. Your policy will be live at:
   `https://YOUR-GITHUB-USERNAME.github.io/HoursTracker/privacy-policy`

Use that URL in the Play Console. It is free, always online, and version controlled alongside your code.

---

## Step 7 — Data Safety Section

In Play Console go to **Policy → Data safety**. Answer as follows for TradeLedger:

| Question | Answer |
|---|---|
| Does your app collect or share any of the required user data types? | **Yes** |
| Is all of the user data collected by your app encrypted in transit? | **Yes** (no data leaves the device, so N/A — select yes) |
| Do you provide a way for users to request that their data is deleted? | **Yes** — explain that uninstalling the app deletes all data |

**Data types to declare:**
- **Financial info → Financial transactions** — users log earnings and expenses (stored locally, not shared)

For each type: select **Collected**, **Not shared**, **Encrypted**, **Required for app functionality**.

---

## Step 8 — Content Rating

Go to **Policy → App content → Content rating** and complete the questionnaire.

TradeLedger answers:
- **Category:** Finance
- Violence, sexual content, drugs: **No** to all
- User-generated content: **No**
- Location sharing: **No**
- In-app purchases: **Yes**

Your rating will likely come out as **PEGI 3** or **Everyone** — suitable for all ages.

---

## Step 9 — Set Up the Premium Subscription Product

Before you can wire billing into the app, you need to create the subscription product in Play Console.

1. Go to **Monetise → Subscriptions**
2. Click **Create subscription**
3. Fill in:
   - **Product ID:** `tradeledger_premium_monthly`
     (write this down — you will hardcode it in the app)
   - **Name:** TradeLedger Premium
   - **Description:** Full access to Dashboard, Tax Rundown, expense tracking and CSV export.
   - **Benefits:** Add bullet points matching your premium feature list
4. Under **Base plans and offers**, click **Add base plan**:
   - **Base plan ID:** `monthly`
   - **Billing period:** Monthly
   - **Price:** Set your price (e.g. £2.99 or £3.99/month — Play Console will auto-convert to other currencies)
   - **Renewal type:** Auto-renewing
5. Click **Save** then **Activate**

You can also create an annual plan (`tradeledger_premium_annual`) at a discounted rate to give users the option.

---

## Step 10 — Wire Google Play Billing Into the App

### Install the package

Add `Plugin.InAppBilling` to the project (by James Montemagno — the standard community library for MAUI/Xamarin billing):

```bash
dotnet add package Plugin.InAppBilling
```

Or via NuGet Package Manager: search `Plugin.InAppBilling`.

### Add the billing permission to AndroidManifest.xml

Open `Platforms/Android/AndroidManifest.xml` and add inside `<manifest>`:

```xml
<uses-permission android:name="com.android.vending.BILLING" />
```

### Create a SubscriptionService

Create `TradeLedger/Services/SubscriptionService.cs`:

```csharp
using Plugin.InAppBilling;

namespace TradeLedger.Services;

public class SubscriptionService
{
    private const string ProductId = "tradeledger_premium_monthly";

    public async Task<bool> PurchasePremiumAsync()
    {
        try
        {
            var billing   = CrossInAppBilling.Current;
            var connected = await billing.ConnectAsync();
            if (!connected) return false;

            var purchase = await billing.PurchaseAsync(ProductId, ItemType.Subscription);
            await billing.DisconnectAsync();

            return purchase?.State == PurchaseState.Purchased;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CheckPremiumStatusAsync()
    {
        try
        {
            var billing   = CrossInAppBilling.Current;
            var connected = await billing.ConnectAsync();
            if (!connected) return false;

            var purchases = await billing.GetPurchasesAsync(ItemType.Subscription);
            await billing.DisconnectAsync();

            return purchases?.Any(p =>
                p.ProductId == ProductId &&
                p.State == PurchaseState.Purchased) ?? false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RestorePurchasesAsync() => await CheckPremiumStatusAsync();
}
```

### Register the service in MauiProgram.cs

```csharp
builder.Services.AddSingleton<SubscriptionService>();
```

### Update the Settings page to use real billing

Replace the `TogglePremium` method and subscription UI in `Settings.razor` to call the real service:

```csharp
@inject SubscriptionService SubscriptionService

// Replace TogglePremium with:
private async Task UpgradeToPremium()
{
    var success = await SubscriptionService.PurchasePremiumAsync();
    if (success)
    {
        var settings = await TrackerService.GetSettingsAsync();
        settings.IsPremium = true;
        await TrackerService.SaveSettingsAsync(settings);
        isPremium = true;
    }
}

private async Task RestorePurchases()
{
    var active = await SubscriptionService.CheckPremiumStatusAsync();
    if (active)
    {
        var settings = await TrackerService.GetSettingsAsync();
        settings.IsPremium = true;
        await TrackerService.SaveSettingsAsync(settings);
        isPremium = true;
    }
}
```

Update the "Upgrade to Premium" button to call `UpgradeToPremium` and add a "Restore Purchases" button (Apple requires this — good practice on Android too).

### Check subscription status on app launch

In `MauiProgram.cs` or `App.xaml.cs`, after the app starts, verify the subscription is still active:

```csharp
// In App.xaml.cs OnStart or similar
var subscriptionService = app.Services.GetRequiredService<SubscriptionService>();
var trackerService      = app.Services.GetRequiredService<HoursTrackerService>();

var isActive = await subscriptionService.CheckPremiumStatusAsync();
var settings = await trackerService.GetSettingsAsync();
if (settings.IsPremium != isActive)
{
    settings.IsPremium = isActive;
    await trackerService.SaveSettingsAsync(settings);
}
```

This means if someone cancels their subscription, the premium features are automatically removed on next app launch.

---

## Step 11 — Upload and Submit

1. In Play Console go to **Release → Production**
2. Click **Create new release**
3. Upload your signed `.aab` file
4. Add release notes (e.g. "Initial release of TradeLedger — track your hours, expenses and tax.")
5. Click **Save** then **Review release**
6. Fix any warnings the pre-launch report flags
7. Click **Start rollout to Production**

**First submission review** typically takes 1–3 days. Subsequent updates are usually reviewed within a few hours.

---

## Quick Reference Checklist

- [ ] Keystore file created and backed up securely
- [ ] Signed `.aab` built in Release configuration
- [ ] App created in Play Console
- [ ] Store listing complete (name, descriptions, screenshots, icon)
- [ ] Privacy policy written and hosted at a public URL
- [ ] Data safety section completed
- [ ] Content rating questionnaire completed
- [ ] Subscription product `tradeledger_premium_monthly` created and activated in Play Console
- [ ] `Plugin.InAppBilling` added to the project
- [ ] `BILLING` permission added to AndroidManifest.xml
- [ ] `SubscriptionService` wired up and `IsPremium` toggle replaced with real billing calls
- [ ] Release `.aab` uploaded and submitted for review

---

## Useful Links

- Play Console: https://play.google.com/console
- Plugin.InAppBilling docs: https://github.com/jamesmontemagno/InAppBillingPlugin
- Google Play Billing overview: https://developer.android.com/google/play/billing
- GitHub Pages (for hosting privacy policy): https://pages.github.com
- Google's data safety guidance: https://support.google.com/googleplay/android-developer/answer/10787469
