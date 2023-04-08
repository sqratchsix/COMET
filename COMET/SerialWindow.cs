using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using System.Windows.Forms;


namespace Comet1
{
    public partial class SerialWindow : Form
    {
        private ActiveSerialPort currentConnection;
        private String currentPortName = "";
        private int currentBaudRate = 9600;
        private Parity currentParity = (Parity)0;
        private int currentDataBits = 8;

        //most hardware does not support StopBits 'None' or 'OnePointFive"; will cause an exception
        private StopBits currentStopBits = (StopBits)1;

        private int timeoutMS = 500;
        private int toggleTime = 500;

        //the time to wait for no more new data before writing the received data to the terminal window
        private int portReadTimeout = 20;

        private bool Timedout = false;
        private bool portOpen = false;
        private bool textReverse = false;
        private bool rememberLastCommand = true;
        private bool showCMD = true;
        private bool keepText = false;
        private bool writeSmartButton = true;
        private bool writeSmartButtonEnabled = true;
        private bool DiscardDuplicateEntriesInList = true;
        private bool AddNewLineToTerminal = true; //Add a newline in between new input messages
        private bool endline = true;
        private bool bytespaces = true;
        private String dataToSend = "";
        private Color textReceive = Color.FromName("Lime");
        private Color textSend = Color.FromName("DarkTurquoise");
        private enumDataType DataType = enumDataType.ASCII;
        private ScriptRunner serialScript;
        private DialogWin ResultWindow;
        private bool stopThread = false;
        private int historyWidth = 0;
        private int WINDOWMARGINS1 = 30;//used to set the buttons size small enough that a horizontal scroll won't appear
        private Transfer transferData;

        //variables fot timestamping the serial string coming in or going out
        private bool timestampserial = false;
        private string timestampformat = "HH:mm:ss:ffff";

        private bool localBuffer = false;
        private string localBufferData = "";

        //variable to let safesleep know that everything is closing and it should quit
        private bool AllWindowsClosed = false;
        private System.Collections.ArrayList lastCommandList = new System.Collections.ArrayList(); //list of last typed commands
        private int lastCommandIndex = 0;

        //Filepaths for SmartButton History
        private string iniFilePath = "COMET.ini";
        private string AButtonsFilePath = "ButtonA.txt";
        private string BButtonsFilePath = "ButtonB.txt";
        private string CButtonsFilePath = "ButtonC.txt";
        private string DButtonsFilePath = "ButtonD.txt";
        private string currentHistoryFile = "";
        private string[] FilesInDirectory;

        //Data for history Buttons
        private Button lastButtonForLocation;
        //Smart Button currrently being edited - to fix a bug in getting the ContextMenu's parent
        private SmartButton SmartButtonVar;

        private Regex r = new Regex("[\\S][\\S]");
        private MatchEvaluator evaluator = new MatchEvaluator(AddSpaceAfter2);

        #region Basic setup & methods

