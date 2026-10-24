using System.Data;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

internal static class Calculations
{
    public static int LeaveDays(DateTime start, DateTime end) => end.Date < start.Date
        ? throw new ArgumentException("Bitiş tarihi başlangıçtan önce olamaz.") : checked((end.Date - start.Date).Days + 1);
    public static decimal WeightedAverage(IEnumerable<(decimal Amount, decimal Weight)> values)
    {
        decimal sum = 0, weights = 0;
        foreach (var value in values)
        {
            if (value.Weight < 0) throw new ArgumentException("Ağırlık negatif olamaz.");
            sum = checked(sum + value.Amount * value.Weight); weights = checked(weights + value.Weight);
        }
        return weights == 0 ? 0 : sum / weights;
    }
}

