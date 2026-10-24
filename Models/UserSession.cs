using System.Data;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

public enum UserRole { Admin, PersonnelManager, Viewer }
public sealed record UserSession(string Username, UserRole Role)
{
    public bool CanEdit => Role is UserRole.Admin or UserRole.PersonnelManager;
    public void DemandEdit() { if (!CanEdit) throw new UnauthorizedAccessException("Bu işlem için düzenleme yetkisi gerekiyor."); }
    public void DemandAdmin() { if (Role != UserRole.Admin) throw new UnauthorizedAccessException("Bu işlem için yönetici yetkisi gerekiyor."); }
    public string RoleLabel => Role switch { UserRole.Admin => "Yönetici", UserRole.PersonnelManager => "Personel sorumlusu", _ => "Görüntüleyici" };
}

