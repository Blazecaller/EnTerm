namespace ENTestTerminal;

/// <summary>OK-only replacement for <see cref="MessageBox"/>, which always centers on the screen instead of the owner window.</summary>
public static class CenteredMessage
{
    public static void Show(IWin32Window owner, string text, string caption, MessageBoxButtons _, MessageBoxIcon icon)
    {
        using var dialog = new Form
        {
            Text = caption,
            Font = AppFonts.FormDefault,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12),
        };

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
        };

        Icon? systemIcon = icon switch
        {
            MessageBoxIcon.Error => SystemIcons.Error,
            MessageBoxIcon.Warning => SystemIcons.Warning,
            MessageBoxIcon.Information => SystemIcons.Information,
            _ => null,
        };
        if (systemIcon is not null)
        {
            layout.Controls.Add(new PictureBox
            {
                Image = systemIcon.ToBitmap(),
                SizeMode = PictureBoxSizeMode.AutoSize,
                Margin = new Padding(0, 0, 12, 0),
            }, 0, 0);
        }

        layout.Controls.Add(new Label
        {
            Text = text,
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            Margin = new Padding(0, 6, 0, 12),
        }, 1, 0);

        var btnOk = new Button
        {
            Text = Loc.T("dlg.ok"),
            AutoSize = true,
            MinimumSize = new Size(80, 0),
            DialogResult = DialogResult.OK,
            Anchor = AnchorStyles.Right,
        };
        layout.Controls.Add(btnOk, 1, 1);

        dialog.AcceptButton = btnOk;
        dialog.CancelButton = btnOk;
        dialog.Controls.Add(layout);

        if (icon == MessageBoxIcon.Error) System.Media.SystemSounds.Hand.Play();
        else if (icon == MessageBoxIcon.Warning) System.Media.SystemSounds.Exclamation.Play();
        else if (icon == MessageBoxIcon.Information) System.Media.SystemSounds.Asterisk.Play();

        dialog.ShowDialog(owner);
    }
}
