using System.Data;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

public enum UserRole { Admin, PersonnelManager, Viewer }
public sealed record UserSession(string Username, UserRole Role)
{
    internal byte[]? SecurityVersion { get; init; }
    internal Guid SecurityEpoch { get; init; }
    internal bool MustChangePassword { get; init; }
    public bool CanEdit => !MustChangePassword && (Role is UserRole.Admin or UserRole.PersonnelManager);
    public void DemandEdit() { if (MustChangePassword) throw new PasswordChangeRequiredException(); if (!CanEdit) throw new UnauthorizedAccessException("Bu işlem için düzenleme yetkisi gerekiyor."); }
    public void DemandAdmin() { if (MustChangePassword) throw new PasswordChangeRequiredException(); if (Role != UserRole.Admin) throw new UnauthorizedAccessException("Bu işlem için yönetici yetkisi gerekiyor."); }
    public string RoleLabel => Role switch { UserRole.Admin => "Yönetici", UserRole.PersonnelManager => "Personel sorumlusu", _ => "Görüntüleyici" };
}

