namespace GreenCityReporter.Services.Email;

public sealed class EmailOptions
{
    public string SmtpHost { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 587;

    public string SenderName { get; set; } = "Green City Reporter";

    public string SenderEmail { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool EnableSsl { get; set; } = true;

    /// <summary>
    /// Optional externally reachable application URL, for example
    /// https://green-city-reporter.example. When omitted outside Production,
    /// confirmation links use the current request's scheme and host.
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    public bool IsReady =>
        !string.IsNullOrWhiteSpace(SmtpHost) &&
        SmtpPort is >= 1 and <= 65535 &&
        !string.IsNullOrWhiteSpace(SenderEmail) &&
        !string.IsNullOrWhiteSpace(SenderName);
}
