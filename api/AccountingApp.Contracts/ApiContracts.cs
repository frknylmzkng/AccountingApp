using System.Text.Json;

namespace AccountingApp.Contracts;

public static class ApiContract { public const string Version = "1.32"; }
public enum PersonnelGroup { General, Joker }
public enum PaymentPurpose { Settlement, Advance }
public enum UserRole { Admin, PersonnelManager, Viewer, ProjectUser }
public enum ColumnType { String, Boolean, Int32, Int64, Decimal, DateTime, Guid, Binary }
public sealed record Column(string Name, ColumnType Type, bool ReadOnly);
public sealed record Table(Column[] Columns, JsonElement[][] Rows);
public sealed record Page(Table Data, long Total, long Index, int Size);
public sealed record VaultStatus(bool KeyAvailable,Guid? ActiveKeyId,DateTime? KeyExportedUtc,int Keep,bool MirrorConfigured,int ArchiveCount,DateTime? LastVerifiedUtc,DateTime? LastMirrorUtc,string MirrorState,string RecoveryState);
public sealed record VaultCommand(string Operation,string Reason,int Keep=14);
public sealed record ReportCenterScope(Guid ProjectId,string Report="Assignments",DateTime? From=null,DateTime? To=null,Guid? TeamId=null,string Person="",bool PendingOnly=false);
public sealed record ReportCenterBrowse(ReportCenterScope Scope,long Index=0,int Size=50);
public sealed record ReportCenterResult(Page Page,DateTime ReadUtc,string Scope);
public sealed record ReportPreset(Guid PresetId,string Name,ReportCenterScope Scope,string Reason="");
public sealed record ReportCenterOptions(Table Teams,ReportPreset[] Presets);
public sealed record ReportPresetDelete(Guid ProjectId,Guid PresetId,string Reason="");
public sealed record ReportCenterExport(ReportCenterScope Scope,string Reason);
public sealed record UserInfo(Guid AccountId, string Username, UserRole Role, bool MustChangePassword, bool CanEdit, bool CanPay, bool CanExportPersonalData, bool CanViewSalary, bool CanViewIban);
public sealed record LoginRequest(string Username, string Password) { public override string ToString() => "LoginRequest (gizli)"; }
public sealed record LoginResponse(string AccessToken, DateTimeOffset AbsoluteExpiryUtc, int IdleTimeoutMinutes, UserInfo User, string ServerScope) { public override string ToString() => "LoginResponse (gizli)"; }
public sealed record AuthorizationRequest(string Access);
public sealed record AccountReset(AccountTarget Target, string AdminPassword, string Reason) { public override string ToString() => "AccountReset (gizli)"; }
public sealed record ResetResult(string Username, string TemporaryPassword, DateTime ExpiresUtc) { public override string ToString() => "ResetResult (gizli)"; }
public sealed record ExportRequest(Table Data, bool Excel, string Title, string Scope);
public sealed record ClipboardText(string Text);
public sealed record BuildInfo(string Version, string FileVersion, string InformationalVersion, string Commit, string SourceState, string ExpectedSchema, string ApiVersion);
public sealed record Diagnostics(BuildInfo Build, int AppliedUpgrades, bool SchemaMatches);
public sealed record PasswordRequest(string Password) { public override string ToString() => "PasswordRequest (gizli)"; }
public sealed record PasswordChangeRequest(string CurrentPassword, string NewPassword, string Confirmation, string Reason) { public override string ToString() => "PasswordChangeRequest (gizli)"; }
public sealed record Error(string Code, string Message, string TraceId);
public sealed record PersonWrite(PersonnelGroup Group, long Tc, string FirstName, string LastName, string Iban, decimal Amount, bool Insert, byte[]? ExpectedVersion, string Reason, DateTime? EmploymentStart = null);
public sealed record PersonArchive(PersonnelGroup Group, long Tc, byte[] ExpectedVersion, string Reason, DateTime? EmploymentEnd = null);
public sealed record PersonRestore(PersonnelGroup Group, Guid PersonId, byte[] ExpectedVersion, string Reason);
public sealed record LeaveItem(long Tc, byte[] PersonVersion, byte[]? LeaveVersion, DateTime Start, DateTime End);
public sealed record LeavePlan(LeaveItem[] Items, string Reason);
public sealed record PaymentPrepare(PersonnelGroup Group, long Tc, decimal Amount, byte[] PersonVersion, PaymentPurpose Purpose, Guid? ProjectId, string Reason);
public sealed record PaymentIntent(Guid RequestId, PersonnelGroup Group, long Tc, decimal Amount, long ClosureId, int FormatVersion,
    byte[]? PersonVersion, Guid? PersonId, PaymentPurpose Purpose, Guid? ProjectId, string? Reason);
