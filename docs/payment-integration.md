# Payment Integration

Green City Reporter currently uses a simulated/demo payment system for academic demonstration purposes. It does not process real financial transactions.

## Demo flow

Development sets `Donations:Gateway:DemoMode` to `true`. The donor selects Card, bKash, Nagad, or Rocket, submits ordinary donor details, and is taken to a local demo page. The page can simulate success, failure, or cancellation. Demo records use `Provider = Demo`, never call an external gateway, and never request or store card numbers, CVV values, wallet PINs, or OTPs.

The server validates amount, payment method, checkout-token ownership, and idempotency. A pending demo attempt can be completed once; later submissions cannot overwrite the saved outcome. Receipt tokens are random and are not indexed by search engines.

## Optional gateway adapter

The repository contains `SslCommerzGateway` and reconciliation code for future sandbox testing. It is disabled by default and is not needed for the academic demo. Enabling it requires merchant credentials, real organization contact metadata, a public HTTPS callback origin, and explicit `Enabled` configuration. Secrets must be supplied through User Secrets or environment variables, never committed JSON.

The adapter uses hosted checkout and does not receive card or wallet authentication fields. Callback data is verified server-side against the provider before confirmation. Unverified, failed, cancelled, and risk-held states remain distinct. IPN and return callbacks are designed to be retryable.

## Verification

Run the isolated payment checks with:

```powershell
dotnet run --project GreenCityReporter.Checks/GreenCityReporter.Checks.csproj
```

The checks use fake HTTP and SMTP adapters and make no real payments or email deliveries. See `docs/donation-payments.md` for detailed optional sandbox, receipt, retry, and deployment notes.
