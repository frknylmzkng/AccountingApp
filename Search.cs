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
    public partial class Search : Form
    {
        public Search()
        {
            InitializeComponent();
        }

        private string connectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;
                        AttachDbFilename=""C:\Users\FURKAN YILMAZ\Documents\Muhasebe2.mdf"";
                        Integrated Security=True;Connect Timeout=30";


        private void Ara_Click(object sender, EventArgs e)
        {
            string tc = txtTC.Text.Trim();
            string ad = txtAd.Text.Trim().ToLower();
            string soyad = txtSoyad.Text.Trim().ToLower();

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Dinamik SQL Sorgusu
                    StringBuilder query = new StringBuilder(@"
                SELECT 
                    COALESCE(J.TC, C.TC) AS TC,
                    COALESCE(J.AD, C.AD) AS AD,
                    COALESCE(J.SOYAD, C.SOYAD) AS SOYAD,
                    COALESCE(J.IBAN, C.IBAN) AS IBAN,
                    COALESCE(J.PARA, C.PARA) AS PARA,
                    I.IZINBASLANGIC,
                    I.IZINBITIS,
                    CASE 
                        WHEN J.TC IS NOT NULL THEN 'Joker'
                        ELSE 'Genel'
                    END AS GRUP
                FROM Joker J
                FULL OUTER JOIN Calisan C ON J.TC = C.TC
                LEFT JOIN izinler I ON COALESCE(J.TC, C.TC) = I.TC
                WHERE 1=1");

                    var parameters = new List<SqlParameter>();

                    // TC için tam eşleşme
                    if (!string.IsNullOrEmpty(tc))
                    {
                        query.Append(" AND (J.TC = @TC OR C.TC = @TC)");
                        parameters.Add(new SqlParameter("@TC", tc));
                    }

                    // Ad için kısmi eşleşme (büyük-küçük harf duyarsız)
                    if (!string.IsNullOrEmpty(ad))
                    {
                        query.Append(" AND (LOWER(J.AD) LIKE @Ad OR LOWER(C.AD) LIKE @Ad)");
                        parameters.Add(new SqlParameter("@Ad", $"%{ad}%"));
                    }

                    // Soyad için kısmi eşleşme (büyük-küçük harf duyarsız)
                    if (!string.IsNullOrEmpty(soyad))
                    {
                        query.Append(" AND (LOWER(J.SOYAD) LIKE @Soyad OR LOWER(C.SOYAD) LIKE @Soyad)");
                        parameters.Add(new SqlParameter("@Soyad", $"%{soyad}%"));
                    }

                    // Sorguyu çalıştır
                    using (SqlCommand cmd = new SqlCommand(query.ToString(), connection))
                    {
                        cmd.Parameters.AddRange(parameters.ToArray());

                        SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);

                        // DataGridView'i formatla
                        dataGridViewAramaSonuclari.DataSource = dt;
                        dataGridViewAramaSonuclari.Columns["PARA"].DefaultCellStyle.Format = "C2"; // Para formatı
                        dataGridViewAramaSonuclari.Columns["IZINBASLANGIC"].DefaultCellStyle.Format = "dd.MM.yyyy";
                        dataGridViewAramaSonuclari.Columns["IZINBITIS"].DefaultCellStyle.Format = "dd.MM.yyyy";

                        // Grup sütununu renklendir (Opsiyonel)
                        dataGridViewAramaSonuclari.CellFormatting += (s, e) =>
                        {
                            if (e.ColumnIndex == dataGridViewAramaSonuclari.Columns["GRUP"].Index && e.Value != null)
                            {
                                e.CellStyle.BackColor = e.Value.ToString() == "Joker" ? Color.LightGreen : Color.LightBlue;
                            }
                        };

                        // Sonuç yoksa uyarı
                        if (dt.Rows.Count == 0) { MessageBox.Show("Kayıt bulunamadı!"); }
                    }
                }
            }
            catch (SqlException ex) { MessageBox.Show("SQL Hatası: " + ex.Message); }
            catch (Exception ex) { MessageBox.Show("Genel Hata: " + ex.Message); }
        }

        private void back_Click(object sender, EventArgs e)
        {
            Dashboard dashboard = new Dashboard();
            dashboard.Show();
            this.Hide();
        }
    }
}
