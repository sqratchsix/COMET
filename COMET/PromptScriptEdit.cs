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
    public partial class PromptScriptEdit : Form
    {
        SmartButton SmartButtonToEdit;
        ScriptRunner ScriptToEdit;
        public PromptScriptEdit(SmartButton SmartIn)
        {
            InitializeComponent();
            this.SmartButtonToEdit = SmartIn;
            this.ScriptToEdit = SmartIn.storedScript;
            populateCurrentAttributes();
            this.Show();
        }

        private void populateCurrentAttributes()
        {
            this.textBoxCommand.Text = SmartButtonToEdit.CommandToSend;
            this.textBoxDescription.Text = SmartButtonToEdit.CommandDescription;
            this.textBoxDelayTime.Text = ScriptToEdit.getDelay().ToString();
            this.dataGridView1.DataSource = ScriptToEdit.convertScriptToDataTable();

            this.textBoxNumLoops.Text = ScriptToEdit.getLoopCount().ToString();
            this.textBoxLoopDelay.Text = ScriptToEdit.getLoopTime().ToString();
        }

        public PromptScriptEdit()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //collect all the changed parameters
            int newLoopTime = 0;
            int newLoops = 0;
            int defaultDelayTime = 0;

            try
            {
                int.TryParse(textBoxLoopDelay.Text, out newLoopTime);
                int.TryParse(textBoxNumLoops.Text, out newLoops);
                int.TryParse(textBoxDelayTime.Text, out defaultDelayTime);

                //update the delay time & loop paramters
                ScriptToEdit.setLoopParamters(newLoopTime, newLoops);
                ScriptToEdit.changeDelay(defaultDelayTime);

                SmartButtonToEdit.storedScript = ScriptToEdit;

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception)
            {
            }
        }

        private void TextBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
    }
}
