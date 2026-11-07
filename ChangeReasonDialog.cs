namespace WinFormsApp2;

internal sealed class ChangeReasonDialog : Form
{
    private readonly TextBox reason = new() { Multiline = true, MaxLength = 500, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
    internal string Reason => AuditContext.ValidateReason(reason.Text);
    internal ChangeReasonDialog()
    {
        Text = "Değişiklik gerekçesi"; ClientSize = new(560, 260); MinimumSize = new(450, 290);
        StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false; MinimizeBox = false; MaximizeBox = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(16), ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.AutoSize));
        var note = new Label { Text = "Bu değişiklik neden yapılıyor? (5–500 karakter)\nParola, geçici parola veya başka bir sır yazmayın.", AutoSize = true, Dock = DockStyle.Fill, Margin = new(0, 0, 0, 10) };
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new(0, 10, 0, 0) };
        var save = new Button { Text = "Devam et", AutoSize = true };
        var cancel = new Button { Text = "Vazgeç", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => { try { _ = Reason; DialogResult = DialogResult.OK; } catch (ArgumentException ex) { MessageBox.Show(this, ex.Message); } };
        buttons.Controls.AddRange([save, cancel]); CancelButton = cancel;
        layout.Controls.Add(note, 0, 0); layout.Controls.Add(reason, 0, 1); layout.Controls.Add(buttons, 0, 2); Controls.Add(layout);
    }
}