public sealed record PaymentResult(string Status, string? Reason);
public sealed record PaymentReverse(Guid RequestId, string Reason);
public sealed record ProjectCreate(string Code, string Name, string Reason);
public sealed record FinancePost(Guid ProjectId, string Kind, string Reference, Guid? PersonId, PersonnelGroup? Group, long? Tc,
    byte[]? PersonVersion, DateTime Start, DateTime End, decimal Amount, string Description, string Reason);
public sealed record FinanceReverse(Guid EntryId, byte[] ExpectedVersion, string Reason);
public sealed record ClosingFinalize(DateTime Day, decimal Declared, string Reason);
public sealed record Summary(long GeneralCount, long JokerCount, long OnLeaveCount, decimal? Balance, decimal? Average, Page Alerts);
public sealed record History(Table Payments, Table Leaves, Table Events, Table LeaveChanges);
public sealed record PrintData(string Title, string[] Lines, string Reference, decimal? Difference);
public sealed record AccountTarget(string Username, byte[] Version, Guid Epoch);
public sealed record AccountAdd(string Username, string Password, UserRole Role, string Reason) { public override string ToString() => "AccountAdd (gizli)"; }
public sealed record AccountActive(AccountTarget Target, bool Active, string Reason);
public sealed record AccountPermissions(AccountTarget Target, bool CanPay, bool CanExportPersonalData, string Reason, bool? CanViewSalary = null, bool? CanViewIban = null);
public sealed record AccountDelete(AccountTarget Target, string Reason);
public sealed record ProjectDetails(Guid ProjectId, byte[] Version, string Name, string Site, string CustomerLabel, DateTime? Start, DateTime? End, bool Active, string Reason);
public sealed record TeamWrite(Guid ProjectId, Guid? TeamId, byte[]? Version, string Code, string Name, bool Active, string Reason);
public sealed record AssignmentWrite(Guid ProjectId, byte[] ProjectVersion, Guid? TeamId, byte[]? TeamVersion, Guid PersonId, PersonnelGroup Group, long Tc, byte[] PersonVersion, DateTime Start, DateTime? End, string Reason, bool IsSupplementary = false);
public sealed record AssignmentCancel(Guid AssignmentId, byte[] Version, string Reason);
public sealed record EmploymentRehire(PersonnelGroup Group, Guid PersonId, byte[] PersonVersion, DateTime Start, string Reason);
public sealed record EmploymentDates(PersonnelGroup Group, Guid PersonId, byte[] PersonVersion, Guid EmploymentId, byte[] EmploymentVersion, DateTime? Start, DateTime? End, string Reason);
public sealed record ProjectGrantWrite(Guid ProjectId, byte[] ProjectVersion, Guid? GrantId, byte[]? GrantVersion, Guid AccountId, byte[] AccountVersion, Guid AccountEpoch, bool IsResponsible, bool CanManageWorkforce, bool CanViewCosts, bool CanExport, string Reason, bool CanPreparePayments = false, bool CanApprovePayments = false, bool CanPlanShifts = false, bool CanApproveLeaves = false, bool CanEnterTimesheets = false, bool CanApproveTimesheets = false);
public sealed record ProjectGrantRevoke(Guid ProjectId, byte[] ProjectVersion, Guid GrantId, byte[] GrantVersion, string Reason);
public sealed record ProjectExport(Guid ProjectId, string Report, string Reason,FinancialFilter? Filter=null);
public sealed record PersonnelCard(Table Header, Table Periods, Table Totals, DateTime ReadUtc, bool CanViewAudit);
public sealed record PersonnelCardDetails(PersonnelGroup Group, Guid PersonId, byte[] Version, string FirstName, string LastName, string? Iban, string Reason);

public sealed record PersonnelGroupTransition(Guid TransitionId, PersonnelGroup SourceGroup, Guid PersonId, byte[] PersonVersion, Guid EmploymentId, byte[] EmploymentVersion, DateTime EffectiveDate, string Reason);

