namespace WinFormsApp2
{
    partial class Giris
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
            giris_btn = new Button();
            label4 = new Label();
            password_txt = new TextBox();
            username_txt = new TextBox();
            label5 = new Label();
            statusLabel = new Label();
            SuspendLayout();
            // 
            // giris_btn
            // 
            giris_btn.Location = new Point(330, 254);
            giris_btn.Name = "giris_btn";
            giris_btn.Size = new Size(125, 30);
            giris_btn.TabIndex = 2;
            giris_btn.Text = "GİRİŞ";
            giris_btn.UseVisualStyleBackColor = true;
            giris_btn.Click += giris_btn_Click;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(220, 187);
            label4.Name = "label4";
            label4.Size = new Size(30, 15);
            label4.TabIndex = 46;
            label4.Text = "Şifre";
            // 
            // password_txt
            // 
            password_txt.Location = new Point(330, 184);
            password_txt.Name = "password_txt";
            password_txt.Size = new Size(100, 23);
            password_txt.TabIndex = 1;
            password_txt.UseSystemPasswordChar = true;
            password_txt.MaxLength = PasswordHasher.MaxPasswordLength;
            // 
            // username_txt
            // 
            username_txt.Location = new Point(330, 138);
            username_txt.MaxLength = 50;
            username_txt.Name = "username_txt";
            username_txt.Size = new Size(100, 23);
            username_txt.TabIndex = 0;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(220, 141);
            label5.Name = "label5";
            label5.Size = new Size(73, 15);
            label5.TabIndex = 43;
            label5.Text = "Kullanıcı Adı";
            //
            // statusLabel
            //
            statusLabel.Location = new Point(180, 305);
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(440, 70);
            statusLabel.TextAlign = ContentAlignment.TopCenter;
            // 
            // Giris
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(label4);
            Controls.Add(password_txt);
            Controls.Add(username_txt);
            Controls.Add(label5);
            Controls.Add(giris_btn);
            Controls.Add(statusLabel);
            Name = "Giris";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Giris";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button giris_btn;
        private Label label4;
        private TextBox password_txt;
        private TextBox username_txt;
        private Label label5;
        private Label statusLabel;
    }
}
