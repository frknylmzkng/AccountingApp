using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WinFormsApp2
{
    public partial class Admin : Form
    {
        public Admin()
        {
            InitializeComponent();
        }

        private string connectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;
                        AttachDbFilename=""C:\Users\FURKAN YILMAZ\Documents\Muhasebe2.mdf"";
                        Integrated Security=True;Connect Timeout=30";

        private void Admin_Load(object sender, EventArgs e)
        {
            Veriler();
        }

        private void back_Click(object sender, EventArgs e)
        {
            Dashboard dashboard = new Dashboard();
            dashboard.Show();
            this.Hide();
        }

        public void Veriler()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string query = "SELECT username, password FROM admin";
                    SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);

                    // DataTable'ı klonlayarak şifre sütununu maskele
                    DataTable maskedDt = dt.Clone();
                    foreach (DataRow row in dt.Rows)
                    {
                        maskedDt.ImportRow(row);
                        maskedDt.Rows[maskedDt.Rows.Count - 1]["password"] = "********"; // 8 yıldız
                    }

                    adminView.DataSource = maskedDt;
                }
            }
            catch (Exception ex) { MessageBox.Show("Veri yükleme hatası: " + ex.Message); }
        }

        private void ekle_btn_Click(object sender, EventArgs e)
        {
            // 1. Boş Alan Kontrolü
            if (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                MessageBox.Show("Kullanıcı adı ve şifre boş olamaz!");
                return;
            }

            // 2. Şifre Uzunluğu Kontrolü (Opsiyonel)
            if (txtPassword.Text.Length < 6)
            {
                MessageBox.Show("Şifre en az 6 karakter olmalıdır!");
                return;
            }

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // 3. Kullanıcı Adı Benzersizlik Kontrolü
                    string checkQuery = "SELECT COUNT(*) FROM admin WHERE username = @username";
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@username", txtUsername.Text.Trim());
                        int userCount = (int)checkCmd.ExecuteScalar();

                        if (userCount > 0)
                        {
                            MessageBox.Show("Bu kullanıcı adı zaten kayıtlı!");
                            return;
                        }
                    }

                    // 4. Yeni Admin Ekleme
                    string insertQuery = "INSERT INTO admin (username, password) VALUES (@username, @password)";
                    using (SqlCommand insertCmd = new SqlCommand(insertQuery, connection))
                    {
                        insertCmd.Parameters.AddWithValue("@username", txtUsername.Text.Trim());
                        insertCmd.Parameters.AddWithValue("@password", txtPassword.Text.Trim()); // Şifre şifrelenmeli!
                        insertCmd.ExecuteNonQuery();
                    }

                    MessageBox.Show("Yönetici başarıyla eklendi!");
                    Veriler(); // DataGridView'i güncelle
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show("SQL Hatası: " + ex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Genel Hata: " + ex.Message);
            }
            finally
            {
                txtUsername.Clear();
                txtPassword.Clear();
            }
        }

        private void sil_btn_Click(object sender, EventArgs e)
        {
            // 1. Seçili satır kontrolü
            if (adminView.SelectedRows.Count == 0)
            {
                MessageBox.Show("Lütfen silmek için bir admin seçin!");
                return;
            }

            // 2. Seçilen adminin kullanıcı adını al
            string username = adminView.SelectedRows[0].Cells["username"].Value.ToString();

            // 3. Kullanıcı onayı al
            DialogResult result = MessageBox.Show(
                $"{username} kullanıcısını silmek istediğinize emin misiniz?",
                "Silme Onayı",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result != DialogResult.Yes) return;

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // 4. Mevcut admin sayısını kontrol et
                    string countQuery = "SELECT COUNT(*) FROM admin";
                    int adminCount;
                    using (SqlCommand countCmd = new SqlCommand(countQuery, connection))
                    {
                        adminCount = (int)countCmd.ExecuteScalar();
                    }

                    // 5. Admin sayısı 1 veya daha azsa işlemi engelle
                    if (adminCount <= 1)
                    {
                        MessageBox.Show("En az bir admin hesabı kalmalıdır! Silme işlemi iptal edildi.");
                        return;
                    }

                    // 6. Admini sil
                    string deleteQuery = "DELETE FROM admin WHERE username = @username";
                    using (SqlCommand deleteCmd = new SqlCommand(deleteQuery, connection))
                    {
                        deleteCmd.Parameters.AddWithValue("@username", username);
                        int affectedRows = deleteCmd.ExecuteNonQuery();

                        if (affectedRows > 0)
                        {
                            MessageBox.Show("Admin başarıyla silindi!");
                            Veriler(); // Grid'i yenile
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show("SQL Hatası: " + ex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Genel Hata: " + ex.Message);
            }
        }
    }
}
