namespace WinFormsApp2
{
    partial class Genel
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Genel));
            money_txt = new TextBox();
            label8 = new Label();
            dtpBitis = new DateTimePicker();
            dtpBaslangic = new DateTimePicker();
            label6 = new Label();
            label7 = new Label();
            sil_btn = new Button();
            edit_btn = new Button();
            ekle_btn = new Button();
            IBAN_txt = new TextBox();
            label3 = new Label();
            soyad_txt = new TextBox();
            label4 = new Label();
            ad_txt = new TextBox();
            label2 = new Label();
            TC_txt = new TextBox();
            label5 = new Label();
            label1 = new Label();
            calisanView = new DataGridView();
            back = new Button();
            ((System.ComponentModel.ISupportInitialize)calisanView).BeginInit();
            SuspendLayout();
            // 
            // money_txt
            // 
            money_txt.Location = new Point(207, 317);
            money_txt.Name = "money_txt";
            money_txt.Size = new Size(166, 23);
            money_txt.TabIndex = 44;
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(40, 320);
            label8.Name = "label8";
            label8.Size = new Size(95, 15);
            label8.TabIndex = 43;
            label8.Text = "VERİLECEK PARA";
            // 
            // dtpBitis
            // 
            dtpBitis.Location = new Point(207, 416);
            dtpBitis.Name = "dtpBitis";
            dtpBitis.Size = new Size(166, 23);
            dtpBitis.TabIndex = 42;
            // 
            // dtpBaslangic
            // 
            dtpBaslangic.Location = new Point(207, 376);
            dtpBaslangic.Name = "dtpBaslangic";
            dtpBaslangic.Size = new Size(166, 23);
            dtpBaslangic.TabIndex = 41;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(40, 422);
            label6.Name = "label6";
            label6.Size = new Size(97, 15);
            label6.TabIndex = 40;
            label6.Text = "İZİN BİTİŞ TARİHİ";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(40, 376);
            label7.Name = "label7";
            label7.Size = new Size(126, 15);
            label7.TabIndex = 39;
            label7.Text = "İZİN BAŞLANIÇ TARİHİ";
            // 
            // sil_btn
            // 
            sil_btn.Location = new Point(232, 475);
            sil_btn.Name = "sil_btn";
            sil_btn.Size = new Size(75, 23);
            sil_btn.TabIndex = 38;
            sil_btn.Text = "SİL";
            sil_btn.UseVisualStyleBackColor = true;
            sil_btn.Click += sil_btn_Click;
            // 
            // edit_btn
            // 
            edit_btn.Location = new Point(133, 475);
            edit_btn.Name = "edit_btn";
            edit_btn.Size = new Size(75, 23);
            edit_btn.TabIndex = 37;
            edit_btn.Text = "DÜZENLE";
            edit_btn.UseVisualStyleBackColor = true;
            edit_btn.Click += edit_btn_Click;
            // 
            // ekle_btn
            // 
            ekle_btn.Location = new Point(40, 475);
            ekle_btn.Name = "ekle_btn";
            ekle_btn.Size = new Size(75, 23);
            ekle_btn.TabIndex = 36;
            ekle_btn.Text = "EKLE";
            ekle_btn.UseVisualStyleBackColor = true;
            ekle_btn.Click += ekle_btn_Click;
            // 
            // IBAN_txt
            // 
            IBAN_txt.Location = new Point(207, 266);
            IBAN_txt.Name = "IBAN_txt";
            IBAN_txt.Size = new Size(166, 23);
            IBAN_txt.TabIndex = 35;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(40, 269);
            label3.Name = "label3";
            label3.Size = new Size(34, 15);
            label3.TabIndex = 34;
            label3.Text = "IBAN";
            // 
            // soyad_txt
            // 
            soyad_txt.Location = new Point(207, 220);
            soyad_txt.Name = "soyad_txt";
            soyad_txt.Size = new Size(166, 23);
            soyad_txt.TabIndex = 33;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(40, 178);
            label4.Name = "label4";
            label4.Size = new Size(26, 15);
            label4.TabIndex = 32;
            label4.Text = "ADI";
            // 
            // ad_txt
            // 
            ad_txt.Location = new Point(207, 175);
            ad_txt.Name = "ad_txt";
            ad_txt.Size = new Size(166, 23);
            ad_txt.TabIndex = 31;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(40, 223);
            label2.Name = "label2";
            label2.Size = new Size(47, 15);
            label2.TabIndex = 30;
            label2.Text = "SOYADI";
            // 
            // TC_txt
            // 
            TC_txt.Location = new Point(207, 129);
            TC_txt.MaxLength = 11;
            TC_txt.Name = "TC_txt";
            TC_txt.Size = new Size(166, 23);
            TC_txt.TabIndex = 29;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(40, 132);
            label5.Name = "label5";
            label5.Size = new Size(21, 15);
            label5.TabIndex = 28;
            label5.Text = "TC";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(40, 70);
            label1.Name = "label1";
            label1.Size = new Size(126, 15);
            label1.TabIndex = 27;
            label1.Text = "ÇALIŞAN PERSONELİN";
            // 
            // calisanView
            // 
            calisanView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            calisanView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            calisanView.Location = new Point(410, 12);
            calisanView.Name = "calisanView";
            calisanView.RowTemplate.Height = 25;
            calisanView.Size = new Size(541, 523);
            calisanView.TabIndex = 45;
            calisanView.CellContentClick += calisanView_CellContentClick;
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
            back.TabIndex = 46;
            back.UseVisualStyleBackColor = true;
            back.Click += back_Click;
            // 
            // Genel
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(963, 547);
            Controls.Add(back);
            Controls.Add(calisanView);
            Controls.Add(money_txt);
            Controls.Add(label8);
            Controls.Add(dtpBitis);
            Controls.Add(dtpBaslangic);
            Controls.Add(label6);
            Controls.Add(label7);
            Controls.Add(sil_btn);
            Controls.Add(edit_btn);
            Controls.Add(ekle_btn);
            Controls.Add(IBAN_txt);
            Controls.Add(label3);
            Controls.Add(soyad_txt);
            Controls.Add(label4);
            Controls.Add(ad_txt);
            Controls.Add(label2);
            Controls.Add(TC_txt);
            Controls.Add(label5);
            Controls.Add(label1);
            Name = "Genel";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Genel";
            Load += Genel_Load;
            ((System.ComponentModel.ISupportInitialize)calisanView).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox money_txt;
        private Label label8;
        private DateTimePicker dtpBitis;
        private DateTimePicker dtpBaslangic;
        private Label label6;
        private Label label7;
        private Button sil_btn;
        private Button edit_btn;
        private Button ekle_btn;
        private TextBox IBAN_txt;
        private Label label3;
        private TextBox soyad_txt;
        private Label label4;
        private TextBox ad_txt;
        private Label label2;
        private TextBox TC_txt;
        private Label label5;
        private Label label1;
        private DataGridView calisanView;
        private Button back;
    }
}