public sealed record ProjectPaymentPrepare(Guid RequestId, Guid ProjectId, Guid PersonId, PersonnelGroup Group, byte[] PersonVersion, decimal Amount, PaymentPurpose Purpose, string Reason);
public sealed record ProjectPaymentDecide(Guid ProjectId, Guid RequestId, byte[] Version, bool Approve, string Reason);

public sealed record ShiftTemplateWrite(Guid ProjectId,Guid TemplateId,byte[]? Version,Guid? TeamId,string Name,string Site,int StartMinute,int DurationMinutes,int BreakMinutes,int RequiredCount,bool Active,string Reason);
public sealed record ShiftCreate(Guid ProjectId,Guid ShiftId,Guid TemplateId,byte[] TemplateVersion,DateTime Day,string Reason);
public sealed record ShiftState(Guid ProjectId,Guid ShiftId,byte[] Version,string State,string Reason);
public sealed record ShiftPerson(Guid ProjectId,Guid ShiftId,byte[] ShiftVersion,Guid AssignmentId,Guid PersonId,PersonnelGroup Group,byte[] PersonVersion,string Reason);
public sealed record ShiftPersonCancel(Guid ProjectId,Guid ShiftId,byte[] ShiftVersion,Guid AssignmentId,byte[] Version,string Reason);
public sealed record LeaveRequestWrite(Guid ProjectId,Guid LeaveId,Guid PersonId,PersonnelGroup Group,byte[] PersonVersion,DateTime Start,DateTime End,string Reason);
public sealed record LeaveRequestState(Guid ProjectId,Guid LeaveId,byte[] Version,string State,string Reason);
public sealed record CalendarWeek(Table Shifts,Table Assignments,Table Leaves,Table LegacyLeaves);

public sealed record ShiftCopySource(Guid ShiftId,byte[] Version);
public sealed record WeekCopy(Guid ProjectId,Guid BatchId,DateTime SourceWeek,DateTime TargetWeek,ShiftCopySource[] Sources,string Reason);

public sealed record TimesheetWrite(Guid TimesheetId,byte[]? Version,Guid ShiftAssignmentId,byte[] AssignmentVersion,byte[] ShiftVersion,string SourceKey,DateTime? Start,DateTime? End,int BreakMinutes,int? PayableMinutes);
public sealed record TimesheetBatch(Guid ProjectId,Guid CommandId,TimesheetWrite[] Rows,string Reason);
public sealed record TimesheetState(Guid ProjectId,Guid CommandId,Guid TimesheetId,byte[] Version,string State,string Reason);
public sealed record TimesheetWeek(Table Rows,Table Assignments,Table Summary,Table Segments);

public sealed record UnplannedTimesheetWrite(Guid ProjectId,Guid CommandId,Guid TimesheetId,byte[]? Version,Guid PersonId,PersonnelGroup Group,byte[] PersonVersion,Guid EmploymentId,Guid RosterAssignmentId,byte[] RosterVersion,DateTime WorkDate,DateTime? Start,DateTime? End,int BreakMinutes,int? PayableMinutes,string SourceKey,string Reason);

public sealed record AttendanceEventRow(Guid EventId,string ExternalId,string Direction,DateTime OccurredLocal);
public sealed record AttendanceImport(Guid ProjectId,Guid CommandId,Guid PersonId,PersonnelGroup Group,byte[] PersonVersion,Guid EmploymentId,Guid RosterAssignmentId,byte[] RosterVersion,string SourceSystem,AttendanceEventRow[] Rows,string Reason);
public sealed record AttendanceMatch(Guid ProjectId,Guid CommandId,Guid MatchId,Guid TimesheetId,byte[] TimesheetVersion,Guid InEventId,byte[] InVersion,Guid OutEventId,byte[] OutVersion,byte[] PersonVersion,byte[] RosterVersion,int BreakMinutes,int? PayableMinutes,string Reason);
public sealed record AttendanceRelease(Guid ProjectId,Guid CommandId,Guid MatchId,byte[] Version,byte[] TimesheetVersion,string Reason);
public sealed record AttendanceIgnore(Guid ProjectId,Guid CommandId,Guid EventId,byte[] Version,string Reason);
public sealed record AttendanceWindow(Table Events,Table Matches);

