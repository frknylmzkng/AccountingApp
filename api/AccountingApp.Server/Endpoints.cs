using Wire = AccountingApp.Contracts;
using WinFormsApp2;

namespace AccountingApp.Server;

internal static class Endpoints
{
    private static UserSession User(HttpContext c) => ApiSessionFilter.Current(c).User;
    private static string Db(HttpContext c) => c.RequestServices.GetRequiredService<ServerRuntime>().ConnectionString;
    private static PersonnelService People(HttpContext c) => new(Db(c), User(c));
    private static ProjectFinanceService Finance(HttpContext c) => new(Db(c), User(c));
    private static DailyClosingService Closings(HttpContext c) => new(Db(c), User(c));
    private static DateTime Day(DateTime value) => value.Kind == DateTimeKind.Unspecified && value.TimeOfDay == TimeSpan.Zero
        ? value : throw new ArgumentException("Business dates must use yyyy-MM-dd without a time zone.");
    private static Wire.UserInfo Info(ApiSession entry) => new(entry.AccountId, entry.User.Username, (Wire.UserRole)entry.User.Role,
        entry.User.MustChangePassword, entry.User.CanEdit, entry.User.CanPay, entry.User.CanExportPersonalData, entry.User.CanViewSalary, entry.User.CanViewIban);

