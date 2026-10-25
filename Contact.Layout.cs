namespace WinFormsApp2;

public partial class Contact
{
    private void ArrangeContact()
    {
        SuspendLayout();
        string firstName = label1.Text, secondName = label2.Text;
        textBox1.Parent?.Controls.Remove(textBox1); textBox2.Parent?.Controls.Remove(textBox2);
        var discarded = Controls.Cast<Control>().ToArray(); Controls.Clear();
        foreach (var control in discarded) control.Dispose();
        Text = "Muhasebe — İletişim"; ClientSize = new Size(720, 320);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        outer.Controls.Add(ClassicLayouts.Banner("İletişim", "İletişim kişileri ve telefon bilgileri"), 0, 0);
        var contacts = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 8, 0, 8) };
        contacts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); contacts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); contacts.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        void Card(string name, TextBox phone, int index)
        {
            var group = new GroupBox { Name = "ContactCard" + index, Text = name, Dock = DockStyle.Fill, Padding = new Padding(14, 18, 14, 10), Margin = new Padding(3) };
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); content.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.Controls.Add(new Label { Text = "Telefon", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            phone.Dock = DockStyle.Fill; phone.ReadOnly = true; phone.Font = AppTheme.TitleFont; phone.TextAlign = HorizontalAlignment.Left; phone.TabIndex = 0; phone.Margin = new Padding(3, 4, 3, 3);
            content.Controls.Add(phone, 0, 1);
            var copy = new Button { Text = "Numarayı kopyala", Size = new Size(138, 30), Anchor = AnchorStyles.Left, TabIndex = 1 };
            copy.Click += (_, _) =>
            {
                try { Clipboard.SetText(phone.Text); SetHint(name + " — telefon numarası kopyalandı."); }
                catch (System.Runtime.InteropServices.ExternalException) { SetHint("Pano şu anda kullanılamıyor. Numarayı seçip Ctrl+C ile tekrar deneyin."); }
            };
            content.Controls.Add(copy, 0, 2); group.Controls.Add(content); contacts.Controls.Add(group, index, 0);
        }
        Card(firstName, textBox1, 0); Card(secondName, textBox2, 1); outer.Controls.Add(contacts, 0, 1);
        outer.Controls.Add(new Label { Text = "Telefon numarasını seçerek Ctrl+C ile de kopyalayabilirsiniz.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
        Controls.Add(outer); ResumeLayout(true); AppTheme.Apply(this);
    }
}
