namespace WinFormsApp2;

internal sealed partial class PasswordChangeForm
{
    private void ArrangePasswordChange(Button save)
    {
        Text = "Muhasebe — Parola Değiştirme"; ClientSize = new Size(840, 560);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 64)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(ClassicLayouts.Banner("Parola Değiştirme", "Hesabınız için yeni bir parola belirleyin."), 0, 0);
        notice.Name = "passwordChangeNotice"; save.Name = "passwordChangeSave";
        var editor = new PasswordChangeEditor(notice, [current, next, repeat]);
        var rules = new PasswordChangeRules();
        var workspace = new PasswordChangeWorkspace(editor, rules) { Dock = DockStyle.Fill, Margin = Padding.Empty, TabIndex = 0 };
        var navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, TabIndex = 2 };
        var toFields = new Button { Name = "passwordChangeShowFields", Text = "Parola bilgileri", Width = 140, Height = 28, Margin = new Padding(3, 4, 6, 4), TabIndex = 0 };
        var toRules = new Button { Name = "passwordChangeShowRules", Text = "Parola kuralları", Width = 140, Height = 28, Margin = new Padding(3, 4, 3, 4), TabIndex = 1 };
        toFields.Click += (_, _) => workspace.AutoScrollPosition = Point.Empty;
        toRules.Click += (_, _) => workspace.ScrollControlIntoView(rules);
        navigation.Controls.Add(toFields); navigation.Controls.Add(toRules);
        outer.Controls.Add(navigation, 0, 1);
        var actions = new PasswordChangeActions(save) { TabIndex = 1 };
        outer.Controls.Add(new PasswordChangeBody(workspace, actions) { Dock = DockStyle.Fill, Margin = Padding.Empty, TabIndex = 0 }, 0, 2);
        Controls.Add(outer); AppTheme.Apply(this);
    }
}
