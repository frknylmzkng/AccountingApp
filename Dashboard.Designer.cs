namespace WinFormsApp2
{
    partial class Dashboard
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
            panel1 = new Panel();
            button3 = new Button();
            button2 = new Button();
            button1 = new Button();
            panel2 = new Panel();
            button4 = new Button();
            panel3 = new Panel();
            button6 = new Button();
            button5 = new Button();
            panel1.SuspendLayout();
            panel2.SuspendLayout();
            panel3.SuspendLayout();
            SuspendLayout();
            // 
            // panel1
            // 
            panel1.Controls.Add(button3);
            panel1.Controls.Add(button2);
            panel1.Controls.Add(button1);
            panel1.Location = new Point(0, 99);
            panel1.Name = "panel1";
            panel1.Size = new Size(963, 351);
            panel1.TabIndex = 0;
            // 
            // button3
            // 
            button3.Location = new Point(659, 181);
            button3.Name = "button3";
            button3.Size = new Size(250, 25);
            button3.TabIndex = 2;
            button3.TabStop = false;
            button3.Text = "KİŞİ ARAMA";
            button3.UseVisualStyleBackColor = true;
            button3.Click += button3_Click;
            // 
            // button2
            // 
            button2.Location = new Point(53, 181);
            button2.Name = "button2";
            button2.Size = new Size(250, 25);
            button2.TabIndex = 1;
            button2.TabStop = false;
            button2.Text = "JOKER PERSONEL İŞLEMLERİ";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // button1
            // 
            button1.Location = new Point(356, 181);
            button1.Name = "button1";
            button1.Size = new Size(250, 25);
            button1.TabIndex = 0;
            button1.TabStop = false;
            button1.Text = "GENEL PERSONEL İŞLEMLERİ";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // panel2
            // 
            panel2.Controls.Add(button4);
            panel2.Dock = DockStyle.Top;
            panel2.Location = new Point(0, 0);
            panel2.Name = "panel2";
            panel2.Size = new Size(963, 100);
            panel2.TabIndex = 1;
            // 
            // button4
            // 
            button4.Location = new Point(365, 41);
            button4.Name = "button4";
            button4.Size = new Size(250, 25);
            button4.TabIndex = 0;
            button4.TabStop = false;
            button4.Text = "YÖNETİCİ BİLGİLERİ İŞLEMLERİ";
            button4.UseVisualStyleBackColor = true;
            button4.Click += button4_Click;
            // 
            // panel3
            // 
            panel3.Controls.Add(button6);
            panel3.Controls.Add(button5);
            panel3.Dock = DockStyle.Bottom;
            panel3.Location = new Point(0, 447);
            panel3.Name = "panel3";
            panel3.Size = new Size(963, 100);
            panel3.TabIndex = 2;
            // 
            // button6
            // 
            button6.Location = new Point(558, 38);
            button6.Name = "button6";
            button6.Size = new Size(250, 25);
            button6.TabIndex = 1;
            button6.TabStop = false;
            button6.Text = "İLETİŞİM";
            button6.UseVisualStyleBackColor = true;
            button6.Click += button6_Click;
            // 
            // button5
            // 
            button5.Location = new Point(154, 38);
            button5.Name = "button5";
            button5.Size = new Size(250, 25);
            button5.TabIndex = 0;
            button5.TabStop = false;
            button5.Text = "GENEL DURUM GÖRÜNTÜLE";
            button5.UseVisualStyleBackColor = true;
            button5.Click += button5_Click;
            // 
            // Dashboard
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(963, 547);
            Controls.Add(panel3);
            Controls.Add(panel2);
            Controls.Add(panel1);
            Name = "Dashboard";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Dashboard";
            panel1.ResumeLayout(false);
            panel2.ResumeLayout(false);
            panel3.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private Panel panel1;
        private Button button3;
        private Button button2;
        private Button button1;
        private Panel panel2;
        private Button button4;
        private Panel panel3;
        private Button button6;
        private Button button5;
    }
}