        //Constructor
        public SerialWindow()
        {
            InitializeComponent();
            updateSerialPortList();

            initializeHistoryButtons();
            selectDefaults();
            registerEvents();

            //look for an .ini file and try to load the initial state
            loadInitialState();

            //this.AutoScaleDimensions = new System.Drawing.SizeF(5F, 12F);
            //this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        }

        public void updateSerialPortList()
        {
            string[] currentSerialPortList = SerialPort.GetPortNames();

            //trying to get the device descriptions - only seems to work for actual serial ports, not FTDI USB serial ports
            try
            {
                using (var searcher = new ManagementObjectSearcher
                ("SELECT * FROM WIN32_SerialPort"))
                //("root\\CIMV2",    "SELECT * FROM Win32_PnPEntity WHERE ClassGuid=\"{4d36e978-e325-11ce-bfc1-08002be10318}\""))
                {

                    var ports = searcher.Get().Cast<ManagementBaseObject>().ToList();
                    var tList = (from n in currentSerialPortList
                                 join p in ports on n equals p["DeviceID"].ToString()
                                 select n + " - " + p["Caption"]).ToList();

                    foreach (string s in tList)
                    {
                        Console.WriteLine(s);
                    }
                }
            }
            catch (Exception)
            {

            }
            Array.Sort(currentSerialPortList, new NaturalComparer());
            comboBoxPortName.DataSource = currentSerialPortList;
        }

        public void setComboBoxStates(bool newstate)
        {
            comboBoxPortName.Enabled = newstate;
            comboBoxBaudRate.Enabled = newstate;
            comboBoxParity.Enabled = newstate;
            comboBoxDataBits.Enabled = newstate;
            comboBoxStopBits.Enabled = newstate;
            if (newstate)
            {
                //panel2.BackColor = SystemColors.ActiveCaption;
            }
            else
            {
                //panel2.BackColor = SystemColors.Control;
            }
        }

        private void selectDefaults()
        {
            comboBoxParity.DataSource = Enum.GetValues(typeof(Parity));
            comboBoxStopBits.DataSource = Enum.GetValues(typeof(StopBits));

            if (comboBoxPortName.Items.Count > 0)
            {
                comboBoxPortName.SelectedIndex = 0;
            }
            comboBoxBaudRate.SelectedIndex = 0;
            comboBoxParity.SelectedIndex = 0;
            comboBoxDataBits.SelectedIndex = 3;
            comboBoxStopBits.SelectedIndex = 1;

            textBoxPortReadTimeout.Text = portReadTimeout.ToString();
            checkBoxAddNewLine.Checked = AddNewLineToTerminal;

            if (DataType == enumDataType.ASCII)
            {
                radioButtonASCII.Select();
            }
            updateToggleTime();
            focusInput();
        }

        private void initializeHistoryButtons()
        {
            lastButtonForLocation = button1;
            historyWidth = lastButtonForLocation.Width;
        }

        private void openNewInstanceOfSerial()
        {
            SerialWindow newInstance = new SerialWindow();
            newInstance.Size = this.Size;
            newInstance.Show();
            //try to open a new port
            newInstance.openPortActionFirstTime();
        }

        private void registerEvents()
        {
            this.Closing += new CancelEventHandler(CleanClose);
        }

        private void loadInitialState()
        {

            string directory = System.IO.Directory.GetParent(Application.ExecutablePath).FullName;

            try
            {
                //build the path based on the current directory
                
                string iniFileFullPath = directory + "\\" + iniFilePath;
                //look for the .ini file
                if (File.Exists(iniFileFullPath))
                {
                    loadSmartButtons(File.ReadAllLines(iniFileFullPath));
                    //set the panel to show the descriptions
                    showCMD = false;
                    changeHistoryButtonDisplay(showCMD);
                }
                else
                {
                    Console.WriteLine(".ini file not found");
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Error Loading .ini file");
            }

            //load the auotload buttons A,B,C,D
            string[] autobuttonsfiles = new string[] { AButtonsFilePath, BButtonsFilePath, CButtonsFilePath, DButtonsFilePath };
            Button[] abuttons = new Button[] { AButton_A, AButton_B, AButton_C, AButton_D };
            for(int afile=0; afile < autobuttonsfiles.Length;  afile++ )
            {
                //build the path based on the current directory
                string autoFileFullPath = directory + "\\" + autobuttonsfiles[afile];
                //look for the .ini file
                if (File.Exists(autoFileFullPath))
                {
                    abuttons[afile].Tag = autobuttonsfiles[afile];
                }
                else
                {
                    Console.WriteLine("autobutton file not found");
                }
            }

        }

        #endregion Basic setup & methods

        #region Serial Port Methods

        private void updateBaudRates()
        {
            //For the selected serial port, get the available baud rates
            //If there was something selected, try to maintain the selected one
        }

        private bool openPortBasic()
        {
            try
            {
                currentConnection = new ActiveSerialPort();
                if (currentConnection.createBasicSerialPort(currentPortName, currentBaudRate, DataType))
                {
                    currentConnection.DataReadyToRead += serialPortDataReadyToRead;
                    Console.Write("\n" + currentPortName + "\n");
                    Console.Write(currentBaudRate + "\n");
                    return portOpen = true;
                }
                else
                {
                    return portOpen = false;
                }
            }
            catch (Exception)
            {
                return portOpen;
            }
        }

        private bool openPort()
        {
            try
            {
                currentConnection = new ActiveSerialPort();
                if (currentConnection.openSerialPort(currentPortName, currentBaudRate, currentParity, currentDataBits, currentStopBits, timeoutMS, DataType, portReadTimeout))
                {
                    currentConnection.DataReadyToRead += serialPortDataReadyToRead;
                    Console.Write("\n" + currentPortName + "\n");
                    Console.Write(currentBaudRate + "\n");
                    return portOpen = true;
                }
                else
                {
                    return portOpen = false;
                }
            }
            catch (Exception)
            {
                return portOpen;
            }
        }

        public bool openPortActionFirstTime()
        {
            int numberOfComPorts = comboBoxPortName.Items.Count;

            for (int i = 0; i < numberOfComPorts; i++)
            {
                if (!openPortAction())
                {//if the port didn't open, try the next port
                    if (comboBoxPortName.SelectedIndex < comboBoxPortName.Items.Count - 1)
                    {
                        comboBoxPortName.SelectedIndex++;
                    }
                }
                else
                {
                    return true;
                }
            }
            return false;
        }

        public bool openPortAction()
        {
            if (openPort())
            {
                buttonOpenSerial.Text = "Close Port";
                setComboBoxStates(false);
                setRTSDTR();
                //set the connection on the status bar
                toolStripStatusLabel1.Text = currentConnection.getConnectionInfo(1);
                this.Text = "COMET - " + currentConnection.getConnectionInfo(0);
                focusInput();
                return true;
            }
            else
            {
                setNoConnection();
                focusComPort();
                return false;
            }
        }

        public bool closePortAction()
        {
            if (closePort())
            {
                buttonOpenSerial.Text = "Open Port";
                setComboBoxStates(true);
                toolStripStatusLabel1.Text = currentConnection.getConnectionInfo(1);
                this.Text = "COMET - " + currentConnection.getConnectionInfo(0);
                //set the focus to the COM Port box
                focusComPort();
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool closePort()
        {
            try
            {
                currentConnection.closeSerialPort();
                portOpen = false;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void sendDataToSerialConnection()
        {
            //Send Command - Interacts with the GUI
            string dataSent = currentConnection.sendData(dataToSend, endline);
            updateTerminal(dataSent, false);
            AcceptButton = button1;
            if (!keepText)
                textBox1.Text = "";
            if (rememberLastCommand)
            {
                //add to the history buttons
                addLastCommandToHistoryButton(dataSent);
            }
            //add the command to arraylist of the last commands
            addLastCommand(dataSent);
            focusInput();
        }

        private void sendDataToSerialConnectionBasic(string data)
        {
            //Basic send command - doesn't affect the GUI
            if (currentConnection != null)
            {
                string dataSent = currentConnection.sendData(data, endline);
                updateTerminal(dataSent, false);
            }
        }

        private void serialPortDataReadyToRead(object sender, EventArgs e)
        {
            updateTerminal(currentConnection.readData(), true);
        }

        private void CleanClose(object sender, EventArgs e)
        {
            //close the serial port on close
            try
            {
                if (currentConnection != null) {
                this.currentConnection.closeSerialPort();
                }
            }
            catch { }

            //close any results window
            try
            {
                if (ResultWindow != null)
                {
                    ResultWindow.Close();
                    ResultWindow.Dispose();
                    ResultWindow = null;
                }
            }
            catch { }

            //close any scripts that are running
            try
            {
                this.serialScript = null;
            }
            catch { }

            this.AllWindowsClosed = true;
        }

        private void updateTimeout()
        {
            float timeoutVal = 0;
            if (float.TryParse(textBoxTimeout.Text, out timeoutVal))
            {
                //if seconds
                //timeoutMS = (int)(timeoutVal * 1000);
                //if ms
                timeoutMS = (int)(timeoutVal);
                //System.Console.WriteLine(timeoutMS);
            }
            this.currentConnection.setPortTimeout(timeoutMS, timeoutMS);
        }

        private void updateToggleTime()
        {
            float timeoutVal = 0;
            if (float.TryParse(textBoxToggle.Text, out timeoutVal))
            {
                //timeoutMS = (int)(timeoutVal * 1000);
                toggleTime = (int)(timeoutVal);
            }
        }

        public void setPortType(enumDataType PortType)
        {
            if (currentConnection != null)
            {
                try
                {
                    currentConnection.DataType = PortType;
                }
                catch (Exception) { }
            }
        }

        private bool initTransfer()
        {
            try
            {
                transferData = new Transfer(currentConnection);
                transferData.ProgressChanged += transferProgressChanged;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion Serial Port Methods

        #region Serial Terminal

        public void focusInput()
        {
            textBox1.Select();
            textBox1.SelectionStart = textBox1.Text.Length;
            textBox1.SelectionLength = 0;
        }
        public void focusComPort()
        {
            comboBoxPortName.Focus();
        }

        private void updateTerminal(string newText, bool output)
        {

            if (newText.Length == 0)
                return;

            //if it's hex and bytes spacing is desired, format the return values
            if (bytespaces && (DataType == enumDataType.HEX)) newText = addByteSpacing(newText);

            //add a timestamp, if selected
            if (timestampserial)
            {
                String timeStamp = DateTime.Now.ToString(timestampformat);
                newText =  timeStamp + " " + newText; 
            }


            //only log the output data!
            if (localBuffer && output)
            {
                //removed the newline from the localbuffer data :JS 2019-04-24
                //localBufferData += newText + Environment.NewLine;
                localBufferData += newText;
            }
            Color textColor = Color.White;
            //different color for in/out text
            if (output)
            {
                textColor = textReceive;
                if (!newText[newText.Length - 1].Equals(Environment.NewLine) && AddNewLineToTerminal)
                {
                    newText = newText + Environment.NewLine;
                }
            }
            else
            {
                textColor = textSend;

                if (!newText[newText.Length - 1].Equals(Environment.NewLine))
                {
                    newText = newText + Environment.NewLine;
                }
            }

            //add the new text on top of the previous text
            if (textReverse)
            {
                //Not implemented
                //terminalText = newText + terminalText;
                try
                {
                    //simply setting the text causes cross-threading errors
                    //textBoxTerminal.AppendText(newText);
                    textBoxTerminal.Invoke(new MethodInvoker(delegate { RichTextBoxExtra.AppendText(textBoxTerminal, newText, textColor); }));
                }
                catch (Exception) { }
            }
            else
            {
                //terminalText = terminalText + newText;
                try
                {
                    //simply setting the text causes cross-threading errors
                    //textBoxTerminal.AppendText(newText);
                    textBoxTerminal.Invoke(new MethodInvoker(delegate { RichTextBoxExtra.AppendText(textBoxTerminal, newText, textColor); }));
                }
                catch (Exception) { }
            }
        }

        private bool addLastCommand(string lastCommandSent)
        {
            if (string.IsNullOrEmpty(lastCommandSent))
                return false;

            //check out the previous command and only add if this command is new
            if (DiscardDuplicateEntriesInList)
            {
                //not implemented
            }

            lastCommandList.Add(lastCommandSent);
            lastCommandIndex = lastCommandList.Count;
            return true;
        }

        public static string AddSpaceAfter2(Match m)
        {
            string newString = m + "";
            return newString.Insert(2, " ");
        }

        #endregion Serial Terminal

        #region History Button Methods

        private SmartButton createSmartButton(string buttonCommandText, string buttonDescriptionText, bool displayCMD, int buttonStyle, SmartButton.buttonTypes buttonType)
        {
            var newbutton = new SmartButton(buttonType, buttonCommandText, buttonDescriptionText, displayCMD);

            setButtonStyle(newbutton, buttonStyle);
            setButtonLocation(newbutton);
            setButtonEventHandlers(newbutton, true);

            //Tooltip needs to be added after the button event handlers are created, as setButtonEventHandlers clears all events!
            if (displayCMD)
            {
                this.toolTip1.SetToolTip(newbutton, newbutton.CommandDescription);
            }
            else
            {
                this.toolTip1.SetToolTip(newbutton, newbutton.CommandToSend);
            }

            
            return newbutton;
        }
        
        private void setButtonLocation(SmartButton newbutton)
        {
            newbutton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));

            //find the relative relative location
            //Based on either the Send button (first time) or the last smart button that was added

            Point basePoint = lastButtonForLocation.Location;
            Point offsetPoint = new Point(0, -29);
            basePoint.Offset(offsetPoint);
            newbutton.Location = basePoint;
            //set the size to match the last button
            newbutton.Size = lastButtonForLocation.Size;
            lastButtonForLocation = newbutton;

        }

        private void setButtonEventHandlers(SmartButton newbutton, bool IsNew)
        {
            //clear any previous event handlers
            newbutton.ClearAllEvents();

            //add the event handlers

            //clicking the button sends its history data to the terminal
            if (newbutton.buttonType == SmartButton.buttonTypes.SerialCommand)
            {
                newbutton.Click += new System.EventHandler(this.SmartButton_SendSerial);
            }
            if (newbutton.buttonType == SmartButton.buttonTypes.ScriptRunner)
            {
                newbutton.Click += new System.EventHandler(this.SmartButton_RunScript);
            }
            if (newbutton.buttonType == SmartButton.buttonTypes.Log)
            {
                newbutton.Click += new System.EventHandler(this.SmartButton_LogTerminal);
            }
            newbutton.ContextMenuStrip = setupToolStripMenu(true);
            newbutton.MouseHover += new System.EventHandler(this.newbutton_MouseHover);
            newbutton.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textBoxTerminal_KeyPress);

            if (IsNew)
            {
                //add the context menu
                newbutton.ContextMenuStrip = contextMenuSmartButton;
                this.panelHistory.Controls.Add(newbutton);
            }
        }

         private ContextMenuStrip setupToolStripMenu(bool isScript)
        {//not used!
            //get the generic Smart Button tool strip
            ContextMenuStrip contextMenu = contextMenuSmartButton;
            ToolStripItemCollection tsic = contextMenu.Items;

            foreach (ToolStripItem tsi in tsic)
            {
                if (tsi.Text == "Stop")
                {
                    tsi.Visible = isScript;
                }
            }

            return contextMenu;
        }

        private void setButtonStyle(Button newbutton, int buttonStyle)
        {
            //change the button style
            switch (buttonStyle)
            {
                case 0:
                    newbutton.BackColor = System.Drawing.SystemColors.Control;
                    break;

                case 1:
                    newbutton.BackColor = System.Drawing.SystemColors.ControlDark;
                    break;

                case 2:
                    //for script buttons
                    newbutton.BackColor = System.Drawing.SystemColors.ControlLight;
                    newbutton.Font = new Font(newbutton.Font.Name, newbutton.Font.Size, FontStyle.Bold);
                    break;

                default:
                    newbutton.BackColor = System.Drawing.SystemColors.Control;
                    break;
            }
        }

        private void addLastCommandToHistoryButton(String lastCommandSent)
        {
            if (!String.IsNullOrEmpty(lastCommandSent) && writeSmartButton && writeSmartButtonEnabled)
            {
                createSmartButton(lastCommandSent, lastCommandSent, showCMD, 0, SmartButton.buttonTypes.SerialCommand);
            }
        }

        private void ClearSmartButtons(int reference, bool direction)
        {
            //direction: 1 = above, 0 = below
            try
            {
                var historyButtons = panelHistory.Controls.OfType<SmartButton>().ToArray();
                foreach (var control in historyButtons)
                {
                    if (direction)
                    {
                        if (((SmartButton)control).Location.Y > reference)
                        {
                            ((SmartButton)control).removeThisButton();
                        }
                    }
                    else
                    {
                        if (((SmartButton)control).Location.Y < reference)
                        {
                            ((SmartButton)control).removeThisButton();
                        }
                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Error Clearing History Buttons");
            }
        }

        private void ClearSmartButtons()
        {
            //direction: 1 = above, 0 = below
            try
            {
                var historyButtons = panelHistory.Controls.OfType<SmartButton>().ToArray();
                foreach (var control in historyButtons)
                {
                    //((SmartButton)control).removeThisButton();
                    ((SmartButton)control).Dispose();
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Error Clearing History Buttons");
            }
        }
        

        private String[] collectAllSmartButtonData()
        {
            String ButtonData;
            var HistoryButtonData = new List<string>();

            try
            {
                var historyButtons = panelHistory.Controls.OfType<SmartButton>().ToArray();
                int i = 0;
                foreach (var control in historyButtons)
                {
                    //format each line in the file
                    //Command {TAB}{TAB}{TAB}{TAB} Description
                    SmartButton CurrentB = (SmartButton)control;

                    //if a history button, just write the command and description
                    if (CurrentB.buttonType == SmartButton.buttonTypes.SerialCommand)
                    {
                        ButtonData = CurrentB.CommandToSend + "\t\t\t\t" + CurrentB.CommandDescription;
                        //add the data to the array
                        HistoryButtonData.Add(ButtonData);
                    }

                    //if a script button, surround the commands with **script; write each command in the script
                    if (CurrentB.buttonType == SmartButton.buttonTypes.ScriptRunner)
                    {
                        object[] scriptItems = CurrentB.storedScript.currentScript.ToArray();
                        HistoryButtonData.Add("**script" + "\t\t\t\t" + CurrentB.CommandToSend);
                        for (int j = 0; j < scriptItems.Length; j++)
                        {
                            object[] currentItem = (object[])((ArrayList)scriptItems[j]).ToArray();
                            //add the data to the array if it's a plain serial command
                            if ((string)currentItem[0] == "SERIAL")
                            {
                                HistoryButtonData.Add((string)currentItem[1]);
                            }
                            if ((string)currentItem[0] == "FUNCTION")
                            {
                                string commandAndArgs = "**" + (string)currentItem[1];
                                //find out how many arguments the function has
                                int lengthItems = currentItem.Length;
                                //add the arguments, tab delmited
                                for (i = 2; i < lengthItems; i++)
                                {
                                    commandAndArgs = commandAndArgs + "\t" + currentItem[i];
                                }
                                //add the function
                                HistoryButtonData.Add(commandAndArgs);
                            }
                        }
                        HistoryButtonData.Add("**script" + "\t\t\t\t" + CurrentB.CommandDescription);
                    }
                }
                return HistoryButtonData.ToArray();
            }
            catch (Exception)
            {
                Console.WriteLine("Error Collecting History Data");
                return new string[] { "Error" };
            }
        }

        private void changeHistoryButtonDisplay(bool ShowCommand)
        {
            //cycle through all the history buttons and change the Text to display either Command or Description
            //Also change the tooltip to be the opposite of the text display
            try
            {
                var historyButtons = panelHistory.Controls.OfType<SmartButton>().ToArray();
                foreach (var control in historyButtons)
                {
                    SmartButton CurrentB = (SmartButton)control;
                    if (ShowCommand)
                    {
                        CurrentB.Text = CurrentB.CommandToSend;
                        this.toolTip1.SetToolTip(CurrentB, CurrentB.CommandDescription);
                    }
                    else
                    {
                        CurrentB.Text = CurrentB.CommandDescription;
                        this.toolTip1.SetToolTip(CurrentB, CurrentB.CommandToSend);
                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Error Changing Button displays");
            }
        }

        private void writeToTextFile(string path, string[] data)
        {
            // This text is added only once to the file.
            if (!File.Exists(path))
            {
                // Create a file to write to.
                File.WriteAllLines(path, data, Encoding.UTF8);
            }
        }

        private void loadSmartButtons(string[] dataIn)
        {
            //suspend layout updates temporarily 
            this.SuspendLayout();
            try
            {
                string[] recalledData = dataIn;
                int max_data_length = 200;
                //if this is a really large file, alert the user
                if (recalledData.Length > max_data_length)
                {
                    var response = MessageBox.Show("File is greater than " + max_data_length.ToString() + " lines long. \nProceed ?", "Large File", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                    if (response == DialogResult.No)
                    {
                        recalledData = null;
                    }
                }
                string[] stringSeparator = new string[] { "\t" };
                int buttonStyle = 0;
                //variable to set wheter a script button is being created
                bool scriptbutton = false;
                SmartButton tempButton = new SmartButton();

                if (recalledData != null && recalledData.Length != 0)
                {
                    for (int i = 0; i < recalledData.Length; i++)
                    {
                        string[] parsed = recalledData[i].Split(stringSeparator, StringSplitOptions.RemoveEmptyEntries);
                        //parse out the data
                        //when encountering an empty line, change the button style
                        //this allows for grouping button appeareance by function
                        if (parsed.Length == 0)
                        {
                            buttonStyle++;
                            buttonStyle = buttonStyle % 2;
                        }
                        //make sure there is data on the line
                        if (parsed != null && parsed.Length != 0 && parsed[0].Length > 0)
                        {
                            string command = parsed[0];
                            string description = parsed[0];

                            //find out if there is a special command
                            if (command.Trim().StartsWith("**"))
                            {
                                if (command.ToLower().Contains("**script"))
                                {
                                    //toggle script creation on and off
                                    scriptbutton = !scriptbutton;
                                    //for the first time, create a script button
                                    if (scriptbutton)
                                    {
                                        if ((parsed.Length > 1) && parsed[1].Length > 0)
                                        {
                                            description = parsed[1];
                                        }
                                        tempButton = createSmartButton(description, description, showCMD, 2, SmartButton.buttonTypes.ScriptRunner);
                                        //create a new script
                                        loadScript(false);
                                    }
                                    else
                                    {
                                        //when the script is done populating, add the script to the button
                                        tempButton.addScript(serialScript);
                                    }
                                }
                            }
                            if (!command.ToLower().Contains("**script"))
                            {
                                if (scriptbutton)
                                {
                                    //add to the script - but only if it is not a command
                                    serialScript.addCommandIntoCurrentScript(recalledData[i]);
                                }
                                else
                                {
                                    //the usual case - not a scripted command or function
                                    if ((parsed.Length > 1) && parsed[1].Length > 0)
                                    {
                                        description = parsed[1];
                                    }
                                    createSmartButton(command, description, showCMD, buttonStyle, SmartButton.buttonTypes.SerialCommand);
                                }
                            }
                        }
                    }
                }

                //if everything loaded correctly, display the descriptions
                showCMD = false;
                changeHistoryButtonDisplay(showCMD);
                //move the scroll bar back to the bottom
                panelHistory.ScrollControlIntoView(button1);
            }
            catch (Exception)
            {
                Console.WriteLine("Error opening / parsing saved data");
            }
            this.ResumeLayout(true);
        }

        private string[] openSavedSmartButtons()
        {
            // Create an instance of the open file dialog box.
            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            // Set filter options and filter index.
            openFileDialog1.Filter = "Text Files (.txt)|*.txt|All Files (*.*)|*.*";
            openFileDialog1.FilterIndex = 1;

            openFileDialog1.Multiselect = false;

            //openfile dialog was hanging after .net4 upgrade - this fixes it?
            openFileDialog1.ShowHelp = true;
            openFileDialog1.AutoUpgradeEnabled = true;

            // Process input if the user clicked OK.
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string readThisFile = openFileDialog1.FileName;
                //set the current file to readThisFile
                currentHistoryFile = readThisFile;
                return File.ReadAllLines(readThisFile);
            }
            else
            {
                return null;
            }
        }

        private string openSingleFile()
        {
            // Create an instance of the open file dialog box.
            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            // Set filter options and filter index.
            //openFileDialog1.Filter = "Text Files (.txt)|*.txt|All Files (*.*)|*.*";
            //openFileDialog1.FilterIndex = 1;

            openFileDialog1.Multiselect = false;

            //openfile dialog was hanging after .net4 upgrade - this fixes it?
            openFileDialog1.ShowHelp = true;

            // Process input if the user clicked OK.
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                return openFileDialog1.FileName;
            }
            else
            {
                return null;
            }
        }

        #endregion History Button Methods

        #region Scripting

        public class Worker
        {
            //this is the script
            private ScriptRunner _scriptToRun;

            // This method will be called when the thread is started.
            public void DoWork()
            {
                while (!_shouldStop)
                {
                    Console.WriteLine("worker thread: working...");
                }
                Console.WriteLine("worker thread: terminating gracefully.");
            }

            public void RequestStop()
            {
                _shouldStop = true;
            }

            // Volatile is used as hint to the compiler that this data
            // member will be accessed by multiple threads.
            private volatile bool _shouldStop;
        }

        private bool loadScript(bool fromFile)
        {
            //return true if loaded a file, return false if exception
            try
            {
                serialScript = new ScriptRunner(ScriptRunner.defaultDelayTimeMs);
                serialScript.clearCurrentScript();
                if (fromFile)
                {
                    if (serialScript.loadScriptFromFile())
                    {
                        return true;
                    }
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        private void runScript(ScriptRunner scriptToRun)
        { //single threaded at this point....
            try
            {
                if (scriptToRun != null)
                {
                    //tell the script that it is going to run
                    scriptToRun.stop = false;
                    SetProgress(true, false);
                    var commandArray = scriptToRun.currentScript.ToArray();
                    ArrayList currentItem = new ArrayList();
                    string typeOfCommand = "";

                    //looping - must be initialized before the script runs
                    int loopIteration = 0;
                    int loopdelay = scriptToRun.getLoopTime();
                    int loopcount = scriptToRun.getLoopCount();

                    for (loopIteration = 0; loopIteration < loopcount; loopIteration++)
                    {
                        //check to see if the script to stop
                        if (scriptToRun.stop) break;

                        //add on the loop delay AFTER the first time the loop is run
                        if (loopIteration > 0) safeSleep(loopdelay);
                        //The Script
                        for (int current = 0; current < commandArray.Length; current++)
                        {
                            //currently this is a blocking command
                            transferProgressChanged((int)(100 * ((float)current / (float)commandArray.Length)), true);
                            if (!stopThread)
                            {
                                //get each instruction
                                //if it is a serial instruction, just send over active serial port
                                currentItem = (ArrayList)commandArray[current];
                                typeOfCommand = (string)currentItem[0];

                                switch (typeOfCommand)
                                {
                                    case "SERIAL":
                                        sendDataToSerialConnectionBasic((string)currentItem[1]);
                                        //wait
                                        safeSleep(scriptToRun.getDelay());
                                        break;

                                    case "FUNCTION":
                                        scriptHandleFunction(currentItem, scriptToRun);
                                        //wait
                                        safeSleep(scriptToRun.getDelay());
                                        break;

                                    default:
                                        break;
                                }
                            }
                        }
                    }

                }
            }
            catch (Exception)
            {
                Console.WriteLine("Error Running Script");
            }
            SetProgress(false, false);
        }

        private void scriptHandleFunction(ArrayList currentItem, ScriptRunner RunningScript)
        {
            //ArrayList format:
            //"FUNCTION"
            //"**functionName"
            //{"arguments1"}
            //{"arguments2"}
            object[] currentItemToRun = currentItem.ToArray();
            int length = currentItemToRun.Length;
            string functionName = "";
            object[] arguments = null;
            //populate the funciton name and arguments, if any
            if (length >= 2)  //FUNCTION , **functionname
            {
                functionName = (string)currentItemToRun[1];
            }
            if (length >= 3) //FUNCTION , **functionname, arguments
            {
                arguments = new Object[length - 2];
                Array.Copy(currentItemToRun, 2, arguments, 0, length - 2);
            }
            //case statement to handle functions
            switch (functionName.ToLower())
            {
                case "ymodem":
                    if (arguments != null)
                    {
                        initTransfer();
                        YModemSendFile((string)arguments[0]);
                    }
                    break;
                case "xmodem":
                    if (arguments != null)
                    {
                        initTransfer();
                        XModemSendFile((string)arguments[0]);
                    }
                    break;

                case "delay":
                    if (arguments != null)
                    {
                        int newDelay = 1000;
                        if (int.TryParse((string)arguments[0], out newDelay))
                        {
                            RunningScript.changeDelay(newDelay);
                        }
                    }
                    break;

                case "sleep":
                    if (arguments != null)
                    {
                        int newSleep = 1000;
                        if (int.TryParse((string)arguments[0], out newSleep))
                        {
                            safeSleep(newSleep);
                        }
                    }
                    break;

                case "time":
                    WriteTimeToTermial();
                    break;

                case "serialbreak":
                    currentConnection.serialBreak();
                    break;

                case "sbreak":
                    if (arguments != null)
                    {
                        bool serialtermval = false;

                        try
                        {
                            serialtermval = Convert.ToBoolean((string)arguments[0]);
                        }
                        catch { }
                        setSBREAK(serialtermval);
                    }
                    break;

                case "rts":
                    if (arguments != null)
                    {
                        bool serialtermval = false;

                        try
                        {
                            serialtermval = Convert.ToBoolean((string)arguments[0]);
                        }
                        catch { }
                        setRTS(serialtermval);
                    }
                    break;

                case "dtr":
                    if (arguments != null)
                    {
                        setDTRandParseInput((string)arguments[0]);
                    }
                    break;

                case "unicode":
                    if (arguments != null)
                    {
                        string hexString = (string)arguments[0];
                        int code = Int32.Parse(hexString, System.Globalization.NumberStyles.HexNumber);
                        string unicodeString = char.ConvertFromUtf32(code);
                        sendDataToSerialConnectionBasic(unicodeString);
                    }
                    break;

                case "settings":
                    //usage:
                    //function name {tab} baud {tab} ASCIIorHEX {tab} DTR {tab}  PortReadTime
                    //**settings   9600   ASCII   True  30
                    ScriptSetCometSettings(arguments);
                    break;

                case "response_str":
                    //usage:
                    //function name {tab} timeout ms {tab} command {tab} expected response
                    //**response_str   2000 qdver   rqdver
                    ScriptCheckForResponse(arguments, enumComparisonType.stringType);
                    break;

                case "response_int_between":
                    //usage:
                    //function name {tab} timeout ms {tab} command {tab} parsetext {tab} low int {tab} high int
                    //**response_int_between {tab} 2000 {tab} QDAIN,1 {tab} RQDAIN,1, Ain[1] (VBAT) = {tab}7000 {tab} 8000
                    ScriptCheckForResponse(arguments, enumComparisonType.between);
                    break;

                case "response_log":
                    //usage:
                    //function name {tab} timeout ms {tab} command {tab} expected response / parseout string
                    //**response_log   500 qdain,1   RQDAIN,1, Ain[1] (VBAT) = 
                    ScriptLogResponse(arguments, "time", true);
                    break;

                case "log":
                    //usage:
                    //function name {tab} timeout ms {tab} command {tab} expected response / parseout string
                    //**response_log   500 :MEASURE:VOLTAGE:DC?
                    ScriptLogResponse(arguments, "time", false);
                    break;

                case "user_input_log":
                    //prompt the user for a string, execute the command, then log the result and the user string
                    //usage:
                    //function name {tab} timeout ms {tab} command {tab}prompt
                    //**user_input_log   500    READ?   Enter Value for WLAN PWR
                    //this asks the user to specify 1 or 0 for the WLAN power
                    string response = GetInputFromUser((string)arguments[1]);
                    ScriptLogResponse(new Object[] { (string)arguments[0], (string)arguments[2], response }, "timeandstring", false);
                    break;

                case "user_input_command":
                    //prompt the user for a response & populate a command with the string value supplied by the user
                    //usage:
                    //function name {tab} timeout ms {tab} command {tab}prompt
                    //**user_input_command   500    sdout,29,   Enter Value for WLAN PWR
                    //this asks the user to specify 1 or 0 for the WLAN power
                    ScriptInputCommand(arguments);
                    break;

                case "user_input_string":
                    //prompt the user for a response & populate a command with the string value supplied by the user
                    //populate the string in the indicated spot: %s
                    //usage:
                    //function name {tab} timeout ms {tab} command {tab}prompt
                    //**user_input_command   500    S268,%s,d  Enter MTID
                    //this asks the user for an MTID, if the user puts 22: S268,22,d  is sent
                    ScriptInputString(arguments);
                    break;
				case "set_writenewline":
					if (arguments != null)
					{
						setWriteNewLineParseInput((string)arguments[0]);
					}
					break;
				case "set_readnewline":
					if (arguments != null)
					{
						setReadNewLineParseInput((string)arguments[0]);
					}
					break;
					
                default:
                    break;
            }
        }

        private void setDTRandParseInput(string argument)
        {
            bool serialtermval = false;

            try
            {
                //usage: false or true as the argument
                serialtermval = Convert.ToBoolean(argument);
            }
            catch { }
            setDTR(serialtermval);
        }

        private void setReadNewLineParseInput(string argument)
        {
            bool AddNL = false;

            try
            {
                //usage: false or true as the argument
                AddNL= Convert.ToBoolean(argument);
            }
            catch { }
            //check the box to add a new line in the GUI
            checkBoxAddNewLine.Checked = AddNL;
        }

		private void setWriteNewLineParseInput(string argument)
		{
			bool WriteNL = false;

			try
			{
				//usage: false or true as the argument
				WriteNL = Convert.ToBoolean(argument);
			}
			catch { }
			//check the box to write new line at end of each command
			checkBox1.Checked = WriteNL;
		}

		private void ScriptSetCometSettings(object[] arguments)
        {
            //function name {tab} baud {tab} ASCIIorHEX {tab} DTR {tab}  PortReadTime
            //**settings   9600   ASCII   True  30 [false]

            //try to set these settings
            closePortAction();
            comboBoxBaudRate.SelectedIndex = comboBoxBaudRate.Items.IndexOf((string)arguments[0]);
            openPortAction();
            setASCIIorHEX((string)arguments[1]);
            if(arguments.Length>2)setDTRandParseInput((string)arguments[2]);
            if (arguments.Length > 3) textBoxPortReadTimeout.Text = ((string)arguments[3]);
            if (arguments.Length > 4) setReadNewLineParseInput((string)arguments[4]);
        }

        private void ScriptCheckForResponse(object[] arguments, enumComparisonType comparisontype)
        {
            //valid comparison types
            // string
            // greaterthan
            // lessthan
            // between

            if (arguments != null)
            {
                int timeout = 3000;
                int.TryParse((string)arguments[0], out timeout);
                string commandToSend = (string)arguments[1];
                //this is the response to look for
                string checkString = (string)arguments[2];

                //make sure there is a dialog available
                createTestDialog();
                ResponseAnalyzer res = new ResponseAnalyzer(checkString, ResultWindow);
                bool passedtest = false;
                bool testcomplete = false;
                string detail = "";

                //turn on the local buffer so the data is kept
                localBuffer = true;

                //send the command to ther terminal
                sendDataToSerialConnectionBasic(commandToSend);

                //start a timer and continue to check the response for a while
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                while (true)
                {
                    //the the GUI redraw
                    Application.DoEvents();
                    //see if the response is in the buffer
                    switch (comparisontype)
                    {
                        case enumComparisonType.stringType:
                            testcomplete = res.checkForResponse(localBufferData, out passedtest);
                            break;

                        case enumComparisonType.between:
                            int lowVal = 0;
                            int.TryParse((string)arguments[3], out lowVal);
                            int highVal = 100;
                            int.TryParse((string)arguments[4], out highVal);
                            testcomplete = res.checkValBetween(localBufferData, lowVal, highVal, out detail, out passedtest);
                            break;

                        case enumComparisonType.greaterThan:
                            throw new NotImplementedException();

                        case enumComparisonType.lessThan:
                            throw new NotImplementedException();

                        default:
                            throw new InvalidEnumArgumentException("comparisontype");
                    }

                    if (testcomplete || stopWatch.ElapsedMilliseconds > timeout)
                        break;
                }
                //timed out or found
                stopWatch.Stop();

                if (passedtest)
                {
                    res.showPass(detail);
                    this.ResultWindow.Location = centerNewWindow(ResultWindow.Width, ResultWindow.Height);
                }
                else
                {
                    res.showFailure(detail);
                    this.ResultWindow.Location = centerNewWindow(ResultWindow.Width, ResultWindow.Height);
                }
                //clear the buffer and stop recording
                resetbuffer();
            }
        }

        //TODO replace logtype with enum
        private void ScriptLogResponse(object[] arguments, string logtype, Boolean validateResponse)
        {
            //valid log types
            // time

            if (arguments != null)
            {
                int timeout = 3000;
                int.TryParse((string)arguments[0], out timeout);
                string commandToSend = (string)arguments[1];
                //this is the response to look for if there is no argument
                string checkString = "";
                if (validateResponse)
                {
                    //this is the response to look for if there is an argument
                    checkString = (string)arguments[2];
                }
                //make sure there is a dialog available
                createTestDialog();
                ResponseAnalyzer res = new ResponseAnalyzer(checkString, ResultWindow);
                bool testcomplete = false;
                string collectedData = "";

                //turn on the local buffer so the data is kept
                localBuffer = true;

                //send the command to the terminal
                sendDataToSerialConnectionBasic(commandToSend);

                //start a timer and continue to check the response for a while
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                long duration = stopWatch.ElapsedMilliseconds;
                bool stopnow = false;
                while (!stopnow && !AllWindowsClosed)
                {
                    //the the GUI redraw
                    Application.DoEvents();
                    //see if the response is in the buffer
                    switch (logtype)
                    {
                        //prepend the time
                        case "time":
                            testcomplete = res.getResponseData(localBufferData, out collectedData);
                            break;
                        case "timeandstring":
                            testcomplete = res.getResponseData(localBufferData, out collectedData);
                            commandToSend = (string)arguments[2];
                            break;
                        default:
                            break;
                    }

                    if (testcomplete)
                    {
                        //take off any trailing endlines, if the user selected it
                        if(!AddNewLineToTerminal) collectedData = Regex.Replace(collectedData, @"\t|\n|\r", "");

                        res.logResponse(commandToSend, collectedData);
                        stopnow = true;
                    }
                    else
                    {
                        //update the timer
                        duration = stopWatch.ElapsedMilliseconds;
                        stopnow = duration > timeout;
                    }
                }
                //timed out or found
                stopWatch.Stop();
                //clear the buffer and stop recording
                resetbuffer();
            }
        }

        private void ScriptInputCommand(object[] arguments)
        {
            //valid log types
            // time

            if (arguments != null)
            {
                int timeout = 3000;
                int.TryParse((string)arguments[0], out timeout);
                string commandToSend = (string)arguments[1];
                string promptToDisplay = (string)arguments[2];
                //ask the user to input the parameter
                string inputFromUser = "";
                inputFromUser = GetInputFromUser(promptToDisplay);
                //build the command
                commandToSend = commandToSend + inputFromUser;

                //send the command to the terminal
                sendDataToSerialConnectionBasic(commandToSend);
            }
        }

        private string GetInputFromUser(string promptToDisplay)
        {
            string inputFromUser;
            DialogWin askDialog = new DialogWin(true);
            askDialog.setInput(promptToDisplay);
            askDialog.Location = centerNewWindow(askDialog.Width, askDialog.Height);
            //wait for the user to click ok
            while (askDialog.waiting)
            { safeSleep(100); }
            //get the text that was in the text box
            inputFromUser = askDialog.getInput();
            //close the dialog
            askDialog.Close();
            return inputFromUser;
        }

        private void ScriptInputString(object[] arguments)
        {
            //the argument for the command has a %s to signify the string
            //replace that with the value specified by the user
            if (arguments == null)
                return;

            int timeout = 3000;
            int.TryParse((string)arguments[0], out timeout);
            string commandToSend = (string)arguments[1];
            string promptToDisplay = (string)arguments[2];
            //ask the user to input the parameter
            string inputFromUser = "";
            DialogWin askDialog = new DialogWin(true);
            askDialog.setInput(promptToDisplay);
            askDialog.Location = centerNewWindow(askDialog.Width, askDialog.Height);
            //wait for the user to click ok
            while (askDialog.waiting)
            { safeSleep(100); }
            //get the text that was in the text box
            inputFromUser = askDialog.getInput();
            //close the dialog
            askDialog.Close();
            //build the command by replacing % with the input
            //%s for string
            //%h for hex
            commandToSend = commandToSend.Replace("%s", inputFromUser);
            commandToSend = commandToSend.Replace("%h", Utilities.ConvertStringToHex(inputFromUser, false));

            //send the command to the terminal
            sendDataToSerialConnectionBasic(commandToSend);
        }

        private void createTestDialog()
        {
            if (ResultWindow == null || ResultWindow.IsDisposed)
            {
                ResultWindow = new DialogWin(true);
            }
            ResultWindow.Location = centerNewWindow(ResultWindow.Width, ResultWindow.Height);
        }

        private void resetbuffer()
        {
            localBuffer = false;
            localBufferData = "";
        }

        #endregion Scripting

        #region GUI Handlers

        private void comboBoxPortName_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.currentPortName = (string)comboBoxPortName.SelectedItem;
        }

        private void comboBoxBaudRate_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxBaudRate.SelectedItem != null)
            {
                //if the port is open, try to close, set the new baud rate then reopen
                if (portOpen)
                {
                }

                bool parseOK = Int32.TryParse(comboBoxBaudRate.SelectedItem.ToString(), out this.currentBaudRate);
            }
        }

        private void buttonOpen_Click(object sender, EventArgs e)
        {
            openClosePort();
        }

        private void openClosePort()
        {
            if (!portOpen)
            {
                openPortAction();
            }
            else
            {
                closePortAction();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                dataToSend = textBox1.Text;
                sendDataToSerialConnection();
            }
            catch (Exception)
            {
                //do nothing - port is probably not open
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {
        }

        private void clearTerminalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            textBoxTerminal.Clear();
            focusInput();
        }

        private void toggleScrollToolStripMenuItem_Click(object sender, EventArgs e)
        {
            textBoxTerminal.HideSelection = !textBoxTerminal.HideSelection;
        }

        private void label2_Click(object sender, EventArgs e)
        {
        }

        private void loadConfigToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            //hide any other windows!
            panelPortOptions.Visible = false;
            //if the up key is pressed, then load the last text input - mimics terminal action
            switch (e.KeyCode)
            {
                case Keys.Up:
                    if (lastCommandIndex > 0)
                    {
                        lastCommandIndex--;
                        textBox1.Text = (string)lastCommandList[lastCommandIndex];
                    }
                    focusInput();
                    e.Handled = true;
                    writeSmartButton = false;
                    break;

                case Keys.Down:
                    if ((lastCommandIndex) <= lastCommandList.Count - 2)
                    {
                        lastCommandIndex++;
                        textBox1.Text = (string)lastCommandList[lastCommandIndex];
                    }
                    else
                    {
                        lastCommandIndex = lastCommandList.Count;
                        textBox1.Text = "";
                    }
                    focusInput();
                    e.Handled = true;
                    writeSmartButton = false;
                    break;

                default:
                    writeSmartButton = true;
                    break;
            }
        }

        private void sendBreakToolStripMenuItem_Click(object sender, EventArgs e)
        {
            currentConnection.serialBreak();
        }

        private void textBoxTerminal_KeyPress(object sender, KeyPressEventArgs e)
        {
            //if the cursor is in the terminal box, send the key to input text box and change the focus
            {
                try
                {
                    if ((Control.ModifierKeys & Keys.Control) != Keys.Control)
                    {
                        textBox1.Text = e.KeyChar.ToString();
                    }
                    focusInput();
                }
                catch (Exception) { }
            }
        }

        private void comboBoxParity_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxParity.SelectedItem != null)
            {
                this.currentParity = (Parity)comboBoxParity.SelectedItem;
            }
        }

        private void comboBoxDataBits_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxDataBits.SelectedItem != null)
            {
                int.TryParse(comboBoxDataBits.SelectedItem.ToString(), out this.currentDataBits);
            }
        }

        private void comboBoxStopBits_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxStopBits.SelectedItem != null)
            {
                this.currentStopBits = (StopBits)comboBoxStopBits.SelectedItem;
            }
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            if (textBoxTerminal.SelectedText.Length > 0)
            {
                Clipboard.SetText(textBoxTerminal.SelectedText);
            }
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            textBoxTerminal.SelectAll();
        }

        private void lastButton1_Click(object sender, EventArgs e)
        {
            //createHistoryButton("new");
        }

        private void launchNewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openNewInstanceOfSerial();
        }

        private void textBoxTimeout_TextChanged(object sender, EventArgs e)
        {
            updateTimeout();
        }

        private void SmartButton_SendSerial(object sender, EventArgs e)
        {
            string storedData = ((SmartButton)sender).CommandToSend;
            sendDataToSerialConnectionBasic(storedData);
        }

        private void SmartButton_RunScript(object sender, EventArgs e)
        {
            //run the script
            SetProgress(true, true);
            runScript(((SmartButton)sender).storedScript);
            SetProgress(false, true);
        }

        private void SmartButton_LogTerminal(object sender, EventArgs e)
        {
            //write the stored data to the terminal window only
            SmartButton SelectedButton = (SmartButton)sender;
            SelectedButton.getTime();
            string LogString = SelectedButton.LogString;

            updateTerminal("[LOG]        " + LogString, false);
        }

        private void WriteTimeToTermial()
        {
            string thistime = "";
            DateTime timeNow = DateTime.Now;
            thistime = timeNow.ToString();
            updateTerminal("[LOG]        " + thistime, false);
        }

        private void newbutton_MouseHover(object sender, EventArgs e)
        {
            //string storedData = ((SmartButton)sender).lastCommand;
            //sendDataToSerialConnectionBasic(storedData);
        }

        private void keepText_CheckedChanged(object sender, EventArgs e)
        {
            this.writeSmartButtonEnabled = !writeSmartButtonEnabled;

            focusInput();
        }

        private void buttonFindPort_Click(object sender, EventArgs e)
        {
        }

        private void toolsToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openNewInstanceOfSerial();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            //SmartButtonVar is populated in the _Opening event of the DropDownOpened event
            //via getSourceSmartButton

            SmartButtonVar.removeThisButton();
        }

        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            //SmartButtonVar is populated in the _Opening event of the DropDownOpened event
            //via getSourceSmartButton
            //pop up a dialog box to change the smart button

            //decide what kind of edit window to open based on the type of button
            if (SmartButtonVar.buttonType == SmartButton.buttonTypes.ScriptRunner)
            {
                PromptScriptEdit Edit = new PromptScriptEdit(SmartButtonVar);
                //center the window
                Edit.Location = centerNewWindow(Edit.Width, Edit.Height);
            }
            else if (SmartButtonVar.buttonType == SmartButton.buttonTypes.SerialCommand)
            {
                PromptSmartButtonEdit Edit = new PromptSmartButtonEdit(SmartButtonVar);
                //center the window
                Edit.Location = centerNewWindow(Edit.Width, Edit.Height);
            }
        }

