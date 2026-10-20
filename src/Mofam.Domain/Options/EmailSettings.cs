namespace Mofam.Domain.Options;
public sealed class EmailSettings
{
    public const string SectionName = "EmailSettings";

    // true = send with SendGrid, false = send with SMTP.
    public bool UseSendGrid { get; set; }

    public string SenderAddress { get; set; } = string.Empty;

    public string SenderName { get; set; } = string.Empty;

    public SmtpOptions Smtp { get; set; } = new();

    public SendGridOptions SendGrid { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public sealed class SendGridOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string BaseAddress { get; set; } = "https://api.sendgrid.com/";
}
