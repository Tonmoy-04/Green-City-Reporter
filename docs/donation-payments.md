# Donation payments

The donation module uses SSLCommerz hosted checkout in sandbox mode by default. The donor chooses an amount, selects Card/bKash/Nagad/Rocket, enters their name and optional contact information, and completes payment on the provider's secure page. The provider collects cardholder name, card number, expiry and CVV, or wallet authentication. These fields never pass through the Green City Reporter server or database.

## Local project demo (no gateway account)

Development configuration now enables `Donations:Gateway:DemoMode`. Run `dotnet run --project GreenCityReporter`, open `/Donation`, select an amount and Card/bKash/Nagad/Rocket, and continue to demo payment. The local demo screen provides editable sample fields with browser validation and buttons to simulate success, failure or cancellation. Successful simulations show a printable demo receipt. Sample payment inputs have no form names and never reach the server. For cards use 4111 1111 1111 1111, a future MM/YY expiry and a 3–4 digit demo CVV; for wallets use a made-up mobile number and demo code 123456. Failure and cancellation do not require completed fields.

Demo records use `Provider = Demo` and `IsSandbox = true`. They never call SSLCommerz, send receipt emails or modify real gateway records. Every demo screen and receipt states that no money is collected. A completed simulation cannot be overwritten by submitting another outcome; start a new donation to demonstrate another outcome. Guest donations are accessible through their receipt links; signed-in users also see them in donation history.

No merchant credentials, public tunnel or SMTP setup is required. The default base configuration keeps demo mode off; Development enables it for this project. Set `Donations:Gateway:DemoMode` to false when returning to gateway testing.
## Sandbox setup

1. Register a sandbox store with SSLCommerz and obtain the sandbox Store ID/password. Keep the password in .NET User Secrets or environment variables, never in committed JSON. The project now has a UserSecretsId.
2. Configure these keys under `Donations:Gateway`:

| Key | Value |
| --- | --- |
| Enabled | `true` once the store is configured |
| Sandbox | `true` |
| StoreId / StorePassword | Your sandbox credentials |
| PublicBaseUrl | Public HTTPS origin, including an application path prefix if used; no query or fragment |
| ContactEmail / ContactPhone | The organization's real contact details, used for required gateway customer metadata only when the donor omits their own |
| Channels:Card | `visacard,mastercard,amexcard` |
| Channels:bKash | `bkash` |
| Channels:Nagad | `nagad`, or the exact channel key confirmed for your store |
| Channels:Rocket | `dbblmobilebanking` |

Set a channel to an empty string to disable it. Availability depends on your merchant store. Wallet initialization rejects a method missing from the provider's returned enabled gateway list instead of silently using a different wallet. Confirm Nagad's exact key and sandbox availability with SSLCommerz if your store does not expose it.

Environment variables use double underscores, for example `Donations__Gateway__StorePassword`. For local secrets:

```powershell
dotnet user-secrets set "Donations:Gateway:StoreId" "YOUR_SANDBOX_STORE_ID" --project GreenCityReporter
dotnet user-secrets set "Donations:Gateway:StorePassword" "YOUR_SANDBOX_STORE_PASSWORD" --project GreenCityReporter
```

Keep localhost as the development listening address and expose it using a public HTTPS development tunnel for gateway callbacks. Set PublicBaseUrl to that tunnel origin. No tunnel or public deployment is created automatically.

3. Apply migrations, then restart:

```powershell
dotnet ef database update --project GreenCityReporter -- --environment Development
dotnet run --project GreenCityReporter
```

4. In the merchant panel, enable HTTP IPN and register `PUBLIC_BASE_URL/Donation/Ipn`. The session uses `PUBLIC_BASE_URL/Donation/Return?token=...` for success, failure and cancellation. Return and IPN accept provider POST callbacks without requiring a user session.
5. Complete provider sandbox tests for success, failure, cancellation, wallet OTP, duplicate IPNs, delayed IPNs, and lost browser returns. Test-card details are available in the provider's documentation. Sandbox receipts and emails are explicitly marked as tests.

