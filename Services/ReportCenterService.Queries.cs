using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;
internal sealed partial class ReportCenterService
{
    private const string Dates="(@first IS NULL OR X.CreatedUtc>=@first) AND (@last IS NULL OR X.CreatedUtc<@last)";
    private const string Person="(@person=N'' OR X.PersonId=TRY_CONVERT(uniqueidentifier,@person) OR X.PersonCaption LIKE @pattern ESCAPE N'~')";
    // EXISTS, rather than a join, prevents overlapping/supplementary assignments
    // from multiplying money. Employment identity excludes recreated personnel.
    private const string Team=@"(@team IS NULL OR EXISTS(SELECT 1 FROM dbo.PersonnelProjectAssignments A
 JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Assignment' AND L.RecordKey=CONVERT(nvarchar(64),A.AssignmentId)
 WHERE A.ProjectId=@project AND A.TeamId=@team AND L.EmploymentId=X.EmploymentId AND A.CancelledUtc IS NULL
 AND A.StartDate<=X.ToDay AND (A.EndDate IS NULL OR A.EndDate>=X.FromDay)))";
    private const string Claims=@"WITH X AS(SELECT C.*,P.RuleCode,P.Revision,P.Name RuleName,JSON_VALUE(C.SnapshotJson,'$.Caption') PersonCaption,
 CASE WHEN EXISTS(SELECT 1 FROM dbo.WageClaimTransfers T WHERE T.ClaimId=C.ClaimId AND T.Kind=N'Reverse') THEN N'Reversed'
 WHEN EXISTS(SELECT 1 FROM dbo.WageClaimActions A WHERE A.ClaimId=C.ClaimId AND A.Kind=N'ApproveReverse') THEN N'ReverseApproved'
 WHEN EXISTS(SELECT 1 FROM dbo.WageClaimActions A WHERE A.ClaimId=C.ClaimId AND A.Kind=N'RequestReverse') THEN N'ReverseRequested'
 WHEN EXISTS(SELECT 1 FROM dbo.WageClaimTransfers T WHERE T.ClaimId=C.ClaimId AND T.Kind=N'Post') THEN N'Posted'
 WHEN EXISTS(SELECT 1 FROM dbo.WageClaimActions A WHERE A.ClaimId=C.ClaimId AND A.Kind=N'Cancel') THEN N'Cancelled'
 WHEN EXISTS(SELECT 1 FROM dbo.WageClaimActions A WHERE A.ClaimId=C.ClaimId AND A.Kind=N'Reject') THEN N'Rejected'
 WHEN EXISTS(SELECT 1 FROM dbo.WageClaimActions A WHERE A.ClaimId=C.ClaimId AND A.Kind=N'Approve') THEN N'Approved' ELSE N'Prepared' END WorkflowState
 FROM dbo.WageClaims C JOIN dbo.WagePolicies P ON P.PolicyId=C.PolicyId WHERE C.ProjectId=@project) ";
    private static string Sql(Wire.ReportCenterScope s)=>s.Report switch
    {
        "Assignments"=>@"WITH X AS(SELECT A.*,COALESCE(C.AD,J.AD,N'')+N' '+COALESCE(C.SOYAD,J.SOYAD,N'') PersonCaption,T.Name TeamName,L.EmploymentId
 FROM dbo.PersonnelProjectAssignments A LEFT JOIN dbo.ProjectTeams T ON T.TeamId=A.TeamId
 LEFT JOIN dbo.Calisan C ON C.PersonId=A.PersonId AND A.PersonnelGroup=0 LEFT JOIN dbo.Joker J ON J.PersonId=A.PersonId AND A.PersonnelGroup=1
 LEFT JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Assignment' AND L.RecordKey=CONVERT(nvarchar(64),A.AssignmentId)
 WHERE A.ProjectId=@project)
 SELECT TOP(500) X.AssignmentId,X.PersonId,X.EmploymentId,X.PersonCaption,X.TeamName,X.StartDate,X.EndDate,X.IsSupplementary,X.CreatedUtc,X.CreatedBy,X.CancelledUtc,X.CancelledBy
 FROM X WHERE "+Dates+" AND "+Person+" AND (@team IS NULL OR X.TeamId=@team) ORDER BY X.CreatedUtc DESC,X.AssignmentId",
        "Policies"=>@"SELECT TOP(500) X.PolicyId,X.RuleCode,X.Revision,X.Name,X.Kind,X.ValidFrom,X.ValidTo,X.UnitAmount,X.PremiumFactor,X.Divisor,X.CreatedUtc,X.CreatedBy,D.Kind DecisionKind,D.Actor DecidedBy,D.CreatedUtc DecidedUtc
 FROM dbo.WagePolicies X LEFT JOIN dbo.WagePolicyDecisions D ON D.PolicyId=X.PolicyId WHERE X.ProjectId=@project AND "+Dates+" AND (@pending=0 OR D.DecisionId IS NULL) ORDER BY X.CreatedUtc DESC,X.PolicyId",
        "Claims" or "Tasks"=>Claims+@"SELECT TOP(500) X.ClaimId,X.PersonId,X.EmploymentId,X.PersonCaption,X.RuleCode,X.Revision,X.RuleName,X.Kind,X.FromDay,X.ToDay,X.DocumentKey,X.Units,X.UnitAmount,X.PremiumFactor,X.Divisor,X.Amount,X.WorkflowState,
 CASE X.WorkflowState WHEN N'Prepared' THEN N'Bağımsız onay / ret bekliyor' WHEN N'Approved' THEN N'Deftere aktarım bekliyor' WHEN N'ReverseRequested' THEN N'Bağımsız ters hareket onayı bekliyor' WHEN N'ReverseApproved' THEN N'Ters defter aktarımı bekliyor' ELSE N'' END NextStep,
 X.CreatedUtc,X.CreatedBy,X.Reason FROM X WHERE "+Dates+" AND "+Person+" AND "+Team+" AND (@pending=0 OR X.WorkflowState IN(N'Prepared',N'Approved',N'ReverseRequested',N'ReverseApproved')) ORDER BY X.CreatedUtc DESC,X.ClaimId",
        "Transfers"=>@"WITH X AS(SELECT T.TransferId,T.ClaimId,T.Kind,T.Amount,T.FinanceEntryId,T.LedgerId,T.Actor,T.CreatedUtc,T.Reason,C.PersonId,C.EmploymentId,C.FromDay,C.ToDay,C.Kind ClaimKind,C.DocumentKey,P.RuleCode,JSON_VALUE(C.SnapshotJson,'$.Caption') PersonCaption
 FROM dbo.WageClaimTransfers T JOIN dbo.WageClaims C ON C.ClaimId=T.ClaimId JOIN dbo.WagePolicies P ON P.PolicyId=C.PolicyId WHERE C.ProjectId=@project)
 SELECT TOP(500) X.TransferId,X.ClaimId,X.PersonId,X.EmploymentId,X.PersonCaption,X.RuleCode,X.ClaimKind,X.DocumentKey,X.Kind,X.Amount,X.FinanceEntryId,X.LedgerId,X.Actor,X.CreatedUtc,X.Reason
 FROM X WHERE "+Dates+" AND "+Person+" AND "+Team+" ORDER BY X.CreatedUtc DESC,X.TransferId",
        _=>throw new ArgumentException("Rapor geçersiz.")
    };
}
