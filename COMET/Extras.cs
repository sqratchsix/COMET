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

    public class ScriptRunner
    {
        //currentScript is an ArrayList of script items
        //each script item is its own ArrayList that starts with a commandtype (SERIAL or FUNCTION)
        //if the command type is serial, the command to be sent is a serial command
        //if it is a function, it can be followed by arguments
        public ArrayList currentScript;
        
        public int delayMS = 1000;     //default delay time between commands

        public void loadScriptFromFile(){
            // Create an instance of the open file dialog box.
            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            // Set filter options and filter index.
            openFileDialog1.Filter = "Text Files (.txt)|*.txt|All Files (*.*)|*.*";
            openFileDialog1.FilterIndex = 1;

            openFileDialog1.Multiselect = false;

            // Process input if the user clicked OK.
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string readThisFile = openFileDialog1.FileName;
                addCommandsIntoCurrentScript(File.ReadAllLines(readThisFile));
            }
        }
       
        public void clearCurrentScript(){
            currentScript = new ArrayList();
        }

        public void changeDelay(int newDelayMS)
        {
            this.delayMS = newDelayMS;
        }
        private void addCommandsIntoCurrentScript(string[] dataIn)
        {
            //command with function structure example:  **{functionname} {tab} {argument(s)}
            // **ymodemupload {tab} C:\firmware\images\RM.00.04.2002.bin
            //
            //command example:
            // QDVER
            //

            String[] recalledData = dataIn;
            string[] stringSeparator = new string[] { "\t" };
            ArrayList newItem;

            try
            {
                if (!(recalledData == null || recalledData.Length == 0))
                {
                    for (int i = 0; i < recalledData.Length; i++)
                    {
                        String[] parsed = recalledData[i].Split(stringSeparator, StringSplitOptions.RemoveEmptyEntries);
                        //parse out the data
                        //look for lines that start with '**' which indicates a function
                        if (!(parsed == null || parsed.Length == 0))
                        {
                            //ignore skippedlines
                            if (parsed[0].Length > 0)
                            {
                                //create a new object that wil contain the data
                                newItem = new ArrayList();
                                if (parsed[0].StartsWith("**"))
                                {
                                    //the item is a function
                                    newItem.Add("FUNCTION");
                                    newItem.Add(parsed[0].Remove(0, 2));
                                }
                                else
                                {
                                    //the item is a serial command
                                    newItem.Add("SERIAL");
                                    newItem.Add(parsed[0]);
                                }

                                //add any arguments
                                for (int j = 1; j < parsed.Length; j++)
                                {
                                    if (parsed[j].Length > 0) newItem.Add(parsed[j]);
                                }
                                //finally, add the new ArrayList item into the current script
                                currentScript.Add(newItem);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Error opening / parsing saved data");
            }
        }
    }
}
