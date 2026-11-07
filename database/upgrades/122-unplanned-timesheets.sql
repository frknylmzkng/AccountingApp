-- Keep existing planned rows unchanged. NULL denotes a genuine unplanned work record.
DECLARE @uq sysname,@fk sysname,@ddl nvarchar(max),@table int=OBJECT_ID(N'dbo.Timesheets');
SELECT @uq=K.name FROM sys.key_constraints K
WHERE K.parent_object_id=@table AND K.type=N'UQ'
 AND 1=(SELECT COUNT(*) FROM sys.index_columns C WHERE C.object_id=@table AND C.index_id=K.unique_index_id AND C.key_ordinal>0)
 AND EXISTS(SELECT 1 FROM sys.index_columns C JOIN sys.columns X ON X.object_id=C.object_id AND X.column_id=C.column_id WHERE C.object_id=@table AND C.index_id=K.unique_index_id AND C.key_ordinal=1 AND X.name=N'ShiftAssignmentId');
IF @uq IS NULL THROW 51140,N'Beklenen vardiya tekillik kısıtı bulunamadı.',1;
SET @ddl=N'ALTER TABLE dbo.Timesheets DROP CONSTRAINT '+QUOTENAME(@uq);
EXEC sys.sp_executesql @ddl;
SELECT @fk=F.name FROM sys.foreign_keys F JOIN sys.foreign_key_columns C ON C.constraint_object_id=F.object_id JOIN sys.columns X ON X.object_id=C.parent_object_id AND X.column_id=C.parent_column_id WHERE F.parent_object_id=@table AND X.name=N'ShiftAssignmentId';
IF @fk IS NULL THROW 51141,N'Beklenen vardiya bağlantısı bulunamadı.',1;
SET @ddl=N'ALTER TABLE dbo.Timesheets DROP CONSTRAINT '+QUOTENAME(@fk);
EXEC sys.sp_executesql @ddl;
ALTER TABLE dbo.Timesheets ALTER COLUMN ShiftAssignmentId uniqueidentifier NULL;
ALTER TABLE dbo.Timesheets WITH CHECK ADD CONSTRAINT FK_TimesheetShiftAssignment FOREIGN KEY(ShiftAssignmentId) REFERENCES dbo.ShiftAssignments(ShiftAssignmentId);
CREATE UNIQUE INDEX UX_TimesheetPlannedAssignment ON dbo.Timesheets(ShiftAssignmentId) WHERE ShiftAssignmentId IS NOT NULL;
ALTER TABLE dbo.Timesheets WITH CHECK ADD CONSTRAINT CK_UnplannedTimesheetWorkDate CHECK(ShiftAssignmentId IS NOT NULL OR LocalStart IS NULL OR CONVERT(date,LocalStart)=WorkDate);
GO
CREATE OR ALTER TRIGGER dbo.GuardTimesheets ON dbo.Timesheets AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.TimesheetId=D.TimesheetId WHERE I.TimesheetId IS NULL) THROW 51131,N'Puantaj silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I LEFT JOIN deleted D ON D.TimesheetId=I.TimesheetId WHERE D.TimesheetId IS NULL AND I.Status<>N'Draft') THROW 51132,N'Puantaj taslak başlamalı.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.TimesheetId=I.TimesheetId WHERE EXISTS(
 SELECT I.ProjectId,I.ShiftAssignmentId,I.PersonId,I.EmploymentId,I.PersonnelGroup,I.RosterAssignmentId,I.WorkDate,I.SourceKey COLLATE Latin1_General_100_BIN2,I.TimeZoneId COLLATE Latin1_General_100_BIN2,I.PolicyVersion COLLATE Latin1_General_100_BIN2,I.CreatedByAccountId,I.CreatedBy COLLATE Latin1_General_100_BIN2,I.CreatedUtc
 EXCEPT SELECT D.ProjectId,D.ShiftAssignmentId,D.PersonId,D.EmploymentId,D.PersonnelGroup,D.RosterAssignmentId,D.WorkDate,D.SourceKey COLLATE Latin1_General_100_BIN2,D.TimeZoneId COLLATE Latin1_General_100_BIN2,D.PolicyVersion COLLATE Latin1_General_100_BIN2,D.CreatedByAccountId,D.CreatedBy COLLATE Latin1_General_100_BIN2,D.CreatedUtc))
 THROW 51133,N'Puantajın kaynak kimliği değiştirilemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.TimesheetId=I.TimesheetId WHERE NOT(
 (D.Status IN(N'Draft',N'Returned') AND I.Status IN(N'Draft',N'Returned',N'Submitted',N'Cancelled')) OR
 (D.Status=N'Submitted' AND I.Status IN(N'Approved',N'Returned')) OR (D.Status=N'Approved' AND I.Status=N'Returned')))
 THROW 51134,N'Geçersiz puantaj durum geçişi.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.TimesheetId=I.TimesheetId WHERE (D.Status NOT IN(N'Draft',N'Returned') OR I.Status<>D.Status) AND EXISTS(
 SELECT I.LocalStart,I.LocalEnd,I.StartUtc,I.EndUtc,I.BreakMinutes,I.PayableMinutes,I.EditedByAccountId,I.EditedBy,I.EditedUtc
 EXCEPT SELECT D.LocalStart,D.LocalEnd,D.StartUtc,D.EndUtc,D.BreakMinutes,D.PayableMinutes,D.EditedByAccountId,D.EditedBy,D.EditedUtc))
 THROW 51135,N'Gönderilmiş puantaj gövdesi değiştirilemez; önce iade/yeniden açma gerekir.',1;
END;
GO
UPDATE dbo.admin SET SessionEpoch=NEWID();