public sealed record WorkSegment(Guid SegmentId,Guid ProjectId,byte[] ProjectVersion,Guid RosterAssignmentId,byte[] RosterVersion,DateTime Start,DateTime End,int BreakMinutes,int PayableMinutes);
public sealed record WorkAllocation(Guid ProjectId,Guid CommandId,Guid SetId,Guid TimesheetId,byte[] Version,byte[] PersonVersion,byte[] RosterVersion,WorkSegment[] Segments,string Reason);
public sealed record WorkAllocationRelease(Guid ProjectId,Guid CommandId,Guid SetId,byte[] Version,byte[] TimesheetVersion,string Reason);
public sealed record WorkAllocationData(Table Sets,Table Segments);

public sealed record SegmentDecision(Guid ProjectId,Guid CommandId,Guid SegmentId,Guid? RoundId,byte[]? ApprovalVersion,byte[] ParentVersion,byte[] SetVersion,string State,string Reason);

public sealed record AccountPersonRead(AccountTarget Target);
public sealed record AccountPersonChange(Guid CommandId,AccountTarget Target,Guid? LinkId,byte[]? LinkVersion,Guid? PersonId,PersonnelGroup? Group,byte[]? PersonVersion,string Reason);

public sealed record TimesheetDraftContext(Guid AccountId,Guid AccountEpoch,string DatabaseScope);
public sealed record TimesheetDraftResolve(Guid ProjectId,string Kind,JsonElement Payload,bool Cancel,string Reason);
public sealed record TimesheetDraftResolution(string State);

public sealed record TimesheetDraftPreviewAccess(Guid ProjectId,Guid[] Projects);

public sealed record ManualTimesheetBatch(Guid ProjectId,Guid CommandId,TimesheetWrite[] PlannedRows,UnplannedTimesheetWrite[] UnplannedRows,string Reason);

public sealed record HourlyRateCreate(Guid ProjectId,Guid RateId,Guid PersonId,Guid EmploymentId,byte[] EmploymentVersion,DateTime From,DateTime? To,decimal HourlyAmount,string Reason);
public sealed record HourlyRateRevoke(Guid ProjectId,Guid RateId,byte[] Version,string Reason);
public sealed record AccrualPreview(Table Lines,string Fingerprint,bool CanRecord,string[] Issues);
public sealed record AccrualCalculate(Guid ProjectId,Guid CalculationId,DateTime From,DateTime To,string Fingerprint,string Reason,Guid? ParentCalculationId=null);

public sealed record AccrualApprove(Guid ProjectId,Guid CalculationId,byte[] CalculationVersion,Guid ApprovalId,string Reason);
public sealed record AccrualPostingQuote(Table Targets,string Fingerprint,bool CanPost,string[] Issues);
public sealed record AccrualPost(Guid ProjectId,Guid CalculationId,Guid PostingId,string Fingerprint,string Reason);

public sealed record AccrualCorrectionCreate(Guid ProjectId,Guid CalculationId,Guid CorrectionId,Guid SourceItemId,Guid? PreviousCorrectionId,decimal OldAmount,decimal NewAmount,string Reason);
public sealed record AccrualCorrectionApprove(Guid ProjectId,Guid CalculationId,Guid CorrectionId,Guid ApprovalId,byte[] Version,string Reason);
public sealed record AccrualCorrectionPost(Guid ProjectId,Guid CalculationId,Guid CorrectionId,Guid TransferId,string Fingerprint,string Reason);

public sealed record PaymentAllocationBoard(Table Payments,Table Accruals,Table Allocations);
public sealed record PaymentAllocationCreate(Guid ProjectId,Guid AllocationId,long PaymentLedgerId,Guid FinanceEntryId,decimal Amount,string Fingerprint,string Reason);
public sealed record PaymentAllocationRelease(Guid ProjectId,Guid AllocationId,Guid ReleaseId,string Reason);

public sealed record ProjectReconciliation(Table Summary,Table People,Table Accruals,Table Payments,Table Allocations,Table Issues,DateTime ReadUtc,string ProjectCaption,string ReadBy,Dictionary<string,PageInfo>? Pages=null,string Scope="");

public sealed record FinancialFilter(DateTime? From=null,DateTime? To=null,string Person="");
public sealed record PageInfo(long Total,long Index,int Size);
public sealed record PageRequest(long Index=0,int Size=50);
public sealed record ReconciliationBrowse(Guid ProjectId,FinancialFilter Filter,Dictionary<string,PageRequest>? Pages=null);
public sealed record AllocationBrowse(Guid ProjectId,string Section,FinancialFilter Filter,long Index=0,int Size=50,long? PaymentLedgerId=null);

