using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Comet1
{
    public partial class About : Form
    {
        private Label labelName;
        private Label label2;
        private Label labelDevelopers;
        private Label labelVersion;
        private Label labelPath;
        private Button button2;
        private Button button1;
    
        public About()
        { 
            InitializeComponent();
            labelVersion.Text = Application.ProductVersion;
            labelName.Text = Application.ProductName;
            labelPath.Text = Application.ExecutablePath;
            labelDevelopers.Text = "J.Simon";
            this.Show();
        }

        private void InitializeComponent()
        {
            this.button1 = new System.Windows.Forms.Button();
            this.labelName = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.labelDevelopers = new System.Windows.Forms.Label();
            this.labelVersion = new System.Windows.Forms.Label();
            this.labelPath = new System.Windows.Forms.Label();
            this.button2 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(234, 85);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 0;
            this.button1.Text = "Close";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // labelName
            // 
            this.labelName.AutoSize = true;
            this.labelName.Location = new System.Drawing.Point(12, 13);
            this.labelName.Name = "labelName";
            this.labelName.Size = new System.Drawing.Size(35, 13);
            this.labelName.TabIndex = 1;
            this.labelName.Text = "Name";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(11, 59);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(76, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Developed by:";
            // 
            // labelDevelopers
            // 
            this.labelDevelopers.AutoSize = true;
            this.labelDevelopers.Location = new System.Drawing.Point(106, 59);
            this.labelDevelopers.Name = "labelDevelopers";
            this.labelDevelopers.Size = new System.Drawing.Size(47, 13);
            this.labelDevelopers.TabIndex = 3;
            this.labelDevelopers.Text = "J. Simon";
            // 
            // labelVersion
            // 
            this.labelVersion.AutoSize = true;
            this.labelVersion.Location = new System.Drawing.Point(106, 13);
            this.labelVersion.Name = "labelVersion";
            this.labelVersion.Size = new System.Drawing.Size(75, 13);
            this.labelVersion.TabIndex = 4;
            this.labelVersion.Text = "currentVersion";
            // 
            // labelPath
            // 
            this.labelPath.AutoSize = true;
            this.labelPath.Location = new System.Drawing.Point(12, 35);
            this.labelPath.Name = "labelPath";
            this.labelPath.Size = new System.Drawing.Size(28, 13);
            this.labelPath.TabIndex = 5;
            this.labelPath.Text = "path";
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(13, 84);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(140, 23);
            this.button2.TabIndex = 6;
            this.button2.Text = "Send Bug Report";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // About
            // 
            this.ClientSize = new System.Drawing.Size(321, 120);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.labelPath);
            this.Controls.Add(this.labelVersion);
            this.Controls.Add(this.labelDevelopers);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.labelName);
            this.Controls.Add(this.button1);
            this.Name = "About";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string reportText = Application.ExecutablePath + "  ::  "+ Application.ProductName + "  ::  "+ Application.ProductVersion;
            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = "mailto:jared.b.simon@gmail.com?subject=COMET : BUG : " + DateTime.Now.ToString() + "&body=" + reportText; ;
            proc.Start();
            this.Close();
        }


    }
}
