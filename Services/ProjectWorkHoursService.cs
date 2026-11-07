using System.Data;
using Microsoft.Data.SqlClient;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed record WorkforceComparisonData(DataTable Summary,Dictionary<string,(DataTable Rows,Wire.PageInfo Info)> Sections,DateTime ReadUtc,string Caption,string ReadBy,string Scope)
{
    internal Wire.WorkforceComparison Encode()=>new(RemoteApi.Encode(Summary),Sections.ToDictionary(p=>p.Key,p=>new Wire.Page(RemoteApi.Encode(p.Value.Rows),p.Value.Info.Total,p.Value.Info.Index,p.Value.Info.Size)),ReadUtc,Caption,ReadBy,Scope);
}
internal sealed class ProjectWorkHoursService(string connectionString,UserSession user)
{
    private const string Sources=@"WITH DatePlans AS(
 SELECT S.*,CONVERT(date,S.LocalStart) WorkDate,CONVERT(bigint,DATEDIFF(minute,S.LocalStart,S.LocalEnd)-S.BreakMinutes) NetPlanMinutes
 FROM dbo.PlannedShifts S WHERE S.ProjectId=@project AND (@fromDay IS NULL OR CONVERT(date,S.LocalStart)>=@fromDay) AND (@toDay IS NULL OR CONVERT(date,S.LocalStart)<=@toDay)),
 PlanRows AS(SELECT S.ShiftId,S.WorkDate,A.PersonId,A.EmploymentId,A.PersonnelGroup,A.ShiftAssignmentId,S.NetPlanMinutes,
 CONVERT(bigint,CASE WHEN EXISTS(SELECT 1 FROM dbo.Timesheets T WHERE T.ShiftAssignmentId=A.ShiftAssignmentId AND T.Status<>N'Cancelled') THEN 0 ELSE 1 END) MissingTimesheet
 FROM DatePlans S JOIN dbo.ShiftAssignments A ON A.ShiftId=S.ShiftId AND A.CancelledUtc IS NULL WHERE S.Status=N'Published' AND (/*PERSON:A*/)),
 RawActual AS(
 SELECT N'Whole' SourceKind,T.TimesheetId SourceId,T.TimesheetId,T.PersonId,T.EmploymentId,T.PersonnelGroup,T.WorkDate,T.Status ParentState,
 CASE WHEN T.Status=N'Approved' THEN N'Approved' WHEN T.Status=N'Submitted' THEN N'Pending' ELSE T.Status END EffectiveState,
 T.ActualMinutes NetMinutes,T.PayableMinutes,T.LocalStart,T.LocalEnd,T.BreakMinutes,T.ShiftAssignmentId,
 CASE WHEN SP.ProjectId=@project THEN SP.ShiftId END PlanShiftId,CONVERT(bit,CASE WHEN SP.ProjectId<>@project THEN 1 ELSE 0 END) ForeignPlan,T.TimeZoneId,T.PolicyVersion
 FROM dbo.Timesheets T LEFT JOIN dbo.ShiftAssignments SA ON SA.ShiftAssignmentId=T.ShiftAssignmentId LEFT JOIN dbo.PlannedShifts SP ON SP.ShiftId=SA.ShiftId
 WHERE T.ProjectId=@project AND NOT EXISTS(SELECT 1 FROM dbo.TimesheetAllocationSets H WHERE H.TimesheetId=T.TimesheetId AND H.ReleasedUtc IS NULL)
 UNION ALL SELECT N'Segment',S.SegmentId,T.TimesheetId,T.PersonId,T.EmploymentId,T.PersonnelGroup,T.WorkDate,T.Status,
 CASE WHEN T.Status IN(N'Cancelled',N'Draft',N'Returned') THEN T.Status
 WHEN H.UsesSegmentApproval=0 THEN CASE WHEN T.Status=N'Approved' THEN N'Approved' ELSE N'Pending' END
 WHEN R.RoundId IS NOT NULL AND D.State=N'Approved' THEN N'Approved' WHEN R.RoundId IS NOT NULL AND D.State=N'Pending' THEN N'Pending' ELSE N'Unresolved' END,
 S.NetMinutes,S.PayableMinutes,S.LocalStart,S.LocalEnd,S.BreakMinutes,T.ShiftAssignmentId,
 CASE WHEN SP.ProjectId=@project THEN SP.ShiftId END,CONVERT(bit,CASE WHEN SP.ProjectId<>@project THEN 1 ELSE 0 END),H.TimeZoneId,H.PolicyVersion
 FROM dbo.TimesheetSegments S JOIN dbo.TimesheetAllocationSets H ON H.SetId=S.SetId AND H.IsSealed=1 AND H.ReleasedUtc IS NULL JOIN dbo.Timesheets T ON T.TimesheetId=H.TimesheetId
 LEFT JOIN dbo.TimesheetApprovalRounds R ON R.SetId=H.SetId AND R.IsReady=1 AND R.ClosedUtc IS NULL LEFT JOIN dbo.TimesheetSegmentApprovals D ON D.RoundId=R.RoundId AND D.SegmentId=S.SegmentId
 LEFT JOIN dbo.ShiftAssignments SA ON SA.ShiftAssignmentId=T.ShiftAssignmentId LEFT JOIN dbo.PlannedShifts SP ON SP.ShiftId=SA.ShiftId WHERE S.ProjectId=@project),
 Actual AS(SELECT A.*,CONVERT(bigint,CASE WHEN A.EffectiveState=N'Approved' AND A.NetMinutes>=0 THEN A.NetMinutes ELSE 0 END) ApprovedMinutes,
 CONVERT(bigint,CASE WHEN A.EffectiveState=N'Pending' AND A.NetMinutes>=0 THEN A.NetMinutes ELSE 0 END) PendingMinutes,
 CONVERT(bigint,CASE WHEN A.EffectiveState NOT IN(N'Approved',N'Pending',N'Cancelled') AND A.NetMinutes>=0 THEN A.NetMinutes ELSE 0 END) DraftMinutes,
 CONVERT(bigint,CASE WHEN A.EffectiveState=N'Approved' AND A.ParentState=N'Approved' AND A.PayableMinutes>=0 THEN A.PayableMinutes ELSE 0 END) ReadyPayableMinutes,
 CONVERT(bigint,CASE WHEN A.EffectiveState<>N'Cancelled' AND (A.NetMinutes IS NULL OR A.NetMinutes<0) THEN 1 ELSE 0 END) IncompleteCount
 FROM RawActual A WHERE (@fromDay IS NULL OR A.WorkDate>=@fromDay) AND (@toDay IS NULL OR A.WorkDate<=@toDay) AND (/*PERSON:A*/)),
 Moves AS(
 SELECT P.WorkDate,P.PersonId,P.EmploymentId,P.PersonnelGroup,P.NetPlanMinutes PlannedMinutes,CONVERT(bigint,0) ApprovedMinutes,CONVERT(bigint,0) PendingMinutes,CONVERT(bigint,0) DraftMinutes,CONVERT(bigint,0) ReadyPayableMinutes,P.MissingTimesheet,CONVERT(bigint,0) UnscheduledMinutes,CONVERT(bigint,0) ForeignPlanMinutes,CONVERT(bigint,0) IncompleteCount FROM PlanRows P
 UNION ALL SELECT A.WorkDate,A.PersonId,A.EmploymentId,A.PersonnelGroup,0,A.ApprovedMinutes,A.PendingMinutes,A.DraftMinutes,A.ReadyPayableMinutes,0,CASE WHEN A.ShiftAssignmentId IS NULL THEN A.ApprovedMinutes ELSE 0 END,CASE WHEN A.ForeignPlan=1 THEN A.ApprovedMinutes ELSE 0 END,A.IncompleteCount FROM Actual A)
 ";
    internal static readonly IReadOnlyDictionary<string,string> SectionNames=new Dictionary<string,string>{["Days"]="Günler",["People"]="Kişi / çalışma dönemi",["Shifts"]="Plan ayrıntıları",["Actual"]="Gerçekleşen kaynaklar",["Issues"]="Eksik kayıt ve istisnalar"};
    private static string Person(string alias)=>$@"(@person=N'' OR {alias}.PersonId=TRY_CONVERT(uniqueidentifier,@person)
 OR EXISTS(SELECT 1 FROM dbo.Calisan C WHERE C.PersonId={alias}.PersonId AND C.AD+N' '+C.SOYAD LIKE @pattern ESCAPE N'~')
 OR EXISTS(SELECT 1 FROM dbo.Joker C WHERE C.PersonId={alias}.PersonId AND C.AD+N' '+C.SOYAD LIKE @pattern ESCAPE N'~')
 OR EXISTS(SELECT 1 FROM dbo.AccrualCalculationLines L JOIN dbo.AccrualCalculations B ON B.CalculationId=L.CalculationId WHERE B.ProjectId=@project AND L.PersonId={alias}.PersonId AND L.PersonCaption LIKE @pattern ESCAPE N'~'))";
    private const string Totals=@"COALESCE(SUM(PlannedMinutes),0) PlannedMinutes,COALESCE(SUM(ApprovedMinutes),0) ApprovedMinutes,COALESCE(SUM(PendingMinutes),0) PendingMinutes,COALESCE(SUM(DraftMinutes),0) DraftMinutes,COALESCE(SUM(ReadyPayableMinutes),0) ReadyPayableMinutes,COALESCE(SUM(MissingTimesheet),0) MissingTimesheet,COALESCE(SUM(UnscheduledMinutes),0) UnscheduledMinutes,COALESCE(SUM(ForeignPlanMinutes),0) ForeignPlanMinutes,COALESCE(SUM(IncompleteCount),0) IncompleteCount";
    private static string Hours(string alias)=>string.Join(",",new[]{"PlannedMinutes","ApprovedMinutes","PendingMinutes","DraftMinutes","ReadyPayableMinutes","UnscheduledMinutes","ForeignPlanMinutes"}.Select(n=>$"CONVERT(decimal(28,2),CONVERT(decimal(28,6),{alias}.{n})/60) {n.Replace("Minutes","Hours")}"))+$",{alias}.ApprovedMinutes-{alias}.PlannedMinutes VarianceMinutes,CONVERT(decimal(28,2),CONVERT(decimal(28,6),{alias}.ApprovedMinutes-{alias}.PlannedMinutes)/60) VarianceHours";
    internal async Task<Wire.WorkforceComparison> ReadAsync(Wire.ReconciliationBrowse input,CancellationToken ct)
    {
        _ =new FinancialQueryScope(input.Filter);
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).SendAsync<Wire.WorkforceComparison>("project-work-hours/browse",input,ct);
        for(int attempt=0;;attempt++)try{await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);var report=await BuildAsync(w,user,input,ct);await w.CommitAsync(ct);return report.Encode();}catch(SqlException ex)when(ex.Number==1205&&attempt<2){await Task.Delay(150*(attempt+1),ct);}
    }
    internal static async Task<WorkforceComparisonData> BuildAsync(IUnitOfWork w,UserSession user,Wire.ReconciliationBrowse input,CancellationToken ct,string? exportSection=null)
    {
        var scope=new FinancialQueryScope(input.Filter);await TimesheetService.AllocationGateAsync(w,ct);await ProjectAuthorization.DemandAsync(w,user,input.ProjectId,ProjectAccess.Read,ct);
        string caption;using(var c=w.Command("SELECT Code+N' · '+Name FROM dbo.FinanceProjects WHERE ProjectId=@project")){c.Parameters.Add("@project",SqlDbType.UniqueIdentifier).Value=input.ProjectId;caption=(string)(await c.ExecuteScalarAsync(ct)??throw new InvalidOperationException("Proje bulunamadı."));}
        void Bind(SqlCommand c){c.Parameters.Add("@project",SqlDbType.UniqueIdentifier).Value=input.ProjectId;scope.Bind(c);c.Parameters.Add("@fromDay",SqlDbType.Date).Value=(object?)scope.Filter.From??DBNull.Value;c.Parameters.Add("@toDay",SqlDbType.Date).Value=(object?)scope.Filter.To??DBNull.Value;}
        string Sql(string body)=>(Sources+body).Replace("/*PERSON:A*/",Person("A")).Replace("/*PERSON:T*/",Person("T"));
        var summarySql=Sql(@", G AS(SELECT "+Totals+@" FROM Moves),D AS(SELECT COUNT_BIG(*) PublishedShiftCount,COALESCE(SUM(NetPlanMinutes*RequiredCount),0) RequiredMinutes FROM DatePlans WHERE Status=N'Published'),
 U AS(SELECT COALESCE(SUM(CONVERT(bigint,CASE WHEN S.RequiredCount>COALESCE(X.CountAssigned,0) THEN S.RequiredCount-COALESCE(X.CountAssigned,0) ELSE 0 END)),0) UnfilledSlots,
 COALESCE(SUM(S.NetPlanMinutes*CASE WHEN S.RequiredCount>COALESCE(X.CountAssigned,0) THEN S.RequiredCount-COALESCE(X.CountAssigned,0) ELSE 0 END),0) UnfilledMinutes
 FROM DatePlans S OUTER APPLY(SELECT COUNT_BIG(*) CountAssigned FROM dbo.ShiftAssignments A WHERE A.ShiftId=S.ShiftId AND A.CancelledUtc IS NULL) X WHERE S.Status=N'Published')
 SELECT G.*,"+Hours("G")+@",D.PublishedShiftCount,CASE WHEN @person=N'' THEN D.RequiredMinutes END RequiredMinutes,CASE WHEN @person=N'' THEN CONVERT(decimal(28,2),CONVERT(decimal(28,6),D.RequiredMinutes)/60) END RequiredHours,
 CASE WHEN @person=N'' THEN U.UnfilledSlots END UnfilledSlots,CASE WHEN @person=N'' THEN U.UnfilledMinutes END UnfilledMinutes,
 (SELECT COUNT_BIG(*) FROM PlanRows) AssignedSlots,(SELECT COUNT(DISTINCT PersonId) FROM Moves) DistinctPeople FROM G CROSS JOIN D CROSS JOIN U");
        DataTable summary;using(var c=w.Command(summarySql)){Bind(c);summary=await c.TableAsync(ct);}
        var statements=new Dictionary<string,string>();
        statements["Days"]=@", G AS(SELECT WorkDate,"+Totals+@" FROM Moves GROUP BY WorkDate),D AS(SELECT WorkDate,SUM(NetPlanMinutes*RequiredCount) RequiredMinutes FROM DatePlans WHERE Status=N'Published' GROUP BY WorkDate)
 SELECT TOP(10001) COALESCE(G.WorkDate,D.WorkDate) WorkDate,COALESCE(G.PlannedMinutes,0) PlannedMinutes,COALESCE(G.ApprovedMinutes,0) ApprovedMinutes,COALESCE(G.PendingMinutes,0) PendingMinutes,COALESCE(G.DraftMinutes,0) DraftMinutes,COALESCE(G.ReadyPayableMinutes,0) ReadyPayableMinutes,COALESCE(G.MissingTimesheet,0) MissingTimesheet,COALESCE(G.IncompleteCount,0) IncompleteCount,
 CONVERT(decimal(28,2),CONVERT(decimal(28,6),COALESCE(G.PlannedMinutes,0))/60) PlannedHours,CONVERT(decimal(28,2),CONVERT(decimal(28,6),COALESCE(G.ApprovedMinutes,0))/60) ApprovedHours,
 COALESCE(G.ApprovedMinutes,0)-COALESCE(G.PlannedMinutes,0) VarianceMinutes,CONVERT(decimal(28,2),CONVERT(decimal(28,6),COALESCE(G.ApprovedMinutes,0)-COALESCE(G.PlannedMinutes,0))/60) VarianceHours,
 CASE WHEN @person=N'' THEN COALESCE(D.RequiredMinutes,0) END RequiredMinutes FROM G FULL OUTER JOIN D ON D.WorkDate=G.WorkDate WHERE @person=N'' OR G.WorkDate IS NOT NULL ORDER BY WorkDate";
        statements["People"]=@", G AS(SELECT PersonId,EmploymentId,PersonnelGroup,"+Totals+@" FROM Moves GROUP BY PersonId,EmploymentId,PersonnelGroup)
 SELECT TOP(10001) CONVERT(nvarchar(36),G.PersonId) PersonReference,CONVERT(nvarchar(36),G.EmploymentId) EmploymentReference,COALESCE(C.AD+N' '+C.SOYAD,J.AD+N' '+J.SOYAD,N'Güncel ad bulunamadı') PersonCaption,G.PersonnelGroup,G.PlannedMinutes,G.ApprovedMinutes,G.PendingMinutes,G.DraftMinutes,G.ReadyPayableMinutes,G.MissingTimesheet,G.UnscheduledMinutes,G.ForeignPlanMinutes,G.IncompleteCount,"+Hours("G")+@"
 FROM G LEFT JOIN dbo.Calisan C ON C.PersonId=G.PersonId AND G.PersonnelGroup=0 LEFT JOIN dbo.Joker J ON J.PersonId=G.PersonId AND G.PersonnelGroup=1 ORDER BY PersonCaption,G.PersonId,G.EmploymentId,G.PersonnelGroup";
        statements["Shifts"]=@"SELECT TOP(10001) CONVERT(nvarchar(36),S.ShiftId) ShiftReference,S.WorkDate,S.Name,S.Site,S.Status,S.LocalStart,S.LocalEnd,S.BreakMinutes,S.RequiredCount,S.NetPlanMinutes,S.TimeZoneId,S.PolicyVersion,
 CONVERT(decimal(28,2),CONVERT(decimal(28,6),S.NetPlanMinutes)/60) PerPersonPlannedHours,
 (SELECT COUNT_BIG(*) FROM dbo.ShiftAssignments A WHERE A.ShiftId=S.ShiftId AND A.CancelledUtc IS NULL) AllAssignedCount,
 (SELECT COUNT_BIG(*) FROM PlanRows P WHERE P.ShiftId=S.ShiftId) SelectedAssignedCount,(SELECT COALESCE(SUM(P.MissingTimesheet),0) FROM PlanRows P WHERE P.ShiftId=S.ShiftId) MissingTimesheet,
 (SELECT COALESCE(SUM(A.ApprovedMinutes),0) FROM Actual A WHERE A.PlanShiftId=S.ShiftId) ApprovedMinutes,
 (SELECT COALESCE(SUM(A.PendingMinutes),0) FROM Actual A WHERE A.PlanShiftId=S.ShiftId) PendingMinutes
 FROM DatePlans S WHERE @person=N'' OR EXISTS(SELECT 1 FROM PlanRows P WHERE P.ShiftId=S.ShiftId) ORDER BY S.WorkDate,S.LocalStart,S.ShiftId";
        statements["Actual"]=@"SELECT TOP(10001) A.SourceKind,CONVERT(nvarchar(36),A.SourceId) SourceReference,CONVERT(nvarchar(36),A.TimesheetId) TimesheetReference,CONVERT(nvarchar(36),A.PersonId) PersonReference,CONVERT(nvarchar(36),A.EmploymentId) EmploymentReference,A.PersonnelGroup,A.WorkDate,A.ParentState,A.EffectiveState,A.TimeZoneId,A.PolicyVersion,A.LocalStart,A.LocalEnd,A.BreakMinutes,A.NetMinutes,A.PayableMinutes,A.ApprovedMinutes,A.PendingMinutes,A.DraftMinutes,A.ReadyPayableMinutes,A.IncompleteCount,A.ForeignPlan,CONVERT(bit,CASE WHEN A.ShiftAssignmentId IS NULL THEN 1 ELSE 0 END) IsUnscheduled FROM Actual A ORDER BY A.WorkDate,A.PersonId,A.EmploymentId,A.SourceKind,A.SourceId";
        statements["Issues"]=@"SELECT TOP(10001) Issue,RecordReference,WorkDate,CONVERT(nvarchar(36),PersonId) PersonReference FROM(
 SELECT N'Yayımlanmış kişi planında etkin puantaj yok' Issue,CONVERT(nvarchar(64),P.ShiftAssignmentId) RecordReference,P.WorkDate,P.PersonId FROM PlanRows P WHERE P.MissingTimesheet=1
 UNION ALL SELECT N'Kaynak süre eksik veya geçersiz',CONVERT(nvarchar(64),A.SourceId),A.WorkDate,A.PersonId FROM Actual A WHERE A.IncompleteCount>0
 UNION ALL SELECT N'Proje onayı bekliyor',CONVERT(nvarchar(64),A.SourceId),A.WorkDate,A.PersonId FROM Actual A WHERE A.EffectiveState=N'Pending'
 UNION ALL SELECT N'Taslak / iade / çözümlenmemiş onay',CONVERT(nvarchar(64),A.SourceId),A.WorkDate,A.PersonId FROM Actual A WHERE A.EffectiveState IN(N'Draft',N'Returned',N'Unresolved')
 UNION ALL SELECT N'Proje parçası onaylı; ana puantaj henüz tamamen onaylı değil',CONVERT(nvarchar(64),A.SourceId),A.WorkDate,A.PersonId FROM Actual A WHERE A.EffectiveState=N'Approved' AND A.ParentState<>N'Approved'
 UNION ALL SELECT N'Tamamlanmamış etkin dağılım; kaynak saatlere katılmadı',CONVERT(nvarchar(64),T.TimesheetId),T.WorkDate,T.PersonId
 FROM dbo.Timesheets T JOIN dbo.TimesheetAllocationSets H ON H.TimesheetId=T.TimesheetId AND H.ReleasedUtc IS NULL AND H.IsSealed=0
 WHERE (T.ProjectId=@project OR EXISTS(SELECT 1 FROM dbo.TimesheetSegments S WHERE S.SetId=H.SetId AND S.ProjectId=@project)) AND T.Status<>N'Cancelled' AND (@fromDay IS NULL OR T.WorkDate>=@fromDay) AND (@toDay IS NULL OR T.WorkDate<=@toDay) AND (/*PERSON:T*/)
 UNION ALL SELECT N'İptal edilmiş plana bağlı onaylı gerçekleşme',CONVERT(nvarchar(64),A.SourceId),A.WorkDate,A.PersonId FROM Actual A JOIN DatePlans P ON P.ShiftId=A.PlanShiftId WHERE P.Status=N'Cancelled' AND A.ApprovedMinutes>0) X ORDER BY WorkDate,Issue,RecordReference";
        var sections=new Dictionary<string,(DataTable,Wire.PageInfo)>();foreach(var pair in statements){var request=input.Pages?.GetValueOrDefault(pair.Key)??new();var page=await FinancialQueryScope.ReadPageAsync(w,Sql(pair.Value),Bind,request.Index,request.Size,ct,exportSection==pair.Key);sections[pair.Key]=(page.Rows,page.Info);}
        DateTime read;using(var c=w.Command("SELECT SYSUTCDATETIME()"))read=DateTime.SpecifyKind((DateTime)(await c.ExecuteScalarAsync(ct))!,DateTimeKind.Utc);
        string description=$"İş günü / vardiya başlangıç günü: {scope.Filter.From?.ToString("dd.MM.yyyy")??"açık"} – {scope.Filter.To?.ToString("dd.MM.yyyy")??"açık"}; kişi: {(scope.Filter.Person.Length==0?"tümü":scope.Filter.Person)}; güncel plan ve onay durumu";
        return new(summary,sections,read,caption,user.Username,description);
    }
}
