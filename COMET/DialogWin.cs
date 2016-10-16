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
    public partial class DialogWin : Form
    {
        private Label displaytext;
        private Button buttonOK;
        private Label detail;
        private bool SingleWindow = false;
    
        public DialogWin(bool single_window)
        {
            InitializeComponent();
            SingleWindow = single_window;
        }

        private void InitializeComponent()
        {
            this.displaytext = new System.Windows.Forms.Label();
            this.buttonOK = new System.Windows.Forms.Button();
            this.detail = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // displaytext
            // 
            this.displaytext.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.displaytext.AutoSize = true;
            this.displaytext.Location = new System.Drawing.Point(66, 9);
            this.displaytext.Name = "displaytext";
            this.displaytext.Size = new System.Drawing.Size(158, 13);
            this.displaytext.TabIndex = 0;
            this.displaytext.Text = "Display this message to the user";
            this.displaytext.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            this.displaytext.Click += new System.EventHandler(this.displaytext_Click);
            // 
            // buttonOK
            // 
            this.buttonOK.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.buttonOK.Location = new System.Drawing.Point(96, 72);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(91, 23);
            this.buttonOK.TabIndex = 1;
            this.buttonOK.Text = "OK";
            this.buttonOK.UseVisualStyleBackColor = true;
            this.buttonOK.Click += new System.EventHandler(this.buttonOK_Click);
            // 
            // detail
            // 
            this.detail.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.detail.Location = new System.Drawing.Point(0, 98);
            this.detail.Name = "detail";
            this.detail.Size = new System.Drawing.Size(284, 23);
            this.detail.TabIndex = 2;
            this.detail.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // DialogWin
            // 
            this.ClientSize = new System.Drawing.Size(284, 121);
            this.Controls.Add(this.detail);
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.displaytext);
            this.MaximumSize = new System.Drawing.Size(500, 160);
            this.MinimumSize = new System.Drawing.Size(300, 160);
            this.Name = "DialogWin";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        public void setDetail(string text)
        {
            this.detail.Text = text;
        }

        public void setFail(string detailtext)
        {
            this.setDetail(detailtext);
            this.BackColor = Color.Red;
            this.displaytext.Text = "FAILED";
            this.displaytext.Font = new Font("Arial", 30,FontStyle.Bold);
            this.displaytext.TextAlign = ContentAlignment.MiddleCenter;
            this.Show();
            this.Focus();
        }

        public void setPass(string detailtext)
        {
            this.setDetail(detailtext);
            this.BackColor = Color.LightGreen;
            this.displaytext.Text = "PASSED";
            this.displaytext.Font = new Font("Arial", 30, FontStyle.Bold);
            this.displaytext.TextAlign = ContentAlignment.MiddleCenter;
            this.Show();
            this.Focus();
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            this.Hide();
            if(!SingleWindow)this.Close();
        }

        private void displaytext_Click(object sender, EventArgs e)
        {

        }
    }
}