        private void copyCommandToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //SmartButtonVar is populated in the _Opening event of the DropDownOpened event
            //via getSourceSmartButton

            //pop up a dialog box to change the smart button
            Clipboard.SetText(SmartButtonVar.CommandToSend);
        }

        private void clearAllButtonsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ClearSmartButtons();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            loadSmartButtons(openSavedSmartButtons());
        }

        private void loadSmartButtonsEvent(object sender, EventArgs e)
        {

            try
            {
                string pathname = ((ToolStripMenuItem)sender).Text;
                loadSmartButtons(File.ReadAllLines(pathname));
            }
            catch (Exception)
            {
                Console.Write("Unable to load smart buttons from directory");
            }
        }

        private void saveToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            string fileToSave = currentHistoryFile;
            //find a filepath that is unused
            if (currentHistoryFile == "")
            {
                fileToSave = Utilities.findUniqueFilepath("COMET_History_", ".txt");
            }
            Utilities.SaveStringArrayToFile(fileToSave, collectAllSmartButtonData());
        }

        private void panelHistory_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            { e.Effect = DragDropEffects.Copy; }
            else e.Effect = DragDropEffects.Move;
        }

        private void panelHistory_DragDrop(object sender, DragEventArgs e)
        {
            
            Console.WriteLine("Loading Files");
            try
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string[] dataToLoad = File.ReadAllLines(files[0]);
                loadSmartButtons(dataToLoad);

                panelHistory.VerticalScroll.Value = panelHistory.VerticalScroll.Maximum;
                updateLayoutHistoryPanel();
            }
            catch (Exception)
            {
                Console.WriteLine("Failed Loading Files");
            }
        }

        public void updateLayoutHistoryPanel()
        {
            panelHistory.PerformLayout();
            resizeButtons(true);
            focusInput();
        }

        private void panelHistory_Paint(object sender, PaintEventArgs e)
        {
        }

        private void radioButtonHEX_CheckedChanged(object sender, EventArgs e)
        {
            DataType = enumDataType.HEX;
            setPortType(enumDataType.HEX);
            focusInput();
        }

        private void radioButtonASCII_CheckedChanged(object sender, EventArgs e)
        {
            DataType = enumDataType.ASCII;
            setPortType(enumDataType.ASCII);
            focusInput();
        }

        private void setASCIIorHEX(enumDataType type)
        {
            switch (type)
            {
                case enumDataType.ASCII:
                    radioButtonASCII.Select();
                    break;
                case enumDataType.HEX:
                    radioButtonHEX.Select();
                    break;
                default:
                    throw new InvalidEnumArgumentException();
            }
        }

        //converts a string of type to an enumDataType
        //direct use of enum is prefered when possible
        private void setASCIIorHEX(string type)
        {
            switch(type.ToLower())
            {
                case "ascii":
                    setASCIIorHEX(enumDataType.ASCII);
                    break;
                case "hex":
                    setASCIIorHEX(enumDataType.HEX);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("type", "invalid encoding type name");
            }
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            return; //remove after implementing
            //HEX validation - not implemented!!!!
            if (DataType == enumDataType.HEX)
            {
                char c = e.KeyChar;
                if (!((c <= 0x66 && c >= 61) || (c <= 0x46 && c >= 0x41) || (c >= 0x30 && c <= 0x39)))
                {
                    e.Handled = true;
                }
            }
            //Console.Out.WriteLine(r.Replace(textBox1.Text, evaluator));
        }

        private void removeAllButtonsToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void toggleCMDDescriptionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            showCMD = !showCMD;
            changeHistoryButtonDisplay(showCMD);
        }

        private void toggleCMDDescriptionToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            showCMD = !showCMD;
            changeHistoryButtonDisplay(showCMD);
        }

        private void sendXModemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //
            //MessageBox.Show("Not Tested");

            if (initTransfer())
            {
                string pathToOpen = openSingleFile();
                if (pathToOpen != null)
                {
                    SetProgress(true, false);
                    transferData.XmodemUploadFile(pathToOpen);
                    SetProgress(false, false);
                }
            }
        }

        private void receiveXModemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (initTransfer())
            {
            }
            //TODO Not Implemented!!
            MessageBox.Show("Not Implemented");
            Console.WriteLine("Not Implemented");
        }

        private void sendYModemToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (initTransfer())
            {
                string pathToOpen = openSingleFile();
                YModemSendFile(pathToOpen);
            }
        }

        private void YModemSendFile(string pathToOpen)
        {
            if (pathToOpen != null)
            {
                SetProgress(true, false);
                //stop updating this GUI - unsubscribe from the event

                transferData.YmodemUploadFile(pathToOpen, true);

                //re-update the GUI
                SetProgress(false, false);
            }
        }

        private void XModemSendFile(string pathToOpen)
        {
            if (pathToOpen != null)
            {
                SetProgress(true, false);
                //stop updating this GUI - unsubscribe from the event

                transferData.XmodemUploadFile(pathToOpen);

                //re-update the GUI
                SetProgress(false, false);
            }
        }

        private void setDelayToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //update the script runner timeout

            PromptScriptEdit testDialog = new PromptScriptEdit();
            int new_timeout = 1000;

            // Show testDialog as a modal dialog and determine if DialogResult = OK.
            if (testDialog.ShowDialog(this) == DialogResult.OK)
            {
                Console.WriteLine("OK");
                // Read the contents of testDialog's TextBox.
                int.TryParse(testDialog.textBoxDelayTime.Text, out new_timeout);
            }
            testDialog.Dispose();

            if (serialScript != null)
            {
                serialScript.changeDelay(new_timeout);
            }
        }

        private void receiveYModemToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (initTransfer())
            {
            }
            //TODO Not Implemented!!
            MessageBox.Show("Not Implemented");
            Console.WriteLine("Not Implemented");
        }

        private void transferProgressChanged(int xferProgress, bool blocking)
        {
            //Action handler for changing the progress from the transfer
            SetProgress(xferProgress, blocking);
            //Console.WriteLine(xferProgress);
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
			endline = ((CheckBox)sender).Checked;
			//endline = checkBox1.Checked;
			focusInput();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            About showAbout = new About();
            showAbout.Location = centerNewWindow(showAbout.Width, showAbout.Height);
        }

        private void allButtonsToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
        }

        private void removeBelowToolStripMenuItem_MouseDown(object sender, MouseEventArgs e)
        {
            //int location = this.PointToClient(new Point(e.X, e.Y)).Y;
            //MessageBox.Show(location.ToString());
            //ClearHistoryButtons(location, true);
        }

        private void removeAboveToolStripMenuItem_MouseDown(object sender, MouseEventArgs e)
        {
            //int location = this.PointToClient(new Point(e.X, e.Y)).Y;
            //MessageBox.Show(location.ToString());
            //ClearHistoryButtons(location, false);
        }

        private void runScriptToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //start a new thread
            //Worker workerObject = new Worker();
            //Thread workerThread = new Thread(runScript());

            SetProgress(true, true);
            runScript(serialScript);
            SetProgress(false, true);
        }

        private void loadScriptToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (loadScript(true))
            {
                //create a new script button of the script loads correctly
                SmartButton loaded = createSmartButton(serialScript.scriptName, serialScript.scriptPath, true, 2, SmartButton.buttonTypes.ScriptRunner);
                //assign the current script to this new button
                loaded.addScript(serialScript);
            }
        }

        private void removeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ClearSmartButtons();
        }

        private void DTR_0_CheckedChanged(object sender, EventArgs e)
        {
            if (currentConnection != null)
            {
                //set DTR low
                currentConnection.setDTR(!((RadioButton)sender).Checked);
            }
        }

        private void DTR_1_CheckedChanged(object sender, EventArgs e)
        {
            if (currentConnection != null)
            {
                //set DTR high
                currentConnection.setDTR(((RadioButton)sender).Checked);
            }
        }

        private void RTS_0_CheckedChanged(object sender, EventArgs e)
        {
            if (currentConnection != null)
            {
                //set RTS low
                currentConnection.setRTS(!((RadioButton)sender).Checked);
            }
        }

        private void RTS_1_CheckedChanged(object sender, EventArgs e)
        {
            if (currentConnection != null)
            {
                //set RTS high
                currentConnection.setRTS(((RadioButton)sender).Checked);
            }
        }

        private void SBREAK_0_CheckedChanged(object sender, EventArgs e)
        {
            if (currentConnection != null)
            {
                //set serial break low
                currentConnection.setSBREAK(!((RadioButton)sender).Checked);
            }
        }

        private void SBREAK_1_CheckedChanged(object sender, EventArgs e)
        {
            if (currentConnection != null)
            {
                //set serial break high
                currentConnection.setSBREAK(((RadioButton)sender).Checked);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            toggleDTR();
            safeSleep(toggleTime);
            toggleDTR();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            toggleRTS();
            safeSleep(toggleTime);
            toggleRTS();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            toggleSBREAK();
            safeSleep(toggleTime);
            toggleSBREAK();
        }

        private void comboBoxPortName_MouseDown(object sender, MouseEventArgs e)
        {
            //update the available ports
            updateSerialPortList();
        }

        private void textBoxToggleTime_TextChanged(object sender, EventArgs e)
        {
            updateToggleTime();
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string fileToSave = Utilities.findUniqueFilepath("COMET_History_", ".txt");
            Utilities.SaveStringArrayToFile(fileToSave, collectAllSmartButtonData());
        }

        private void openFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //open the folder this program (COMET) is running in
            string directory = System.IO.Directory.GetParent(Application.ExecutablePath).FullName;
            try
            {
                Process.Start(@directory);
            }
            catch (Exception)
            {
                Console.WriteLine("Couldn't open directory");
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            panelPortOptions.Visible = !panelPortOptions.Visible;
            if(panelPortOptions.Visible && portOpen)closePortAction();
            if (!panelPortOptions.Visible && !portOpen) openPortAction();
            focusInput();
        }

        private void panelPortOptions_MouseEnter(object sender, EventArgs e)
        {
            panelPortOptions.Visible = true;
        }

        private void panelPortOptions_MouseLeave(object sender, EventArgs e)
        {
			//panelPortOptions.Visible = false;
			//button5_Click(sender,e);
        }

        private void panelPortOptions_MouseHover(object sender, EventArgs e)
        {
            panelPortOptions.Visible = true;
        }

        private void textBoxPortReadTimeout_TextChanged(object sender, EventArgs e)
        {
            updatePortTimeout(textBoxPortReadTimeout.Text);
        }

        private void updatePortTimeout(string portTimeoutString)
        {
            int portReadTimeoutTemp = 0;
            int.TryParse(portTimeoutString, out portReadTimeoutTemp);
            portReadTimeout = portReadTimeoutTemp;
            if (currentConnection != null)
            {
                try
                {
                    currentConnection.changeReadTimeout(portReadTimeout);
                }
                catch (Exception) { }
            }
        }

        private void textBoxTerminal_Click(object sender, EventArgs e)
        {
            panelPortOptions.Visible = false;
        }

        private void checkBoxAddNewLine_CheckedChanged(object sender, EventArgs e)
        {
            AddNewLineToTerminal = ((CheckBox)sender).Checked;
        }

        private void toolStripMenuItemStopScript_Click(object sender, EventArgs e)
        {
            ToolStripItem TSItem = (ToolStripItem)sender;
            ContextMenuStrip CMenu = (ContextMenuStrip)TSItem.Owner;
            SmartButton SButton = (SmartButton)CMenu.SourceControl;
            //pop up a dialog box to change the smart button

            //decide what kind of edit window to open based on the type of button
            if (SButton.buttonType == SmartButton.buttonTypes.ScriptRunner)
            {
                //stop the script from running
                SButton.storedScript.Close();
            }

        }

        private void labelPort_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                //when the user double clicks, close the port 
                if (currentConnection.IsOpen)
                {
                    //try to close the port
                    closePortAction();
                }
                else
                {
                    openPortAction();
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Error Closing/Opening Port");
            }
        }

        private void labelPort_Click(object sender, EventArgs e)
        {
        }

        private void load_folder_SmartButtons(object sender, EventArgs e)
        {
            loadDirectoriesIntoMenu(false);
        }

        private void toolStripMenuItem9_Click_1(object sender, EventArgs e)
        {
            loadDirectoriesIntoMenu(true);
        }

        private void loadDirectoriesIntoMenu(bool eraseprevious)
        {
            //look through the folder (and sub folders) that COMET is in an populate all the .txt files into the menu
            //open the folder this program (COMET) is running in
            if (eraseprevious)
            {
                FilesInDirectory = null;
                foreach (ToolStripItem d in toolStripMenuItem9.DropDownItems)
                {
                    d.Click -= loadSmartButtonsEvent;
                }
                this.toolStripMenuItem9.DropDownItems.Clear();
            }
            //only do this the first time!
            if (FilesInDirectory == null)
            {
                string directory = System.IO.Directory.GetParent(Application.ExecutablePath).FullName;
                try
                {
                    RecursiveFileProcessor FilesInDir = new RecursiveFileProcessor();
                    FilesInDirectory = FilesInDir.GetAllPaths(new string[] { directory });
                    //make a menu item for each file
                    foreach (var filename in FilesInDirectory)
                    {
                        ToolStripItem subItem = new ToolStripMenuItem(filename);
                        this.toolStripMenuItem9.DropDownItems.Add(subItem);
                        //register an event
                        subItem.Click += new System.EventHandler(loadSmartButtonsEvent);
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("Couldn't load textfile Smartbuttons");
                }
            }
        }

        private void OpenToolbox_Click(object sender, EventArgs e)
        {
            ToolBox ToolBox = new ToolBox();
            ToolBox.Show();
        }

        private void closeOtherPortsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //get the name of the current form
            Form currentopenform = Form.ActiveForm;
            for (int i = Application.OpenForms.Count - 1; i >= 0; i--)
            {
                if (Application.OpenForms[i] != currentopenform)
                    Application.OpenForms[i].Close();
            }
            //reset the window to the default size
            currentopenform.Size = new Size(856, 527);
            this.CenterToScreen();
        }

        private void openAllPortsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Open all the ports and tile the COMET windows so that the user can see all serial activity
            for (int openPort = 1; openPort < comboBoxPortName.Items.Count; openPort++)
            {
                openNewInstanceOfSerial();
            }
            //tile the windows
            TileOpenWindows(true);
        }

        private void tileWindowsToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            TileOpenWindows(true);
        }

        private void findPortWithLoopBackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            findPortWithLoopBack();
        }

        private void exitAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            for (int i = Application.OpenForms.Count - 1; i >= 0; i--)
            {
                Application.OpenForms[i].Close();
            }
        }

        private void timeStampButtonToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SmartButtonVar = createSmartButton("TIME", "TIME", true, 0, SmartButton.buttonTypes.ScriptRunner);
            //create a generic script called serialScript
            bool success = loadScript(false);
            //add the function
            serialScript.addCommandIntoCurrentScript("**TIME");
            //add this script to the current button
            SmartButtonVar.addScript(serialScript);
            setButtonStyle(SmartButtonVar, 2);
            setButtonEventHandlers(SmartButtonVar, false);
        }

        private void tileWindowsToolStripMenuItem1_Click_1(object sender, EventArgs e)
        {
            TileOpenWindows(false);
        }
        #endregion

        #region GUI customizations

        //These methods change the behavior of the slider & history buttons
        private void SerialWindow_SizeChanged(object sender, EventArgs e)
        {
            if ((this.WindowState == FormWindowState.Maximized) || (this.WindowState == FormWindowState.Normal))
            {
                correctHistoryFrame();
            }
            //base.OnSizeChanged(e);

            panelHistory.VerticalScroll.Value = panelHistory.VerticalScroll.Maximum;
            panelHistory.PerformLayout();
        }
        private void correctHistoryFrame()
        {
            try
            {
                splitContainer1.SplitterDistance = this.ClientSize.Width - button1.Width - WINDOWMARGINS1;
            }
            catch (Exception) { }
        }
        private void SerialWindow_ResizeEnd(object sender, EventArgs e)
        {
            //correctHistoryFrame();
        }
        private void splitContainer1_MouseDown(object sender, MouseEventArgs e)
        {
            // This disables the normal move behavior
            ((SplitContainer)sender).IsSplitterFixed = true;
        }
        private void splitContainer1_MouseUp(object sender, MouseEventArgs e)
        {
            // This allows the splitter to be moved normally again
            ((SplitContainer)sender).IsSplitterFixed = false;
        }
        private void splitContainer1_MouseMove(object sender, MouseEventArgs e)
        {
            // Check to make sure the splitter won't be updated by the
            // normal move behavior also
            if (((SplitContainer)sender).IsSplitterFixed)
            {
                // Make sure that the button used to move the splitter
                // is the left mouse button
                if (e.Button.Equals(MouseButtons.Left))
                {
                    // Checks to see if the splitter is aligned Vertically
                    if (((SplitContainer)sender).Orientation.Equals(Orientation.Vertical))
                    {
                        // Only move the splitter if the mouse is within
                        // the appropriate bounds
                        if (e.X > 0 && e.X < ((SplitContainer)sender).Width)
                        {
                            // Move the splitter & force a visual refresh
                            ((SplitContainer)sender).SplitterDistance = e.X;
                            ((SplitContainer)sender).Refresh();
                        }
                    }
                    // If it isn't aligned vertically then it must be
                    // horizontal
                    else
                    {
                        // Only move the splitter if the mouse is within
                        // the appropriate bounds
                        if (e.Y > 0 && e.Y < ((SplitContainer)sender).Height)
                        {
                            // Move the splitter & force a visual refresh
                            ((SplitContainer)sender).SplitterDistance = e.Y;
                            ((SplitContainer)sender).Refresh();
                        }
                    }
                    //resize the buttons
                    resizeButtons(false);
                }
                // If a button other than left is pressed or no button
                // at all
                else
                {
                    // This allows the splitter to be moved normally again
                    ((SplitContainer)sender).IsSplitterFixed = false;
                }
            }
        }
        private void resizeButtons(bool loading)
        {
            button1.Width = panelHistory.ClientSize.Width - WINDOWMARGINS1;
            foreach (Control ctrl in panelHistory.Controls)
            {
                if (ctrl is SmartButton)
                {
                    if (loading)
                        ctrl.Width = button1.Width;
                    else
                        ctrl.Width = panelHistory.ClientSize.Width - WINDOWMARGINS1;
                }
            }
        }
        private void setNoConnection()
        {
            toolStripStatusLabel1.Text = "No Connection";
            this.Text = "No Connection";
        }
        public void SetProgress(int progress, bool blocking)
        {
            try
            {
                toolStripProgressBar1.Visible = true;
                toolStripProgressBar1.Value = progress;
                //handle all pending events
                if (!blocking) Application.DoEvents();
            }
            catch (Exception) { }
        }
        public void SetProgress(bool visible, bool busy)
        {
            try
            {
                toolStripProgressBar1.Visible = visible;
                if (busy)
                {
                    toolStripProgressBar1.Style = ProgressBarStyle.Marquee;
                }
                else
                {
                    toolStripProgressBar1.Style = ProgressBarStyle.Continuous;
                }
            }
            catch (Exception)
            {
                Console.WriteLine("");
            }
        }
        private void toggleRTS()
        {
            if (RTS_0.Checked)
            {
                RTS_1.Checked = true;
            }
            else
            {
                RTS_0.Checked = true;
            }
        }
        private void toggleDTR()
        {
            if (DTR_0.Checked)
            {
                DTR_1.Checked = true;
            }
            else
            {
                DTR_0.Checked = true;
            }
        }
        private void toggleSBREAK()
        {
            if (SBREAK_0.Checked)
            {
                SBREAK_1.Checked = true;
            }
            else
            {
                SBREAK_0.Checked = true;
            }
        }
        public Point centerNewWindow(int newWinWidth, int newWinHeight)
        {
            Point cornerApp = this.Location;
            Point centerApp = new Point(this.Width / 2, this.Height / 2);
            Point centerWait = new Point(newWinWidth / 2, newWinHeight / 2);
            return new Point(cornerApp.X + centerApp.X - centerWait.X, cornerApp.Y + centerApp.Y - centerWait.Y);
        }
        //resize all open windows and tile them
        public void TileOpenWindows(bool colsfirst)
        {
            try
            {
                //get the screen resolution
                int screen_w = Screen.PrimaryScreen.WorkingArea.Width;
                int screen_h = Screen.PrimaryScreen.WorkingArea.Height;

                int tile_w = 100;
                int tile_h = 100;

                //find out how nmany open forms there are
                int openForms = Application.OpenForms.Count;

                //calculate the tile size based on how many forms are open
                double columns = 1;
                double rows = 1;
                if (colsfirst)
                {
                    columns = Math.Ceiling(Math.Sqrt(openForms));
                    rows = Math.Ceiling(openForms / (double)columns);
                }
                else
                {
                    rows = Math.Ceiling(Math.Sqrt(openForms));
                    columns = Math.Ceiling(openForms / (double)rows);
                }

                //divide the available screensize to find each tile's dimensions
                tile_w = (int)(screen_w / (int)columns);
                tile_h = (int)(screen_h / (int)rows);

                int _col = 0;
                int _row = 0;
                //this.SuspendLayout();
                foreach (Form frm in Application.OpenForms)
                {
                    //fill up the columns first
                    //if all the columns are full, move to the next row
                    if (_col == columns)
                    {
                        _col = 0;
                        _row++;
                    }

                    frm.Width = tile_w;
                    frm.Height = tile_h;
                    frm.Location = new Point(tile_w * (_col), (tile_h * (_row)));

                    //move to the next column
                    _col++;
    
                }
            }
            catch (Exception) { }
        }
        private void setRTSDTR()
        {
            //autopopulate the RTS and DTR values from the serial port
            try
            {
                if (currentConnection.getDTR())
                {
                    DTR_1.Select();
                }
                else
                {
                    DTR_0.Select();
                }

                if (currentConnection.getRTS())
                {
                    RTS_1.Select();
                }
                else
                {
                    RTS_0.Select();
                }
            }
            catch (Exception) { }
        }
        private void setDTR(bool value)
        {
            if (value) DTR_1.Checked = true;
            else DTR_0.Checked = true;
        }
        private void setRTS(bool value)
        {
            if (value) RTS_1.Checked = true;
            else RTS_0.Checked = true;
        }
        private void setSBREAK(bool value)
        {
            if (value) SBREAK_1.Checked = true;
            else SBREAK_0.Checked = true;
        }
        private void SerialWindow_Shown(object sender, EventArgs e)
        {
            correctHistoryFrame();
        }
        private void SerialWindow_Load(object sender, EventArgs e)
        {
            //start a scroll timer
            /*
            Timer MyTimer = new Timer();
            MyTimer.Interval = (200);
            MyTimer.Tick += new EventHandler((sender2, e2) => MyTimer_Tick(sender, e));
            MyTimer.Start();
             * */
        }

        #endregion

        #region Future Features
        //unused - timer starts on load out is not used ... yet :-)

        private void MyTimer_Tick(object sender, EventArgs e)
        {
            //((SmartButton)sender).Text = ((SmartButton)sender).Text.Substring(1, ((SmartButton)sender).Text.Length - 1) + ((SmartButton)sender).Text.Substring(0, 1);
        }
        private void findPortWithLoopBackUsingCurrentConnection()
        {
            //close the current port
            closePortAction();
            toolStripProgressBar1.Visible = true;
            //cycle through the serial ports and find one that returns the same thing that was sent
            for (int openPort = 0; openPort < comboBoxPortName.Items.Count; openPort++)
            {
                toolStripProgressBar1.Value = (int)(((float)(openPort + 1) / (float)comboBoxPortName.Items.Count) * 100);
                try
                {
                    comboBoxPortName.SelectedIndex = openPort;
                    openPortAction();

                    //send the port name as a string and check the response
                    //TODO should wait for the event that data is received
                    currentConnection.sendData(currentPortName, endline);

                    safeSleep(this.timeoutMS);

                    //the event is already reading and clearing the data, so this won't work if using currentConnection
                    //Need to create a different connectiont that doesn't listen if this functionality is desired
                    /*String response = currentConnection.readData();
                    if (response.Contains(currentPortName))
                    {//port found
                        MessageBox.Show("Loopback on: " + currentPortName, "Port Found", MessageBoxButtons.OKCancel, MessageBoxIcon.Asterisk);
                        break;
                    }
                    else
                    {
                    }  */
                    closePortAction();
                }
                catch (Exception)
                {
                    //throw;
                }
            }
            toolStripProgressBar1.Visible = false;
        }
        private void findPortWithLoopBack()
        {
            //close the current port
            closePortAction();
            //make a generic port for this test
            ActiveSerialPort testport = new ActiveSerialPort();
            toolStripProgressBar1.Visible = true;
            //cycle through the serial ports and find one that returns the same thing that was sent
            for (int openPort = 0; openPort < comboBoxPortName.Items.Count; openPort++)
            {
                toolStripProgressBar1.Value = (int)(((float)(openPort + 1) / (float)comboBoxPortName.Items.Count) * 100);
                try
                {
                    comboBoxPortName.SelectedIndex = openPort;
                    testport.createBasicSerialPort(currentPortName, currentBaudRate, DataType);

                    //send the port name as a string and check the response
                    testport.sendData(currentPortName, endline);

                    safeSleep(this.timeoutMS);

                    string response = testport.readData();
                    if (response.Contains(currentPortName))
                    {//port found
                        MessageBox.Show("Loopback on: " + currentPortName, "Port Found", MessageBoxButtons.OKCancel, MessageBoxIcon.Asterisk);
                        testport.closeSerialPort();
                        openPortAction();
                        break;
                    }
                    else
                    {
                        testport.closeSerialPort();
                    }
                }
                catch (Exception)
                {
                    //throw;
                }
            }
            toolStripProgressBar1.Visible = false;
        }
        private void safeSleep(int timeoutMS)
        {
            Timedout = false;
            System.Timers.Timer aTimer = new System.Timers.Timer();
            aTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            aTimer.Interval = timeoutMS;
            aTimer.Enabled = true;
            aTimer.Start();
            while (!Timedout)
            {
                //handle all pending events
                Application.DoEvents();
                System.Threading.Thread.Sleep(10);
                if (AllWindowsClosed) break;
            }
            aTimer.Stop();
        }
        private void OnTimedEvent(object source, EventArgs e)
        {
            Timedout = true;
        }


        #endregion

        private void HandleAddScriptClick(object sender, EventArgs e)
        {
            //if the button is a serial button, turn it into a script button
            //SmartButtonVar is populated in the _Opening event of the DropDownOpened event
            //via getSourceSmartButton

            if (SmartButtonVar.buttonType == SmartButton.buttonTypes.ScriptRunner)
            {
                //Do Nothing
            }
            else if (SmartButtonVar.buttonType == SmartButton.buttonTypes.SerialCommand)
            {
                //get the serial command from the current button
                string command = SmartButtonVar.CommandToSend;

                //Change this to a script button
                SmartButtonVar.buttonType = SmartButton.buttonTypes.ScriptRunner;
                //create a generic script called serialScript
                loadScript(false);
                //add the current serial command into it
                serialScript.addCommandIntoCurrentScript(command);
                //add this script to the current button
                SmartButtonVar.addScript(serialScript);
                setButtonStyle(SmartButtonVar, 2);
                setButtonEventHandlers(SmartButtonVar, false);
            }

            //now open the button to edit the script
            PromptScriptEdit Edit = new PromptScriptEdit(SmartButtonVar);
            //center the window
            Edit.Location = centerNewWindow(Edit.Width, Edit.Height);
        }

        private void getSourceSmartButton(object sender, EventArgs e)
        {
            //get the Smart Button that opened the context menu
            ContextMenuStrip CMenu = (ContextMenuStrip)sender;
            SmartButtonVar = (SmartButton)CMenu.SourceControl;
        }

        private void hEXACSIIToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string asciiText = "";

            if(textBoxTerminal.SelectedText.Length > 0)
            {
                asciiText = Utilities.ConvertHex(textBoxTerminal.SelectedText, true);

                MessageBoxButtons buttons = MessageBoxButtons.OK;
                DialogResult result;

                // Displays the MessageBox.

                result = MessageBox.Show(this, asciiText, "Converted", buttons,
                    MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

            }
        }

        private void textBoxTerminal_TextChanged(object sender, EventArgs e)
        {

        }

        private void statusStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }


        private void timeStampOffToolStripMenuItem_Click(object sender, EventArgs e)
        {
            timestampserial = false;
        }

        private void changeTimeStamp(object sender, EventArgs e)
        {
            timestampserial = true;
            timestampformat = ((ToolStripMenuItem)sender).Text;
        }

        private void customFormatToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string promptToDisplay = "Enter a format for the timestamp. Here are some examples:\n" +
                "yyyyMMddHHmmssffff       " +
                "HHmmss;        " +
                "mm:ss:ffff >>";

            string desiredTimestampFormat = GetInputFromUser(promptToDisplay);

            try
            {
                string trytimeformat = DateTime.Now.ToString(desiredTimestampFormat);
                timestampformat = desiredTimestampFormat;
                timestampserial = true;
            }
            catch
            {
                System.Windows.Forms.MessageBox.Show("Yeah ... Not gonna work");
            }    
            
        }

        private void AButton_Click(object sender, EventArgs e)
        {
            try
            {
                string pathname = (string)((Button)sender).Tag;
                string[] smartbuttonslist = File.ReadAllLines(pathname);
                ClearSmartButtons();
                loadSmartButtons(smartbuttonslist);
            }
            catch (Exception )
            {
                Console.WriteLine("no smart buttons loaded");
            }
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            bytespaces = checkBox2.Checked;
            focusInput();
        }

        private string addByteSpacing(string unspacedByteString)
        {
            {
                int msgsize = unspacedByteString.Length;
                if (msgsize == 0)
                {
                    return "";
                }

                //makes sure it's even
                if (msgsize % 2 != 0)
                {
                    unspacedByteString = unspacedByteString.PadLeft(unspacedByteString.Length + 1, '0');
                }

                StringBuilder addingSpacing = new StringBuilder();
                int indexChar = 0;
                while (true)
                {
                    addingSpacing.Append(unspacedByteString[indexChar]);
                    addingSpacing.Append(unspacedByteString[indexChar + 1]);
                    indexChar = indexChar + 2;
                    if (indexChar >= msgsize)
                        break;
                    addingSpacing.Append(" ");
                }
                return addingSpacing.ToString().ToUpper();
            }
        }

        private void toolTip1_Popup(object sender, PopupEventArgs e)
        {

        }
    }



    public enum enumComparisonType
    {
        stringType,
        between,
        lessThan,
        greaterThan
    }
}