namespace WinFormsApp2;

internal enum PersonnelGroup { General, Joker }
internal sealed record PersonnelDetails(long Tc, string FirstName, string LastName, string Iban, decimal Amount);
internal sealed record LeavePeriod(DateTime Start, DateTime End);
