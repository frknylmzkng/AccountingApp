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
        Text = "Muhasebe — İletişim"; ClientSize = new Size(800, 400);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        outer.Controls.Add(ClassicLayouts.Banner("İletişim", "İletişim kişileri ve telefon bilgileri"), 0, 0);
        var copyHint = new Label { Name = "contactCopyHint", Text = "Telefon numarasını seçerek Ctrl+C ile de kopyalayabilirsiniz.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, TabStop = false };
        ContactCard Card(string name, TextBox phone, int index)
        {
            var card = new ContactCard(name, phone, index);
            void Feedback(string message) { copyHint.Text = message; SetHint(message); }
            card.Copy.Click += (_, _) =>
            {
                try { copyPhone(phone.Text); Feedback(card.Text + " — telefon numarası kopyalandı."); }
                catch (System.Runtime.InteropServices.ExternalException) { Feedback("Pano şu anda kullanılamıyor. Numarayı seçip Ctrl+C ile tekrar deneyin."); }
            };
            return card;
        }
        var cards = new[] { Card(firstName, textBox1, 0), Card(secondName, textBox2, 1) };
        outer.Controls.Add(new ContactWorkspace(cards) { Dock = DockStyle.Fill, Margin = Padding.Empty, TabIndex = 0 }, 0, 1);
        outer.Controls.Add(copyHint, 0, 2);
        Controls.Add(outer); ResumeLayout(true); AppTheme.Apply(this);
    }
}
