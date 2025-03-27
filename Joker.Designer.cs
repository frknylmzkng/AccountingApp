namespace WinFormsApp2
{
    partial class Joker
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Joker));
            money_txt = new TextBox();
            label6 = new Label();
            sil_btn = new Button();
            edit_btn = new Button();
            ekle_btn = new Button();
            jokerView = new DataGridView();
            IBAN_txt = new TextBox();
            label3 = new Label();
            soyad_txt = new TextBox();
            label4 = new Label();
            ad_txt = new TextBox();
            label2 = new Label();
            TC_txt = new TextBox();
            label5 = new Label();
            label1 = new Label();
            back = new Button();
            ((System.ComponentModel.ISupportInitialize)jokerView).BeginInit();
            SuspendLayout();
            // 
            // money_txt
            // 
            money_txt.Location = new Point(156, 317);
            money_txt.Name = "money_txt";
            money_txt.Size = new Size(100, 23);
            money_txt.TabIndex = 51;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(46, 320);
            label6.Name = "label6";
            label6.Size = new Size(95, 15);
            label6.TabIndex = 50;
            label6.Text = "VERİLECEK PARA";
            // 
            // sil_btn
            // 
            sil_btn.Location = new Point(210, 396);
            sil_btn.Name = "sil_btn";
            sil_btn.Size = new Size(75, 23);
            sil_btn.TabIndex = 49;
            sil_btn.Text = "SİL";
            sil_btn.UseVisualStyleBackColor = true;
            sil_btn.Click += sil_btn_Click;
            // 
            // edit_btn
            // 
            edit_btn.Location = new Point(111, 396);
            edit_btn.Name = "edit_btn";
            edit_btn.Size = new Size(75, 23);
            edit_btn.TabIndex = 48;
            edit_btn.Text = "DÜZENLE";
            edit_btn.UseVisualStyleBackColor = true;
            edit_btn.Click += edit_btn_Click;
            // 
            // ekle_btn
            // 
            ekle_btn.Location = new Point(18, 396);
            ekle_btn.Name = "ekle_btn";
            ekle_btn.Size = new Size(75, 23);
            ekle_btn.TabIndex = 47;
            ekle_btn.Text = "EKLE";
            ekle_btn.UseVisualStyleBackColor = true;
            ekle_btn.Click += ekle_btn_Click;
            // 
            // jokerView
            // 
            jokerView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            jokerView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            jokerView.Location = new Point(382, 12);
            jokerView.Name = "jokerView";
            jokerView.RowTemplate.Height = 25;
            jokerView.Size = new Size(569, 523);
            jokerView.TabIndex = 46;
            jokerView.CellContentClick += jokerView_CellContentClick;
            // 
            // IBAN_txt
            // 
            IBAN_txt.Location = new Point(156, 268);
            IBAN_txt.Name = "IBAN_txt";
            IBAN_txt.Size = new Size(100, 23);
            IBAN_txt.TabIndex = 45;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(46, 271);
            label3.Name = "label3";
            label3.Size = new Size(34, 15);
            label3.TabIndex = 44;
            label3.Text = "IBAN";
            // 
            // soyad_txt
            // 
            soyad_txt.Location = new Point(156, 222);
            soyad_txt.Name = "soyad_txt";
            soyad_txt.Size = new Size(100, 23);
            soyad_txt.TabIndex = 43;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(46, 180);
            label4.Name = "label4";
            label4.Size = new Size(26, 15);
            label4.TabIndex = 42;
            label4.Text = "ADI";
            // 
            // ad_txt
            // 
            ad_txt.Location = new Point(156, 177);
            ad_txt.Name = "ad_txt";
            ad_txt.Size = new Size(100, 23);
            ad_txt.TabIndex = 41;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(46, 225);
            label2.Name = "label2";
            label2.Size = new Size(47, 15);
            label2.TabIndex = 40;
            label2.Text = "SOYADI";
            // 
            // TC_txt
            // 
            TC_txt.Location = new Point(156, 131);
            TC_txt.MaxLength = 11;
            TC_txt.Name = "TC_txt";
            TC_txt.Size = new Size(100, 23);
            TC_txt.TabIndex = 39;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(46, 134);
            label5.Name = "label5";
            label5.Size = new Size(21, 15);
            label5.TabIndex = 38;
            label5.Text = "TC";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(46, 72);
            label1.Name = "label1";
            label1.Size = new Size(111, 15);
            label1.TabIndex = 37;
            label1.Text = "JOKER PERSONELİN";
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
            back.TabIndex = 36;
            back.UseVisualStyleBackColor = true;
            back.Click += back_Click_1;
            // 
            // Joker
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(963, 547);
            Controls.Add(money_txt);
            Controls.Add(label6);
            Controls.Add(sil_btn);
            Controls.Add(edit_btn);
            Controls.Add(ekle_btn);
            Controls.Add(jokerView);
            Controls.Add(IBAN_txt);
            Controls.Add(label3);
            Controls.Add(soyad_txt);
            Controls.Add(label4);
            Controls.Add(ad_txt);
            Controls.Add(label2);
            Controls.Add(TC_txt);
            Controls.Add(label5);
            Controls.Add(label1);
            Controls.Add(back);
            Name = "Joker";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Joker";
            Load += Joker_Load;
            ((System.ComponentModel.ISupportInitialize)jokerView).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox money_txt;
        private Label label6;
        private Button sil_btn;
        private Button edit_btn;
        private Button ekle_btn;
        private DataGridView jokerView;
        private TextBox IBAN_txt;
        private Label label3;
        private TextBox soyad_txt;
        private Label label4;
        private TextBox ad_txt;
        private Label label2;
        private TextBox TC_txt;
        private Label label5;
        private Label label1;
        private Button back;
    }
}