public sealed record WorkforceComparison(Table Summary,Dictionary<string,Page> Sections,DateTime ReadUtc,string ProjectCaption,string ReadBy,string Scope);

public sealed record ProjectBudgetSave(Guid ProjectId,Guid BudgetId,Guid? PreviousBudgetId,byte[] ProjectVersion,decimal LaborBudget,decimal OtherBudget,string Reason);
public sealed record ProjectBudgetBrowse(Guid ProjectId,Dictionary<string,PageRequest>? Pages=null);
public sealed record ProjectBudgetBoard(Table Summary,Page History,Page Costs,DateTime ReadUtc,string ProjectCaption,string ReadBy);

public sealed record BankAccountCreate(Guid AccountId,string Name,string Iban,string Reason);
// Columns: source account IBAN, bank transaction ID, day, signed amount, currency,
// counterparty IBAN, description, application payment GUID. Optional columns use -1.
public sealed record BankCsvSource(Guid AccountId,string FileName,string Csv,string Separator,string DateFormat,string DecimalSeparator,int[] Columns);
public sealed record BankImportPreview(Table Rows,string Fingerprint,int NewRows,int DuplicateRows,bool AlreadyImported);
public sealed record BankImport(Guid BatchId,BankCsvSource Source,string Fingerprint,string Reason);
public sealed record BankBrowse(Guid AccountId,string Section,long Index=0,int Size=50,Guid? TransactionId=null,long? LedgerId=null);
public sealed record BankMatchQuote(Table Details,string Fingerprint,string[] Warnings);
public sealed record BankMatchQuoteRequest(Guid AccountId,Guid TransactionId,long LedgerId);
public sealed record BankMatchCreate(Guid AccountId,Guid MatchId,Guid TransactionId,long LedgerId,string Fingerprint,string Reason);
public sealed record BankMatchRelease(Guid AccountId,Guid MatchId,Guid ReleaseId,byte[] Version,string Reason);

public sealed record FiscalPeriodScope(Guid ProjectId,DateTime From,DateTime To);
public sealed record FiscalPeriodQuote(Table Summary,Table Lines,long TotalLines,Guid? PreviousClosingId,string Fingerprint,string[] Notices);
public sealed record FiscalPeriodFinalize(Guid ProjectId,Guid ClosingId,Guid? PreviousClosingId,DateTime From,DateTime To,string Fingerprint,string Reason);
public sealed record FiscalPeriodBrowse(Guid ProjectId,long Index=0,int Size=50);
public sealed record FiscalPeriodHistory(Table Context,Page Closings);
public sealed record FiscalPeriodRead(Guid ProjectId,Guid ClosingId,long Index=0,int Size=50);
public sealed record FiscalPeriodView(Table Header,Table Summary,Page Lines);

public sealed record WagePolicyCreate(Guid ProjectId,Guid PolicyId,Guid? PreviousPolicyId,string RuleCode,string Name,string Kind,DateTime From,DateTime? To,decimal UnitAmount,decimal PremiumFactor,int Divisor,string Reason);
public sealed record WagePolicyDecision(Guid ProjectId,Guid PolicyId,Guid DecisionId,byte[] Version,string Kind,string Reason);
public sealed record WageRuleBrowse(Guid ProjectId,string Section,long Index=0,int Size=50);
public sealed record WageClaimScope(Guid ProjectId,Guid PolicyId,Guid? EmploymentId,byte[]? EmploymentVersion,DateTime From,DateTime To,decimal Units,Guid? PremiumLineId,string DocumentKey);
public sealed record WageClaimPreview(Table Detail,string Fingerprint,string[] Notices);
public sealed record WageClaimCreate(Guid ClaimId,WageClaimScope Scope,string Fingerprint,string Reason);
public sealed record WageClaimAction(Guid ProjectId,Guid ClaimId,Guid ActionId,byte[] Version,string Kind,string Reason);
public sealed record WageTransferQuoteRequest(Guid ProjectId,Guid ClaimId,string Kind);
public sealed record WageClaimTransfer(Guid ProjectId,Guid ClaimId,Guid TransferId,string Kind,string Fingerprint,string Reason);
public sealed record WageClaimRead(Guid ProjectId,Guid ClaimId);
public sealed record WageClaimView(Table Header,Table Evidence,Table Actions,Table Transfers);
public sealed record WageBasisBrowse(Guid ProjectId,Guid EmploymentId,DateTime From,DateTime To);
