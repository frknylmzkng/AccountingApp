using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2
{
    public partial class Genel : Form
    {
        public Genel()
        {
            InitializeComponent();
        }

        private string connectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;
                        AttachDbFilename=""C:\Users\FURKAN YILMAZ\Documents\Muhasebe2.mdf"";
                        Integrated Security=True;Connect Timeout=30";

        /***********************************************************************************************************/

        private void back_Click(object sender, EventArgs e)
        {
            Dashboard dashboard = new Dashboard();
            dashboard.Show();
            this.Hide();
        }

        /*****************************************************************************************************************/

        public void Veriler()
        {
            using (SqlConnection genelConnection = new SqlConnection(connectionString))
            {
                genelConnection.Open();
                string query = @"SELECT C.TC, C.AD, C.SOYAD, C.IBAN, C.PARA, 
                         I.IZINBASLANGIC, I.IZINBITIS, I.IzinSuresi
                         FROM Calisan C
                         LEFT JOIN izinler I ON C.TC = I.TC";

                SqlDataAdapter uyumla = new SqlDataAdapter(query, genelConnection);
                var veriSeti = new DataSet();
                uyumla.Fill(veriSeti);
                calisanView.DataSource = veriSeti.Tables[0];
            }
        }

        /******************************************************************************************************************/

        private void ekle_btn_Click(object sender, EventArgs e)
        {
            if (!ValidateInputs()) return;

            try
            {
                using (SqlConnection genelConnection = new SqlConnection(connectionString))
                {
                    genelConnection.Open();

                    // 1. TC'nin Joker veya Calisan'da olup olmadığını kontrol et
                    string grupKontrolQuery = @"
                SELECT 
                    CASE 
                        WHEN EXISTS (SELECT 1 FROM Joker WHERE TC = @TC) THEN 'Joker'
                        WHEN EXISTS (SELECT 1 FROM Calisan WHERE TC = @TC) THEN 'Calisan'
                        ELSE 'Yok'
                    END AS Grup";

                    string bulunanGrup;
                    using (SqlCommand grupKontrolCmd = new SqlCommand(grupKontrolQuery, genelConnection))
                    {
                        grupKontrolCmd.Parameters.AddWithValue("@TC", long.Parse(TC_txt.Text));
                        bulunanGrup = grupKontrolCmd.ExecuteScalar()?.ToString();
                    }

                    // 2. Grup kontrolü yap
                    if (bulunanGrup != "Yok")
                    {
                        MessageBox.Show($"Bu TC zaten {bulunanGrup} grubunda kayıtlı!");
                        return;
                    }

                    // 3. Çalışanı ekle (Artık IF NOT EXISTS'e gerek yok çünkü yukarıda kontrol edildi)
                    string calisanQuery = @"
                INSERT INTO Calisan (TC, AD, SOYAD, IBAN, PARA) 
                VALUES (@TC, @AD, @SOYAD, @IBAN, @PARA)";

                    using (SqlCommand cmdCalisan = new SqlCommand(calisanQuery, genelConnection))
                    {
                        cmdCalisan.Parameters.AddWithValue("@TC", long.Parse(TC_txt.Text));
                        cmdCalisan.Parameters.AddWithValue("@AD", ad_txt.Text);
                        cmdCalisan.Parameters.AddWithValue("@SOYAD", soyad_txt.Text);
                        cmdCalisan.Parameters.AddWithValue("@IBAN", IBAN_txt.Text);
                        cmdCalisan.Parameters.AddWithValue("@PARA", decimal.Parse(money_txt.Text));
                        cmdCalisan.ExecuteNonQuery();
                    }

                    // 4. İzni ekle
                    string izinQuery = @"
                INSERT INTO izinler (TC, IZINBASLANGIC, IZINBITIS) 
                VALUES (@TC, @IZINBASLANGIC, @IZINBITIS)";

                    using (SqlCommand cmdIzin = new SqlCommand(izinQuery, genelConnection))
                    {
                        cmdIzin.Parameters.AddWithValue("@TC", long.Parse(TC_txt.Text));
                        cmdIzin.Parameters.AddWithValue("@IZINBASLANGIC", dtpBaslangic.Value.Date);
                        cmdIzin.Parameters.AddWithValue("@IZINBITIS", dtpBitis.Value.Date);
                        cmdIzin.ExecuteNonQuery();
                    }

                    MessageBox.Show("Çalışan ve izin bilgileri eklendi!");
                    Veriler(); // Grid'i güncelle
                }
            }
            catch (SqlException ex) when (ex.Number == 2627) // TC zaten Calisan'da
            { MessageBox.Show("Bu TC zaten Calisan grubunda kayıtlı!"); }
            catch (Exception ex)
            { MessageBox.Show("Hata: " + ex.Message); }
            finally
            { Temizle(); }
        }

        /*************************************************************************************************************************************************/

        private void edit_btn_Click(object sender, EventArgs e)
        {
            if (!ValidateInputs()) return;

            try
            {
                using (SqlConnection genelConnection = new SqlConnection(connectionString))
                {
                    genelConnection.Open();

                    // 1. TC'nin Hangi Grupta Olduğunu Kontrol Et
                    string grupKontrolQuery = @"
                SELECT 
                    CASE 
                        WHEN EXISTS (SELECT 1 FROM Joker WHERE TC = @TC) THEN 'Joker'
                        WHEN EXISTS (SELECT 1 FROM Calisan WHERE TC = @TC) THEN 'Calisan'
                        ELSE 'Yok'
                    END AS Grup";

                    string bulunanGrup;
                    using (SqlCommand grupKontrolCmd = new SqlCommand(grupKontrolQuery, genelConnection))
                    {
                        grupKontrolCmd.Parameters.AddWithValue("@TC", long.Parse(TC_txt.Text));
                        bulunanGrup = grupKontrolCmd.ExecuteScalar()?.ToString() ?? "Yok";
                    }

                    // 2. TC Kontrolleri
                    if (bulunanGrup == "Yok")
                    {
                        MessageBox.Show("Bu TC sistemde kayıtlı değil! Düzenleme yapamazsınız.");
                        return;
                    }
                    else if (bulunanGrup != "Calisan")
                    {
                        MessageBox.Show($"Bu TC {bulunanGrup} grubunda! Sadece Calisan grubunu düzenleyebilirsiniz.");
                        return;
                    }

                    // 3. Çalışan Bilgilerini Güncelle
                    string calisanGuncelleQuery = @"
                UPDATE Calisan 
                SET AD = @AD, 
                    SOYAD = @SOYAD, 
                    IBAN = @IBAN, 
                    PARA = @PARA 
                WHERE TC = @TC";

                    using (SqlCommand cmdCalisan = new SqlCommand(calisanGuncelleQuery, genelConnection))
                    {
                        cmdCalisan.Parameters.AddWithValue("@TC", long.Parse(TC_txt.Text));
                        cmdCalisan.Parameters.AddWithValue("@AD", ad_txt.Text);
                        cmdCalisan.Parameters.AddWithValue("@SOYAD", soyad_txt.Text);
                        cmdCalisan.Parameters.AddWithValue("@IBAN", IBAN_txt.Text);
                        cmdCalisan.Parameters.AddWithValue("@PARA", decimal.Parse(money_txt.Text));
                        cmdCalisan.ExecuteNonQuery();
                    }

                    // 4. İzin Bilgilerini Güncelle (Varsa)
                    string izinGuncelleQuery = @"
                UPDATE izinler 
                SET IZINBASLANGIC = @BASLANGIC, 
                    IZINBITIS = @BITIS 
                WHERE TC = @TC";

                    using (SqlCommand cmdIzin = new SqlCommand(izinGuncelleQuery, genelConnection))
                    {
                        cmdIzin.Parameters.AddWithValue("@TC", long.Parse(TC_txt.Text));
                        cmdIzin.Parameters.AddWithValue("@BASLANGIC", dtpBaslangic.Value.Date);
                        cmdIzin.Parameters.AddWithValue("@BITIS", dtpBitis.Value.Date);
                        int affectedRows = cmdIzin.ExecuteNonQuery();

                        // Eğer izin kaydı yoksa yeni ekle
                        if (affectedRows == 0)
                        {
                            string izinEkleQuery = @"INSERT INTO izinler (TC, IZINBASLANGIC, IZINBITIS) 
                                          VALUES (@TC, @BASLANGIC, @BITIS)";
                            using (SqlCommand insertCmd = new SqlCommand(izinEkleQuery, genelConnection))
                            {
                                insertCmd.Parameters.AddWithValue("@TC", long.Parse(TC_txt.Text));
                                insertCmd.Parameters.AddWithValue("@BASLANGIC", dtpBaslangic.Value.Date);
                                insertCmd.Parameters.AddWithValue("@BITIS", dtpBitis.Value.Date);
                                insertCmd.ExecuteNonQuery();
                            }
                        }
                    }

                    MessageBox.Show("Çalışan ve izin bilgileri güncellendi!");
                    Veriler(); // Grid'i yenile
                }
            }
            catch (SqlException ex) { MessageBox.Show("SQL Hatası: " + ex.Message); }
            catch (Exception ex) { MessageBox.Show("Genel Hata: " + ex.Message); }
            finally { Temizle(); }
        }

        /****************************************************************************************************************************************/

        private void sil_btn_Click(object sender, EventArgs e)
        {
            // 1. DataGridView'den seçilen satırı kontrol et
            if (calisanView.SelectedRows.Count == 0)
            {
                MessageBox.Show("Lütfen silmek için bir kayıt seçin!");
                return;
            }

            DataGridViewRow selectedRow = calisanView.SelectedRows[0];
            string tc = selectedRow.Cells["TC"].Value?.ToString();

            // 2. TC geçerlilik kontrolü
            if (string.IsNullOrWhiteSpace(tc) || tc.Length != 11 || !tc.All(char.IsDigit))
            {
                MessageBox.Show("Geçersiz TC numarası!");
                return;
            }

            // 3. Kullanıcı onayı al
            DialogResult result = MessageBox.Show(
                $"{tc} numaralı çalışanı ve ilişkili izinleri silmek istediğinize emin misiniz?",
                "Silme Onayı",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result != DialogResult.Yes) return;

            try
            {
                using (SqlConnection genelConnection = new SqlConnection(connectionString))
                {
                    genelConnection.Open();

                    // 4. TC'nin hangi grupta olduğunu kontrol et
                    string grupKontrolQuery = @"
                SELECT 
                    CASE 
                        WHEN EXISTS (SELECT 1 FROM Calisan WHERE TC = @TC) THEN 'Calisan'
                        WHEN EXISTS (SELECT 1 FROM Joker WHERE TC = @TC) THEN 'Joker'
                        ELSE 'Yok'
                    END AS Grup";

                    string bulunanGrup;
                    using (SqlCommand grupKontrolCmd = new SqlCommand(grupKontrolQuery, genelConnection))
                    {
                        grupKontrolCmd.Parameters.AddWithValue("@TC", long.Parse(tc));
                        bulunanGrup = grupKontrolCmd.ExecuteScalar()?.ToString() ?? "Yok";
                    }

                    // 5. Grup ve TC kontrolü
                    if (bulunanGrup == "Yok")
                    {
                        MessageBox.Show("Bu TC sistemde kayıtlı değil!");
                        return;
                    }
                    else if (bulunanGrup != "Calisan")
                    {
                        MessageBox.Show($"Bu TC {bulunanGrup} grubunda! Sadece Calisan grubunu silebilirsiniz.");
                        return;
                    }

                    // 6. İzin kayıtlarını sil (CASCADE DELETE yoksa)
                    string izinSilQuery = "DELETE FROM izinler WHERE TC = @TC";
                    using (SqlCommand izinSilCmd = new SqlCommand(izinSilQuery, genelConnection))
                    {
                        izinSilCmd.Parameters.AddWithValue("@TC", long.Parse(tc));
                        izinSilCmd.ExecuteNonQuery();
                    }

                    // 7. Ana kaydı sil
                    string anaSilQuery = "DELETE FROM Calisan WHERE TC = @TC";
                    using (SqlCommand anaSilCmd = new SqlCommand(anaSilQuery, genelConnection))
                    {
                        anaSilCmd.Parameters.AddWithValue("@TC", long.Parse(tc));
                        int affectedRows = anaSilCmd.ExecuteNonQuery();

                        if (affectedRows > 0)
                        {
                            MessageBox.Show("Çalışan ve izin bilgileri silindi!");
                            Veriler(); // Grid'i yenile
                        }
                    }
                }
            }
            catch (SqlException ex) when (ex.Number == 547) { MessageBox.Show("Önce ilişkili izin kayıtlarını silmelisiniz!"); }
            catch (SqlException ex) { MessageBox.Show($"SQL Hatası: {ex.Message}"); }
            catch (Exception ex) { MessageBox.Show($"Genel Hata: {ex.Message}"); }
            finally { Temizle(); }
        }

        /***************************************************************************************************************************************/

        private void Genel_Load(object sender, EventArgs e)
        {
            Veriler();
        }

        /*************************************************************************************************************************************************/

        private void TC_txt_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Sadece rakam, Backspace ve Delete'e izin ver
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
                System.Media.SystemSounds.Beep.Play(); // Hata sesi
                return;
            }
        }

        private void TC_txt_TextChanged(object sender, EventArgs e)
        {
            // Mevcut metni al
            string originalText = TC_txt.Text;

            // Sadece rakamları koru
            string filteredText = new string(originalText.Where(c => char.IsDigit(c)).ToArray());

            // Değişiklik varsa uygula
            if (originalText != filteredText)
            {
                int cursorPosition = TC_txt.SelectionStart;
                TC_txt.Text = filteredText;
                TC_txt.SelectionStart = Math.Max(0, cursorPosition - (originalText.Length - filteredText.Length));
            }

            // 11 karakter sınırı
            if (TC_txt.Text.Length > 11)
            {
                TC_txt.Text = TC_txt.Text.Substring(0, 11);
                TC_txt.SelectionStart = 11;
            }
        }

        /*****************************************************************************************************************************************************/

        private void calisanView_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            // Form constructor veya Load olayında:
            calisanView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            // Başlık satırına tıklanırsa veya geçersiz rowIndex ise işlem yapma
            if (e.RowIndex < 0 || calisanView.SelectedRows.Count == 0)
                return;

            // Seçilen satırı al
            DataGridViewRow selectedRow = calisanView.Rows[e.RowIndex];

            // TextBox'lara değerleri aktar
            TC_txt.Text = selectedRow.Cells[0].Value?.ToString() ?? "";
            ad_txt.Text = selectedRow.Cells[1].Value?.ToString() ?? "";
            soyad_txt.Text = selectedRow.Cells[2].Value?.ToString() ?? "";
            IBAN_txt.Text = selectedRow.Cells[3].Value?.ToString() ?? "";
            money_txt.Text = selectedRow.Cells[4].Value?.ToString() ?? "";

            // Tarih kontrollerini doldur
            if (selectedRow.Cells[5].Value != DBNull.Value &&
                DateTime.TryParse(selectedRow.Cells[5].Value.ToString(), out DateTime baslangic))
            {
                dtpBaslangic.Value = baslangic;
            }
            else
            {
                dtpBaslangic.Value = DateTime.Today; // Varsayılan değer
            }

            if (selectedRow.Cells[6].Value != DBNull.Value &&
                DateTime.TryParse(selectedRow.Cells[6].Value.ToString(), out DateTime bitis))
            {
                dtpBitis.Value = bitis;
            }
            else
            {
                dtpBitis.Value = DateTime.Today; // Varsayılan değer
            }
        }

        /***************************************************************************************************/

        // Yardımcı metod: Giriş kontrolleri
        private bool ValidateInputs()
        {
            // Boş veri kontrolü
            if (string.IsNullOrWhiteSpace(TC_txt.Text) ||
                string.IsNullOrWhiteSpace(ad_txt.Text) ||
                string.IsNullOrWhiteSpace(soyad_txt.Text) ||
                string.IsNullOrWhiteSpace(IBAN_txt.Text) ||
                string.IsNullOrWhiteSpace(money_txt.Text))
            {
                MessageBox.Show("Eksik veri!");
                return false;
            }

            // TC kontrolü
            if (TC_txt.Text.Length != 11 || !TC_txt.Text.All(char.IsDigit))
            {
                MessageBox.Show("TC 11 haneli ve sadece rakamlardan oluşmalıdır!");
                return false;
            }

            // IBAN kontrolü
            IBAN_txt.Text = IBAN_txt.Text.ToUpper().Replace(" ", "");
            if (IBAN_txt.Text.Length < 16 || IBAN_txt.Text.Length > 34)
            {
                MessageBox.Show("Geçersiz IBAN!");
                return false;
            }

            // PARA kontrolü
            if (!decimal.TryParse(money_txt.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            {
                MessageBox.Show("Geçersiz para formatı! Örnek: 1500.50");
                return false;
            }

            return true;
        }

        // Yardımcı metod: TextBox'ları temizle
        private void Temizle()
        {
            TC_txt.Clear();
            ad_txt.Clear();
            soyad_txt.Clear();
            IBAN_txt.Clear();
            money_txt.Clear();
        }


    }
}
