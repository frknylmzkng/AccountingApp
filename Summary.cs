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
    public partial class Summary : Form
    {
        public Summary()
        {
            InitializeComponent();
            this.Load += FormYukle;
        }

        private string connectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;
                        AttachDbFilename=""C:\Users\FURKAN YILMAZ\Documents\Muhasebe2.mdf"";
                        Integrated Security=True;Connect Timeout=30";


        private void FormYukle(object sender, EventArgs e)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Joker grubundaki kişi sayısını çek
                    string query = "SELECT COUNT(*) FROM Joker";
                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        int jokerKisiSayisi = (int)cmd.ExecuteScalar();
                        jokerLabel.Text = jokerKisiSayisi.ToString();
                    }
                }

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string query = "SELECT COUNT(*) FROM Calisan";
                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        int personelSayisi = (int)cmd.ExecuteScalar();
                        personelLbl.Text = personelSayisi.ToString(); // Label adınıza göre düzenleyin
                    }
                }
            }

            catch (SqlException ex) { MessageBox.Show("SQL Hatası: " + ex.Message); }
            catch (Exception ex) { MessageBox.Show("Genel Hata: " + ex.Message); }
        }

        private void IzinliKisileriYukle()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Bugünün tarihini al
                    DateTime bugun = DateTime.Today;

                    // Bugün izinli olan kişi sayısını çek
                    string query = @"
                SELECT COUNT(*) 
                FROM Calisan C
                INNER JOIN izinler I ON C.TC = I.TC
                WHERE @Bugun BETWEEN I.IZINBASLANGIC AND I.IZINBITIS";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Bugun", bugun);
                        int izinliKisiSayisi = (int)cmd.ExecuteScalar();

                        // Label'a yaz
                        bugunLbl.Text = izinliKisiSayisi.ToString();
                    }
                }
            }
            catch (SqlException ex)
            {
                bugunLbl.Text = "Veri çekilemedi!";
                MessageBox.Show("SQL Hatası: " + ex.Message);
            }
            catch (Exception ex)
            {
                bugunLbl.Text = "Hata oluştu!";
                MessageBox.Show("Genel Hata: " + ex.Message);
            }
        }

        private void ToplamKayitliKisiSayisi()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Calisan tablosundaki kayıt sayısı
                    string queryCalisan = "SELECT COUNT(*) FROM Calisan";
                    int calisanSayisi;
                    using (SqlCommand cmdCalisan = new SqlCommand(queryCalisan, connection))
                    {
                        calisanSayisi = (int)cmdCalisan.ExecuteScalar();
                    }

                    // Joker tablosundaki kayıt sayısı
                    string queryJoker = "SELECT COUNT(*) FROM Joker";
                    int jokerSayisi;
                    using (SqlCommand cmdJoker = new SqlCommand(queryJoker, connection))
                    {
                        jokerSayisi = (int)cmdJoker.ExecuteScalar();
                    }

                    // Toplam kayıtlı kişi sayısı
                    int toplamKisi = calisanSayisi + jokerSayisi;

                    // Label'a yazdır
                    toplamLbl.Text = toplamKisi.ToString();
                }
            }
            catch (SqlException ex)
            {
                toplamLbl.Text = "Veri çekilemedi!";
                MessageBox.Show($"SQL Hatası: {ex.Message}", "Hata!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                toplamLbl.Text = "Hata oluştu!";
                MessageBox.Show($"Genel Hata: {ex.Message}", "Hata!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Summary_Load(object sender, EventArgs e)
        {
            IzinliKisileriYukle();
            ToplamKayitliKisiSayisi();
        }

        private void back_Click(object sender, EventArgs e)
        {
            Dashboard dashboard = new Dashboard();
            dashboard.Show();
            this.Hide();
        }
    }
}
