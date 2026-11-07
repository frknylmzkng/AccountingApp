using System.Data;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed partial class WageRuleService
{
    internal async Task<DataTable> TargetsAsync(Guid project,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).TableAsync($"wage-rules/{project:D}/targets",ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,project,true,ct);using var c=w.Command(@"SELECT DISTINCT TOP(2001) E.EmploymentId,E.PersonId,E.RecordVersion EmploymentVersion,E.StartDate,E.EndDate,E.PersonnelGroup,C.AD,C.SOYAD FROM dbo.EmploymentPeriods E
 JOIN(SELECT PersonId,CurrentEmploymentId,AD,SOYAD,ArchivedUtc,0 PersonnelGroup FROM dbo.Calisan UNION ALL SELECT PersonId,CurrentEmploymentId,AD,SOYAD,ArchivedUtc,1 FROM dbo.Joker) C ON C.PersonId=E.PersonId AND C.CurrentEmploymentId=E.EmploymentId AND C.PersonnelGroup=E.PersonnelGroup
 WHERE E.IsClosed=0 AND C.ArchivedUtc IS NULL AND EXISTS(SELECT 1 FROM dbo.PersonnelProjectAssignments A JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Assignment' AND L.RecordKey=CONVERT(nvarchar(64),A.AssignmentId) WHERE A.ProjectId=@project AND L.EmploymentId=E.EmploymentId) ORDER BY E.PersonId,E.EmploymentId");Id(c,"@project",project);var rows=await c.TableAsync(ct);if(rows.Rows.Count>2000)throw new InvalidOperationException("Hedef sayısı 2000 sınırını aşıyor.");await w.CommitAsync(ct);return rows;
    }
    internal async Task<DataTable> BasisAsync(Wire.WageBasisBrowse input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return RemoteApi.Decode(await RemoteApi.For(connectionString,user).SendAsync<Wire.Table>("wage-rules/basis",input,ct));
        Date(input.From);Date(input.To);if(input.From>input.To||(input.To-input.From).Days>365)throw new ArgumentException("Kaynak aralığı en fazla 366 gün olmalı.");await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,input.ProjectId,true,ct);
        using var c=w.Command(@"SELECT TOP(201) L.LineId,L.WorkDate,L.PaidMinutes,L.HourlyAmount,L.Amount,C.CalculationId FROM dbo.AccrualCalculationLines L JOIN dbo.AccrualCalculations C ON C.CalculationId=L.CalculationId JOIN dbo.AccrualPostings P ON P.CalculationId=C.CalculationId AND P.IsSealed=1 JOIN dbo.AccrualItemBalances I ON I.PostingId=P.PostingId AND I.EmploymentId=L.EmploymentId
 WHERE C.ProjectId=@project AND L.EmploymentId=@employment AND L.WorkDate BETWEEN @from AND @to AND L.PaidMinutes>0 AND I.FinanceEntryId IS NOT NULL AND I.EffectiveAmount>0 AND NOT EXISTS(SELECT 1 FROM dbo.AccrualCorrectionTransfers T WHERE T.SourceItemId=I.ItemId) AND NOT EXISTS(SELECT 1 FROM dbo.WageClaims W WHERE W.PremiumLineId=L.LineId AND W.IsReserved=1) ORDER BY L.WorkDate DESC,L.LineId");Id(c,"@project",input.ProjectId);Id(c,"@employment",input.EmploymentId);Day(c,"@from",input.From);Day(c,"@to",input.To);var rows=await c.TableAsync(ct);if(rows.Rows.Count>200)throw new InvalidOperationException("Kaynak sayısı 200 sınırını aşıyor; daha dar tarih aralığı seçin.");await w.CommitAsync(ct);return rows;
    }
    internal async Task<Wire.Page> BrowseAsync(Wire.WageRuleBrowse input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).SendAsync<Wire.Page>("wage-rules/browse",input,ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,input.ProjectId,false,ct);
        string sql=input.Section switch
        {
            "Policies"=>@"SELECT TOP(500) P.PolicyId,P.PreviousPolicyId,P.RuleCode,P.Revision,P.Name,P.Kind,P.ValidFrom,P.ValidTo,P.UnitAmount,P.PremiumFactor,P.Divisor,P.CreatedBy,P.CreatedUtc,P.Reason,P.RecordVersion,D.Kind DecisionKind,D.Actor DecidedBy,D.CreatedUtc DecidedUtc,
 CAST(CASE WHEN @admin=1 AND D.DecisionId IS NULL AND dbo.SameAccountPerson(TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),P.CreatedByAccountId)=0 THEN 1 ELSE 0 END AS bit) CanDecide FROM dbo.WagePolicies P LEFT JOIN dbo.WagePolicyDecisions D ON D.PolicyId=P.PolicyId WHERE P.ProjectId=@project ORDER BY P.RuleCode,P.Revision DESC,P.PolicyId",
            "Claims"=>@"SELECT TOP(500) C.ClaimId,C.PolicyId,P.RuleCode,P.Revision,C.Kind,C.PersonId,C.EmploymentId,C.FromDay,C.ToDay,C.DocumentKey,C.Units,C.UnitAmount,C.PremiumFactor,C.Divisor,C.Amount,C.CreatedBy,C.CreatedUtc,C.Reason,C.RecordVersion,JSON_VALUE(C.SnapshotJson,'$.Caption') PersonCaption,
 CASE WHEN EXISTS(SELECT 1 FROM dbo.WageClaimTransfers T WHERE T.ClaimId=C.ClaimId AND T.Kind=N'Reverse') THEN N'Reversed'
 WHEN EXISTS(SELECT 1 FROM dbo.WageClaimActions A WHERE A.ClaimId=C.ClaimId AND A.Kind=N'ApproveReverse') THEN N'ReverseApproved'
 WHEN EXISTS(SELECT 1 FROM dbo.WageClaimActions A WHERE A.ClaimId=C.ClaimId AND A.Kind=N'RequestReverse') THEN N'ReverseRequested'
 WHEN EXISTS(SELECT 1 FROM dbo.WageClaimTransfers T WHERE T.ClaimId=C.ClaimId AND T.Kind=N'Post') THEN N'Posted'
 WHEN EXISTS(SELECT 1 FROM dbo.WageClaimActions A WHERE A.ClaimId=C.ClaimId AND A.Kind=N'Cancel') THEN N'Cancelled'
 WHEN EXISTS(SELECT 1 FROM dbo.WageClaimActions A WHERE A.ClaimId=C.ClaimId AND A.Kind=N'Reject') THEN N'Rejected'
 WHEN EXISTS(SELECT 1 FROM dbo.WageClaimActions A WHERE A.ClaimId=C.ClaimId AND A.Kind=N'Approve') THEN N'Approved' ELSE N'Prepared' END WorkflowState
 FROM dbo.WageClaims C JOIN dbo.WagePolicies P ON P.PolicyId=C.PolicyId WHERE C.ProjectId=@project ORDER BY C.CreatedUtc DESC,C.ClaimId",
            _=>throw new ArgumentException("Ücret/gider listesi geçersiz.")
        };
        var page=await FinancialQueryScope.ReadPageAsync(w,sql,c=>{Id(c,"@project",input.ProjectId);c.Parameters.Add("@admin",SqlDbType.Bit).Value=user.Role==UserRole.Admin;},input.Index,input.Size,ct);await w.CommitAsync(ct);return new(RemoteApi.Encode(page.Rows),page.Info.Total,page.Info.Index,page.Info.Size);
    }
    internal async Task<Wire.WageClaimView> ReadAsync(Wire.WageClaimRead input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).SendAsync<Wire.WageClaimView>("wage-rules/claim/read",input,ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,input.ProjectId,false,ct);var (h,e)=await ClaimAsync(w,input.ProjectId,input.ClaimId,ct);var header=new DataTable();header.Columns.Add("ClaimId",typeof(Guid));header.Columns.Add("CreatedBy");header.Columns.Add("Reason");header.Rows.Add(input.ClaimId,h["CreatedBy"],h["Reason"]);
        using var a=w.Command("SELECT ActionId,Kind,Actor,CreatedUtc,Reason FROM dbo.WageClaimActions WHERE ClaimId=@id ORDER BY CreatedUtc,ActionId");Id(a,"@id",input.ClaimId);var actions=await a.TableAsync(ct);
        using var t=w.Command("SELECT TransferId,Kind,FinanceEntryId,LedgerId,Amount,CASE WHEN @admin=1 THEN BeforeBalance ELSE NULL END BeforeBalance,CASE WHEN @admin=1 THEN AfterBalance ELSE NULL END AfterBalance,Actor,CreatedUtc,Reason FROM dbo.WageClaimTransfers WHERE ClaimId=@id ORDER BY CreatedUtc,TransferId");Id(t,"@id",input.ClaimId);t.Parameters.Add("@admin",SqlDbType.Bit).Value=user.Role==UserRole.Admin;var transfers=await t.TableAsync(ct);await w.CommitAsync(ct);return new(RemoteApi.Encode(header),RemoteApi.Encode(EvidenceTable(e)),RemoteApi.Encode(actions),RemoteApi.Encode(transfers));
    }
}
