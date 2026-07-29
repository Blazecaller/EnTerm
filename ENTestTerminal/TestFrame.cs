using System.Globalization;

namespace ENTestTerminal;

public sealed class TestFrame
{
    public double DigitalTemperature { get; init; }
    public double Sht20Temperature { get; init; }
    public double Sht20Humidity { get; init; }
    public double BatteryLevel { get; init; }
    public double WindSpeed { get; init; }
    public double WindSpeedSetpoint { get; init; }
    public double AccelTotalG { get; init; }
    public double AccelThreshold { get; init; }
    public int GlobalStatus { get; init; }
    public int WindZeroSpeedPwm1 { get; init; }
    public int WindZeroSpeedPwm2 { get; init; }
    public int WindZeroSpeedPwm3 { get; init; }
    public int WindRoomTempPwm1 { get; init; }
    public int WindRoomTempPwm3 { get; init; }
    public int WindPwmOutput { get; init; }
    public int DeviceSleepInterval { get; init; }
    public int Version { get; init; }
    public int SensorLinkStatus { get; init; }

    public DateTime ReceivedAt { get; init; } = DateTime.Now;

    private const int FieldCount = 18;

    public static bool TryParse(string body, out TestFrame? frame)
    {
        frame = null;
        var parts = body.Split(',');
        if (parts.Length != FieldCount) return false;

        var ci = CultureInfo.InvariantCulture;
        try
        {
            double F(int i) => double.Parse(parts[i].Trim(), NumberStyles.Float, ci);
            int I(int i) => int.Parse(parts[i].Trim(), NumberStyles.Integer, ci);
            int Hex(int i)
            {
                var s = parts[i].Trim();
                if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
                return int.Parse(s, NumberStyles.HexNumber, ci);
            }

            frame = new TestFrame
            {
                DigitalTemperature = F(0),
                Sht20Temperature = F(1),
                Sht20Humidity = F(2),
                BatteryLevel = F(3),
                WindSpeed = F(4),
                WindSpeedSetpoint = F(5),
                AccelTotalG = F(6),
                AccelThreshold = F(7),
                GlobalStatus = I(8),
                WindZeroSpeedPwm1 = I(9),
                WindZeroSpeedPwm2 = I(10),
                WindZeroSpeedPwm3 = I(11),
                WindRoomTempPwm1 = I(12),
                WindRoomTempPwm3 = I(13),
                WindPwmOutput = I(14),
                DeviceSleepInterval = I(15),
                Version = I(16),
                SensorLinkStatus = Hex(17),
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
