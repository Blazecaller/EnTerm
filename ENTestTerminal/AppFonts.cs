namespace ENTestTerminal;

/// <summary>
/// Every font used in the UI, grouped in the same top-to-bottom order the
/// controls appear on screen. Change a size/style here — don't add a
/// `new Font(...)` call anywhere else.
/// </summary>
public static class AppFonts
{
    private const string UiFamily = "Segoe UI";
    private const string MonoFamily = "Consolas";

    // === 1. Menu strip — the very top bar ===
    public static readonly Font MenuStrip = new(UiFamily, 14f);

    // === 2. Mode bar (Calibration/Standard button + serial number) and the calibration page button row ===
    /// <summary>Calibration page buttons (Set Zero Wind, Advanced).</summary>
    public static readonly Font ToolbarRowText = new(UiFamily, 14f);
    /// <summary>The Calibration/Standard mode button and the serial number next to it.</summary>
    public static readonly Font ToolbarRowTitle = new(UiFamily, 14f, FontStyle.Bold);

    // === 3. Connection strip (Port, Baud, Parity, ... Connect) ===
    /// <summary>Labels, drop-downs and the Refresh button in the connection strip.</summary>
    public static readonly Font ConnectionStripText = new(UiFamily, 11f);
    /// <summary>The "Connection" title and the Connect/Disconnect button.</summary>
    public static readonly Font ConnectionStripTitle = new(UiFamily, 12f, FontStyle.Bold);
    /// <summary>The "?" help icon next to the Connect button.</summary>
    public static readonly Font ConnectionHelpIcon = new(UiFamily, 12f, FontStyle.Bold);
    /// <summary>Text inside the connection-status tooltip shown when hovering the "?" icon.</summary>
    public static readonly Font ConnectionHelpTooltip = new(UiFamily, 11f);

    // === 4. AT Commands row — hidden until "Advanced" is expanded ===
    public static readonly Font AtCommandsRowTitle = new(UiFamily, 9f, FontStyle.Bold);

    // === 5. Dashboard tiles (the main content area) ===
    /// <summary>Colored header bar on each dashboard group card.</summary>
    public static readonly Font DashboardCardHeader = new(UiFamily, 12f, FontStyle.Bold);
    /// <summary>Small caption above a tile's value.</summary>
    public static readonly Font DashboardTileCaption = new(UiFamily, 11f);
    /// <summary>The large text inside a tile — shared by sensor readings (e.g. "26.98 °C") and status words (e.g. "HIGH"), since both go through the same value label.</summary>
    public static readonly Font DashboardTileValue = new(MonoFamily, 13f, FontStyle.Bold);

    // === 6. Sensor configuration / Device settings pop-ups ===
    public static readonly Font SettingsDialogText = new(UiFamily, 11f);

    // === 7. Terminal / log panel ===
    public static readonly Font TerminalLog = new(MonoFamily, 9.5f);

    // === 8. Form-wide fallback ===
    /// <summary>MainForm's own default Font; every visible control above overrides it.</summary>
    public static readonly Font FormDefault = new(UiFamily, 9f);
}
