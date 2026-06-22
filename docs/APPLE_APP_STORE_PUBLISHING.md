# TradeLedger — Apple App Store Publishing Guide

This document covers everything you need to publish TradeLedger on the Apple App Store, from certificates and signing through store listing copy, privacy policy, TestFlight beta testing, and wiring in Apple's subscription billing (StoreKit).

---

## Key Differences From Google Play

Before you start, be aware of the main differences:

| | Google Play | Apple App Store |
|---|---|---|
| Developer fee | $25 one-time | $99/year |
| Build machine | Windows or Mac | **Mac required** |
| Build tool | Visual Studio / dotnet CLI | Xcode (on Mac) |
| Signing | Keystore file | Certificate + Provisioning Profile |
| Upload tool | Play Console web | Xcode Organizer or Transporter app |
| Beta testing | Internal test track | TestFlight |
| Review time | 1–3 days first time | 1–3 days (can be up to 7) |
| Billing API | Google Play Billing | StoreKit |

---

## Before You Start — Prerequisites

- Apple Developer Program membership ($99/year at developer.apple.com/programs)
- A Mac running macOS 14 (Sonoma) or later
- Xcode 15 or later installed from the Mac App Store (free)
- .NET MAUI workload installed on the Mac: `sudo dotnet workload install maui`
- An Apple ID enrolled in the Developer Program (not just a regular Apple ID)
- The TradeLedger project building cleanly in Release configuration

---

## Step 1 — Certificates and Provisioning Profiles

Apple signing is more involved than Android. You need two things:

- A **Distribution Certificate** — proves the app came from you (like Android's keystore)
- A **Provisioning Profile** — ties your certificate to your specific App ID

### Register Your App ID

1. Go to [developer.apple.com](https://developer.apple.com) → **Account → Certificates, Identifiers & Profiles**
2. Click **Identifiers → +**
3. Select **App IDs → App**
4. Fill in:
   - **Description:** TradeLedger
   - **Bundle ID:** Explicit — e.g. `com.yourname.tradeledger`
     (Use reverse domain notation. This must match your `.csproj` `ApplicationId` exactly.)
   - **Capabilities:** Check **In-App Purchase** (required for subscriptions)
5. Click **Continue → Register**

### Create a Distribution Certificate

1. In **Certificates, Identifiers & Profiles → Certificates → +**
2. Select **Apple Distribution** (used for both App Store and TestFlight)
3. Follow the prompts to create a **Certificate Signing Request (CSR)** from your Mac using Keychain Access
4. Upload the CSR, then download and double-click the resulting `.cer` file to install it in your keychain

### Create a Provisioning Profile

1. In **Profiles → +**
2. Select **App Store Connect** under Distribution
3. Choose your App ID (`com.yourname.tradeledger`)
4. Select your Distribution Certificate
5. Name it `TradeLedger App Store` and download it
6. Double-click the `.mobileprovision` file — Xcode adds it automatically

### Update the MAUI project

In `TradeLedger.csproj`, confirm (or add) the iOS bundle ID:

```xml
<PropertyGroup Condition="$(TargetFramework.Contains('-ios'))">
    <ApplicationId>com.yourname.tradeledger</ApplicationId>
    <ApplicationTitle>TradeLedger</ApplicationTitle>
    <ApplicationVersion>1</ApplicationVersion>
    <ApplicationDisplayVersion>1.0.0</ApplicationDisplayVersion>
</PropertyGroup>
```

---

## Step 2 — Build and Archive for iOS

iOS apps must be built and archived on a Mac.

### Via Xcode (recommended for first submission)

1. Open Terminal on the Mac and navigate to the project folder
2. Build the MAUI project for iOS in Release:

```bash
dotnet build -f net10.0-ios -c Release \
  /p:ArchiveOnBuild=true \
  /p:RuntimeIdentifier=ios-arm64
```

3. Open Xcode → **Window → Organizer**
4. Your archive should appear under Archives
5. Click **Distribute App → App Store Connect → Upload**
6. Follow the wizard — Xcode will handle signing automatically if your certificate and profile are installed

### Via command line (alternative)

```bash
dotnet publish -f net10.0-ios -c Release \
  /p:RuntimeIdentifier=ios-arm64 \
  /p:CodesignKey="Apple Distribution: Your Name (XXXXXXXXXX)" \
  /p:CodesignProvision="TradeLedger App Store"
```

The `.ipa` file appears in `bin/Release/net10.0-ios/publish/`.

Upload it using the **Transporter** app (free on the Mac App Store) or via Xcode Organizer.

---

## Step 3 — Create the App in App Store Connect

1. Go to [appstoreconnect.apple.com](https://appstoreconnect.apple.com)
2. Click **Apps → +** (New App)
3. Fill in:
   - **Platform:** iOS
   - **Name:** TradeLedger
   - **Primary Language:** English (UK)
   - **Bundle ID:** Select the one you registered (`com.yourname.tradeledger`)
   - **SKU:** `tradeledger` (internal identifier, not shown to users)
   - **User Access:** Full Access
4. Click **Create**

---

## Step 4 — Store Listing

Navigate to your app → **1.0 Prepare for Submission**.

---

### App Name (max 30 characters)
```
TradeLedger
```

### Subtitle (max 30 characters)
```
Earnings & Tax for the Self-Employed
```
The subtitle appears directly beneath the app name on the App Store — make it count.

### Promotional Text (max 170 characters — can be updated without a new app review)
```
Track your shifts, log expenses and know exactly what tax you owe. Built for tradespeople, freelancers, and anyone who works for themselves.
```

### Description (max 4000 characters)
```
TradeLedger is the earnings and tax tracker built for people who work for 
themselves — tradespeople, freelancers, tattoo artists, hair stylists, market 
traders, and anyone else who runs their own show.

Stop guessing what you owe in tax. Log your shifts and business expenses, and 
TradeLedger works out your true taxable profit and exactly what you owe — not 
just a rough estimate based on your gross income.

FREE FEATURES

• Log daily shifts by start and end time — hours calculated automatically
• Set your hourly rate and tax rate
• See your gross earnings, tax owed, and net earnings update in real time
• Choose weekly or monthly pay periods with a custom start day
• Navigate back and forward through pay periods
• See whether you earned more or less than the previous period
• Edit or delete any logged shift
• 3-period earnings history at a glance
• Daily reminder notifications so you never forget to log your hours
• Dark mode support

PREMIUM FEATURES

• Expense tracking — log Materials, Travel, Equipment and Other expenses. Your 
  taxable profit is calculated correctly as Gross minus Expenses, so your tax 
  figure is actually accurate.

• Yearly Dashboard — a full 12-month earnings chart, monthly breakdown table, 
  and insights showing your best month, monthly average and effective tax rate.

• Tax Rundown — pick any date range and get a full breakdown of gross earnings, 
  less expenses, taxable profit, and tax owed. Designed to make your 
  self-assessment straightforward.

• CSV Export — export your complete earnings and expenses history to a 
  spreadsheet-compatible file and share it anywhere.

DESIGNED FOR

Plumbers, electricians, decorators, carpenters, tattoo artists, barbers, 
hairdressers, market traders, delivery drivers, gardeners, cleaners, 
consultants, photographers — anyone who is self-employed and needs to stay on 
top of their income and tax without a complicated accounting package.

No subscription needed for the core tracker. Upgrade to Premium when you need 
the full picture.
```

### Keywords (max 100 characters, comma-separated — used for search ranking)
```
self employed,tax,earnings,hours,freelancer,tradesperson,income,expense tracker
```

Apple's search algorithm uses these keywords plus your app name and subtitle. Do not repeat words that are already in your app name or subtitle.

### Support URL
Link to your privacy policy page (see Step 6). Apple requires this and it must be a real working URL.

### Marketing URL (optional)
Leave blank for now, or point to a simple landing page later.

---

## Step 5 — Screenshots and Previews

Apple requires screenshots for specific device sizes. Unlike Google Play, there are mandatory size requirements per device category.

**Required (must have at least one of these):**

| Device | Size (points) | Notes |
|---|---|---|
| iPhone 6.9" (16 Pro Max) | 1320 × 2868 px | Required from 2024 onwards |
| iPhone 6.7" (15 Plus / 14 Plus) | 1290 × 2796 px | Can use same as 6.9" set scaled |
| iPad Pro 13" | 2064 × 2752 px | Required if targeting iPad |

**Optional but recommended:**
- iPhone 6.1" (standard/plus size)
- iPad Pro 11"

**Easiest approach for a first submission:** Capture screenshots on an iPhone 15 Pro Max simulator in Xcode — these satisfy the 6.7" requirement. Use the same screenshots for the 6.9" requirement (App Store Connect lets you reuse them).

**Screenshots to capture for TradeLedger:**
1. Home screen with the earnings card showing realistic figures
2. The daily shift log with a few entries
3. Dashboard bar chart with data populated
4. Tax Rundown results
5. Settings page showing the subscription upgrade

Use realistic but fictional data (e.g. hourly rate £32, a few months of shifts). Screenshots with £0 everywhere look abandoned.

**App Previews (short video clips — optional but valuable):**
Up to 3 × 30-second preview videos showing the app in use. These autoplay on the store listing and can significantly improve conversion. Not required for initial submission.

---

## Step 6 — Privacy Policy

Apple requires a privacy policy URL. This is the same policy you wrote for Google Play — you can use the exact same hosted page.

If you haven't hosted it yet, the GitHub Pages approach from the Google Play guide works here too:

1. Create `docs/privacy-policy.md` in your GitHub repository
2. Enable GitHub Pages (Settings → Pages → Deploy from main → /docs)
3. Your URL: `https://YOUR-USERNAME.github.io/HoursTracker/privacy-policy`

Use this URL in the **Support URL** and **Privacy Policy URL** fields in App Store Connect.

The privacy policy content from the Google Play guide is already appropriate for Apple. The only addition for iOS:

```
In-app subscriptions on iOS are processed by Apple Inc. via the App Store. 
Please refer to Apple's privacy policy for information on how subscription 
and payment data is handled: https://www.apple.com/legal/privacy/
```

---

## Step 7 — App Privacy (Nutrition Labels)

In App Store Connect, under your app → **App Privacy**, you declare what data the app collects. Apple displays this as "Nutrition Labels" on the store page.

For TradeLedger (all data stored locally, no server):

**Does this app collect data?** — Select **Yes** (you store financial data on-device)

**Data types to declare:**

| Category | Type | Used For | Linked to Identity? | Tracking? |
|---|---|---|---|---|
| Financial Info | Other financial info | App functionality | No | No |
| Usage Data | Product interaction | App functionality | No | No |

For both: set **Collected** = Yes, **Linked to identity** = No, **Used for tracking** = No.

**Important:** Apple defines "Financial Info" as any data about income, expenses, or assets. Even though it never leaves the device, you must declare that you collect it. Claiming you collect nothing when you store financial data is grounds for rejection.

---

## Step 8 — Age Rating

In App Store Connect under **App Information → Age Rating**, click **Edit** and complete the questionnaire:

- Cartoon/fantasy violence: **None**
- Realistic violence: **None**
- Sexual content: **None**
- Profanity: **None**
- Alcohol, tobacco, drugs: **None**
- Horror/fear: **None**
- Gambling: **None**
- Medical/treatment information: **None**
- In-App Purchases: **Yes**

Result will be **4+** (suitable for all ages).

---

## Step 9 — Pricing

In App Store Connect → **Pricing and Availability**:

- **Price:** Free
- **Availability:** Make available in all territories (or select specific ones)

Revenue comes from the in-app subscription, not the upfront price.

---

## Step 10 — Set Up the Subscription Product in App Store Connect

Apple subscriptions are set up as "In-App Purchases" within your app record.

### Create a Subscription Group

1. In App Store Connect, open your app → **Monetisation → Subscriptions**
2. Click **+** to create a **Subscription Group**
3. Name it: `TradeLedger Premium`
4. Click **Create**

### Create the Monthly Subscription

1. Inside the group, click **+**
2. Fill in:
   - **Reference Name:** TradeLedger Premium Monthly (internal only)
   - **Product ID:** `tradeledger_premium_monthly`
     (Use the same ID as your Google Play product — the `SubscriptionService` code from the Google Play guide uses this string and works on both platforms)
3. Click **Create**
4. Under **Subscription Duration:** Monthly
5. Under **Prices:** Click **+** and set your price tier
   - Tier 3 = approx £2.99/month (Apple sets exact prices per territory)
   - Apple auto-converts to local currencies
6. Under **Localizations (App Store Information):**
   - **Display Name:** TradeLedger Premium
   - **Description:** Full access to expense tracking, yearly dashboard, tax rundown and CSV export.
7. Under **Review Information:** Upload a screenshot of the premium settings page showing the subscription toggle (Apple reviewers need to be able to find and trigger the purchase in your app)
8. Click **Save**

### Create an Annual Subscription (optional)

Repeat the above with:
- **Product ID:** `tradeledger_premium_annual`
- **Duration:** 1 Year
- **Price:** approximately £24.99 (saves users ~30% vs monthly)

---

## Step 11 — Wire Apple Billing Into the App

The good news: the `SubscriptionService.cs` you wrote for Google Play already works on iOS. `Plugin.InAppBilling` by James Montemagno abstracts both Google Play Billing and Apple StoreKit behind the same API.

### Confirm the package is installed

```bash
dotnet add package Plugin.InAppBilling
```

### No additional iOS permissions needed

Unlike Android, iOS does not require a manifest permission for StoreKit. The framework handles everything.

### The existing SubscriptionService works as-is

```csharp
// This code from the Google Play guide works on iOS too — no changes needed
public async Task<bool> PurchasePremiumAsync()
{
    try
    {
        var billing   = CrossInAppBilling.Current;
        var connected = await billing.ConnectAsync();
        if (!connected) return false;

        var purchase = await billing.PurchaseAsync("tradeledger_premium_monthly", ItemType.Subscription);
        await billing.DisconnectAsync();

        return purchase?.State == PurchaseState.Purchased;
    }
    catch
    {
        return false;
    }
}
```

`Plugin.InAppBilling` detects the platform at runtime and routes to StoreKit on iOS or Google Play Billing on Android automatically.

### The Restore Purchases button is mandatory on iOS

Apple's App Store Review Guidelines (Guideline 3.1.1) **require** that any app with in-app purchases includes a visible "Restore Purchases" button. Apps that are missing this button are **rejected during review**.

Confirm your Settings.razor has both buttons:

```razor
<button class="btn-main" @onclick="UpgradeToPremium">Upgrade to Premium</button>
<button class="btn-secondary" @onclick="RestorePurchases">Restore Purchases</button>
```

### Test purchases without being charged

Before submitting, create a **Sandbox Tester account** in App Store Connect:

1. Go to **Users and Access → Sandbox → Testers → +**
2. Create a new Apple ID (use a throwaway email — sandbox accounts are separate from real Apple IDs)
3. On a physical iPhone, sign out of your real Apple ID in Settings → App Store
4. Sign in with the sandbox account instead
5. Run the app from Xcode and trigger a purchase — sandbox purchases are free and won't charge you

---

## Step 12 — TestFlight (Beta Testing)

TestFlight is Apple's official beta distribution platform. It is worth using before you go live — it catches issues in a real install that the simulator misses.

### Internal testing (up to 100 testers, no Apple review needed)

1. Upload a build to App Store Connect (Step 2)
2. In your app → **TestFlight → Internal Testing → +**
3. Add yourself and anyone you want to test (must have App Store Connect user accounts)
4. They receive an email and install via the TestFlight app

### External testing (up to 10,000 testers, requires brief Apple review)

1. In **TestFlight → External Testing → +**
2. Add the build and a brief "What to test" note
3. Submit for Beta App Review — usually approved within 24 hours
4. Share the public TestFlight link

External testing is worth doing for a week or two before the full production launch. Real-world installs on varied devices catch edge cases that internal testing misses.

---

## Step 13 — Submit for Review

Once the build is uploaded, the store listing is complete, and all required metadata is filled in:

1. In App Store Connect → your app → **1.0 Prepare for Submission**
2. Select the build you uploaded via Xcode / Transporter
3. Fill in:
   - **Version Release:** Manually release this version (lets you approve it before it goes live)
   - **Release Notes:** "Initial release of TradeLedger — track your hours, expenses and tax."
4. Check that every section shows a green tick
5. Click **Submit to App Review**

**First-time review typically takes 1–3 days.** Apple may ask clarification questions via the Resolution Centre in App Store Connect — check your email and the console regularly.

**Common reasons for first-time rejection:**

| Issue | Fix |
|---|---|
| Restore Purchases button missing | Add it to Settings.razor — mandatory for IAP apps |
| Privacy policy URL returns 404 | Confirm GitHub Pages is deployed and the URL is live |
| Screenshots don't show the app clearly | Re-capture with populated data, no overlays |
| In-app purchase not triggerable by reviewer | Ensure the Upgrade button is visible without needing to already be premium |
| App crashes on launch | Test a Release build on a physical device, not just the simulator |

---

## Step 14 — After Approval

When Apple approves the app:

1. If you chose **Manual Release**, go to App Store Connect and click **Release This Version**
2. The app appears on the App Store within a few hours
3. Updates after the initial release go through the same process but review is usually faster (hours, not days)

---

## Quick Reference Checklist

- [ ] Apple Developer Program membership active ($99/year)
- [ ] App ID registered at developer.apple.com with In-App Purchase capability enabled
- [ ] Distribution Certificate created and installed in Keychain on Mac
- [ ] Provisioning Profile created and downloaded
- [ ] `ApplicationId` in `.csproj` matches the registered App ID exactly
- [ ] App archived and uploaded to App Store Connect via Xcode Organizer or Transporter
- [ ] App record created in App Store Connect
- [ ] Store listing complete (name, subtitle, description, keywords, screenshots)
- [ ] Privacy policy written and hosted at a public URL
- [ ] App Privacy (Nutrition Labels) completed
- [ ] Age rating questionnaire completed
- [ ] Pricing set to Free
- [ ] Subscription group and `tradeledger_premium_monthly` product created and approved
- [ ] `Plugin.InAppBilling` added to the project
- [ ] **Restore Purchases button present and wired up** (mandatory for App Store approval)
- [ ] Sandbox tester account created and purchase flow tested
- [ ] TestFlight build distributed to at least yourself on a physical device
- [ ] Submitted for App Review

---

## Useful Links

- App Store Connect: https://appstoreconnect.apple.com
- Apple Developer Portal: https://developer.apple.com/account
- Plugin.InAppBilling docs: https://github.com/jamesmontemagno/InAppBillingPlugin
- App Store Review Guidelines: https://developer.apple.com/app-store/review/guidelines/
- TestFlight overview: https://developer.apple.com/testflight/
- Apple's Human Interface Guidelines (design reference): https://developer.apple.com/design/human-interface-guidelines/
- Transporter app (for uploading builds): search "Transporter" on the Mac App Store

---

## Notes Specific to MAUI on iOS

- **Physical device testing is essential.** The iOS Simulator does not test the StoreKit purchase flow — you must test on a real iPhone with a Sandbox account.
- **Minimum iOS version:** MAUI targets iOS 15 by default. This covers over 99% of active iPhones as of 2025.
- **iPhone-only vs Universal:** TradeLedger's current layout is phone-optimised. You can mark it as iPhone-only in App Store Connect to avoid iPad screenshot requirements. If you later optimise the layout for iPad, you can add iPad support without a new app record.
- **Notch and Dynamic Island:** The MAUI Blazor WebView handles safe areas automatically via the default MAUI shell. Verify on a physical device with a notch or Dynamic Island (iPhone 14+ Pro) that the navbar and content are not obscured.
