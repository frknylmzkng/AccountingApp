using Microsoft.Data.SqlClient;
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

namespace WinFormsApp2
{
    public partial class Joker : Form
    {
        public Joker()
        {
            InitializeComponent();
        }

        private void Joker_Load(object sender, EventArgs e)
        {
            Veriler();
        }

        /*********************************************************************************************/

        private string connectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;
                        AttachDbFilename=""C:\Users\FURKAN YILMAZ\Documents\Muhasebe2.mdf"";
                        Integrated Security=True;Connect Timeout=30";

        /********************************************************************************************/
        private void back_Click_1(object sender, EventArgs e)
        {
            Dashboard dashboard = new Dashboard();
            dashboard.Show();
            this.Hide();
        }

        /***********************************************************************************************/

        public void Veriler()
        {
            using (SqlConnection genelConnection = new SqlConnection(connectionString)) // Yeni bağlantı
            {
                genelConnection.Open();
                string query = "SELECT * FROM Joker";
                SqlDataAdapter uyumla = new SqlDataAdapter(query, genelConnection);
                SqlCommandBuilder builder = new SqlCommandBuilder(uyumla);
                var veriSeti = new DataSet();
                uyumla.Fill(veriSeti);
                jokerView.DataSource = veriSeti.Tables[0];
            } // Bağlantı otomatik kapanır
        }

        /****************************************************************************************************/

        private void TC_txt_TextChanged(object sender, EventArgs e)
        {
            // Rakam dışı karakterleri filtrele
            string filteredText = new string(TC_txt.Text.Where(c => char.IsDigit(c)).ToArray());

            if (TC_txt.Text != filteredText)
            {
                TC_txt.Text = filteredText;
                TC_txt.SelectionStart = TC_txt.Text.Length; // İmleci sona taşı
            }
        }

        private void TC_txt_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Sadece rakam (0-9), Backspace (ASCII 8) veya Delete (ASCII 127) izin ver
            if (!char.IsDigit(e.KeyChar) && e.KeyChar != (char)Keys.Back && e.KeyChar != (char)Keys.Delete)
            {
                e.Handled = true; // Karakteri engelle
            }
        }

        /***************************************************************************************************/
        private void ekle_btn_Click(object sender, EventArgs e)
        {
            if (!ValidateInputs()) return;

            try
            {
                using (SqlConnection genelConnection = new SqlConnection(connectionString))
                {
                    genelConnection.Open();

                    // Hangi grupta olduğunu kontrol et
                    string grupKontrolQuery = @"
                SELECT 
                    CASE 
                        WHEN EXISTS (SELECT 1 FROM Joker WHERE TC = @TC) THEN 'Joker'
                        WHEN EXISTS (SELECT 1 FROM Calisan WHERE TC = @TC) THEN 'Calisan'
                        ELSE 'Yok'
                    END AS Grup";

                    using (SqlCommand grupKontrolCmd = new SqlCommand(grupKontrolQuery, genelConnection))
                    {
                        grupKontrolCmd.Parameters.AddWithValue("@TC", long.Parse(TC_txt.Text));
                        string bulunanGrup = grupKontrolCmd.ExecuteScalar()?.ToString();

                        if (bulunanGrup != "Yok")
                        {
                            MessageBox.Show($"Bu TC zaten {bulunanGrup} grubunda kayıtlı!");
                            return;
                        }
                    }

                    // Ekleme işlemi
                    string insertQuery = @"INSERT INTO Joker (TC, AD, SOYAD, IBAN, PARA) 
                                VALUES (@TC, @AD, @SOYAD, @IBAN, @PARA)";

                    using (SqlCommand insertCmd = new SqlCommand(insertQuery, genelConnection))
                    {
                        insertCmd.Parameters.AddWithValue("@TC", long.Parse(TC_txt.Text));
                        insertCmd.Parameters.AddWithValue("@AD", ad_txt.Text);
                        insertCmd.Parameters.AddWithValue("@SOYAD", soyad_txt.Text);
                        insertCmd.Parameters.AddWithValue("@IBAN", IBAN_txt.Text);
                        insertCmd.Parameters.AddWithValue("@PARA", decimal.Parse(money_txt.Text));

                        insertCmd.ExecuteNonQuery();
                        MessageBox.Show("Veriler eklendi!");
                    }
                    Veriler();
                }
            }
            catch (SqlException ex) when (ex.Number == 2627) // Primary Key Violation
            { MessageBox.Show("Bu TC zaten Joker grubunda kayıtlı!"); }
            catch (Exception ex)
            { MessageBox.Show("Hata: " + ex.Message); }
            finally
            { Temizle(); }
        }

        /*******************************************************************************************************/

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

                    // 2. TC Kayıtlı Değilse Uyarı Ver
                    if (bulunanGrup == "Yok")
                    {
                        MessageBox.Show("Bu TC sistemde kayıtlı değil! Düzenleme yapamazsınız.");
                        return;
                    }

                    // 3. Forma Özgü Kontrol (Örneğin: Joker Formu)
                    if (bulunanGrup != "Joker") // Eğer form Joker'e özgüyse
                    {
                        MessageBox.Show($"Bu TC {bulunanGrup} grubunda kayıtlı! Bu formda düzenleme yapamazsınız.");
                        return;
                    }

                    // 4. Güncelleme İşlemi
                    string updateQuery = @"
                UPDATE Joker 
                SET AD = @AD, 
                    SOYAD = @SOYAD, 
                    IBAN = @IBAN, 
                    PARA = @PARA 
                WHERE TC = @TC";

