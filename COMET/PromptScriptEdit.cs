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

        // Per-row hint cache used by CellPainting to draw ghost text
        private readonly Dictionary<int, string[]> _rowArgHints = new Dictionary<int, string[]>();

        private static readonly string[] _argColNames = new string[] { "Arg1", "Arg2", "Arg3", "Arg4", "Arg5" };

        // Built once – reused across all EditingControlShowing calls for fast autocomplete
        private static readonly AutoCompleteStringCollection _cmdAutoComplete = BuildCmdAutoComplete();

        private static AutoCompleteStringCollection BuildCmdAutoComplete()
        {
            var col = new AutoCompleteStringCollection();
            col.AddRange(Enum.GetNames(typeof(ScriptCommands)));
            return col;
        }

        private static readonly Dictionary<string, string[]> _cmdArgHints =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "delay",                new string[] { "ms" } },
                { "sleep",                new string[] { "ms" } },
                { "serialbreak",          new string[0] },
                { "sbreak",               new string[0] },
                { "rts",                  new string[] { "0/1" } },
                { "dtr",                  new string[] { "0/1" } },
                { "settings",             new string[] { "baud", "bits", "parity", "stop" } },
                { "time",                 new string[0] },
                { "response_str",         new string[] { "expected", "timeout_ms" } },
                { "response_int_between", new string[] { "min", "max" } },
                { "response_log",         new string[] { "filepath" } },
                { "user_input_command",   new string[0] },
                { "user_input_string",    new string[] { "prompt" } },
                { "ymodem",               new string[] { "filepath" } },
                { "set_writenewline",     new string[] { "0/1" } },
                { "set_readnewline",      new string[] { "0/1" } },
            };
        public PromptScriptEdit(SmartButton SmartIn)
        {
            InitializeComponent();
            this.SmartButtonToEdit = SmartIn;
            this.ScriptToEdit = SmartIn.storedScript;
            SetupDataGridColumns();
            populateCurrentAttributes();
        }

        private void populateCurrentAttributes()
        {
            this.textBoxCommand.Text = SmartButtonToEdit.CommandToSend;
            this.textBoxDescription.Text = SmartButtonToEdit.CommandDescription;
            this.textBoxDelayTime.Text = ScriptToEdit.getDelay().ToString();
            this.dataGridView1.DataSource = ScriptToEdit.convertScriptToDataTable();
            RefreshCommandColumnItems();
            ApplyArgHintsAllRows();

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

        // Handler invoked by the new Help->Commands menu entry
        private void HandleShowScriptCommands(object sender, EventArgs e)
        {
            var dlg = new ScriptCommandsForm();
            dlg.ShowDialog(this);
        }

        private void SetupDataGridColumns()
        {
            dataGridView1.AutoGenerateColumns = false;
            dataGridView1.Columns.Clear();

            var typeCol = new DataGridViewTextBoxColumn();
            typeCol.Name = "Type";
            typeCol.HeaderText = "Type";
            typeCol.DataPropertyName = "Type";
            dataGridView1.Columns.Add(typeCol);

            var cmdCol = new DataGridViewComboBoxColumn();
            cmdCol.Name = "Command";
            cmdCol.HeaderText = "Command";
            cmdCol.DataPropertyName = "Command";
            foreach (ScriptCommands cmd in Enum.GetValues(typeof(ScriptCommands)))
                cmdCol.Items.Add(cmd.ToString());
            dataGridView1.Columns.Add(cmdCol);

            string[] argNames = new string[] { "Arg1", "Arg2", "Arg3", "Arg4", "Arg5" };
            foreach (string arg in argNames)
            {
                var argCol = new DataGridViewTextBoxColumn();
                argCol.Name = arg;
                argCol.HeaderText = arg;
                argCol.DataPropertyName = arg;
                dataGridView1.Columns.Add(argCol);
            }

            dataGridView1.EditingControlShowing += dataGridView1_EditingControlShowing_Cmd;
            dataGridView1.DataError += dataGridView1_DataError;
            dataGridView1.CellEndEdit += dataGridView1_CellEndEdit_Cmd;
            dataGridView1.CellPainting += dataGridView1_CellPainting;
        }

        private void dataGridView1_EditingControlShowing_Cmd(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (dataGridView1.CurrentCell != null && dataGridView1.CurrentCell.ColumnIndex == 1)
            {
                ComboBox cb = e.Control as ComboBox;
                if (cb != null)
                {
                    cb.DropDownStyle = ComboBoxStyle.DropDown;
                    cb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                    cb.AutoCompleteSource = AutoCompleteSource.CustomSource;
                    cb.AutoCompleteCustomSource = _cmdAutoComplete;
                }
            }
        }

        private void dataGridView1_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
        }

        // Adds any Command values already in the grid to the combo column's Items list so that
        // DataError is never raised during rendering (the main source of dropdown lag).
        private void RefreshCommandColumnItems()
        {
            var cmdCol = dataGridView1.Columns["Command"] as DataGridViewComboBoxColumn;
            if (cmdCol == null) return;
            var dt = dataGridView1.DataSource as DataTable;
            if (dt == null) return;
            foreach (DataRow row in dt.Rows)
            {
                string val = row["Command"] != null ? row["Command"].ToString() : "";
                if (val.Length > 0 && !cmdCol.Items.Contains(val))
                    cmdCol.Items.Add(val);
            }
        }

        private void ApplyArgHintsAllRows()
        {
            _rowArgHints.Clear();
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
                ApplyArgHintsForRow(i);
        }

        private void ApplyArgHintsForRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dataGridView1.Rows.Count) return;
            var row = dataGridView1.Rows[rowIndex];
            string command = row.Cells["Command"].Value != null ? row.Cells["Command"].Value.ToString() : "";

            string[] hints;
            bool knownCmd = _cmdArgHints.TryGetValue(command, out hints);
            bool noArgs = knownCmd && hints.Length == 0;

            for (int i = 0; i < _argColNames.Length; i++)
            {
                var argCell = row.Cells[_argColNames[i]];
                if (noArgs)
                {
                    argCell.ReadOnly = true;
                    argCell.Style.BackColor = SystemColors.Control;
                    argCell.Style.ForeColor = SystemColors.GrayText;
                }
                else
                {
                    argCell.ReadOnly = false;
                    argCell.Style.BackColor = Color.Empty;
                    argCell.Style.ForeColor = Color.Empty;
                }
            }

            if (knownCmd && hints.Length > 0)
                _rowArgHints[rowIndex] = hints;
            else
                _rowArgHints.Remove(rowIndex);

            dataGridView1.InvalidateRow(rowIndex);
        }

        private void dataGridView1_CellEndEdit_Cmd(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 1)
                ApplyArgHintsForRow(e.RowIndex);
        }

        private void dataGridView1_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            // Only handle Arg1-Arg5 columns (indices 2-6)
            if (e.RowIndex < 0 || e.ColumnIndex < 2 || e.ColumnIndex > 6) return;

            int argIndex = e.ColumnIndex - 2;
            string cellValue = e.Value != null ? e.Value.ToString() : "";
            if (cellValue.Length > 0) return; // cell has content – draw normally

            string[] hints;
            if (!_rowArgHints.TryGetValue(e.RowIndex, out hints) || argIndex >= hints.Length) return;

            // Paint background, border and selection highlight, then overlay the ghost text
            e.Paint(e.ClipBounds, DataGridViewPaintParts.Background
                | DataGridViewPaintParts.Border
                | DataGridViewPaintParts.SelectionBackground
                | DataGridViewPaintParts.Focus);

            using (var brush = new SolidBrush(SystemColors.GrayText))
            {
                var rect = e.CellBounds;
                rect.Inflate(-3, 0);
                var fmt = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                e.Graphics.DrawString(hints[argIndex], e.CellStyle.Font, brush, rect, fmt);
            }
            e.Handled = true;
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
