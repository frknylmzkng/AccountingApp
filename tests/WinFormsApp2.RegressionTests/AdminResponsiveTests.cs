using System.Data;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable, Category("Appearance")]
public class AdminResponsiveTests
{
    [TestCase(0), TestCase(1), TestCase(2)]
    public void ReflowKeepsNewAccountInputsMaskedAndOriginalSelectedAccount(int newRoleIndex)
    {
        using var form = new Admin { Session = new("preview-admin", UserRole.Admin), PreviewOnly = true, PersistGridLayout = false, Opacity = 0, ShowInTaskbar = false };
        ResponsiveTestWindow.Show(form, new Size(1120, 650)); AppTheme.Apply(form, classicOverride: true); var defaultSize = form.Size;
        var grid = AppTheme.Descendants(form).OfType<DataGridView>().Single();
        using var data = Accounts(); grid.BindingContext = new BindingContext(); grid.DataSource = data;
        data.DefaultView.Sort = "username DESC"; grid.ClearSelection(); grid.Rows[0].Selected = true;
        var target = AccountTarget.FromGrid(grid);
        var workspace = AppTheme.Descendants(form).OfType<AdminWorkspace>().Single();
        var create = workspace.Controls.OfType<AdminCreateCard>().Single();
        var actions = workspace.Controls.OfType<AdminActionsCard>().Single();
        var records = workspace.Controls.Find("adminRecords", false).Single();
        var username = create.Controls.OfType<TextBox>().Single(c => c.Name == "txtUsername");
        var password = create.Controls.OfType<TextBox>().Single(c => c.Name == "txtPassword");
        var role = create.Controls.OfType<ComboBox>().Single();
        const string samplePassword = "Yalnızca test için örnek parola 2026!";
        username.Text = "henüz-kaydedilmemiş"; password.Text = samplePassword; role.SelectedIndex = newRoleIndex;
        var columns = new HashSet<int>(); bool sawPaired = false, sawStacked = false;
        var widths = Enumerable.Range(0, 24).Select(i => 560 + i * 60).Concat(new[] { 1040, 1136 }).ToArray();
        foreach (int width in widths.Concat(widths.Reverse()))
        {
            ResponsiveTestWindow.Resize(form, new Size(width, 734)); form.PerformLayout(); Application.DoEvents();
            Assert.That(workspace.HorizontalScroll.Visible, Is.False, $"Page overflow at {width}");
            columns.Add(create.Controls.Cast<Control>().Where(c => c is TextBox or ComboBox).GroupBy(c => c.Top).Max(g => g.Count()));
            sawPaired |= create.Top == actions.Top; sawStacked |= actions.Top > create.Top;
            var panels = new Control[] { create, actions, records };
            foreach (var panel in panels)
            {
                Assert.That(panel.Left, Is.GreaterThanOrEqualTo(0));
                Assert.That(panel.Right, Is.LessThanOrEqualTo(workspace.ClientSize.Width));
            }
            for (int i = 0; i < panels.Length; i++) for (int j = i + 1; j < panels.Length; j++)
                Assert.That(panels[i].Bounds.IntersectsWith(panels[j].Bounds), Is.False);
            foreach (var control in AppTheme.Descendants(form).Where(c => c is TextBox or ComboBox or Button or Label))
                Assert.That(control.Parent!.ClientRectangle.Contains(control.Bounds), Is.True, control.Name);
            foreach (var button in create.Controls.OfType<Button>().Concat(actions.Controls.OfType<Button>()))
            {
                var text = TextRenderer.MeasureText(button.Text.Replace("&", ""), button.Font);
                Assert.That(text.Width + form.LogicalToDeviceUnits(12), Is.LessThanOrEqualTo(button.Width));
                Assert.That(button.Enabled && button.TabStop, Is.True);
            }
            Assert.That(username.Text, Is.EqualTo("henüz-kaydedilmemiş"));
            Assert.That(password.Text == samplePassword && password.UseSystemPasswordChar, Is.True, "The unsaved test password must remain masked and intact.");
            Assert.That(role.SelectedIndex, Is.EqualTo(newRoleIndex));
            Assert.That(grid.DataSource, Is.SameAs(data));
            var current = AccountTarget.FromGrid(grid);
            Assert.That(current.Username, Is.EqualTo(target.Username)); Assert.That(current.Version, Is.EqualTo(target.Version)); Assert.That(current.Epoch, Is.EqualTo(target.Epoch));
        }
        Assert.That(columns, Is.EquivalentTo(new[] { 1, 2, 3 })); Assert.That(sawPaired && sawStacked, Is.True);
        Assert.That(password.MaxLength, Is.EqualTo(PasswordHasher.MaxPasswordLength)); Assert.That(username.MaxLength, Is.EqualTo(50));
        Assert.That(role.DropDownStyle, Is.EqualTo(ComboBoxStyle.DropDownList));
        Assert.That(grid.ReadOnly, Is.True);
        Assert.That(grid.Columns.Cast<DataGridViewColumn>().Where(RecordSelection.IsInternalColumn).All(c => !c.Visible), Is.True);
        Assert.That(grid.Columns.Contains("password") || grid.Columns.Contains("password_hash"), Is.False);
        ResponsiveTestWindow.Resize(form, new Size(560, 480)); form.PerformLayout(); Application.DoEvents();
        void Jump(string name) { AppTheme.Descendants(form).OfType<Button>().Single(b => b.Name == name).PerformClick(); Application.DoEvents(); }
        Jump("adminShowCreate"); Assert.That(workspace.ClientRectangle.Contains(create.Bounds), Is.True);
        Jump("adminShowActions"); Assert.That(workspace.ClientRectangle.Contains(actions.Bounds), Is.True);
        Jump("adminShowList"); Assert.That(workspace.ClientRectangle.Contains(records.Bounds), Is.True);
        foreach (var control in create.Controls.Cast<Control>().Where(c => c is TextBox or ComboBox).Concat(actions.Controls.OfType<Button>()))
        {
            control.Focus(); Application.DoEvents();
            Assert.That(workspace.ClientRectangle.Contains(workspace.RectangleToClient(control.RectangleToScreen(control.ClientRectangle))), Is.True, control.Name);
        }
        ResponsiveTestWindow.ResizeDevice(form, defaultSize); form.PerformLayout(); Application.DoEvents();
        Assert.That(workspace.VerticalScroll.Visible || workspace.HorizontalScroll.Visible, Is.False);
        foreach (bool dark in new[] { false, true })
        {
            AppTheme.Apply(form, darkOverride: dark, classicOverride: false); form.PerformLayout(); Application.DoEvents();
            var note = actions.Controls.OfType<Label>().Single();
            Assert.That(TextRenderer.MeasureText(note.Text, note.Font, new Size(note.Width, 0), TextFormatFlags.WordBreak).Height, Is.LessThanOrEqualTo(note.Height));
            Assert.That(password.Text == samplePassword && password.UseSystemPasswordChar, Is.True);
        }
        Assert.That(role.SelectedIndex, Is.EqualTo(newRoleIndex));
        Assert.That(data.Rows.Cast<DataRow>().All(r => r.RowState == DataRowState.Unchanged), Is.True);
        // No create/delete/state/reset button is invoked: this verifies layout
        // without changing accounts, credentials or database state.
    }

