namespace SampleApp;

/// <summary>
/// Main window. Planted bugs (each one should be reported by a rule):
/// <list type="bullet">
/// <item><c>btnOk</c> uses a fixed location with no anchoring: clipped on small windows (clipping).</item>
/// <item><c>btnHelp</c> overlaps <c>btnCancel</c> (overlap).</item>
/// <item><c>lblDescription</c> is too narrow for its text (text truncation).</item>
/// <item><c>btnTiny</c> is a 10x10 clickable target (minimum size).</item>
/// <item><c>btnGhost</c> is placed far outside the client area (offscreen / clipping).</item>
/// </list>
/// </summary>
internal sealed class MainForm : Form
{
    public MainForm()
    {
        Text = "Sample App";
        Name = "MainForm";
        // Layout is authored at 96 DPI and scaled by WinForms at higher DPI.
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1000, 700);
        StartPosition = FormStartPosition.CenterScreen;

        var lblName = new Label { Name = "lblName", Text = "Name:", Location = new Point(20, 24), AutoSize = true };
        var txtName = new TextBox
        {
            Name = "txtName",
            Location = new Point(120, 20),
            Width = 840,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        var lblDescription = new Label
        {
            Name = "lblDescription",
            Text = "This description is far too long to fit inside its label",
            Location = new Point(20, 70),
            Size = new Size(120, 20),
            AutoSize = false,
        };

        var btnSettings = new Button { Name = "btnSettings", Text = "Settings...", Location = new Point(20, 120), Size = new Size(120, 32) };
        btnSettings.Click += (_, _) =>
        {
            using var dialog = new SettingsForm();
            dialog.ShowDialog(this);
        };

        var btnTiny = new Button { Name = "btnTiny", Text = "", Location = new Point(160, 130), Size = new Size(10, 10) };

        var btnCancel = new Button { Name = "btnCancel", Text = "Cancel", Location = new Point(20, 560), Size = new Size(120, 32) };
        var btnHelp = new Button { Name = "btnHelp", Text = "Help", Location = new Point(100, 570), Size = new Size(120, 32) };

        // Not anchored: stays at x=860 even when the window shrinks.
        var btnOk = new Button { Name = "btnOk", Text = "OK", Location = new Point(860, 560), Size = new Size(120, 32) };

        var btnGhost = new Button { Name = "btnGhost", Text = "Ghost", Location = new Point(3000, 3000), Size = new Size(80, 30) };

        var status = new StatusStrip { Name = "statusBar" };
        status.Items.Add(new ToolStripStatusLabel("Ready"));

        Controls.AddRange([lblName, txtName, lblDescription, btnSettings, btnTiny, btnCancel, btnHelp, btnOk, btnGhost, status]);
    }
}