    internal static void MapAccountingEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/auth/login", async (Wire.LoginRequest request, HttpContext c, ServerRuntime runtime, ApiSessions sessions, ServerOptions options) =>
        {
            await runtime.VerifySchemaAsync(c.RequestAborted);
            var user = await new AccountService(runtime.ConnectionString).AuthenticateAsync(request.Username, request.Password, c.RequestAborted);
            if (user == null) return ApiSessionFilter.Failure(c, 401, "INVALID_CREDENTIALS", "Giriş doğrulanamadı.");
            Guid accountId = await runtime.VerifiedAccountIdAsync(user, c.RequestAborted);
            var issued = sessions.Issue(user, accountId);
            return Results.Ok(new Wire.LoginResponse(issued.Token, issued.Session.AbsoluteExpiryUtc, options.IdleMinutes, Info(issued.Session), runtime.Scope));
        });
        var api = app.MapGroup("/api/v1").AddEndpointFilter<ApiSessionFilter>();
        api.MapGet("/workforce/projects",async(HttpContext c)=>Results.Ok(WireTables.Encode(await new ProjectWorkforceService(Db(c),User(c)).ProjectsAsync(c.RequestAborted))));
        api.MapPost("/workforce/project",async(Wire.ProjectDetails r,HttpContext c)=>{await new ProjectWorkforceService(Db(c),User(c)).SaveProjectAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapGet("/workforce/{id:guid}/teams",async(Guid id,HttpContext c)=>Results.Ok(WireTables.Encode(await new ProjectWorkforceService(Db(c),User(c)).TeamsAsync(id,c.RequestAborted))));
        api.MapPost("/workforce/team",async(Wire.TeamWrite r,HttpContext c)=>{await new ProjectWorkforceService(Db(c),User(c)).SaveTeamAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapGet("/workforce/{id:guid}/assignments",async(Guid id,long? index,int? size,HttpContext c)=>Results.Ok(WireTables.Encode(await new ProjectWorkforceService(Db(c),User(c)).AssignmentsAsync(id,index??0,size??50,c.RequestAborted))));
        api.MapPost("/workforce/assign",async(Wire.AssignmentWrite r,HttpContext c)=>{await new ProjectWorkforceService(Db(c),User(c)).AssignAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/workforce/cancel",async(Wire.AssignmentCancel r,HttpContext c)=>{await new ProjectWorkforceService(Db(c),User(c)).CancelAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapGet("/allocations/{project:guid}/segments/{segment:guid}/decisions",async(Guid project,Guid segment,HttpContext c)=>Results.Ok(WireTables.Encode(await new TimesheetService(Db(c),User(c)).SegmentHistoryAsync(project,segment,c.RequestAborted))));
        api.MapPost("/allocations/segment-decision",async(Wire.SegmentDecision r,HttpContext c)=>{await new TimesheetService(Db(c),User(c)).DecideSegmentAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapGet("/allocations/{project:guid}/{timesheet:guid}/targets",async(Guid project,Guid timesheet,HttpContext c)=>Results.Ok(WireTables.Encode(await new TimesheetService(Db(c),User(c)).AllocationTargetsAsync(project,timesheet,c.RequestAborted))));
        api.MapGet("/allocations/{project:guid}/{timesheet:guid}",async(Guid project,Guid timesheet,HttpContext c)=>{var d=await new TimesheetService(Db(c),User(c)).AllocationsAsync(project,timesheet,c.RequestAborted);return Results.Ok(new Wire.WorkAllocationData(WireTables.Encode(d.Sets),WireTables.Encode(d.Segments)));});
        api.MapPost("/allocations/save",async(Wire.WorkAllocation r,HttpContext c)=>{await new TimesheetService(Db(c),User(c)).SaveAllocationAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/allocations/release",async(Wire.WorkAllocationRelease r,HttpContext c)=>{await new TimesheetService(Db(c),User(c)).ReleaseAllocationAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapGet("/attendance/{project:guid}/window",async(Guid project,DateTime first,HttpContext c)=>{var d=await new TimesheetService(Db(c),User(c)).EventsAsync(project,Day(first),c.RequestAborted);return Results.Ok(new Wire.AttendanceWindow(WireTables.Encode(d.Events),WireTables.Encode(d.Matches)));});
        api.MapPost("/attendance/import",async(Wire.AttendanceImport r,HttpContext c)=>{await new TimesheetService(Db(c),User(c)).ImportEventsAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/attendance/match",async(Wire.AttendanceMatch r,HttpContext c)=>{await new TimesheetService(Db(c),User(c)).MatchEventsAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/attendance/release",async(Wire.AttendanceRelease r,HttpContext c)=>{await new TimesheetService(Db(c),User(c)).ReleaseMatchAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/attendance/ignore",async(Wire.AttendanceIgnore r,HttpContext c)=>{await new TimesheetService(Db(c),User(c)).IgnoreEventAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapGet("/timesheets/{project:guid}/rosters/{person:guid}",async(Guid project,Guid person,Wire.PersonnelGroup group,HttpContext c)=>Results.Ok(WireTables.Encode(await new TimesheetService(Db(c),User(c)).RostersAsync(project,person,group,c.RequestAborted))));
        api.MapPost("/timesheets/unplanned",async(Wire.UnplannedTimesheetWrite r,HttpContext c)=>{await new TimesheetService(Db(c),User(c)).SaveUnplannedAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapGet("/timesheets/{project:guid}/csv-template",async(Guid project,DateTime first,HttpContext c)=>Results.Ok(new Wire.ClipboardText(await new TimesheetService(Db(c),User(c)).CsvTemplateAsync(project,Day(first),c.RequestAborted))));
        api.MapGet("/timesheets/{project:guid}/week",async(Guid project,DateTime first,bool? segmentHistory,HttpContext c)=>{var x=await new TimesheetService(Db(c),User(c)).WeekAsync(project,Day(first),c.RequestAborted,segmentHistory??false);return Results.Ok(new Wire.TimesheetWeek(WireTables.Encode(x.Rows),WireTables.Encode(x.Assignments),WireTables.Encode(x.Summary),WireTables.Encode(x.Segments)));});
        api.MapGet("/accrual/{project:guid}/targets",async(Guid project,HttpContext c)=>Results.Ok(WireTables.Encode(await new AccrualFoundationService(Db(c),User(c)).TargetsAsync(project,c.RequestAborted))));
        api.MapGet("/accrual/{project:guid}/rates",async(Guid project,HttpContext c)=>Results.Ok(WireTables.Encode(await new AccrualFoundationService(Db(c),User(c)).RatesAsync(project,c.RequestAborted))));
        api.MapGet("/accrual/{project:guid}/preview",async(Guid project,DateTime from,DateTime to,Guid? parent,HttpContext c)=>Results.Ok(await new AccrualFoundationService(Db(c),User(c)).PreviewAsync(project,from,to,c.RequestAborted,parent)));
        api.MapGet("/accrual/{project:guid}/history",async(Guid project,DateTime from,DateTime to,HttpContext c)=>Results.Ok(WireTables.Encode(await new AccrualFoundationService(Db(c),User(c)).HistoryAsync(project,from,to,c.RequestAborted))));
        api.MapGet("/accrual/{project:guid}/{id:guid}/lines",async(Guid project,Guid id,HttpContext c)=>Results.Ok(WireTables.Encode(await new AccrualFoundationService(Db(c),User(c)).LinesAsync(project,id,c.RequestAborted))));
        api.MapPost("/accrual/rate/create",async(Wire.HourlyRateCreate r,HttpContext c)=>{await new AccrualFoundationService(Db(c),User(c)).CreateRateAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/accrual/rate/revoke",async(Wire.HourlyRateRevoke r,HttpContext c)=>{await new AccrualFoundationService(Db(c),User(c)).RevokeRateAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapGet("/report-center/{project:guid}/options",async(Guid project,HttpContext c)=>Results.Ok(await new ReportCenterService(Db(c),User(c)).OptionsAsync(project,c.RequestAborted)));
        api.MapPost("/report-center/browse",async(Wire.ReportCenterBrowse r,HttpContext c)=>Results.Ok(await new ReportCenterService(Db(c),User(c)).BrowseAsync(r,c.RequestAborted)));
        api.MapPost("/report-center/presets/save",async(Wire.ReportPreset r,HttpContext c)=>{await new ReportCenterService(Db(c),User(c)).SavePresetAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/report-center/presets/delete",async(Wire.ReportPresetDelete r,HttpContext c)=>{await new ReportCenterService(Db(c),User(c)).DeletePresetAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/report-center/export",async(Wire.ReportCenterExport r,HttpContext c)=>
        {
            await new ReportCenterService(Db(c),User(c)).DeliverZipAsync(r,async(bytes,ct)=>{c.Response.ContentType="application/zip";c.Response.ContentLength=bytes.Length;await c.Response.Body.WriteAsync(bytes,ct);await c.Response.Body.FlushAsync(ct);},c.RequestAborted);
        });
        api.MapPost("/wage-rules/policy/create",async(Wire.WagePolicyCreate r,HttpContext c)=>{await new WageRuleService(Db(c),User(c)).CreatePolicyAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/wage-rules/policy/decide",async(Wire.WagePolicyDecision r,HttpContext c)=>{await new WageRuleService(Db(c),User(c)).DecidePolicyAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/wage-rules/browse",async(Wire.WageRuleBrowse r,HttpContext c)=>Results.Ok(await new WageRuleService(Db(c),User(c)).BrowseAsync(r,c.RequestAborted)));
        api.MapGet("/wage-rules/{project:guid}/targets",async(Guid project,HttpContext c)=>Results.Ok(WireTables.Encode(await new WageRuleService(Db(c),User(c)).TargetsAsync(project,c.RequestAborted))));
        api.MapPost("/wage-rules/basis",async(Wire.WageBasisBrowse r,HttpContext c)=>Results.Ok(WireTables.Encode(await new WageRuleService(Db(c),User(c)).BasisAsync(r,c.RequestAborted))));
        api.MapPost("/wage-rules/claim/quote",async(Wire.WageClaimScope r,HttpContext c)=>Results.Ok(await new WageRuleService(Db(c),User(c)).QuoteAsync(r,c.RequestAborted)));
        api.MapPost("/wage-rules/claim/create",async(Wire.WageClaimCreate r,HttpContext c)=>{await new WageRuleService(Db(c),User(c)).CreateClaimAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/wage-rules/claim/action",async(Wire.WageClaimAction r,HttpContext c)=>{await new WageRuleService(Db(c),User(c)).ActAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/wage-rules/claim/transfer-quote",async(Wire.WageTransferQuoteRequest r,HttpContext c)=>Results.Ok(await new WageRuleService(Db(c),User(c)).TransferQuoteAsync(r,c.RequestAborted)));
        api.MapPost("/wage-rules/claim/transfer",async(Wire.WageClaimTransfer r,HttpContext c)=>{await new WageRuleService(Db(c),User(c)).TransferAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/wage-rules/claim/read",async(Wire.WageClaimRead r,HttpContext c)=>Results.Ok(await new WageRuleService(Db(c),User(c)).ReadAsync(r,c.RequestAborted)));
        api.MapPost("/fiscal-periods/quote",async(Wire.FiscalPeriodScope r,HttpContext c)=>Results.Ok(await new FiscalPeriodService(Db(c),User(c)).QuoteAsync(r,c.RequestAborted)));
        api.MapPost("/fiscal-periods/finalize",async(Wire.FiscalPeriodFinalize r,HttpContext c)=>{await new FiscalPeriodService(Db(c),User(c)).FinalizeAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/fiscal-periods/browse",async(Wire.FiscalPeriodBrowse r,HttpContext c)=>Results.Ok(await new FiscalPeriodService(Db(c),User(c)).HistoryAsync(r,c.RequestAborted)));
        api.MapPost("/fiscal-periods/read",async(Wire.FiscalPeriodRead r,HttpContext c)=>Results.Ok(await new FiscalPeriodService(Db(c),User(c)).ReadAsync(r,c.RequestAborted)));
        api.MapGet("/bank/accounts",async(HttpContext c)=>Results.Ok(WireTables.Encode(await new BankReconciliationService(Db(c),User(c)).AccountsAsync(c.RequestAborted))));
        api.MapPost("/bank/accounts",async(Wire.BankAccountCreate r,HttpContext c)=>{await new BankReconciliationService(Db(c),User(c)).CreateAccountAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/bank/import/preview",async(Wire.BankCsvSource r,HttpContext c)=>Results.Ok(await new BankReconciliationService(Db(c),User(c)).PreviewAsync(r,c.RequestAborted)));
        api.MapPost("/bank/import",async(Wire.BankImport r,HttpContext c)=>{await new BankReconciliationService(Db(c),User(c)).ImportAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/bank/browse",async(Wire.BankBrowse r,HttpContext c)=>Results.Ok(await new BankReconciliationService(Db(c),User(c)).BrowseAsync(r,c.RequestAborted)));
        api.MapPost("/bank/matches/quote",async(Wire.BankMatchQuoteRequest r,HttpContext c)=>Results.Ok(await new BankReconciliationService(Db(c),User(c)).QuoteAsync(r,c.RequestAborted)));
        api.MapPost("/bank/matches",async(Wire.BankMatchCreate r,HttpContext c)=>{await new BankReconciliationService(Db(c),User(c)).MatchAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/bank/matches/release",async(Wire.BankMatchRelease r,HttpContext c)=>{await new BankReconciliationService(Db(c),User(c)).ReleaseAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/project-budgets/browse",async(Wire.ProjectBudgetBrowse r,HttpContext c)=>Results.Ok(await new ProjectBudgetService(Db(c),User(c)).ReadAsync(r,c.RequestAborted)));
        api.MapPost("/project-budgets/save",async(Wire.ProjectBudgetSave r,HttpContext c)=>{await new ProjectBudgetService(Db(c),User(c)).SaveAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/project-work-hours/browse",async(Wire.ReconciliationBrowse r,HttpContext c)=>Results.Ok(await new ProjectWorkHoursService(Db(c),User(c)).ReadAsync(r,c.RequestAborted)));
        api.MapPost("/project-reconciliation/browse",async(Wire.ReconciliationBrowse r,HttpContext c)=>Results.Ok(await new ProjectReconciliationService(Db(c),User(c)).ReadAsync(r.ProjectId,c.RequestAborted,new(r.Filter,r.Pages))));
        api.MapPost("/payment-allocations/browse",async(Wire.AllocationBrowse r,HttpContext c)=>Results.Ok(await new PaymentAllocationService(Db(c),User(c)).PageAsync(r,c.RequestAborted)));
        api.MapGet("/project-reconciliation/{project:guid}",async(Guid project,HttpContext c)=>Results.Ok(await new ProjectReconciliationService(Db(c),User(c)).ReadAsync(project,c.RequestAborted)));
        api.MapGet("/payment-allocations/{project:guid}",async(Guid project,HttpContext c)=>Results.Ok(await new PaymentAllocationService(Db(c),User(c)).BoardAsync(project,c.RequestAborted)));
        api.MapGet("/payment-allocations/{project:guid}/quote",async(Guid project,long payment,Guid finance,HttpContext c)=>Results.Ok(await new PaymentAllocationService(Db(c),User(c)).QuoteAsync(project,payment,finance,c.RequestAborted)));
        api.MapPost("/payment-allocations/create",async(Wire.PaymentAllocationCreate r,HttpContext c)=>{await new PaymentAllocationService(Db(c),User(c)).CreateAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/payment-allocations/release",async(Wire.PaymentAllocationRelease r,HttpContext c)=>{await new PaymentAllocationService(Db(c),User(c)).ReleaseAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/accrual/correction/create",async(Wire.AccrualCorrectionCreate r,HttpContext c)=>{await new AccrualFoundationService(Db(c),User(c)).CreateCorrectionAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/accrual/correction/approve",async(Wire.AccrualCorrectionApprove r,HttpContext c)=>{await new AccrualFoundationService(Db(c),User(c)).ApproveCorrectionAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/accrual/correction/post",async(Wire.AccrualCorrectionPost r,HttpContext c)=>{await new AccrualFoundationService(Db(c),User(c)).PostCorrectionAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapGet("/accrual/{project:guid}/{id:guid}/corrections",async(Guid project,Guid id,HttpContext c)=>Results.Ok(WireTables.Encode(await new AccrualFoundationService(Db(c),User(c)).CorrectionHistoryAsync(project,id,c.RequestAborted))));
        api.MapGet("/accrual/{project:guid}/{id:guid}/corrections/{correction:guid}/quote",async(Guid project,Guid id,Guid correction,HttpContext c)=>Results.Ok(await new AccrualFoundationService(Db(c),User(c)).CorrectionQuoteAsync(project,id,correction,c.RequestAborted)));
        api.MapPost("/accrual/approve",async(Wire.AccrualApprove r,HttpContext c)=>{await new AccrualFoundationService(Db(c),User(c)).ApproveAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/accrual/post",async(Wire.AccrualPost r,HttpContext c)=>{await new AccrualFoundationService(Db(c),User(c)).PostAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapGet("/accrual/{project:guid}/{id:guid}/posting-quote",async(Guid project,Guid id,HttpContext c)=>Results.Ok(await new AccrualFoundationService(Db(c),User(c)).PostingQuoteAsync(project,id,c.RequestAborted)));
        api.MapGet("/accrual/{project:guid}/{id:guid}/posting-items",async(Guid project,Guid id,HttpContext c)=>Results.Ok(WireTables.Encode(await new AccrualFoundationService(Db(c),User(c)).PostingItemsAsync(project,id,c.RequestAborted))));
        api.MapPost("/accrual/calculate",async(Wire.AccrualCalculate r,HttpContext c)=>{await new AccrualFoundationService(Db(c),User(c)).CalculateAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/timesheets/manual-batch",async(Wire.ManualTimesheetBatch r,HttpContext c)=>{await new TimesheetService(Db(c),User(c)).SaveManualBatchAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/timesheets/save",async(Wire.TimesheetBatch r,HttpContext c)=>{await new TimesheetService(Db(c),User(c)).SaveAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/timesheets/state",async(Wire.TimesheetState r,HttpContext c)=>{await new TimesheetService(Db(c),User(c)).StateAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapGet("/schedule/{project:guid}/templates",async(Guid project,HttpContext c)=>Results.Ok(WireTables.Encode(await new SchedulingService(Db(c),User(c)).TemplatesAsync(project,c.RequestAborted))));
        api.MapGet("/schedule/{project:guid}/week",async(Guid project,DateTime first,Guid? team,bool? history,HttpContext c)=>{var x=await new SchedulingService(Db(c),User(c)).WeekAsync(project,Day(first),team,history??false,c.RequestAborted);return Results.Ok(new Wire.CalendarWeek(WireTables.Encode(x.Shifts),WireTables.Encode(x.Assignments),WireTables.Encode(x.Leaves),WireTables.Encode(x.Legacy)));});
        api.MapPost("/schedule/copy-week",async(Wire.WeekCopy r,HttpContext c)=>{await new SchedulingService(Db(c),User(c)).CopyWeekAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/schedule/template",async(Wire.ShiftTemplateWrite r,HttpContext c)=>{await new SchedulingService(Db(c),User(c)).SaveTemplateAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/schedule/create",async(Wire.ShiftCreate r,HttpContext c)=>{await new SchedulingService(Db(c),User(c)).CreateShiftAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/schedule/state",async(Wire.ShiftState r,HttpContext c)=>{await new SchedulingService(Db(c),User(c)).StateAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/schedule/assign",async(Wire.ShiftPerson r,HttpContext c)=>{await new SchedulingService(Db(c),User(c)).AssignAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/schedule/unassign",async(Wire.ShiftPersonCancel r,HttpContext c)=>{await new SchedulingService(Db(c),User(c)).CancelAssignmentAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/schedule/leave",async(Wire.LeaveRequestWrite r,HttpContext c)=>{await new SchedulingService(Db(c),User(c)).RequestLeaveAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/schedule/leave-state",async(Wire.LeaveRequestState r,HttpContext c)=>{await new SchedulingService(Db(c),User(c)).LeaveStateAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapGet("/project-payments/{project:guid}", async(Guid project,long? index,int? size,HttpContext c)=>Results.Ok(WireTables.Encode(await new ProjectPaymentService(Db(c),User(c)).PageAsync(project,index??0,size??50,c.RequestAborted))));
        api.MapPost("/project-payments/prepare",async(Wire.ProjectPaymentPrepare r,HttpContext c)=>{await new ProjectPaymentService(Db(c),User(c)).PrepareAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/project-payments/decide",async(Wire.ProjectPaymentDecide r,HttpContext c)=>{await new ProjectPaymentService(Db(c),User(c)).DecideAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapGet("/employment/{group}/{personId:guid}", async (Wire.PersonnelGroup group, Guid personId, HttpContext c) => Results.Ok(WireTables.Encode(await new EmploymentService(Db(c), User(c)).ListAsync(WireTables.Group(group), personId, c.RequestAborted))));
        api.MapPost("/employment/dates", async (Wire.EmploymentDates r, HttpContext c) => { await new EmploymentService(Db(c), User(c)).DatesAsync(r, c.RequestAborted); return Results.NoContent(); });
        api.MapPost("/employment/group-transition", async (Wire.PersonnelGroupTransition r, HttpContext c) => { await new EmploymentService(Db(c), User(c)).TransitionGroupAsync(r, c.RequestAborted); return Results.NoContent(); });
        api.MapPost("/employment/rehire", async (Wire.EmploymentRehire r, HttpContext c) => { await new EmploymentService(Db(c), User(c)).RehireAsync(r, c.RequestAborted); return Results.NoContent(); });
        api.MapGet("/project-access/accounts",async(HttpContext c)=>Results.Ok(WireTables.Encode(await new ProjectAccessService(Db(c),User(c)).AccountsAsync(c.RequestAborted))));
        api.MapGet("/project-access/{project:guid}/grants",async(Guid project,HttpContext c)=>Results.Ok(WireTables.Encode(await new ProjectAccessService(Db(c),User(c)).GrantsAsync(project,c.RequestAborted))));
        api.MapPost("/project-access/save",async(Wire.ProjectGrantWrite r,HttpContext c)=>{await new ProjectAccessService(Db(c),User(c)).SaveAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/project-access/revoke",async(Wire.ProjectGrantRevoke r,HttpContext c)=>{await new ProjectAccessService(Db(c),User(c)).RevokeAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapGet("/project-access/{project:guid}/people",async(Guid project,long? index,int? size,HttpContext c)=>Results.Ok(WireTables.Encode(await new ProjectAccessService(Db(c),User(c)).PeopleAsync(project,index??0,size??50,c.RequestAborted))));
        api.MapPost("/project-access/export",async(Wire.ProjectExport r,HttpContext c)=>
        {
            using var reason=AuditContext.UseReason(AuditContext.ValidateReason(r.Reason));
            await new ProjectReportService(Db(c),User(c)).DeliverCsvAsync(r.ProjectId,r.Report,async(bytes,ct)=>{c.Response.ContentType="application/octet-stream";c.Response.ContentLength=bytes.Length;await c.Response.Body.WriteAsync(bytes,ct);await c.Response.Body.FlushAsync(ct);},c.RequestAborted,r.Filter);
        });
        api.MapPost("/personnel-card/details",async(Wire.PersonnelCardDetails r,HttpContext c)=>{await new PersonnelCardService(Db(c),User(c)).SaveDetailsAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapGet("/personnel-card/{group}/{person:guid}",async(Wire.PersonnelGroup group,Guid person,Guid? employment,HttpContext c)=>
        {
            var r=await new PersonnelCardService(Db(c),User(c)).ReadAsync(WireTables.Group(group),person,employment,c.RequestAborted);
            return Results.Ok(new Wire.PersonnelCard(WireTables.Encode(r.Header),WireTables.Encode(r.Periods),WireTables.Encode(r.Totals),r.ReadUtc,r.CanViewAudit));
        });
        api.MapGet("/personnel-card/{group}/{person:guid}/{section}",async(Wire.PersonnelGroup group,Guid person,PersonnelCardSection section,Guid? employment,long? index,int? size,HttpContext c)=>Results.Ok(WireTables.Encode(await new PersonnelCardService(Db(c),User(c)).PageAsync(WireTables.Group(group),person,employment,section,index??0,size??50,c.RequestAborted))));
        api.MapGet("/backup-vault/status",async(HttpContext c)=>Results.Ok(await new VaultManagementService(Db(c),User(c)).StatusAsync(c.RequestAborted)));
        api.MapPost("/backup-vault/command",async(Wire.VaultCommand r,HttpContext c)=>{await new VaultManagementService(Db(c),User(c)).ExecuteAsync(r,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/support/safe-package",async(HttpContext c)=>
        {
            await new SupportPackageService(Db(c),User(c)).DeliverAsync(async(bytes,ct)=>{c.Response.ContentType="application/zip";c.Response.ContentLength=bytes.Length;await c.Response.Body.WriteAsync(bytes,ct);await c.Response.Body.FlushAsync(ct);},c.RequestAborted);
        });
        api.MapGet("/diagnostics", async (HttpContext c) => Results.Ok(await BuildDiagnostics.ReadAsync(Db(c), User(c), c.RequestAborted)))
            .WithMetadata(new SessionAccess(AllowTemporary: true, Touch: false));
        api.MapGet("/session/validate", (HttpContext c) => Results.Ok(Info(ApiSessionFilter.Current(c))))
            .WithMetadata(new SessionAccess(AllowLocked: true, AllowTemporary: true, Touch: false));
        api.MapPost("/authorization", async (Wire.AuthorizationRequest request, HttpContext c) =>
        {
            if (!Enum.TryParse<AccountAccess>(request.Access, out var access) || !Enum.IsDefined(access)) throw new ArgumentException("Invalid access.");
            await AccountAuthorization.ValidateAsync(Db(c), User(c), access, c.RequestAborted);
            return Results.Ok(Info(ApiSessionFilter.Current(c)));
        }).WithMetadata(new SessionAccess(AllowTemporary: true, Touch: false));
        api.MapGet("/audit", async (HttpContext c, long? before) => Results.Ok(WireTables.Encode(await new AuditHistoryService(Db(c), User(c)).ReadAsync(before, c.RequestAborted))));
        api.MapPost("/exports/file", async (Wire.ExportRequest request, HttpContext c) =>
        {
            if (request.Title.Length > 200 || request.Scope.Length > 500 || request.Data.Columns.Length is < 1 or > 100 || request.Data.Rows.Length > 10000 || (long)request.Data.Rows.Length * request.Data.Columns.Length > 200000)
                throw new ArgumentException("Report limit exceeded.");
            await using var authorization = await SqlUnitOfWork.OpenAsync(Db(c), c.RequestAborted);
            await AccountAuthorization.DemandAsync(authorization, User(c), AccountAccess.FullDataExport, c.RequestAborted);
            using var data = RemoteApi.Decode(request.Data);
            data.ExtendedProperties["Export.ReportTitle"] = request.Title; data.ExtendedProperties["Export.ReportScope"] = request.Scope;
            string path = Path.Combine(Path.GetTempPath(), "AccountingApp-api-export-" + Guid.NewGuid().ToString("N") + (request.Excel ? ".xlsx" : ".csv"));
            try
            {
                await new PersonalDataExportService(Db(c), User(c)).ExportAsync(data, path, request.Excel, c.RequestAborted);
                byte[] bytes = await File.ReadAllBytesAsync(path, c.RequestAborted);
                c.Response.ContentType = "application/octet-stream"; c.Response.ContentLength = bytes.Length;
                await c.Response.Body.WriteAsync(bytes, c.RequestAborted); await c.Response.Body.FlushAsync(c.RequestAborted);
                // Keep the live account read lease until authorized bytes leave the server.
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }).WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(8 * 1024 * 1024));
        api.MapPost("/exports/clipboard", async (Wire.ClipboardText request, HttpContext c) =>
        {
            if (request.Text.Length > 1000000) throw new ArgumentException("Clipboard limit exceeded.");
            await using var work = await SqlUnitOfWork.OpenAsync(Db(c), c.RequestAborted);
            await AccountAuthorization.DemandAsync(work, User(c), AccountAccess.FullDataExport, c.RequestAborted);
            await People(c).AuditAsync(work, "Export.Clipboard", c.RequestAborted); await work.CommitAsync(c.RequestAborted);
            return Results.Ok(request);
        }).WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(8 * 1024 * 1024));
        api.MapPost("/accounts/reset-password", async (Wire.AccountReset request, HttpContext c) =>
        {
            using var reason = AuditContext.UseReason(request.Reason);
            var result = await new AccountService(Db(c)).ResetPasswordAsync(User(c), WireTables.Target(request.Target), request.AdminPassword, c.RequestAborted);
            return Results.Ok(new Wire.ResetResult(result.Username, result.TemporaryPassword, result.ExpiresUtc));
        });
        api.MapGet("/session", (HttpContext c) => Results.Ok(new { User = Info(ApiSessionFilter.Current(c)), ApiVersion = Wire.ApiContract.Version }))
            .WithMetadata(new SessionAccess(AllowTemporary: true, Touch: false));
        api.MapPost("/auth/logout", (HttpContext c, ApiSessions sessions) => { sessions.Remove(ApiSessionFilter.Current(c)); return Results.NoContent(); })
            .WithMetadata(new SessionAccess(AllowLocked: true, AllowTemporary: true, Touch: false));
        api.MapPost("/auth/lock", (HttpContext c, ApiSessions sessions) => { sessions.SetLocked(ApiSessionFilter.Current(c), true); return Results.NoContent(); })
            .WithMetadata(new SessionAccess(AllowLocked: true, AllowTemporary: true));
        api.MapPost("/auth/unlock", async (Wire.PasswordRequest request, HttpContext c, ApiSessions sessions) =>
        {
            var entry = ApiSessionFilter.Current(c);
            bool valid = await new AccountService(Db(c)).UnlockSessionAsync(entry.User, request.Password, c.RequestAborted);
            if (!valid) return ApiSessionFilter.Failure(c, 401, "INVALID_CREDENTIALS", "Doğrulama başarısız.");
            sessions.SetLocked(entry, false); return Results.Ok(Info(entry));
        }).WithMetadata(new SessionAccess(AllowLocked: true, AllowTemporary: true));
        api.MapPost("/auth/change-password", async (Wire.PasswordChangeRequest request, HttpContext c, ApiSessions sessions) =>
        {
            using var reason = AuditContext.UseReason(request.Reason);
            await new AccountService(Db(c)).ChangePasswordAsync(User(c), request.CurrentPassword, request.NewPassword, request.Confirmation, c.RequestAborted);
            sessions.RevokeUser(User(c).Username); return Results.NoContent();
        }).WithMetadata(new SessionAccess(AllowTemporary: true));
        api.MapGet("/health/ready", (HttpContext c) => { User(c).DemandAdmin(); return Results.Ok(new { Status = "ready", ApiVersion = Wire.ApiContract.Version, SchemaCompatible = true }); })
            .WithMetadata(new SessionAccess(Touch: false));

        api.MapGet("/personnel", async (HttpContext c, Wire.PersonnelGroup? group, long? index, int? size, string? tc, string? first, string? last, bool? includeArchived, bool? archivedOnly, bool? includeLeaves) =>
            Results.Ok(WireTables.Encode(await People(c).PageAsync(group.HasValue ? WireTables.Group(group.Value) : null, index ?? 0, size ?? 50, c.RequestAborted,
                tc ?? "", first ?? "", last ?? "", includeArchived ?? false, archivedOnly ?? false, includeLeaves ?? true))));
        api.MapPost("/personnel/save", async (Wire.PersonWrite request, HttpContext c) =>
        {
            using var reason = AuditContext.UseReason(request.Reason);
            await People(c).SaveAsync(WireTables.Group(request.Group), new(request.Tc, request.FirstName, request.LastName, request.Iban, request.Amount),
                request.Insert, c.RequestAborted, request.ExpectedVersion?.ToArray(), request.EmploymentStart); return Results.NoContent();
        });
        api.MapPost("/personnel/archive", async (Wire.PersonArchive request, HttpContext c) =>
        {
            using var reason = AuditContext.UseReason(request.Reason);
            await People(c).ArchiveAsync(WireTables.Group(request.Group), request.Tc, request.ExpectedVersion?.ToArray(), c.RequestAborted, request.EmploymentEnd); return Results.NoContent();
        });
        api.MapPost("/personnel/restore", async (Wire.PersonRestore request, HttpContext c) =>
        {
            using var reason = AuditContext.UseReason(request.Reason);
            await People(c).RestoreArchivedAsync(WireTables.Group(request.Group), request.PersonId, request.ExpectedVersion?.ToArray(), c.RequestAborted); return Results.NoContent();
        });
        api.MapGet("/personnel/{group}/{personId:guid}/history", async (Wire.PersonnelGroup group, Guid personId, HttpContext c) =>
        {
            var h = await People(c).HistoryAsync(WireTables.Group(group), personId, c.RequestAborted);
            return Results.Ok(new Wire.History(WireTables.Encode(h.Payments), WireTables.Encode(h.Leaves), WireTables.Encode(h.Events), WireTables.Encode(h.LeaveChanges)));
        });
        api.MapPost("/leaves/plan", async (Wire.LeavePlan request, HttpContext c) =>
        {
            if (request.Items is not { Length: > 0 and <= 200 }) throw new ArgumentException("Invalid plan size.");
            using var reason = AuditContext.UseReason(request.Reason);
            if (request.Items.Any(i => i == null)) throw new ArgumentException("Invalid plan item.");
            var plan = request.Items.Select(i => (new LeaveTarget(i.Tc, WireTables.Version(i.PersonVersion), i.LeaveVersion == null ? null : WireTables.Version(i.LeaveVersion)), new LeavePeriod(Day(i.Start), Day(i.End)))).ToArray();
            await People(c).SaveLeavesAsync(plan, c.RequestAborted); return Results.NoContent();
        });
        api.MapGet("/summary", async (HttpContext c, DateTime day, long? index, int? size) =>
        {
            var s = await People(c).SummaryAsync(Day(day), index ?? 0, size ?? 50, c.RequestAborted);
            return Results.Ok(new Wire.Summary(s.GeneralCount, s.JokerCount, s.OnLeaveCount, s.Balance, s.Average, WireTables.Encode(s.Alerts)));
        });
        api.MapGet("/ledger", async (HttpContext c, long? index, int? size) =>
            Results.Ok(WireTables.Encode(await People(c).LedgerPageAsync(index ?? 0, size ?? 50, c.RequestAborted))));
        api.MapPost("/payments/prepare", async (Wire.PaymentPrepare request, HttpContext c) =>
        {
            using var reason = AuditContext.UseReason(request.Reason);
            var purpose = WireTables.Purpose(request.Purpose);
            var prepared = purpose == PaymentPurpose.Advance
                ? await People(c).PrepareAdvanceAsync(WireTables.Group(request.Group), request.Tc, request.Amount, WireTables.Version(request.PersonVersion), request.ProjectId ?? throw new ArgumentException("Project required."), c.RequestAborted)
                : await People(c).PreparePaymentAsync(WireTables.Group(request.Group), request.Tc, request.Amount, WireTables.Version(request.PersonVersion), c.RequestAborted);
            if (purpose == PaymentPurpose.Settlement && request.ProjectId.HasValue) throw new ArgumentException("Project allocation of settlement is not supported.");
            return Results.Ok(WireTables.Payment(prepared));
        });
        api.MapPost("/payments/inspect", async (Wire.PaymentIntent request, HttpContext c) =>
        {
            var result = await People(c).InspectPaymentAsync(WireTables.Payment(request), c.RequestAborted);
            return Results.Ok(new Wire.PaymentResult(result.Status.ToString(), result.Reason));
        });
        api.MapPost("/payments/submit", async (Wire.PaymentIntent request, HttpContext c) =>
        {
            if (request.FormatVersion == 3 && request.Reason == null) throw new ArgumentException("Payment reason required.");
            var result = await People(c).SubmitPaymentAsync(WireTables.Payment(request), c.RequestAborted);
            return Results.Ok(new Wire.PaymentResult(result.Status.ToString(), result.Reason));
        });
        api.MapPost("/payments/reverse", async (Wire.PaymentReverse request, HttpContext c) =>
        {
            using var reason = AuditContext.UseReason(request.Reason);
            await People(c).ReversePaymentAsync(request.RequestId, c.RequestAborted); return Results.NoContent();
        });
        api.MapGet("/reports/receipt/{requestId:guid}", async (Guid requestId, HttpContext c) =>
            Results.Ok(WireTables.Encode(await new PaymentReportService(Db(c), User(c)).ReceiptAsync(requestId, c.RequestAborted))));
        api.MapGet("/reports/closing-preview", async (DateTime day, decimal declared, HttpContext c) =>
            Results.Ok(WireTables.Encode(await new PaymentReportService(Db(c), User(c)).ClosingAsync(Day(day), declared, c.RequestAborted))));

        api.MapGet("/projects", async (HttpContext c) => Results.Ok(WireTables.Encode(await Finance(c).ProjectsAsync(c.RequestAborted))));
        api.MapPost("/projects", async (Wire.ProjectCreate request, HttpContext c) =>
        {
            using var reason = AuditContext.UseReason(request.Reason);
            return Results.Ok(new { ProjectId = await Finance(c).CreateProjectAsync(request.Code, request.Name, c.RequestAborted) });
        });
        api.MapGet("/projects/{projectId:guid}/entries", async (Guid projectId, HttpContext c) => Results.Ok(WireTables.Encode(await Finance(c).EntriesAsync(projectId, c.RequestAborted))));
        api.MapPost("/finance/post", async (Wire.FinancePost request, HttpContext c) =>
        {
            using var reason = AuditContext.UseReason(request.Reason);
            return Results.Ok(new { EntryId = await Finance(c).PostAsync(new(request.ProjectId, request.Kind, request.Reference, request.PersonId,
                request.Group.HasValue ? WireTables.Group(request.Group.Value) : null, request.Tc, request.PersonVersion?.ToArray(),
                Day(request.Start), Day(request.End), request.Amount, request.Description), c.RequestAborted) });
        });
        api.MapPost("/finance/reverse", async (Wire.FinanceReverse request, HttpContext c) =>
        {
            using var reason = AuditContext.UseReason(request.Reason);
            await Finance(c).ReverseAsync(request.EntryId, WireTables.Version(request.ExpectedVersion), c.RequestAborted); return Results.NoContent();
        });
        api.MapGet("/closings", async (HttpContext c, long? index, int? size) => Results.Ok(WireTables.Encode(await Closings(c).PageAsync(index ?? 0, size ?? 50, c.RequestAborted))));
        api.MapPost("/closings/finalize", async (Wire.ClosingFinalize request, HttpContext c) =>
        {
            using var reason = AuditContext.UseReason(request.Reason);
            return Results.Ok(new { ClosingId = await Closings(c).FinalizeAsync(Day(request.Day), request.Declared, c.RequestAborted) });
        });
        api.MapGet("/closings/{id:guid}/summary", async (Guid id, HttpContext c) => Results.Ok(WireTables.Encode(await Closings(c).ExportSummaryAsync(id, c.RequestAborted))));
        api.MapGet("/closings/{id:guid}/view", async (Guid id, HttpContext c) => Results.Ok(await Closings(c).ReadAsync(id, c.RequestAborted)));
        api.MapGet("/closings/{id:guid}/lines", async (Guid id, HttpContext c) => Results.Ok(WireTables.Encode(await Closings(c).ExportLinesAsync(id, c.RequestAborted))));
        api.MapGet("/closings/{id:guid}/print", async (Guid id, HttpContext c) => Results.Ok(WireTables.Encode(await Closings(c).PrintAsync(id, c.RequestAborted))));

        api.MapPost("/accounts/person/read", async (Wire.AccountPersonRead request, HttpContext c) => Results.Ok(WireTables.Encode(await new AccountPersonService(Db(c), User(c)).ReadAsync(WireTables.Target(request.Target), c.RequestAborted))));
        api.MapPost("/accounts/person/change", async (Wire.AccountPersonChange request, HttpContext c) =>
        {
            await new AccountPersonService(Db(c), User(c)).ChangeAsync(request, c.RequestAborted); return Results.NoContent();
        });
        api.MapGet("/timesheets/{project:guid}/draft-context", async (Guid project,HttpContext c)=>Results.Ok(await new TimesheetService(Db(c),User(c)).DraftContextAsync(project,c.RequestAborted)));
        api.MapPost("/timesheets/drafts/preview-access",async (Wire.TimesheetDraftPreviewAccess request,HttpContext c)=>{await new TimesheetService(Db(c),User(c)).CheckDraftPreviewAsync(request,c.RequestAborted);return Results.NoContent();});
        api.MapPost("/timesheets/drafts/resolve", async (Wire.TimesheetDraftResolve request,HttpContext c)=>Results.Ok(await new TimesheetService(Db(c),User(c)).ResolveDraftAsync(request,c.RequestAborted)));
        api.MapGet("/accounts", async (HttpContext c) => Results.Ok(WireTables.Encode(await new AccountService(Db(c)).ListAsync(User(c), c.RequestAborted))));
        api.MapPost("/accounts/add", async (Wire.AccountAdd request, HttpContext c) =>
        {
            if (!Enum.IsDefined(request.Role)) throw new ArgumentException("Invalid role.");
            using var reason = AuditContext.UseReason(request.Reason);
            await new AccountService(Db(c)).AddAsync(User(c), request.Username, request.Password, (UserRole)request.Role, c.RequestAborted); return Results.NoContent();
        });
        api.MapPost("/accounts/active", async (Wire.AccountActive request, HttpContext c) =>
        {
            using var reason = AuditContext.UseReason(request.Reason);
            await new AccountService(Db(c)).SetActiveAsync(User(c), WireTables.Target(request.Target), request.Active, c.RequestAborted); return Results.NoContent();
        });
        api.MapPost("/accounts/permissions", async (Wire.AccountPermissions request, HttpContext c) =>
        {
            using var reason = AuditContext.UseReason(request.Reason);
            await new AccountService(Db(c)).SetPermissionsAsync(User(c), WireTables.Target(request.Target), request.CanPay, request.CanExportPersonalData, request.CanViewSalary, request.CanViewIban, c.RequestAborted); return Results.NoContent();
        });
        api.MapPost("/accounts/delete", async (Wire.AccountDelete request, HttpContext c) =>
        {
            using var reason = AuditContext.UseReason(request.Reason);
            await new AccountService(Db(c)).DeleteAsync(User(c), WireTables.Target(request.Target), c.RequestAborted); return Results.NoContent();
        });
    }
}