    [TestCase(UserRole.PersonnelManager), TestCase(UserRole.Viewer)]
    public void NonAdministratorsStillCannotOpenAdminPage(UserRole actorRole)
    {
        int adminFormsCreated = 0;
        using var context = new AccountingApplicationContext(page =>
        {
            if (page == AppPage.Admin) { adminFormsCreated++; return new Admin { PreviewOnly = true, Opacity = 0, ShowInTaskbar = false }; }
            return page == AppPage.Login ? new Giris(() => Task.CompletedTask, (_, _) => Task.FromResult(false)) { Opacity = 0, ShowInTaskbar = false } : new SessionForm { PreviewOnly = true, Opacity = 0, ShowInTaskbar = false };
        });
        Assert.That(context.StartSession("preview-user", actorRole), Is.True);
        Assert.That(context.Navigate(AppPage.Admin), Is.False);
        Assert.That(adminFormsCreated, Is.Zero);
    }

    private static DataTable Accounts()
    {
        var data = new DataTable(); data.Columns.Add("username"); data.Columns.Add("RoleName"); data.Columns.Add("IsActive", typeof(bool));
        data.Columns.Add("MustChangePassword", typeof(bool)); data.Columns.Add("PasswordResetExpiresUtc", typeof(DateTime));
        data.Columns.Add(AccountTarget.VersionColumn, typeof(byte[])); data.Columns.Add(AccountTarget.EpochColumn, typeof(Guid));
        data.Rows.Add("ornek-yonetici", "Admin", true, false, DBNull.Value, new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }, Guid.NewGuid());
        data.Rows.Add("ornek-personel", "PersonnelManager", false, true, DateTime.UtcNow.AddHours(12), new byte[] { 0, 0, 0, 0, 0, 0, 0, 2 }, Guid.NewGuid());
        data.Rows.Add("ornek-izleyici", "Viewer", true, false, DBNull.Value, new byte[] { 0, 0, 0, 0, 0, 0, 0, 3 }, Guid.NewGuid());
        data.AcceptChanges(); return data;
    }
}
