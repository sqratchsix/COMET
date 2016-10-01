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
        private Form parent;
    
        public DialogWin()
        {
            InitializeComponent();
            this.StartPosition = FormStartPosition.CenterParent;
        }

        private void InitializeComponent()
        {
            this.displaytext = new System.Windows.Forms.Label();
            this.buttonOK = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // displaytext
            // 
            this.displaytext.AutoSize = true;
            this.displaytext.Location = new System.Drawing.Point(57, 29);
            this.displaytext.Name = "displaytext";
            this.displaytext.Size = new System.Drawing.Size(158, 13);
            this.displaytext.TabIndex = 0;
            this.displaytext.Text = "Display this message to the user";
            this.displaytext.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            this.displaytext.Click += new System.EventHandler(this.displaytext_Click);
            // 
            // buttonOK
            // 
            this.buttonOK.Location = new System.Drawing.Point(100, 79);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(75, 23);
            this.buttonOK.TabIndex = 1;
            this.buttonOK.Text = "OK";
            this.buttonOK.UseVisualStyleBackColor = true;
            this.buttonOK.Click += new System.EventHandler(this.buttonOK_Click);
            // 
            // DialogWin
            // 
            this.ClientSize = new System.Drawing.Size(284, 121);
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.displaytext);
            this.Name = "DialogWin";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        public void setFail()
        {
            this.BackColor = Color.Red;
            this.displaytext.Text = "FAILED";
            this.displaytext.Font = new Font("Arial", 30,FontStyle.Bold);
            this.displaytext.TextAlign = ContentAlignment.MiddleCenter;
            this.Show();
        }

        public void setPass()
        {
            this.BackColor = Color.LightGreen;
            this.displaytext.Text = "PASSED";
            this.displaytext.Font = new Font("Arial", 30, FontStyle.Bold);
            this.displaytext.TextAlign = ContentAlignment.MiddleCenter;
            this.Show();
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            this.Hide();
            this.Close();
        }

        private void displaytext_Click(object sender, EventArgs e)
        {

        }

        public Point centerNewWindow(Point center)
        {
            int newWinWidth = this.Width;
            int newWinHeight = this.Height;
            Point centerWait = new Point(newWinWidth / 2, newWinHeight / 2);
            return new Point(center.X + centerWait.X, center.Y + centerWait.Y);
        }
    }
}
