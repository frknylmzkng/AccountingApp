using System.Data;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

public enum UserRole { Admin, PersonnelManager, Viewer, ProjectUser }
public sealed record UserSession(string Username, UserRole Role)
{
    internal RemoteApi? Remote { get; init; }
    internal byte[]? SecurityVersion { get; init; }
    internal Guid SecurityEpoch { get; init; }
    internal bool MustChangePassword { get; init; }
    internal bool PaymentGranted { get; init; }
    internal bool PersonalDataExportGranted { get; init; }
    internal bool SalaryViewGranted { get; init; }
    internal bool IbanViewGranted { get; init; }
    public bool CanViewSalary => !MustChangePassword && Enum.IsDefined(Role) && (Role == UserRole.Admin || SalaryViewGranted);
    public bool CanViewIban => !MustChangePassword && Enum.IsDefined(Role) && (Role == UserRole.Admin || IbanViewGranted);
    public void DemandViewSalary() { if (!CanViewSalary) throw new UnauthorizedAccessException("Ücret/bakiye ve finansal tutarları görüntüleme yetkisi gerekiyor."); }
    public void DemandViewIban() { if (!CanViewIban) throw new UnauthorizedAccessException("IBAN görüntüleme yetkisi gerekiyor."); }
    public void DemandFullDataExport() { DemandViewSalary(); DemandViewIban(); DemandExportPersonalData(); }
    public bool CanEdit => !MustChangePassword && (Role is UserRole.Admin or UserRole.PersonnelManager);
    public bool CanPay => CanViewSalary && !MustChangePassword && Enum.IsDefined(Role) && Role != UserRole.ProjectUser && (Role == UserRole.Admin || PaymentGranted);
    public bool CanExportPersonalData => !MustChangePassword && Enum.IsDefined(Role) && Role != UserRole.ProjectUser && (Role == UserRole.Admin || PersonalDataExportGranted);
    public void DemandGlobalRead() { if (Role == UserRole.ProjectUser) throw new UnauthorizedAccessException("Bu hesap yalnız yetkili olduğu proje ekranını kullanabilir."); }
    public void DemandEdit() { if (MustChangePassword) throw new PasswordChangeRequiredException(); if (!CanEdit) throw new UnauthorizedAccessException("Bu işlem için düzenleme yetkisi gerekiyor."); }
    public void DemandPay() { if (MustChangePassword) throw new PasswordChangeRequiredException(); if (!CanPay) throw new UnauthorizedAccessException("Bu işlem için ayrı ödeme yetkisi gerekiyor."); }
    public void DemandExportPersonalData() { if (MustChangePassword) throw new PasswordChangeRequiredException(); if (!CanExportPersonalData) throw new UnauthorizedAccessException("Bu işlem için kişisel veri dışa aktarma/yazdırma yetkisi gerekiyor."); }
    public void DemandAdmin() { if (MustChangePassword) throw new PasswordChangeRequiredException(); if (Role != UserRole.Admin) throw new UnauthorizedAccessException("Bu işlem için yönetici yetkisi gerekiyor."); }
    public string RoleLabel => Role switch { UserRole.Admin => "Yönetici", UserRole.PersonnelManager => "Personel sorumlusu (genel)", UserRole.ProjectUser => "Proje kullanıcısı", _ => "Görüntüleyici (genel)" };
}

