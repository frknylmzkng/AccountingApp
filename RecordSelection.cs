using System.Data;
using System.Globalization;

namespace WinFormsApp2
{
    internal static class RecordSelection
    {
        public static void Configure(DataGridView grid)
        {
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            ConfigureClipboard(grid);
            grid.DataBindingComplete += (_, _) => { HideInternalColumns(grid); if (grid.FindForm() is SessionForm screen && screen.Session != null) SensitiveData.Hide(grid, screen.User); };
        }
        internal static void ConfigureClipboard(DataGridView grid, SessionForm? owner = null)
        {
            // Native grid copying has no live account check. Route both standard
            // shortcuts through the same export authorization as files.
            grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable;
            grid.KeyDown += async (_, e) =>
            {
                if (!e.Control || e.KeyCode is not (Keys.C or Keys.Insert)) return;
                e.Handled = e.SuppressKeyPress = true;
                if ((owner ?? grid.FindForm() as SessionForm) is { } screen) await screen.CopyGridAsync(grid);
            };
        }

        internal static bool IsInternalColumnName(string name) => name is "CurrentEmploymentId" or "PersonId" or "ArchivedUtc" or "ArchivedBy" or PersonnelConcurrency.ColumnName or LeaveTarget.ColumnName or AccountTarget.VersionColumn or AccountTarget.EpochColumn;
        internal static bool IsInternalColumn(DataGridViewColumn column) => IsInternalColumnName(column.Name) || IsInternalColumnName(column.DataPropertyName);

        internal static void HideInternalColumns(DataGridView grid)
        {
            foreach (var column in grid.Columns.Cast<DataGridViewColumn>().Where(IsInternalColumn)) column.Visible = false;
        }

        internal static byte[] OriginalVersion(DataGridView grid)
        {
            if (grid.SelectedRows.Count != 1 || grid.SelectedRows[0].DataBoundItem is not DataRowView record ||
                !record.Row.HasVersion(DataRowVersion.Original) || !record.Row.Table.Columns.Contains(PersonnelConcurrency.ColumnName))
                return PersonnelConcurrency.RequireVersion(null);
            return PersonnelConcurrency.RequireVersion(record.Row[PersonnelConcurrency.ColumnName, DataRowVersion.Original] as byte[]);
        }

        internal static bool RestoreDraftSelection(DataGridView grid, string tc, string? draftVersion)
        {
            grid.ClearSelection();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.DataBoundItem is not DataRowView bound || !bound.Row.HasVersion(DataRowVersion.Original) ||
                    !bound.Row.Table.Columns.Contains("TC") || !bound.Row.Table.Columns.Contains(PersonnelConcurrency.ColumnName) ||
                    Convert.ToString(bound.Row["TC", DataRowVersion.Original], CultureInfo.InvariantCulture) != tc) continue;
                if (bound.Row[PersonnelConcurrency.ColumnName, DataRowVersion.Original] is not byte[] version ||
                    !PersonnelConcurrency.MatchesDraft(draftVersion, version)) return false;
                row.Selected = true;
                return true;
            }
            return false;
        }

        public static bool TryGetKey(DataGridView grid, string columnName, out string key)
        {
            key = "";
            if (grid.SelectedRows.Count != 1 || grid.SelectedRows[0].IsNewRow ||
                grid.SelectedRows[0].DataBoundItem is not DataRowView record ||
                !record.Row.Table.Columns.Contains(columnName) ||
                !record.Row.HasVersion(DataRowVersion.Original))
                return false;

            // A display edit must never redirect a write to a different record.
            key = Convert.ToString(record.Row[columnName, DataRowVersion.Original],
                CultureInfo.InvariantCulture) ?? "";
            return !string.IsNullOrWhiteSpace(key);
        }

        public static bool TryGetTc(DataGridView grid, out long tc)
        {
            tc = 0;
            return TryGetKey(grid, "TC", out string key) && key.Length == 11 &&
                key.All(c => c >= '0' && c <= '9') &&
                long.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out tc);
        }
        public static decimal OriginalAmount(DataGridView grid)
        {
            if (grid.SelectedRows.Count != 1 || grid.SelectedRows[0].DataBoundItem is not DataRowView record || !record.Row.HasVersion(DataRowVersion.Original)) throw new InvalidOperationException("Personeli listeden yeniden seçin.");
            object value = record.Row["PARA", DataRowVersion.Original]; return value == DBNull.Value ? 0 : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }
    }
}
