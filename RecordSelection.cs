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
            grid.DataBindingComplete += (_, _) => HideInternalColumns(grid);
        }

        internal static bool IsInternalColumnName(string name) => name is PersonnelConcurrency.ColumnName or LeaveTarget.ColumnName or AccountTarget.VersionColumn or AccountTarget.EpochColumn;
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
