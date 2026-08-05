namespace ENTestTerminal;

/// <summary>
/// Gives every ToolStripButton a thin cyan frame and a pale cyan fill, so
/// buttons read as clickable at a glance next to the plain labels and
/// drop-downs sharing the same row. Only ToolStripButton is affected —
/// labels/combo boxes keep their normal system look.
/// </summary>
public sealed class ButtonFrameRenderer : ToolStripSystemRenderer
{
    private static readonly Color BorderColor = Color.FromArgb(0, 151, 167);
    private static readonly Color FillNormal = Color.FromArgb(224, 247, 250);
    private static readonly Color FillHover = Color.FromArgb(178, 235, 242);
    private static readonly Color FillPressed = Color.FromArgb(128, 222, 234);

    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
    {
        var bounds = new Rectangle(Point.Empty, e.Item.Size);
        Color fill = e.Item.Pressed ? FillPressed : e.Item.Selected ? FillHover : FillNormal;

        using (var brush = new SolidBrush(fill))
        {
            e.Graphics.FillRectangle(brush, bounds);
        }

        var borderRect = new Rectangle(0, 0, bounds.Width - 1, bounds.Height - 1);
        using var pen = new Pen(BorderColor);
        e.Graphics.DrawRectangle(pen, borderRect);
    }
}