## Confirmation and receipts

A callback status is never treated as proof of payment. The server calls the order validation API using its own credentials and checks the transaction reference, exact BDT amount, currency, validation ID and bank transaction ID. If the provider returns a store ID, it must match. High-risk or missing-risk results remain UnderReview and require an administrator's customer risk review before confirmation.

Failure/cancel callbacks without a validation ID trigger a server-side transaction query. If no authoritative result exists, the payment stays pending rather than claiming success or failure. A background worker rechecks up to five unresolved attempts every 30 seconds, with a five-minute check interval, for seven days. For older unresolved attempts, use the payment-status page's manual check or merchant support. Verified success may replace an earlier failure, but failures cannot overwrite confirmed or reviewed donations.

Signed-in donors see their recent history. Guests can also donate. Receipts are protected by random 256-bit bearer tokens; keep receipt links private. Receipt responses disable caching, indexing and referrer disclosure. Confirmed receipts can be printed or saved as PDF through the browser. Status pages for unconfirmed transactions are not receipts.

Unique checkout keys make repeated form submissions reuse an existing attempt; a key cannot change its donor, amount or method. Unique bank transaction IDs prevent one payment confirming multiple donation records. Existing manual donations keep their original status values and remain reviewable at `/Donation/Manage`. Administrators cannot manually mark an unverified online checkout as paid.

## Email delivery

Configure `Donations:Email:Enabled`, `Host`, `Port` (usually 587), `UseStartTls` (`true`), `FromAddress`, `Username`, and `Password`. Keep SMTP credentials in secrets or environment variables. Use an authenticated SMTP relay with a verified sender/domain. STARTTLS and certificate validation are required in production; implicit TLS on port 465 is not supported by this sender.

Successful gateway confirmation makes the stored optional email eligible for delivery in the same database update. The worker leases eligible records, uses a 30-second delivery timeout, retries failures with increasing delays up to an hour, and records successful delivery. Donation confirmation does not depend on SMTP availability. No email is sent for pending, failed or rejected payments. With email disabled, addresses stay queued until it is enabled and the UI offers the web receipt.

SMTP provides at-least-once delivery: a process crash after the relay accepts a message but before the database records delivery can produce a duplicate receipt email. Leases prevent ordinary concurrent sends. Production deployments should monitor pending receipts, relay bounces and delivery failures.

## Live deployment

Live transactions are not enabled by default. After merchant onboarding and end-to-end sandbox approval, supply live credentials, set Sandbox to false, use your real public HTTPS origin, and register the live IPN URL. Store IDs and sandbox/live mode are captured per donation so old attempts are not verified with another store. Reconcile pending attempts before switching stores or environments.

Deploy with HTTPS, a restricted AllowedHosts configuration, shared persisted data-protection keys for multiple instances, private database access, and a managed secrets store. Checkout rate limits are process-local (10 attempts per minute per authenticated user or remote IP); use trusted proxy configuration and a shared/WAF limiter for multi-instance deployments. Restrict access to transaction logs and receipt URLs. The gateway HttpClient disables URL logging because the provider validation API includes credentials in the query string. Do not enable sensitive EF logging or log callback bodies. Monitor gateway verification warnings and unresolved transactions.

Gateway merchant credentials, public HTTPS callbacks, enabled payment channels and SMTP configuration are required for an operational deployment. They cannot be replaced by a simulated successful payment. No real or sandbox provider transaction was completed during the isolated automated checks.

## Verification

```powershell
dotnet run --project GreenCityReporter.Checks
```

The checks use an in-memory database and fake HTTP/SMTP adapters. They verify amount/reference/currency/store binding, redirect host restrictions, wallet availability, idempotency, duplicate callbacks, late failures, risk holds, receipt access, admin restrictions, unique bank IDs, and email retries. They make no real payments or email deliveries.

References: [SSLCommerz V4 integration](https://developer.sslcommerz.com/doc/v4/index.html), [supported payment channels](https://sslcommerz.com/pricing/).
