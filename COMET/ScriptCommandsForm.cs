using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Comet1
{
    public partial class ScriptCommandsForm : Form
    {
        public ScriptCommandsForm()
        {
            InitializeComponent();
            SetupGrid();
            PopulateCommands();
        }

        private void SetupGrid()
        {
            dataGridView1.Columns.Clear();
            dataGridView1.Columns.Add("Command", "Command");
            dataGridView1.Columns.Add("Description", "Description");
            dataGridView1.Columns.Add("Example", "Example");
            dataGridView1.Columns[0].Width = 140;
            dataGridView1.Columns[1].Width = 280;
            dataGridView1.Columns[2].Width = 290;
        }

        private void PopulateCommands()
        {
            dataGridView1.Rows.Clear();
            var commands = Enum.GetValues(typeof(ScriptCommands)).Cast<ScriptCommands>();
            foreach (var cmd in commands)
            {
                dataGridView1.Rows.Add(cmd.ToString(), GetDescription(cmd), GetExample(cmd));
            }
        }

        private string GetDescription(ScriptCommands cmd)
        {
            switch (cmd)
            {
                case ScriptCommands.delay: return "Pause execution for specified milliseconds";
                case ScriptCommands.sleep: return "Alias for delay (sleep)";
                case ScriptCommands.serialbreak: return "Send a serial break on the port";
                case ScriptCommands.sbreak: return "Short break (alias)";
                case ScriptCommands.rts: return "Set RTS line (arguments: 0/1)";
                case ScriptCommands.dtr: return "Set DTR line (arguments: 0/1)";
                case ScriptCommands.settings: return "Change serial port settings";
                case ScriptCommands.time: return "Insert timestamp or wait for time-related event";
                case ScriptCommands.response_str: return "Wait for a string response and capture it";
                case ScriptCommands.response_int_between: return "Check numeric response falls between two values";
                case ScriptCommands.response_log: return "Log response to file";
                case ScriptCommands.user_input_command: return "Prompt user for command input during script";
                case ScriptCommands.user_input_string: return "Prompt user for string input during script";
                case ScriptCommands.ymodem: return "Initiate a YModem file transfer (arguments: filepath)";
                case ScriptCommands.set_writenewline: return "Configure write newline behavior";
                case ScriptCommands.set_readnewline: return "Configure read newline behavior";
                default: return "(no description)";
            }
        }

        private string GetExample(ScriptCommands cmd)
        {
            switch (cmd)
            {
                case ScriptCommands.delay:                  return "**delay\t1000";
                case ScriptCommands.sleep:                  return "**sleep\t500";
                case ScriptCommands.serialbreak:            return "**serialbreak";
                case ScriptCommands.sbreak:                 return "**sbreak";
                case ScriptCommands.rts:                    return "**rts\t1";
                case ScriptCommands.dtr:                    return "**dtr\t0";
                case ScriptCommands.settings:               return "**settings\t9600\t8\tN\t1";
                case ScriptCommands.time:                   return "**time";
                case ScriptCommands.response_str:           return "**response_str\tOK\t5000";
                case ScriptCommands.response_int_between:   return "**response_int_between\t0\t100";
                case ScriptCommands.response_log:           return "**response_log\tresult.txt";
                case ScriptCommands.user_input_command:     return "**user_input_command";
                case ScriptCommands.user_input_string:      return "**user_input_string\tEnter value:";
                case ScriptCommands.ymodem:                 return "**ymodem\tC:\\firmware\\image.bin";
                case ScriptCommands.set_writenewline:       return "**set_writenewline\t1";
                case ScriptCommands.set_readnewline:        return "**set_readnewline\t1";
                default:                                    return "";
            }
        }

        private void buttonCopy_Click(object sender, EventArgs e)
        {
            var lines = new List<string>();
            if (dataGridView1.SelectedRows.Count > 0)
            {
                foreach (DataGridViewRow row in dataGridView1.SelectedRows)
                {
                    lines.Add(row.Cells[0].Value + "\t" + row.Cells[1].Value + "\t" + row.Cells[2].Value);
                }
            }
            else
            {
                foreach (DataGridViewRow row in dataGridView1.Rows)
                {
                    if (row.IsNewRow) continue;
                    lines.Add(row.Cells[0].Value + "\t" + row.Cells[1].Value + "\t" + row.Cells[2].Value);
                }
            }
            try { Clipboard.SetText(string.Join(Environment.NewLine, lines)); } catch { }
        }
    }
}
