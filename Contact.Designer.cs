namespace WinFormsApp2
{
    partial class Contact
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Contact));
            back = new Button();
            textBox1 = new TextBox();
            textBox2 = new TextBox();
            label1 = new Label();
            label2 = new Label();
            SuspendLayout();
            // 
            // back
            // 
            back.BackColor = SystemColors.Control;
            back.BackgroundImage = (Image)resources.GetObject("back.BackgroundImage");
            back.BackgroundImageLayout = ImageLayout.Stretch;
            back.Cursor = Cursors.Hand;
            back.FlatAppearance.BorderSize = 0;
            back.FlatStyle = FlatStyle.Flat;
            back.ForeColor = SystemColors.ControlText;
            back.Location = new Point(12, 12);
            back.Name = "back";
            back.Size = new Size(25, 25);
            back.TabIndex = 37;
            back.UseVisualStyleBackColor = false;
            back.Click += back_Click;
            // 
            // textBox1
            // 
            textBox1.Location = new Point(300, 100);
            textBox1.Name = "textBox1";
            textBox1.ReadOnly = true;
            textBox1.Size = new Size(100, 23);
            textBox1.TabIndex = 38;
            textBox1.Text = "0553 002 8652";
            textBox1.TextAlign = HorizontalAlignment.Center;
            // 
            // textBox2
            // 
            textBox2.Location = new Point(300, 200);
            textBox2.Name = "textBox2";
            textBox2.ReadOnly = true;
            textBox2.Size = new Size(100, 23);
            textBox2.TabIndex = 39;
            textBox2.Text = "0507 877 4096";
            textBox2.TextAlign = HorizontalAlignment.Center;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(150, 100);
            label1.Name = "label1";
            label1.Size = new Size(97, 15);
            label1.TabIndex = 40;
            label1.Text = "FURKAN YILMAZ";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(150, 200);
            label2.Name = "label2";
            label2.Size = new Size(88, 15);
            label2.TabIndex = 42;
            label2.Text = "FURKAN DENİZ";
            // 
            // Contact
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(600, 338);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(textBox2);
            Controls.Add(textBox1);
            Controls.Add(back);
            FormBorderStyle = FormBorderStyle.None;
            Name = "Contact";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Contact";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button back;
        private TextBox textBox1;
        private TextBox textBox2;
        private Label label1;
        private Label label2;
    }
}