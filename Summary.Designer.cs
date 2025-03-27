namespace WinFormsApp2
{
    partial class Summary
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Summary));
            back = new Button();
            panel1 = new Panel();
            jokerLabel = new Label();
            panel2 = new Panel();
            label1 = new Label();
            panel3 = new Panel();
            personelLbl = new Label();
            panel4 = new Panel();
            label3 = new Label();
            panel5 = new Panel();
            bugunLbl = new Label();
            panel6 = new Panel();
            label4 = new Label();
            panel7 = new Panel();
            toplamLbl = new Label();
            panel8 = new Panel();
            label5 = new Label();
            panel1.SuspendLayout();
            panel2.SuspendLayout();
            panel3.SuspendLayout();
            panel4.SuspendLayout();
            panel5.SuspendLayout();
            panel6.SuspendLayout();
            panel7.SuspendLayout();
            panel8.SuspendLayout();
            SuspendLayout();
            // 
            // back
            // 
            back.BackgroundImage = (Image)resources.GetObject("back.BackgroundImage");
            back.BackgroundImageLayout = ImageLayout.Stretch;
            back.Cursor = Cursors.Hand;
            back.FlatAppearance.BorderSize = 0;
            back.FlatStyle = FlatStyle.Flat;
            back.ForeColor = Color.Orange;
            back.Location = new Point(12, 12);
            back.Name = "back";
            back.Size = new Size(25, 25);
            back.TabIndex = 37;
            back.UseVisualStyleBackColor = true;
            back.Click += back_Click;
            // 
            // panel1
            // 
            panel1.Controls.Add(jokerLabel);
            panel1.Controls.Add(panel2);
            panel1.Location = new Point(76, 48);
            panel1.Name = "panel1";
            panel1.Size = new Size(200, 100);
            panel1.TabIndex = 38;
            // 
            // jokerLabel
            // 
            jokerLabel.AutoSize = true;
            jokerLabel.Location = new Point(81, 33);
            jokerLabel.Name = "jokerLabel";
            jokerLabel.Size = new Size(38, 15);
            jokerLabel.TabIndex = 39;
            jokerLabel.Text = "label2";
            // 
            // panel2
            // 
            panel2.Controls.Add(label1);
            panel2.Location = new Point(0, 80);
            panel2.Name = "panel2";
            panel2.Size = new Size(200, 20);
            panel2.TabIndex = 39;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(50, 2);
            label1.Name = "label1";
            label1.Size = new Size(97, 15);
            label1.TabIndex = 39;
            label1.Text = "JOKER KİŞİ SAYISI";
            // 
            // panel3
            // 
            panel3.Controls.Add(personelLbl);
            panel3.Controls.Add(panel4);
            panel3.Location = new Point(431, 48);
            panel3.Name = "panel3";
            panel3.Size = new Size(200, 100);
            panel3.TabIndex = 39;
            // 
            // personelLbl
            // 
            personelLbl.AutoSize = true;
            personelLbl.Location = new Point(81, 33);
            personelLbl.Name = "personelLbl";
            personelLbl.Size = new Size(38, 15);
            personelLbl.TabIndex = 39;
            personelLbl.Text = "label2";
            // 
            // panel4
            // 
            panel4.Controls.Add(label3);
            panel4.Location = new Point(0, 80);
            panel4.Name = "panel4";
            panel4.Size = new Size(200, 20);
            panel4.TabIndex = 39;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(50, 2);
            label3.Name = "label3";
            label3.Size = new Size(98, 15);
            label3.TabIndex = 39;
            label3.Text = "PERSONEL SAYISI";
            // 
            // panel5
            // 
            panel5.Controls.Add(bugunLbl);
            panel5.Controls.Add(panel6);
            panel5.Location = new Point(76, 194);
            panel5.Name = "panel5";
            panel5.Size = new Size(200, 100);
            panel5.TabIndex = 40;
            // 
            // bugunLbl
            // 
            bugunLbl.AutoSize = true;
            bugunLbl.Location = new Point(81, 33);
            bugunLbl.Name = "bugunLbl";
            bugunLbl.Size = new Size(38, 15);
            bugunLbl.TabIndex = 39;
            bugunLbl.Text = "label2";
            // 
            // panel6
            // 
            panel6.Controls.Add(label4);
            panel6.Location = new Point(0, 80);
            panel6.Name = "panel6";
            panel6.Size = new Size(200, 20);
            panel6.TabIndex = 39;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(30, 2);
            label4.Name = "label4";
            label4.Size = new Size(138, 15);
            label4.TabIndex = 39;
            label4.Text = "BUGÜN İZİNLİ KİŞİ SAYISI";
            // 
            // panel7
            // 
            panel7.Controls.Add(toplamLbl);
            panel7.Controls.Add(panel8);
            panel7.Location = new Point(431, 194);
            panel7.Name = "panel7";
            panel7.Size = new Size(200, 100);
            panel7.TabIndex = 41;
            // 
            // toplamLbl
            // 
            toplamLbl.AutoSize = true;
            toplamLbl.Location = new Point(81, 33);
            toplamLbl.Name = "toplamLbl";
            toplamLbl.Size = new Size(38, 15);
            toplamLbl.TabIndex = 39;
            toplamLbl.Text = "label2";
            // 
            // panel8
            // 
            panel8.Controls.Add(label5);
            panel8.Location = new Point(0, 80);
            panel8.Name = "panel8";
            panel8.Size = new Size(200, 20);
            panel8.TabIndex = 39;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(23, 2);
            label5.Name = "label5";
            label5.Size = new Size(154, 15);
            label5.TabIndex = 39;
            label5.Text = "TOPLAM KAYITLI KİŞİ SAYISI";
            // 
            // Summary
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Orange;
            ClientSize = new Size(800, 450);
            Controls.Add(panel7);
            Controls.Add(panel5);
            Controls.Add(panel3);
            Controls.Add(panel1);
            Controls.Add(back);
            FormBorderStyle = FormBorderStyle.None;
            Name = "Summary";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Summary";
            Load += Summary_Load;
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            panel2.ResumeLayout(false);
            panel2.PerformLayout();
            panel3.ResumeLayout(false);
            panel3.PerformLayout();
            panel4.ResumeLayout(false);
            panel4.PerformLayout();
            panel5.ResumeLayout(false);
            panel5.PerformLayout();
            panel6.ResumeLayout(false);
            panel6.PerformLayout();
            panel7.ResumeLayout(false);
            panel7.PerformLayout();
            panel8.ResumeLayout(false);
            panel8.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Button back;
        private Panel panel1;
        private Label jokerLabel;
        private Panel panel2;
        private Label label1;
        private Panel panel3;
        private Label personelLbl;
        private Panel panel4;
        private Label label3;
        private Panel panel5;
        private Label bugunLbl;
        private Panel panel6;
        private Label label4;
        private Panel panel7;
        private Label toplamLbl;
        private Panel panel8;
        private Label label5;
    }
}