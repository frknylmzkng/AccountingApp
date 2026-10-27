using System.Data;
using System.Reflection;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable, Category("Appearance")]
public class PaymentsResponsiveTests
{
    [TestCase(UserRole.Admin, 0), TestCase(UserRole.Admin, 1), TestCase(UserRole.Admin, 2), TestCase(UserRole.Admin, 3), TestCase(UserRole.Admin, 4)]
    [TestCase(UserRole.PersonnelManager, 0), TestCase(UserRole.PersonnelManager, 1), TestCase(UserRole.PersonnelManager, 2), TestCase(UserRole.PersonnelManager, 3), TestCase(UserRole.PersonnelManager, 4)]
    [TestCase(UserRole.Viewer, 0), TestCase(UserRole.Viewer, 1), TestCase(UserRole.Viewer, 2), TestCase(UserRole.Viewer, 3), TestCase(UserRole.Viewer, 4)]
    public void ReflowPreservesPaymentJournalSelectionInputsAndStateLocks(UserRole role, int stateIndex)
    {
        using var form = new PaymentsForm { Session = new("preview-user", role), PreviewOnly = true, PersistGridLayout = false, Opacity = 0, ShowInTaskbar = false };
        form.Show(); AppTheme.Apply(form, classicOverride: true); var defaultSize = form.Size;
        var grids = AppTheme.Descendants(form).OfType<DataGridView>().ToArray();
        var people = grids.Single(g => g.Name == "People"); var ledger = grids.Single(g => g.Name == "Ledger");
        using var persons = People(); using var history = History();
        people.BindingContext = new BindingContext(); ledger.BindingContext = new BindingContext();
        people.DataSource = persons; ledger.DataSource = history;
        persons.DefaultView.Sort = "TC DESC"; history.DefaultView.Sort = "LedgerId DESC";
        people.ClearSelection(); ledger.ClearSelection(); people.Rows[0].Selected = ledger.Rows[0].Selected = true;
        var workspace = AppTheme.Descendants(form).OfType<PaymentsWorkspace>().Single();
        var payment = workspace.Controls.OfType<PaymentEntryCard>().Single(); var daily = workspace.Controls.OfType<PaymentClosingCard>().Single();
        var peoplePanel = workspace.Controls.Find("paymentPeopleList", false).Single(); var ledgerPanel = workspace.Controls.Find("paymentLedgerList", false).Single();
        var amount = AppTheme.Descendants(form).OfType<TextBox>().Single(t => t.Name == "paymentAmount");
        var declared = AppTheme.Descendants(form).OfType<TextBox>().Single(t => t.Name == "declaredTotal");
        var date = AppTheme.Descendants(form).OfType<DateTimePicker>().Single(); var paper = AppTheme.Descendants(form).OfType<CheckBox>().Single();
        var state = stateIndex == 0 ? null : new PaymentState(new(Guid.NewGuid(), PersonnelGroup.Joker, 22222222222L, 150m, 1), new((PaymentStatus)(stateIndex - 1), "Örnek ret nedeni: güncel bakiye yetersiz."));
        Invoke(form, "ApplyPaymentState", state); Invoke(form, "RefreshEditState");
        if (state == null) amount.Text = "150,50";
        declared.Text = "2500,75"; date.Value = DateTime.Today.AddDays(-2); paper.Checked = true;
        string originalAmount = amount.Text; var selected = (DataRow)Invoke(form, "SelectedPayment")!;
        var controls = AppTheme.Descendants(form).Where(c => c is Button or TextBox or DateTimePicker or DataGridView or CheckBox).ToDictionary(c => c, c => c.Enabled);
        var widths = Enumerable.Range(0, 24).Select(i => 560 + i * 60).Concat(new[] { 1136, 1800 }).ToArray();
        bool sawPaired = false, sawStacked = false, sawWideLists = false;
        foreach (int width in widths.Concat(widths.Reverse()))
        {
            form.Size = form.LogicalToDeviceUnits(new Size(width, 900)); form.PerformLayout(); Application.DoEvents();
            Assert.That(workspace.HorizontalScroll.Visible, Is.False, $"Page overflow at {width}");
            sawPaired |= payment.Top == daily.Top; sawStacked |= daily.Top > payment.Top; sawWideLists |= peoplePanel.Top == ledgerPanel.Top;
            var panels = new Control[] { payment, daily, peoplePanel, ledgerPanel };
            foreach (var panel in panels) { Assert.That(panel.Left, Is.GreaterThanOrEqualTo(0)); Assert.That(panel.Right, Is.LessThanOrEqualTo(workspace.ClientSize.Width)); }
            for (int i = 0; i < panels.Length; i++) for (int j = i + 1; j < panels.Length; j++) Assert.That(panels[i].Bounds.IntersectsWith(panels[j].Bounds), Is.False);
            foreach (var control in AppTheme.Descendants(form).Where(c => c is TextBox or Button or DateTimePicker or CheckBox or Label))
                Assert.That(control.Parent!.ClientRectangle.Contains(control.Bounds), Is.True, control.Name);
            foreach (var button in payment.Controls.OfType<Button>())
                Assert.That(TextRenderer.MeasureText(button.Text, button.Font).Width + form.LogicalToDeviceUnits(12), Is.LessThanOrEqualTo(button.Width), button.Text);
            Assert.That(controls.All(p => p.Key.Enabled == p.Value), Is.True, "Resizing must not unlock a pending payment.");
            Assert.That(amount.Text, Is.EqualTo(originalAmount)); Assert.That(declared.Text, Is.EqualTo("2500,75"));
            Assert.That(date.Value.Date, Is.EqualTo(DateTime.Today.AddDays(-2))); Assert.That(paper.Checked, Is.True);
            Assert.That(people.DataSource, Is.SameAs(persons)); Assert.That(ledger.DataSource, Is.SameAs(history));
            Assert.That(RecordSelection.TryGetTc(people, out long tc) && tc == 22222222222L, Is.True);
            Assert.That(Invoke(form, "SelectedPayment"), Is.SameAs(selected));
            Assert.That(typeof(PaymentsForm).GetField("pending", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(form), Is.SameAs(state));
        }
        Assert.That(sawPaired && sawStacked && sawWideLists, Is.True);
        bool edit = role != UserRole.Viewer;
        Button FindButton(string name) => AppTheme.Descendants(form).OfType<Button>().Single(b => b.Name == name);
        Assert.That(FindButton("paymentSave").Enabled, Is.EqualTo(edit && (state == null || state.Outcome.Status == PaymentStatus.Unconfirmed)));
        Assert.That(FindButton("paymentAcknowledge").Enabled, Is.EqualTo(edit && state != null && state.Outcome.Status != PaymentStatus.Unconfirmed));
        Assert.That(FindButton("paymentReverse").Enabled, Is.EqualTo(role == UserRole.Admin));
        Assert.That(amount.Enabled, Is.EqualTo(edit && state == null)); Assert.That(people.Enabled, Is.EqualTo(state == null));
        form.Size = form.LogicalToDeviceUnits(new Size(560, 480)); form.PerformLayout(); Application.DoEvents();
        foreach (string name in new[] { "paymentsShowClosing", "paymentsShowPeople", "paymentsShowLedger" })
        {
            FindButton(name).PerformClick(); Application.DoEvents();
            var panel = name == "paymentsShowClosing" ? (Control)daily : name == "paymentsShowPeople" ? peoplePanel : ledgerPanel;
            Assert.That(workspace.ClientRectangle.Contains(panel.Bounds), Is.True, name);
        }
        FindButton("paymentsShowEntry").PerformClick(); Application.DoEvents(); Assert.That(workspace.AutoScrollPosition, Is.EqualTo(Point.Empty));
        foreach (var control in payment.Controls.Cast<Control>().Concat(daily.Controls.Cast<Control>()).Where(c => c is Button or TextBox or DateTimePicker or CheckBox))
        {
            workspace.ScrollControlIntoView(control); Application.DoEvents();
            Assert.That(workspace.ClientRectangle.Contains(workspace.RectangleToClient(control.RectangleToScreen(control.ClientRectangle))), Is.True, control.Name);
        }
        foreach (bool dark in new[] { false, true })
        {
            form.Size = defaultSize; AppTheme.Apply(form, darkOverride: dark, classicOverride: false); form.PerformLayout(); Application.DoEvents();
            var label = payment.Controls.OfType<Label>().Single(l => l.Name == "paymentState");
            Assert.That(TextRenderer.MeasureText(label.Text, label.Font, new Size(label.Width, 0), TextFormatFlags.WordBreak).Height, Is.LessThanOrEqualTo(label.Height));
        }
        Assert.That(controls.All(p => p.Key.Enabled == p.Value), Is.True);
        Assert.That(persons.Rows.Cast<DataRow>().Concat(history.Rows.Cast<DataRow>()).All(r => r.RowState == DataRowState.Unchanged), Is.True);
        // Only navigation buttons run; payment, retry, acknowledgement, reversal,
        // inspection and report actions are not invoked against a database.
    }
    private static object? Invoke(PaymentsForm form, string method, params object?[] args) => typeof(PaymentsForm).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(form, args);
    internal static DataTable People()
    {
        var t = new DataTable(); t.Columns.Add("TC", typeof(long)); foreach (string n in new[] { "AD", "SOYAD", "IBAN", "GRUP" }) t.Columns.Add(n); t.Columns.Add("PARA", typeof(decimal));
        t.Rows.Add(11111111111L, "Ada", "Örnek", "TR000000000000000000000000", "Genel", 1500m); t.Rows.Add(22222222222L, "Ece", "Örnek", "TR000000000000000000000000", "Joker", 2500m); t.AcceptChanges(); return t;
    }
    internal static DataTable History()
    {
        var t = new DataTable(); t.Columns.Add("LedgerId", typeof(long)); t.Columns.Add("TC", typeof(long)); t.Columns.Add("PersonnelGroup", typeof(int)); t.Columns.Add("CreatedUtc", typeof(DateTime)); t.Columns.Add("Kind"); t.Columns.Add("Delta", typeof(decimal)); t.Columns.Add("RunningBalance", typeof(decimal)); t.Columns.Add("Actor"); t.Columns.Add("RequestId", typeof(Guid)); t.Columns.Add("ReversedUtc", typeof(DateTime));
        t.Rows.Add(1L, 11111111111L, 0, DateTime.UtcNow, "Payment", -150m, 1350m, "örnek-kullanıcı", Guid.NewGuid(), DBNull.Value);
        t.Rows.Add(2L, 22222222222L, 1, DateTime.UtcNow, "Payment", -200m, 2300m, "örnek-kullanıcı", Guid.NewGuid(), DBNull.Value); t.AcceptChanges(); return t;
    }
}