                    using (SqlCommand cmd = new SqlCommand(updateQuery, genelConnection))
                    {
                        cmd.Parameters.AddWithValue("@TC", long.Parse(TC_txt.Text));
                        cmd.Parameters.AddWithValue("@AD", ad_txt.Text);
                        cmd.Parameters.AddWithValue("@SOYAD", soyad_txt.Text);
                        cmd.Parameters.AddWithValue("@IBAN", IBAN_txt.Text);
                        cmd.Parameters.AddWithValue("@PARA", decimal.Parse(money_txt.Text));

                        int affectedRows = cmd.ExecuteNonQuery();

                        // 5. Geri Bildirim
                        if (affectedRows > 0)
                        {
                            MessageBox.Show("Veri başarıyla güncellendi!");
                            Veriler();
                        }
                        else
                            MessageBox.Show("Güncellenecek kayıt bulunamadı!"); // Bu satıra ulaşılması teorik olarak imkansız                        
                    }
                }
            }
            catch (SqlException ex) { MessageBox.Show("SQL Hatası: " + ex.Message); }
            catch (Exception ex) { MessageBox.Show("Genel Hata: " + ex.Message); }
            finally { Temizle(); }
        }

        /*********************************************************************************************************/

        private void sil_btn_Click(object sender, EventArgs e)
        {
            // 1. DataGridView'den seçilen satırı al
            if (jokerView.SelectedRows.Count == 0)
            {
                MessageBox.Show("Lütfen silmek için bir kayıt seçin!"); return;
            }

            DataGridViewRow selectedRow = jokerView.SelectedRows[0];
            string tc = selectedRow.Cells["TC"].Value?.ToString();

            // 2. TC geçerlilik kontrolü
            if (string.IsNullOrWhiteSpace(tc) || tc.Length != 11 || !tc.All(char.IsDigit))
            {
                MessageBox.Show("Geçersiz TC numarası!"); return;
            }

            // 3. Kullanıcı onayı al
            DialogResult result = MessageBox.Show(
                $"{tc} numaralı kaydı silmek istediğinize emin misiniz?",
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
                        WHEN EXISTS (SELECT 1 FROM Joker WHERE TC = @TC) THEN 'Joker'
                        WHEN EXISTS (SELECT 1 FROM Calisan WHERE TC = @TC) THEN 'Calisan'
                        ELSE 'Yok'
                    END AS Grup";

                    string bulunanGrup;
                    using (SqlCommand grupKontrolCmd = new SqlCommand(grupKontrolQuery, genelConnection))
                    {
                        grupKontrolCmd.Parameters.AddWithValue("@TC", long.Parse(tc));
                        bulunanGrup = grupKontrolCmd.ExecuteScalar()?.ToString() ?? "Yok";
                    }

                    if (bulunanGrup == "Yok")
                    {
                        MessageBox.Show("Bu TC sistemde kayıtlı değil!"); return;
                    }

                    // 5. İlişkili izin kayıtlarını sil (CASCADE DELETE yoksa)
                    if (bulunanGrup == "Calisan")
                    {
                        string izinSilQuery = "DELETE FROM izinler WHERE TC = @TC";
                        using (SqlCommand izinSilCmd = new SqlCommand(izinSilQuery, genelConnection))
                        {
                            izinSilCmd.Parameters.AddWithValue("@TC", long.Parse(tc));
                            izinSilCmd.ExecuteNonQuery();
                        }
                    }

                    // 6. Ana kaydı sil
                    string anaSilQuery = $"DELETE FROM {bulunanGrup} WHERE TC = @TC";
                    using (SqlCommand anaSilCmd = new SqlCommand(anaSilQuery, genelConnection))
                    {
                        anaSilCmd.Parameters.AddWithValue("@TC", long.Parse(tc));
                        int affectedRows = anaSilCmd.ExecuteNonQuery();

                        if (affectedRows > 0)
                        {
                            MessageBox.Show("Kayıt başarıyla silindi!");
                            Veriler(); // Grid'i yenile
                        }
                    }
                }
            }
            catch (SqlException ex) { MessageBox.Show($"SQL Hatası: {ex.Message}"); }
            catch (Exception ex) { MessageBox.Show($"Genel Hata: {ex.Message}"); }
            finally { Temizle(); }
        }

        /**************************************************************************************************************/

        private void jokerView_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            // Form constructor veya Load olayında:
            jokerView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            // Başlık satırına tıklanırsa veya geçersiz rowIndex ise işlem yapma
            if (e.RowIndex < 0 || jokerView.SelectedRows.Count == 0)
                return;

            // Seçilen satırı al
            DataGridViewRow selectedRow = jokerView.Rows[e.RowIndex];

            // TextBox'lara değerleri aktar
            TC_txt.Text = selectedRow.Cells[0].Value?.ToString() ?? "";
            ad_txt.Text = selectedRow.Cells[1].Value?.ToString() ?? "";
            soyad_txt.Text = selectedRow.Cells[2].Value?.ToString() ?? "";
            IBAN_txt.Text = selectedRow.Cells[3].Value?.ToString() ?? "";
            money_txt.Text = selectedRow.Cells[4].Value?.ToString() ?? "";
        }

        /**********************************************************************************************/

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
            if (TC_txt.Text.Length != 11 || !TC_txt.Text.All(char.IsDigit) || string.IsNullOrWhiteSpace(TC_txt.Text))
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
