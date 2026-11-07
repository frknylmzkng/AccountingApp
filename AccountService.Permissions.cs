using System.Data;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

internal sealed partial class AccountService
{
    public Task SetPermissionsAsync(UserSession user, AccountTarget? selected, bool canPay, bool canExportPersonalData, CancellationToken token = default)
        => SetPermissionsAsync(user, selected, canPay, canExportPersonalData, null, null, token);
    public async Task SetPermissionsAsync(UserSession user, AccountTarget? selected, bool canPay, bool canExportPersonalData, bool? canViewSalary, bool? canViewIban, CancellationToken token = default)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("accounts/permissions", new AccountingApp.Contracts.AccountPermissions(RemoteApi.Target(selected), canPay, canExportPersonalData, RemoteApi.Reason, canViewSalary, canViewIban), token).ConfigureAwait(false); return; }
        user.DemandAdmin(); var target = AccountTarget.RequireSnapshot(selected);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.LockAccountChangesAsync(work, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Admin, token).ConfigureAwait(false);
        bool previousPay, previousExport, previousSalary, previousIban; Guid epoch;
        using (var find = work.Command("SELECT RoleName,CanPay,CanExportPersonalData,SessionEpoch,CanViewSalary,CanViewIban FROM dbo.admin WITH(UPDLOCK,HOLDLOCK) WHERE username=@name AND SessionVersion=@version AND SessionEpoch=@epoch"))
        {
            find.Text("@name", target.Username); find.Parameters.Add("@version", SqlDbType.Binary, 8).Value = target.Version;
            find.Parameters.Add("@epoch", SqlDbType.UniqueIdentifier).Value = target.Epoch;
            using var r = await find.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (!await r.ReadAsync(token).ConfigureAwait(false)) throw new InvalidOperationException(AccountTarget.ConflictMessage);
            if (r.GetString(0) == "Admin")
            {
                if (!canPay || !canExportPersonalData || canViewSalary == false || canViewIban == false) throw new InvalidOperationException("Yönetici rolü bu yetkileri her zaman içerir. Bu ekrandan yönetici yetkileri azaltılamaz.");
                return;
            }
            if (r.GetString(0) == "ProjectUser" && (canPay || canExportPersonalData)) throw new InvalidOperationException("Proje kullanıcısına genel ödeme/dışa aktarma yetkisi verilemez. Proje ekranındaki kapsamlı yetkileri kullanın.");
            previousSalary = r.GetBoolean(4); previousIban = r.GetBoolean(5);
            canViewSalary ??= previousSalary; canViewIban ??= previousIban;
            if (canPay && canViewSalary != true) throw new InvalidOperationException("Ödeme için ücret/bakiye görüntüleme de açık olmalı.");
            previousPay = r.GetBoolean(1); previousExport = r.GetBoolean(2); epoch = r.GetGuid(3);
            if (await r.ReadAsync(token).ConfigureAwait(false)) throw new InvalidOperationException("Hesap tekil değil.");
        }
        if (previousPay == canPay && previousExport == canExportPersonalData && previousSalary == canViewSalary && previousIban == canViewIban) return;
        using var update = work.Command("UPDATE dbo.admin SET CanPay=@pay,CanExportPersonalData=@export,CanViewSalary=@salary,CanViewIban=@iban,SessionEpoch=NEWID() WHERE username=@name AND SessionVersion=@version AND SessionEpoch=@epoch");
        update.Text("@name", target.Username); update.Parameters.Add("@version", SqlDbType.Binary, 8).Value = target.Version;
        update.Parameters.Add("@epoch", SqlDbType.UniqueIdentifier).Value = target.Epoch;
        update.Parameters.Add("@pay", SqlDbType.Bit).Value = canPay; update.Parameters.Add("@export", SqlDbType.Bit).Value = canExportPersonalData;
        update.Parameters.Add("@salary", SqlDbType.Bit).Value = canViewSalary!.Value; update.Parameters.Add("@iban", SqlDbType.Bit).Value = canViewIban!.Value;
        if (await update.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) throw new InvalidOperationException(AccountTarget.ConflictMessage);
        await LoginThrottle.ClearAsync(work, epoch, token).ConfigureAwait(false);
        await new PersonnelService(connectionString, user).AuditAsync(work, "Account.PermissionsChange", token).ConfigureAwait(false);
        await work.CommitAsync(token).ConfigureAwait(false);
    }
}
