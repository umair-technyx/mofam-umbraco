using System.ComponentModel.DataAnnotations;
using Mofam.Domain.Constants;

namespace Mofam.Domain.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class SafeTextAttribute : ValidationAttribute
{
    private const char LineFeed = (char)10;
    private const char CarriageReturn = (char)13;
    private const char Tab = (char)9;

    public SafeTextAttribute() : base(GeneralConstants.FieldInvalidCharactersMessage)
    {
    }

    public bool AllowLineBreaks { get; set; }

    public override bool IsValid(object? value)
    {
        if (value is not string text) return true;

        foreach (var c in text)
        {
            if (AllowLineBreaks && (c == LineFeed || c == CarriageReturn || c == Tab)) continue;

            if (char.IsControl(c)) return false;
            if (c is (char)0x2028 or (char)0x2029) return false;
            if (c is >= (char)0x202A and <= (char)0x202E) return false;
            if (c is >= (char)0x2066 and <= (char)0x2069) return false;
        }

        return true;
    }
}
