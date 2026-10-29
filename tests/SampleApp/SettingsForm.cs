namespace SampleApp;

/// <summary>
/// Fixed-size settings dialog used to exercise navigation steps.
/// Planted bugs: a truncated checkbox caption and a Save button that is
/// pushed out of the client area.
/// </summary>
internal sealed class SettingsForm : Form
{
    public SettingsForm()
    {
        Text = "Settings";
        Name = "SettingsForm";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(420, 260);
        AutoScaleMode = AutoScaleMode.Dpi;

        var chkUpdates = new CheckBox
        {
            Name = "chkUpdates",
            Text = "Automatically download and install updates in the background",
            Location = new Point(20, 20),
            Size = new Size(200, 24),
            AutoSize = false,
        };

        var cmbTheme = new ComboBox { Name = "cmbTheme", Location = new Point(20, 60), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbTheme.Items.AddRange(["Light", "Dark", "System"]);
        cmbTheme.SelectedIndex = 2;

        var btnSave = new Button { Name = "btnSave", Text = "Save", Location = new Point(360, 220), Size = new Size(100, 30), DialogResult = DialogResult.OK };
        var btnClose = new Button { Name = "btnClose", Text = "Close", Location = new Point(240, 220), Size = new Size(100, 30), DialogResult = DialogResult.Cancel };

        Controls.AddRange([chkUpdates, cmbTheme, btnSave, btnClose]);
        AcceptButton = btnSave;
        CancelButton = btnClose;
    }
}
