using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Collections;
using System.IO;

namespace Comet1
{
    public class SmartButton : System.Windows.Forms.Button
    {
        public enum buttonTypes { SerialCommand, ScriptRunner, Log };
        public buttonTypes buttonType { get; set; } = 0;
        public string CommandToSend { get; set; }
        public string CommandDescription { get; set; }
        public string LogString { get; set; } = "";
        //a stored script is the smart button is being used for a script
        public ScriptRunner storedScript { get; set; }
        

        public SmartButton()
        {
        }
        //constructor
        public SmartButton(buttonTypes buttonTypeIn, string InputCommand, string InputDescription, bool displayCMD)
        {
            this.buttonType = buttonTypeIn;
            this.CommandToSend = InputCommand;
            this.CommandDescription = InputDescription;
            //default behavior has the Command as the button text
            //description should be used for tooltip

            if (displayCMD || (CommandDescription.Length < 1))
            {
                this.Text = CommandToSend;
            }
            else
            {
                this.Text = CommandDescription;
            }
            this.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        }
        public void removeThisButton()
        {
            this.Dispose();
        }
        public void changeCommand(String NewCommand)
        {
            this.CommandToSend = NewCommand;
            this.Text = this.CommandToSend;
        }
        public void changeDescription(String NewDescription)
        {
            this.CommandDescription = NewDescription;

        }
        public void addScript(ScriptRunner scriptToAdd)
        {
            this.storedScript = scriptToAdd;
        }

        public void getTime()
        {
            DateTime timeNow = DateTime.Now;
            this.LogString = timeNow.ToString();
        }

        public void ClearAllEvents()
        {
            System.ComponentModel.EventHandlerList list = this.Events;
            typeof(System.ComponentModel.EventHandlerList).GetMethod("Dispose").Invoke(list, null);
        }
    }
    public class RichTextBoxExtra : RichTextBox
    {
        //this allows multiple colors in a text box
        //need a static method so that it is thread safe
        public static void AppendText(System.Windows.Forms.RichTextBox box, string text, System.Drawing.Color color)
        {
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;

            box.SelectionColor = color;
            box.AppendText(text);
            //box.SelectionColor = box.ForeColor;
        }
        public static void ScrollDown(System.Windows.Forms.RichTextBox box)
        {
            box.ScrollToCaret();
        }
        public static void nope()
        {
        }
    }
}
