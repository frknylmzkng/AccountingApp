namespace WinFormsApp2
{
    partial class Search
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Search));
            txtSoyad = new TextBox();
            label4 = new Label();
            txtAd = new TextBox();
            label2 = new Label();
            txtTC = new TextBox();
            label5 = new Label();
            dataGridViewAramaSonuclari = new DataGridView();
            Ara = new Button();
            back = new Button();
            ((System.ComponentModel.ISupportInitialize)dataGridViewAramaSonuclari).BeginInit();
            SuspendLayout();
            // 
            // txtSoyad
            // 
            txtSoyad.Location = new Point(136, 162);
            txtSoyad.Name = "txtSoyad";
            txtSoyad.Size = new Size(100, 23);
            txtSoyad.TabIndex = 49;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(26, 120);
            label4.Name = "label4";
            label4.Size = new Size(26, 15);
            label4.TabIndex = 48;
            label4.Text = "ADI";
            // 
            // txtAd
            // 
            txtAd.Location = new Point(136, 117);
            txtAd.Name = "txtAd";
            txtAd.Size = new Size(100, 23);
            txtAd.TabIndex = 47;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(26, 165);
            label2.Name = "label2";
            label2.Size = new Size(47, 15);
            label2.TabIndex = 46;
            label2.Text = "SOYADI";
            // 
            // txtTC
            // 
            txtTC.Location = new Point(136, 71);
            txtTC.MaxLength = 11;
            txtTC.Name = "txtTC";
            txtTC.Size = new Size(100, 23);
            txtTC.TabIndex = 45;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(26, 74);
            label5.Name = "label5";
            label5.Size = new Size(21, 15);
            label5.TabIndex = 44;
            label5.Text = "TC";
            // 
            // dataGridViewAramaSonuclari
            // 
            dataGridViewAramaSonuclari.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dataGridViewAramaSonuclari.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewAramaSonuclari.Location = new Point(382, 12);
            dataGridViewAramaSonuclari.Name = "dataGridViewAramaSonuclari";
            dataGridViewAramaSonuclari.RowTemplate.Height = 25;
            dataGridViewAramaSonuclari.Size = new Size(569, 523);
            dataGridViewAramaSonuclari.TabIndex = 50;
            // 
            // Ara
            // 
            Ara.Location = new Point(136, 232);
            Ara.Name = "Ara";
            Ara.Size = new Size(75, 23);
            Ara.TabIndex = 51;
            Ara.Text = "ARA";
            Ara.UseVisualStyleBackColor = true;
            Ara.Click += Ara_Click;
            // 
            // back
            // 
            back.BackgroundImage = (Image)resources.GetObject("back.BackgroundImage");
            back.BackgroundImageLayout = ImageLayout.Stretch;
            back.Cursor = Cursors.Hand;
            back.FlatAppearance.BorderSize = 0;
            back.FlatStyle = FlatStyle.Flat;
            back.Location = new Point(12, 12);
            back.Name = "back";
            back.Size = new Size(25, 25);
            back.TabIndex = 52;
            back.UseVisualStyleBackColor = true;
            back.Click += back_Click;
            // 
            // Search
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(963, 547);
            Controls.Add(back);
            Controls.Add(Ara);
            Controls.Add(dataGridViewAramaSonuclari);
            Controls.Add(txtSoyad);
            Controls.Add(label4);
            Controls.Add(txtAd);
            Controls.Add(label2);
            Controls.Add(txtTC);
            Controls.Add(label5);
            Name = "Search";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Search";
            ((System.ComponentModel.ISupportInitialize)dataGridViewAramaSonuclari).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox txtSoyad;
        private Label label4;
        private TextBox txtAd;
        private Label label2;
        private TextBox txtTC;
        private Label label5;
        private DataGridView dataGridViewAramaSonuclari;
        private Button Ara;
        private Button back;
    }
}