# TradeLedger — Accounts, Cloud Sync & Subscription Validation

This document covers the planned move from a purely local-only app to one with optional
accounts, cross-device sync, and server-verified premium status. **Nothing here is built
yet** — this is the design to implement against. See `docs/CONTEXT.md` for the app's
current (already-built) state.

---

## Why this is needed

TradeLedger today stores everything in a local SQLite database with no server component.
Two problems follow from that once real money is involved:

1. **Data loss on device replacement.** A tradesperson who loses or upgrades their phone
   loses their entire earnings/tax history.
2. **Subscriptions don't travel.** `Plugin.InAppBilling` purchases are tied to the store
   account on the device that bought them (`docs/GOOGLE_PLAY_PUBLISHING.md`,
   `docs/APPLE_APP_STORE_PUBLISHING.md`). `CheckPremiumStatusAsync()` only ever checks the
   local device's receipts — it has no way to know a user is a paying subscriber on a
   second device, or after a reinstall, unless they're signed into an account.

Both are solved by adding an optional account, backed by Supabase, that the local SQLite
database syncs to.

---

## Why Supabase (not Azure)

Supabase bundles Postgres, Auth, Row Level Security, and Edge Functions (serverless
TypeScript/Deno functions) in a single project, which is everything this feature needs.
Running Azure alongside it would mean maintaining two overlapping backends for no
benefit — so Azure is not part of this design. If a future need arises that Supabase
genuinely can't cover, address it then rather than provisioning for it now.

---

## Billing stays on native store billing, not Stripe

Apple requires native In-App Purchase for digital subscriptions consumed in-app
(App Store Review Guideline 3.1.1); a Stripe checkout for the premium subscription risks
rejection or removal. Both platforms already have a working native billing design
(`Plugin.InAppBilling`, wired identically on Android and iOS) — that doesn't change.
Supabase's role is **not** to process payments, only to hold a server-side record of the
entitlement that native billing already granted, and to be the target of the pricing
recommendation below.

---

## Accounts: sign-up and login

Two new pages, styled like `Onboarding.razor` (bare `EmptyLayout`, no tab bar):

- **`/signup`** — email + password, create account via Supabase Auth
- **`/login`** — email + password, sign in via Supabase Auth

Design decisions:

- **Signing in is optional, never forced.** The app must stay fully usable with
  local-only storage — tradespeople need to be able to open the app and log a shift
  immediately, on the first run, without creating an account. Add a "Skip — use without
  an account" link on both screens, and surface sign-in/sign-up from Settings for anyone
  who wants sync later.
- **Email/password only for v1.** Don't add Google or other third-party social sign-in
  without also adding Sign in with Apple — Apple requires it (Guideline 4.8) as soon as
  any third-party login is offered on iOS. Skipping social login entirely for v1 avoids
  that requirement and keeps the surface small; it can be added later as a pair if
  conversion data justifies the extra work.
- **Settings gains an Account section**: shows the signed-in email, a Sign Out button,
  and a Delete Account action (must cascade-delete the user's rows in Supabase per GDPR,
  since UK users are the primary market).

---

## Data model: local SQLite stays primary, Postgres mirrors it

The local database remains the source of truth for reads and writes while offline —
this doesn't become an always-online app. Each local table (`HoursEntries`, `Expenses`,
`Settings`) gains:

| Column | Purpose |
|---|---|
| `UserId` | Supabase auth user id; `null` until the user signs in |
| `RemoteId` | Client-generated UUID, set at row creation — gives every row a stable identity before it's ever synced |
| `UpdatedAt` | Timestamp bumped on every local write; drives conflict resolution |
| `IsDeleted` | Soft-delete flag so deletions can be synced instead of silently vanishing remotely |

Supabase Postgres gets matching tables (`hours_entries`, `expenses`, `settings`) plus a
`profiles` table keyed by `auth.uid()` holding subscription state (see below). Every
table has Row Level Security enabled with a policy of `user_id = auth.uid()` — a user can
only ever read or write their own rows; there is no server-side trust in client requests.

