using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;


namespace Comet1
{
    public class SmartButton : System.Windows.Forms.Button
    {
        public String CommandToSend; // { get { return lastCommand; } set { lastCommand = value; } }
        public String CommandDescription;
        //constructor
        public SmartButton(String InputCommand, String InputDescription, Boolean displayCMD)
        {
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


        //cm.MenuItems.Add("Item 1", new EventHandler(Removepicture_Click));             
        //cm.MenuItems.Add("Item 2", new EventHandler(Addpicture_Click)); 
    }
    public static class RichTextBoxExtensions
    {
        public static void AppendText(System.Windows.Forms.RichTextBox box, string text, System.Drawing.Color color)
        {
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;

            box.SelectionColor = color;
            box.AppendText(text);
            box.SelectionColor = box.ForeColor;

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
