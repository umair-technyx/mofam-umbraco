namespace Mofam.Domain.Constants;

public static class ExportConstants
{
    public static readonly string[] ContactHeaders = ["Id", "Date (UAE)", "Subject", "First name", "Last name", "Email", "Phone", "Message", "Language", "Email sent"];
    public const int ContactPhoneColumn = 6;
    public static readonly string[] NewsletterHeaders = ["Id", "Date (UAE)", "Email", "Language", "Email sent"];
    public const string DateFormat = "yyyy-MM-dd HH:mm:ss";
    public const string Yes = "Yes";
    public const string No = "No";
}