---

## Sync strategy

Offline-first, last-write-wins:

- **Trigger points:** on app foreground, after every mutating action (debounced ~2s so a
  burst of edits doesn't fire a request per keystroke), and on a periodic timer while the
  app is open and online.
- **Push:** rows with `UpdatedAt` newer than `last_synced_at` are upserted to Supabase via
  PostgREST, keyed by `RemoteId` so retries are idempotent.
- **Pull:** rows changed remotely since `last_synced_at` are fetched and upserted locally.
- **Conflicts resolved by `UpdatedAt`** — acceptable here because TradeLedger data is
  single-user, not collaboratively edited; there's no case where two people edit the same
  shift concurrently.
- **Deletes** are soft (`IsDeleted = true`, `UpdatedAt` bumped) so they propagate through
  the same push/pull path as any other change, then hard-purged locally once confirmed
  synced.

---

## Subscription validation (server-side source of truth)

Client-only entitlement checks (today's `CheckPremiumStatusAsync`) can't be trusted across
devices and are also spoofable. Fix:

1. After `Plugin.InAppBilling.PurchaseAsync` succeeds, send the purchase token / receipt
   to a Supabase Edge Function, e.g. `validate-subscription`.
2. That function verifies the purchase server-side instead of trusting the client:
   - **Android:** calls the Google Play Developer API
     (`purchases.subscriptions.get`) using a service account.
   - **iOS:** calls Apple's App Store Server API to verify the transaction.
3. On success, it writes `is_premium`, `subscription_expires_at`, and
   `subscription_platform` onto the user's `profiles` row.
4. To keep that record accurate without the app being open (renewals, cancellations,
   refunds, billing grace periods), point Google Play **Real-time Developer
   Notifications** (Pub/Sub push) and Apple **App Store Server Notifications V2** at the
   same Edge Function.
5. The local `IsPremium` flag becomes a synced mirror of `profiles.is_premium` (pulled
   down like any other row), not the source of truth. Keep the existing local
   StoreKit/Play Billing check too, as an offline fallback, so premium features don't
   flicker off mid-flight when there's no connection.

This is additive to the existing billing docs, not a replacement — the purchase flow in
`docs/GOOGLE_PLAY_PUBLISHING.md` (Step 10) and `docs/APPLE_APP_STORE_PUBLISHING.md`
(Step 11) is unchanged; only what happens *after* a successful purchase gains a
server-side confirmation step.

---

## Pricing recommendation

Comparable UK self-employed tracking apps: Everlance (~£6.99/mo), Hurdlr (~£8/mo
equivalent), QuickBooks Self-Employed (~£8–15/mo) — but all of those bundle mileage
tracking, invoicing, or filing that TradeLedger doesn't do, so pricing under them fits a
narrower, single-purpose tool.

- **£4.99/month** or **£39.99/year** (~£3.33/mo, a ~33% discount — a standard
  indie-app annual anchor that rewards commitment without undercutting perceived value)
- A **7-day free trial** on the subscription product is worth adding to lower purchase
  friction
- Both price and trial length are configured entirely in Play Console (Step 9 of
  `docs/GOOGLE_PLAY_PUBLISHING.md`) and App Store Connect (Step 10 of
  `docs/APPLE_APP_STORE_PUBLISHING.md`) — no app code needed to change them later

---

## What's not yet built

- Supabase project not yet created
- `/login`, `/signup` pages don't exist
- `AuthService`, `SyncService` not implemented
- Local schema doesn't yet have `UserId` / `RemoteId` / `UpdatedAt` / `IsDeleted` columns
- Supabase Postgres schema (`profiles`, `hours_entries`, `expenses`, `settings`) and RLS
  policies not yet created
- `validate-subscription` Edge Function not implemented
- Google Play RTDN / Apple App Store Server Notifications V2 not wired up
- Account deletion (GDPR cascade delete) not implemented
