# SSLCommerz payment integration

Green City Reporter uses SSLCommerz Hosted Checkout for real donations. Card numbers, CVV values, wallet PINs and OTPs are entered only on SSLCommerz. The application creates a pending donation, redirects to the provider, and confirms payment only after server-side validation.

## Render environment variables

Set these values in the Render service environment:

```text
SSLCOMMERZ__STOREID=YOUR_STORE_ID
SSLCOMMERZ__STOREPASSWORD=YOUR_STORE_PASSWORD
SSLCOMMERZ__ISSANDBOX=true
SSLCOMMERZ__CONTACTEMAIL=payments@example.com
SSLCOMMERZ__CONTACTPHONE=+8801XXXXXXXXX
APP_BASE_URL=https://green-city-reporter.onrender.com
```

`CONTACTEMAIL` and `CONTACTPHONE` must be real organization contact details. SSLCommerz requires customer contact metadata; these values are used only when a donor leaves an optional contact field empty. Do not commit credentials to JSON, source code or frontend JavaScript.

The existing `Donations__Gateway__...` variables remain supported for older deployments. When the `SSLCOMMERZ__...` variables contain credentials, the real gateway is enabled automatically and demo mode is disabled.

For Render PostgreSQL also configure `Database__Provider=PostgreSQL` and `ConnectionStrings__DefaultConnection` with the Render connection string. The application applies provider migrations during startup before seeding reference data.

## Sandbox setup

1. Register at the [SSLCommerz sandbox portal](https://developer.sslcommerz.com/registration/) and obtain a sandbox Store ID and Store Password.
2. Add the Render variables above with `SSLCOMMERZ__ISSANDBOX=true`.
3. In the SSLCommerz merchant panel, enable HTTP IPN and set the listener to `https://green-city-reporter.onrender.com/Donation/Ipn`.
4. Deploy, open `/Donation`, choose an amount and method, and continue to SSLCommerz Hosted Checkout.
5. Verify the resulting receipt and donation record. A browser return alone never confirms a payment.

Checkout sessions use these public HTTPS callbacks: `/Donation/Success`, `/Donation/Fail`, `/Donation/Cancel`, and `/Donation/Ipn`. Success and IPN validate `val_id` through the Order Validation API and compare the transaction ID, exact amount, BDT currency, validation ID, store and bank transaction. Failure and cancellation use the transaction query API when no validation ID is supplied. Repeated IPNs are idempotent.

## Switching to live

After sandbox testing and merchant onboarding, replace the sandbox credentials and set `SSLCOMMERZ__ISSANDBOX=false`. Keep the HTTPS application URL and configure the live merchant panel IPN URL. The application never changes to live mode automatically.

## Local testing

Development keeps the clearly labelled local demo available. To test the real sandbox locally, disable `Donations:Gateway:DemoMode`, store credentials with .NET User Secrets, and set `APP_BASE_URL` to a public HTTPS tunnel. SSLCommerz cannot call a localhost IPN URL.

Run isolated checks with:

```powershell
dotnet run --project GreenCityReporter.Checks/GreenCityReporter.Checks.csproj
```

These checks use fake gateway responses and make no payment. A real success can only be verified with a configured SSLCommerz sandbox or live store.

See [donation-payments.md](donation-payments.md) for transaction states, retries, receipts and email delivery details.
