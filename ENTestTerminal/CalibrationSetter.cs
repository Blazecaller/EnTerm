using System.Globalization;
using System.Text.RegularExpressions;

namespace ENTestTerminal;

public enum CalibInput
{
    Integer,
    Float,
    None,
}

public sealed record CalibrationSetter(
    string Command,
    CalibInput Input,
    Func<double, bool> InRange,
    string RangeText,
    bool Temporary = false)
{
    // Mirrors the firmware parser: optional sign, up to 6 integer digits, optional '.' and up to 6 decimals, no exponent.
    private static readonly Regex FloatFormat = new(@"^[+-]?\d{1,6}(\.\d{1,6})?$", RegexOptions.Compiled);
    private static readonly Regex IntegerFormat = new(@"^\d{1,6}$", RegexOptions.Compiled);

    public bool TryNormalize(string input, out string value)
    {
        value = input.Trim().Replace(',', '.');
        var format = Input == CalibInput.Integer ? IntegerFormat : FloatFormat;
        if (!format.IsMatch(value)) return false;
        return InRange(double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture));
    }

    public string BuildCommand(string? value) => Input == CalibInput.None ? Command : $"{Command}={value}";
}
