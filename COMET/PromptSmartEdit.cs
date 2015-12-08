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
    public partial class PromptSmartButtonEdit : Form
    {
        SmartButton SmartButtonToEdit;
        public PromptSmartButtonEdit(SmartButton SmartIn)
        {
            InitializeComponent();
            this.SmartButtonToEdit = SmartIn;
            populateCurrentAttributes();
            this.Show();
        }

        private void populateCurrentAttributes()
        {
            this.textBoxCommand.Text = SmartButtonToEdit.CommandToSend;
            this.textBoxDescription.Text = SmartButtonToEdit.CommandDescription;
        }

        private void updateSmartButton()
        {
            SmartButtonToEdit.changeCommand(this.textBoxCommand.Text);
            SmartButtonToEdit.changeDescription(this.textBoxDescription.Text);
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            updateSmartButton();
            this.Dispose();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.Dispose();
        }
    }
}
