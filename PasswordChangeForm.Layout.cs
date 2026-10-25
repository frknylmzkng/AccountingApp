namespace WinFormsApp2;

internal sealed partial class PasswordChangeForm
{
    private void ArrangePasswordChange(Button save)
    {
        Text = "Muhasebe — Parola Değiştirme"; ClientSize = new Size(720, 420);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 5 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 62)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 170)); outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        outer.Controls.Add(ClassicLayouts.Banner("Parola Değiştirme", "Hesabınız için yeni bir parola belirleyin."), 0, 0);
        notice.AutoSize = false; notice.MaximumSize = Size.Empty; notice.Dock = DockStyle.Fill; notice.TextAlign = ContentAlignment.MiddleLeft; notice.Padding = new Padding(3);
        outer.Controls.Add(notice, 0, 1);
        var group = new GroupBox { Text = "Parola bilgileri", Dock = DockStyle.Fill, Padding = new Padding(12, 18, 12, 8) };
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        TextBox[] secrets = [current, next, repeat]; string[] captions = ["Mevcut / geçici parola:", "Yeni parola:", "Yeni parola tekrar:"];
        for (int i = 0; i < secrets.Length; i++)
        {
            fields.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333f));
            fields.Controls.Add(new Label { Text = captions[i], Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, i);
            secrets[i].Dock = DockStyle.Fill; secrets[i].Margin = new Padding(3, 10, 3, 3); secrets[i].TabIndex = i;
            fields.Controls.Add(secrets[i], 1, i);
        }
        group.Controls.Add(fields); outer.Controls.Add(group, 0, 2);
        outer.Controls.Add(new Label { Text = PasswordPolicy.Description, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(3) }, 0, 3);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 174)); actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        save.Dock = DockStyle.Fill; save.Margin = new Padding(3, 5, 3, 5); save.TabIndex = 0; actions.Controls.Add(save, 1, 0); outer.Controls.Add(actions, 0, 4);
        Controls.Add(outer); Load += (_, _) => MinimumSize = LogicalToDeviceUnits(new Size(650, 460));
        AppTheme.Apply(this);
    }
}
