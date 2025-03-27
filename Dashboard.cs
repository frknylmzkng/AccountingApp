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
    public partial class Dashboard : Form
    {
        public Dashboard()
        {
            InitializeComponent();

            // Form üzerinde hiçbir butonun AcceptButton olarak ayarlanmaması için
            this.AcceptButton = null;

            // Ayrıca butonun focus'unu kaldırmak için
            this.ActiveControl = null;

        }

        private void button4_Click(object sender, EventArgs e) // Yönetici
        {
            Admin admin = new Admin();
            admin.Show();
            this.Hide();
        }

        private void button1_Click(object sender, EventArgs e) // Genel personel
        {
            Genel genel = new Genel();
            genel.Show();
            this.Hide();
        }

        private void button2_Click(object sender, EventArgs e) // Joker personel
        {
            Joker joker = new Joker();
            joker.Show();
            this.Hide();
        }

        private void button3_Click(object sender, EventArgs e) // Kişi arama
        {
            Search search = new Search();
            search.Show();
            this.Hide();
        }

        private void button5_Click(object sender, EventArgs e) // Genel durum
        {
            Summary summary = new Summary();
            summary.Show();
            this.Hide();
        }

        private void button6_Click(object sender, EventArgs e) // İletişim
        {
            Contact contact = new Contact();
            contact.Show();
            this.Hide();
        }
    }
}
