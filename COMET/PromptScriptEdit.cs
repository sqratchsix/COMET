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
        int timeSelectedIndex = 1; //min
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
            //figure out ms as sec
            this.comboBoxtime.SelectedIndex = timeSelectedIndex;
            this.textBoxLoopDelay.Text = Utilities.convertMS(ScriptToEdit.getLoopTime(), comboBoxtime.Text, false).ToString();
            
            //this.textBoxLoopDelay.Text = ScriptToEdit.getLoopTime().ToString();
            

        }

        public PromptScriptEdit()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //collect all the changed parameters
            double newLoopTime = 0;
            int newLoops = 0;
            int defaultDelayTime = 0;

            try
            {
                double.TryParse(textBoxLoopDelay.Text, out newLoopTime);
                int.TryParse(textBoxNumLoops.Text, out newLoops);
                int.TryParse(textBoxDelayTime.Text, out defaultDelayTime);

                //convert the loop time
                newLoopTime = (int)Utilities.convertMS(newLoopTime, comboBoxtime.Text, true);

                //update the delay time & loop paramters
                ScriptToEdit.setLoopParamters((int)newLoopTime, newLoops);
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

        private void comboBoxtime_SelectedIndexChanged(object sender, EventArgs e)
        {
            //convert the previous time to MS, then convert to the new time
            double oldTime = 0;
            double.TryParse(textBoxLoopDelay.Text, out oldTime);
            int ms = (int)Utilities.convertMS(oldTime, comboBoxtime.Items[timeSelectedIndex].ToString(), true);

            this.textBoxLoopDelay.Text = Utilities.convertMS(ms, comboBoxtime.Text, false).ToString();

            //now update to the current time variable index
            timeSelectedIndex = comboBoxtime.SelectedIndex;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
