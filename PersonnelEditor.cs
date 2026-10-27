namespace WinFormsApp2;

// Reuse the actual fields/buttons so resize never rebinds a record, clears an
// unfinished edit, or changes the original identity/version used by a write.
internal sealed class PersonnelEditor : GroupBox
{
    private readonly Label[] labels;
    private readonly TextBox[] fields;
    private readonly Button[] actions;

    internal PersonnelEditor(Label[] labels, TextBox[] fields, Button[] actions)
    {
        this.labels = labels; this.fields = fields; this.actions = actions;
        SuspendLayout();
        Name = "personnelEditor"; Text = "Personel bilgileri"; TabIndex = 0; TabStop = false;
        string[] captions = ["TC kimlik no:", "Adı:", "Soyadı:", "IBAN:", "Kalan (TL):"];
        for (int i = 0; i < fields.Length; i++)
        {
            labels[i].Text = captions[i]; labels[i].AutoSize = false;
            labels[i].Dock = fields[i].Dock = DockStyle.None;
            labels[i].Anchor = fields[i].Anchor = AnchorStyles.Top | AnchorStyles.Left;
            labels[i].TextAlign = ContentAlignment.MiddleLeft;
            fields[i].AutoSize = true;
            Controls.Add(labels[i]); Controls.Add(fields[i]);
        }
        string[] commands = ["&Ekle", "&Düzenle", "&Sil", "Alanları &temizle"];
        for (int i = 0; i < actions.Length; i++)
        {
            actions[i].Text = commands[i]; actions[i].AutoSize = false; actions[i].Dock = DockStyle.None;
            actions[i].Anchor = AnchorStyles.Top | AnchorStyles.Left;
            actions[i].TabStop = true; actions[i].TabIndex = 10 + i;
            Controls.Add(actions[i]);
        }
        ResumeLayout(true);
    }

    private int Columns(int width) => width >= LogicalToDeviceUnits(900) ? 3 : width >= LogicalToDeviceUnits(620) ? 2 : 1;
    internal int HeightForWidth(int width)
    {
        int columns = Columns(width);
        int rows = columns == 1 ? 5 : columns == 2 ? 3 : 2;
        int commandRows = columns == 1 ? 2 : 1;
        return LogicalToDeviceUnits(26 + rows * 54 + 8 + commandRows * 38 + 12);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int columns = Columns(ClientSize.Width), inset = LogicalToDeviceUnits(12), gap = LogicalToDeviceUnits(14);
        int available = Math.Max(1, ClientSize.Width - inset * 2);
        int top = LogicalToDeviceUnits(26), rowHeight = LogicalToDeviceUnits(54);
        void Field(int index, int column, int row, int span, int order)
        {
            int left = column * (available + gap) / columns;
            int right = (column + span) * (available + gap) / columns - gap;
            labels[index].TabIndex = order * 2; fields[index].TabIndex = order * 2 + 1;
            labels[index].SetBounds(inset + left, top + row * rowHeight, right - left, LogicalToDeviceUnits(18));
            fields[index].SetBounds(inset + left, top + row * rowHeight + LogicalToDeviceUnits(20), right - left, fields[index].PreferredHeight);
        }
        int rows;
        if (columns == 3)
        {
            Field(0, 0, 0, 1, 0); Field(1, 1, 0, 1, 1); Field(2, 2, 0, 1, 2);
            Field(3, 0, 1, 2, 3); Field(4, 2, 1, 1, 4); rows = 2;
        }
        else if (columns == 2)
        {
            Field(0, 0, 0, 1, 0); Field(4, 1, 0, 1, 1);
            Field(1, 0, 1, 1, 2); Field(2, 1, 1, 1, 3); Field(3, 0, 2, 2, 4); rows = 3;
        }
        else
        {
            for (int i = 0; i < fields.Length; i++) Field(i, 0, i, 1, i);
            rows = 5;
        }
        int commandTop = top + rows * rowHeight + LogicalToDeviceUnits(8);
        int commandColumns = columns == 1 ? 2 : 4;
        for (int i = 0; i < actions.Length; i++)
        {
            int column = i % commandColumns;
            int left = column * (available + gap) / commandColumns;
            int right = (column + 1) * (available + gap) / commandColumns - gap;
            actions[i].SetBounds(inset + left, commandTop + i / commandColumns * LogicalToDeviceUnits(38),
                right - left, LogicalToDeviceUnits(30));
        }
    }
}
