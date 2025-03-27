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
    public partial class Giris : Form
    {
        public Giris()
        {
            InitializeComponent();

            // Enter tuşuna basıldığında giriş butonunun tetiklenmesi için
            this.AcceptButton = giris_btn;
        }

        private void giris_btn_Click(object sender, EventArgs e)
        {
            string kullaniciAdi = username_txt.Text;
            string sifre = password_txt.Text;

            if (string.IsNullOrEmpty(kullaniciAdi) || string.IsNullOrEmpty(sifre))
            {
                MessageBox.Show("Kullanıcı adı ve şifre boş bırakılamaz!", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (KullaniciDogrula(kullaniciAdi, sifre))
            {
                MessageBox.Show("Giriş başarılı!", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Dashboard formunu açma
                Dashboard dashboardForm = new Dashboard();
                this.Hide();
                dashboardForm.ShowDialog();
                this.Hide();
            }
            else
            {
                MessageBox.Show("Kullanıcı adı veya şifre hatalı!", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool KullaniciDogrula(string kullaniciAdi, string sifre)
        {
            bool dogrulama = false;

            // Veritabanı bağlantı stringinizi buraya yazın
            string connectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=""C:\Users\FURKAN YILMAZ\Documents\Muhasebe2.mdf"";Integrated Security=True;Connect Timeout=30";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();

                    string query = "SELECT COUNT(1) FROM [dbo].[admin] WHERE [username]=@username AND [password]=@password";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@username", kullaniciAdi);
                        command.Parameters.AddWithValue("@password", sifre);

                        int count = Convert.ToInt32(command.ExecuteScalar());

                        if (count == 1)
                        {
                            dogrulama = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Veritabanı bağlantısında bir hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    if (connection.State == ConnectionState.Open)
                    {
                        connection.Close();
                    }
                }
            }

            return dogrulama;
        }
    }
}
