using System.Diagnostics.CodeAnalysis;

namespace WinFormsApp2
{
    internal static class LeaveInput
    {
        public static bool TryCreate(DateTime start, DateTime end,
            [NotNullWhen(true)] out LeavePeriod? leave, out string error)
        {
            leave = null;
            error = "";
            if (end.Date < start.Date)
            {
                error = "İzin bitiş tarihi başlangıç tarihinden önce olamaz.";
                return false;
            }

            // Leave is recorded as calendar days; the same day is a valid period.
            leave = new LeavePeriod(start.Date, end.Date);
            return true;
        }
    }
}
