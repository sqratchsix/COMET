using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Collections;
using System.IO;
using System.Data;

namespace Comet1
{
    public class ScriptRunner
        {
            //currentScript is an ArrayList of script items
            //each script item is its own ArrayList that starts with a commandtype (SERIAL or FUNCTION)
            //if the command type is serial, the command to be sent is a serial command
            //if it is a function, it can be followed by arguments
            public ArrayList currentScript;
            public DataTable dt;

            int delayMS = 1000;     //default delay time between commands
            public string scriptName = "";
            public string scriptPath = "";

            bool stop = false;

            //looping parameters
            int loopTimeMS = 5000;
            int loopCount = 1;

            public ScriptRunner(int delay_in_MS)
            {
                this.delayMS = delay_in_MS;
            }

            public bool loadScriptFromFile()
            {
                // Create an instance of the open file dialog box.
                OpenFileDialog openFileDialog1 = new OpenFileDialog();

                // Set filter options and filter index.
                openFileDialog1.Filter = "Text Files (.txt)|*.txt|All Files (*.*)|*.*";
                openFileDialog1.FilterIndex = 1;

                openFileDialog1.Multiselect = false;

                //openfile dialog was hanging after .net4 upgrade - this fixes it?
                openFileDialog1.ShowHelp = true;

                // Process input if the user clicked OK.
                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string readThisFile = openFileDialog1.FileName;
                        //give this script a name
                        this.scriptPath = readThisFile;
                        this.scriptName = Path.GetFileName(readThisFile);
                        addCommandsIntoCurrentScript(File.ReadAllLines(readThisFile));
                        return true;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Couldn't Load Script");
                        return false;
                    }

                }
                return false;
            }

            public void clearCurrentScript()
            {
                currentScript = new ArrayList();
            }

            public void changeDelay(int newDelayMS)
            {
                this.delayMS = newDelayMS;
            }

            public int getDelay()
            {
                return delayMS;
            }

            public void setLoopParamters(int loopTime, int loops)
            {
                this.loopTimeMS = loopTime;
                this.loopCount = loops;
            }

            public int getLoopTime()
            {
                return this.loopTimeMS;
            }
            public int getLoopCount()
            {
                return this.loopCount;
            }

            public void addCommandIntoCurrentScript(string dataIn)
            {
                addCommandsIntoCurrentScript(new string[] { dataIn });
            }

            public void addCommandsIntoCurrentScript(string[] dataIn)
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

            public DataTable convertScriptToDataTable()
            {
                DataTable tempDT = new DataTable();

                //create the default columns 
                List<string> cols = new List<string>(new string[] { "Type", "Command", "Arg1", "Arg2", "Arg3", "Arg4", "Arg5" });
                for (int i = 0; i < cols.Count; i++)
                {
                    DataColumn column = new DataColumn();
                    column.DataType = Type.GetType("System.String");
                    column.ColumnName = cols[i];
                    tempDT.Columns.Add(column);
                    //add each of the commands      
                }
                
                    foreach (ArrayList commandlist in currentScript)
                    {
                        DataRow row = tempDT.NewRow();
                        //most commands do not have multiple arguments, so only add what's there
                        for (int i = 0; i < commandlist.Count; i++)
                        {
                            row[cols[i]] = commandlist[i];
                        }
                        tempDT.Rows.Add(row);
                    }

                return tempDT;
            }


            internal void Close()
            {
                stop = true;
            }
        }



}
