using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;

namespace GreenCityReporter.Services.Email;

public interface IAccountEmailSender
{
    Task SendVerificationEmailAsync(
        string recipientEmail,
        string recipientName,
        string verificationUrl,
        CancellationToken cancellationToken = default);
}

public sealed class SmtpAccountEmailSender(IOptions<EmailOptions> options) : IAccountEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendVerificationEmailAsync(
        string recipientEmail,
        string recipientName,
        string verificationUrl,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsReady)
        {
            throw new InvalidOperationException("The transactional email service is not configured.");
        }

        var safeName = HtmlEncoder.Default.Encode(recipientName);
        var safeUrl = HtmlEncoder.Default.Encode(verificationUrl);
        var displayName = string.IsNullOrWhiteSpace(recipientName) ? "there" : safeName;

        var textBody = $"""
            Green City Reporter

            Hello {recipientName},

            Thank you for creating an account.

            Please verify your email address to activate your account:
            {verificationUrl}

            If you did not create this account, you can ignore this message.
            """;

        var htmlBody = $$"""
            <!doctype html>
            <html lang="en">
            <body style="margin:0;background:#f4f7f5;font-family:Arial,sans-serif;color:#24342b">
              <div style="max-width:600px;margin:0 auto;padding:32px 16px">
                <div style="background:#ffffff;border-radius:12px;padding:32px;border:1px solid #dce8e0">
                  <h1 style="margin:0 0 24px;color:#198754;font-size:26px">Green City Reporter</h1>
                  <p>Hello {{displayName}},</p>
                  <p>Thank you for creating an account.</p>
                  <p>Please verify your email address to activate your account.</p>
                  <p style="margin:28px 0">
                    <a href="{{safeUrl}}" style="display:inline-block;background:#198754;color:#ffffff;text-decoration:none;font-weight:bold;padding:13px 22px;border-radius:7px">Verify Email Address</a>
                  </p>
                  <p style="font-size:14px;color:#5c6f63">If the button does not work, copy and paste this verification link into your browser:</p>
                  <p style="font-size:13px;word-break:break-all"><a href="{{safeUrl}}" style="color:#146c43">{{safeUrl}}</a></p>
                  <p style="font-size:13px;color:#6c757d;margin-top:28px">If you did not create this account, you can ignore this message.</p>
                </div>
              </div>
            </body>
            </html>
            """;

        using var message = new MailMessage
        {
            From = new MailAddress(_options.SenderEmail, _options.SenderName, Encoding.UTF8),
            Subject = "Verify your Green City Reporter account",
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8
        };
        message.To.Add(new MailAddress(recipientEmail));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(textBody, Encoding.UTF8, "text/plain"));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, "text/html"));

        using var smtp = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.EnableSsl,
            UseDefaultCredentials = false,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 30_000
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            smtp.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        await smtp.SendMailAsync(message, cancellationToken);
    }
}
