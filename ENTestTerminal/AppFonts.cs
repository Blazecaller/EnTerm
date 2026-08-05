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

    // === 2. Serial number banner — thin bar just under the menu strip ===
    public static readonly Font SerialNumberBanner = new(UiFamily, 9f, FontStyle.Bold);

    // === 3. Connection row & Commands row (the two toolbar rows) ===
    /// <summary>Plain labels and buttons in the Connection/Commands rows.</summary>
    public static readonly Font ToolbarRowText = new(UiFamily, 14f);
    /// <summary>Row titles and the Connect/Disconnect button.</summary>
    public static readonly Font ToolbarRowTitle = new(UiFamily, 14f, FontStyle.Bold);
    /// <summary>Drop-down lists in the Connection/Commands rows.</summary>
    public static readonly Font ToolbarRowDropdown = new(UiFamily, 13f);
    /// <summary>The "?" help icon on the Connection row.</summary>
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

    // === 6. Terminal / log panel ===
    public static readonly Font TerminalLog = new(MonoFamily, 9.5f);

    // === 7. Form-wide fallback ===
    /// <summary>MainForm's own default Font; every visible control above overrides it.</summary>
    public static readonly Font FormDefault = new(UiFamily, 9f);
}
