using System.Globalization;

namespace ENTestTerminal;

public sealed class CalibrationFrame
{
    public double Sht20Temperature { get; init; }
    public double WindSpeed { get; init; }
    public int HeaterPwmOutput { get; init; }
    public double HeaterTemperature { get; init; }
    public int WindZeroSpeedPwm1 { get; init; }
    public int WindZeroSpeedPwm2 { get; init; }
    public int WindZeroSpeedPwm3 { get; init; }
    public int WindRoomTempPwm1 { get; init; }
    public int WindRoomTempPwm3 { get; init; }
    public double CalibrationSpeed1 { get; init; }
    public double CalibrationSpeed2 { get; init; }
    public double CalibrationSpeed3 { get; init; }
    public int NoWindPwm { get; init; }
    public double HeaterSetpoint { get; init; }
    public double Kp { get; init; }
    public double Ki { get; init; }
    public double Kd { get; init; }

    public const string Tag = "C";
    private const int FieldCount = 17;

    public static bool IsCalibrationBody(string body) => body.StartsWith(Tag + ",", StringComparison.Ordinal);

    public static bool TryParse(string body, out CalibrationFrame? frame)
    {
        frame = null;
        if (!IsCalibrationBody(body)) return false;
        var parts = body.Split(',');
        if (parts.Length != FieldCount + 1) return false;

        var ci = CultureInfo.InvariantCulture;
        try
        {
            double F(int i) => double.Parse(parts[i + 1].Trim(), NumberStyles.Float, ci);
            int I(int i) => int.Parse(parts[i + 1].Trim(), NumberStyles.Integer, ci);

            frame = new CalibrationFrame
            {
                Sht20Temperature = F(0),
                WindSpeed = F(1),
                HeaterPwmOutput = I(2),
                HeaterTemperature = F(3),
                WindZeroSpeedPwm1 = I(4),
                WindZeroSpeedPwm2 = I(5),
                WindZeroSpeedPwm3 = I(6),
                WindRoomTempPwm1 = I(7),
                WindRoomTempPwm3 = I(8),
                CalibrationSpeed1 = F(9),
                CalibrationSpeed2 = F(10),
                CalibrationSpeed3 = F(11),
                NoWindPwm = I(12),
                HeaterSetpoint = F(13),
                Kp = F(14),
                Ki = F(15),
                Kd = F(16),
            };
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
