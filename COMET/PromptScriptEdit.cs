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
        //cell to use for editing
        DataGridViewCell cell;
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

                SmartButtonToEdit.storedScript.currentScript = ScriptToEdit.convertDataTableToScript((DataTable)dataGridView1.DataSource);
                SmartButtonToEdit.storedScript = ScriptToEdit;

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception) { }
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

        private void dataGridView1_Click(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                DataGridViewCell cell = (DataGridViewCell)dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex];
                if (cell.Value.ToString() == "")
                {
                    if (true)
                    {
                        //cell.Value = "X";
                    }
                    else
                    {
                        //cell.Value = "Y";
                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine("error writing to cell");
            }
        }

        private void dataGridView1_CellContextMenuStripNeeded(object sender, DataGridViewCellContextMenuStripNeededEventArgs e)
        {
            try
            {
                //populate the cell that was clicked
                cell = (DataGridViewCell)dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex];

                //a popup for entering data into cells
                locationToolStripMenuItem.Text = "(" + e.ColumnIndex.ToString() + " , " + e.RowIndex.ToString() + ")";

                //decide what to display based on the cell chose
                if (e.ColumnIndex == 0)
                {
                    //Type
                    CommToolStripMenuItem.Enabled = false;
                    TypeToolStripMenuItem.Enabled = true;
                }
                else if (e.ColumnIndex == 1)
                {
                    //Commands
                    CommToolStripMenuItem.Enabled = true;
                    TypeToolStripMenuItem.Enabled = false;
                }
                else
                {
                    //Arguments
                    CommToolStripMenuItem.Enabled = false;
                    TypeToolStripMenuItem.Enabled = false;
                }
            }
            catch (Exception) { }
        }

        private void sERIALToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //the cell that was clicked is populated in dataGridView1_CellContextMenuStripNeeded
            cell.Value = "SERIAL";
        }

        private void fUNCTIONToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //the cell that was clicked is populated in dataGridView1_CellContextMenuStripNeeded
            cell.Value = "FUNCTION";
        }

        private void CommToolStripMenuItem_MouseEnter(object sender, EventArgs e)
        {
            //load the available commands from the enum
            var commands = Enum.GetValues(typeof(ScriptCommands)).Cast<ScriptCommands>();

            this.CommToolStripMenuItem.DropDownItems.Clear();

            try
            {
                foreach (var ScriptCommand in commands)
                {
                    ToolStripItem subItem = new ToolStripMenuItem(ScriptCommand.ToString());
                    this.CommToolStripMenuItem.DropDownItems.Add(subItem);
                    //register an event
                    subItem.Click += new System.EventHandler(loadCommandEvent);
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Couldn't load commands from enum");
            }
        }

        private void loadCommandEvent(object sender, EventArgs e)
        {
            //write the command into the textbox that was clicked
            try
            {
                string commandname = ((ToolStripMenuItem)sender).Text;
                //the cell that was clicked is populated in dataGridView1_CellContextMenuStripNeeded
                cell.Value = commandname;
            }
            catch (Exception)
            {
                Console.Write("Unable to write command to button");
            }
        }
    }
}
