namespace WinFormsApp2
{
    partial class Admin
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
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Admin));
            label2 = new Label();
            label3 = new Label();
            txtUsername = new TextBox();
            txtPassword = new TextBox();
            ekle_btn = new Button();
            sil_btn = new Button();
            back = new Button();
            adminView = new DataGridView();
            contextMenuStrip1 = new ContextMenuStrip(components);
            ((System.ComponentModel.ISupportInitialize)adminView).BeginInit();
            SuspendLayout();
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(45, 140);
            label2.Name = "label2";
            label2.Size = new Size(73, 15);
            label2.TabIndex = 2;
            label2.Text = "Kullanıcı Adı";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(45, 177);
            label3.Name = "label3";
            label3.Size = new Size(30, 15);
            label3.TabIndex = 3;
            label3.Text = "Şifre";
            // 
            // txtUsername
            // 
            txtUsername.Location = new Point(175, 137);
            txtUsername.Name = "txtUsername";
            txtUsername.Size = new Size(100, 23);
            txtUsername.TabIndex = 4;
            // 
            // txtPassword
            // 
            txtPassword.Location = new Point(175, 174);
            txtPassword.Name = "txtPassword";
            txtPassword.Size = new Size(100, 23);
            txtPassword.TabIndex = 5;
            // 
            // ekle_btn
            // 
            ekle_btn.Location = new Point(45, 264);
            ekle_btn.Name = "ekle_btn";
            ekle_btn.Size = new Size(75, 23);
            ekle_btn.TabIndex = 6;
            ekle_btn.Text = "EKLE";
            ekle_btn.UseVisualStyleBackColor = true;
            ekle_btn.Click += ekle_btn_Click;
            // 
            // sil_btn
            // 
            sil_btn.Location = new Point(286, 264);
            sil_btn.Name = "sil_btn";
            sil_btn.Size = new Size(75, 23);
            sil_btn.TabIndex = 8;
            sil_btn.Text = "SİL";
            sil_btn.UseVisualStyleBackColor = true;
            sil_btn.Click += sil_btn_Click;
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
            back.TabIndex = 37;
            back.UseVisualStyleBackColor = true;
            back.Click += back_Click;
            // 
            // adminView
            // 
            adminView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            adminView.Location = new Point(512, 12);
            adminView.Name = "adminView";
            adminView.RowTemplate.Height = 25;
            adminView.Size = new Size(439, 523);
            adminView.TabIndex = 38;
            // 
            // contextMenuStrip1
            // 
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.Size = new Size(61, 4);
            // 
            // Admin
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(963, 547);
            Controls.Add(adminView);
            Controls.Add(back);
            Controls.Add(sil_btn);
            Controls.Add(ekle_btn);
            Controls.Add(txtPassword);
            Controls.Add(txtUsername);
            Controls.Add(label3);
            Controls.Add(label2);
            Name = "Admin";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Admin";
            Load += Admin_Load;
            ((System.ComponentModel.ISupportInitialize)adminView).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private Label label2;
        private Label label3;
        private TextBox txtUsername;
        private TextBox txtPassword;
        private Button ekle_btn;
        private Button sil_btn;
        private Button back;
        private DataGridView adminView;
        private ContextMenuStrip contextMenuStrip1;
    }
}