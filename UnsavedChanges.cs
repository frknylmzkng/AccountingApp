using System.Security.Cryptography;
using System.Text;

namespace WinFormsApp2;

internal sealed class UnsavedChanges(SessionForm owner) : IDisposable
{
    private sealed class Field(Control control, string group, byte[] baseline)
    {
        internal Control Control = control;
        internal string Group = group;
        internal byte[] Baseline = baseline;
        internal bool Edited;
    }
    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
    private readonly List<Field> fields = [];
    private readonly List<NativeWindow> rowGuards = [];
    private bool asking;
    private byte[] Value(Control control)
    {
        string text = control switch { DateTimePicker date => date.ShowCheckBox && !date.Checked ? "NULL" : (date.Format == DateTimePickerFormat.Time || (date.Format == DateTimePickerFormat.Custom && ((date.CustomFormat ?? string.Empty).Contains('H') || (date.CustomFormat ?? string.Empty).Contains('h'))) ? date.Value.Ticks : date.Value.Date.Ticks).ToString(System.Globalization.CultureInfo.InvariantCulture),
            CheckBox check => check.Checked.ToString(), ComboBox combo => combo.SelectedIndex + "|" + combo.Text,
            DataGridView grid => string.Join("|", grid.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index).Select(r =>
                r.DataBoundItem is System.Data.DataRowView row && row.Row.Table.Columns.Contains("PersonId")
                    ? row.Row["PersonId", System.Data.DataRowVersion.Original].ToString() : r.Index.ToString(System.Globalization.CultureInfo.InvariantCulture))), _ => control.Text };
        return HMACSHA256.HashData(key, Encoding.Unicode.GetBytes(text));
    }
    internal void Track(string group, params Control[] controls)
    {
        foreach (var control in controls)
        {
            var field = new Field(control, group, Value(control)); fields.Add(field);
            control.Enter += (_, _) => { if (!field.Edited) field.Baseline = Value(control); };
            void Changed(object? sender, EventArgs e)
            {
                if (!owner.IsOperationRunning && (control.ContainsFocus || ReferenceEquals(owner.ActiveControl, control)) && !SessionUiState.IsLocked)
                    field.Edited = !Value(control).SequenceEqual(field.Baseline);
            }
            if (control is DateTimePicker date) { date.ValueChanged += Changed; date.CloseUp += Changed; date.MouseUp += (_, e) => Changed(date,e); date.KeyUp += (_, e) => Changed(date,e); }
            else if (control is CheckBox check) check.CheckedChanged += Changed;
            else if (control is ComboBox combo) combo.SelectedIndexChanged += Changed;
            else if (control is DataGridView grid) grid.SelectionChanged += Changed;
            else control.TextChanged += Changed;
        }
    }
    internal bool HasChanges(string? except = null) => fields.Any(f => f.Group != except && f.Edited && !f.Control.IsDisposed && !Value(f.Control).SequenceEqual(f.Baseline));
    internal void Accept(string? group = null)
    { foreach (var field in fields.Where(f => group == null || f.Group == group).Where(f => !f.Control.IsDisposed)) { field.Baseline = Value(field.Control); field.Edited = false; } }
    internal void DraftLoaded()
    { foreach (var field in fields.Where(f => !f.Control.IsDisposed)) field.Edited = !Value(field.Control).SequenceEqual(field.Baseline); }
    internal bool Confirm(string action, string? except = null)
    {
        if (!HasChanges(except)) return true;
        if (asking || SessionUiState.IsLocked) return false;
        asking = true;
        try
        {
            var groups = fields.Where(f => f.Group != except && f.Edited && !f.Control.IsDisposed && !Value(f.Control).SequenceEqual(f.Baseline)).Select(f => f.Group).Distinct();
            return MessageBox.Show(owner, "Kaydedilmemiş değişiklikler: " + string.Join(", ", groups) +
                "\n" + action + " sırasında bu değişiklikler kaybolabilir. Kaydetmeden devam edilsin mi?\nHayır seçerseniz bulunduğunuz yerde kalırsınız.",
                "Kaydedilmemiş değişiklikler", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }
        finally { asking = false; }
    }
    internal void GuardSelection(DataGridView grid)
    {
        var guard = new SelectionGuard(this, owner, grid); rowGuards.Add(guard);
        grid.HandleCreated += (_, _) => guard.AssignHandle(grid.Handle);
        grid.HandleDestroyed += (_, _) => guard.ReleaseHandle();
        if (grid.IsHandleCreated) guard.AssignHandle(grid.Handle);
    }
    private sealed class SelectionGuard(UnsavedChanges changes, SessionForm owner, DataGridView grid) : NativeWindow
    {
        protected override void WndProc(ref Message message)
        {
            if (!owner.IsOperationRunning && !SessionUiState.IsLocked && changes.HasChanges())
            {
                bool move = false;
                if (message.Msg == 0x0201)
                {
                    long point = message.LParam.ToInt64(); var hit = grid.HitTest((short)point, (short)(point >> 16));
                    move = hit.RowIndex >= 0 && !grid.Rows[hit.RowIndex].Selected;
                }
                else if (message.Msg == 0x0100)
                {
                    var key = (Keys)message.WParam.ToInt32();
                    move = key is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown || ((Control.ModifierKeys & Keys.Control) != 0 && key is Keys.Home or Keys.End);
                }
                if (move)
                {
                    if (!changes.Confirm("Personel seçimini değiştirme")) return;
                    changes.Accept();
                }
            }
            base.WndProc(ref message);
        }
    }
    public void Dispose()
    {
        foreach (var window in rowGuards) window.ReleaseHandle();
        fields.Clear(); CryptographicOperations.ZeroMemory(key);
    }
}
