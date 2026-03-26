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
        private SerialPortManager serialPortManager;
        private SmartButtonManager smartButtonManager;
        private DataFormatter dataFormatter;

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
        private bool writeCR = true;
        private bool writeNL = true;
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

        #region Basic setup & methods

        //Constructor
        public SerialWindow()
        {
            InitializeComponent();

            // If the control is being created inside the WinForms designer, skip
            // runtime-only initialization. The designer instantiates the form
            // and running code that touches serial ports, files, processes or
            // other runtime resources will throw and prevent the designer from
            // opening. LicenseManager.UsageMode is the most reliable check at
            // constructor time.
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            {
                return;
            }

            // Create ToolTip as a local variable so the designer never sees it as
            // an IComponent field and tries to manage it (which caused designer load errors).
            var runtimeToolTip = new System.Windows.Forms.ToolTip(this.components);
            runtimeToolTip.ShowAlways = true;

            // Initialize managers
            serialPortManager = new SerialPortManager();
            serialPortManager.DataReceived += (s, data) => updateTerminal(data, true);
            dataFormatter = new DataFormatter();

            updateSerialPortList();

            initializeHistoryButtons();
            smartButtonManager = new SmartButtonManager(panelHistory, button1, runtimeToolTip, contextMenuSmartButton);
            smartButtonManager.SetShowCommand(showCMD);

            // Add "Smart Button" and "Script" options to the panelHistory context menu Add submenu
            try
            {
                var smartMenu = new ToolStripMenuItem("Smart Button...");
                smartMenu.Click += (s, e) =>
                {
                    // create a placeholder smart button then open the edit dialog that edits the button in-place
                    var btn = createSmartButton("", "", showCMD, 0, SmartButton.buttonTypes.SerialCommand);
                    setButtonEventHandlers(btn, true);
                    // open the existing PromptSmartButtonEdit which edits a SmartButton instance
                    PromptSmartButtonEdit editDlg = new PromptSmartButtonEdit(btn);
                    editDlg.Location = centerNewWindow(editDlg.Width, editDlg.Height);
                    editDlg.ShowDialog(this);
                };

                var scriptMenu = new ToolStripMenuItem("Script...");
                scriptMenu.Click += (s, e) =>
                {
                    // create a new script smart button and open the script edit dialog
                    var btn = createSmartButton("Script", "Script", true, 2, SmartButton.buttonTypes.ScriptRunner);
                    // ensure a ScriptRunner exists
                    loadScript(false);
                    btn.addScript(serialScript);
                    using (var dlg = new PromptScriptEdit(btn))
                    {
                        dlg.ShowDialog(this);
                    }
                };

                // Insert at top of Add menu
                addToolStripMenuItem.DropDownItems.Insert(0, scriptMenu);
                addToolStripMenuItem.DropDownItems.Insert(0, smartMenu);
            }
            catch (Exception) { }

            selectDefaults();
            registerEvents();

            //this.AutoScaleDimensions = new System.Drawing.SizeF(5F, 12F);
            //this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        }

        public void updateSerialPortList()
        {
            if (serialPortManager == null) return;
            comboBoxPortName.DataSource = serialPortManager.GetAvailablePortNames();
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
            serialPortManager.SetPortConfiguration(currentPortName, currentBaudRate, currentParity, currentDataBits, currentStopBits);
            portOpen = serialPortManager.OpenPortBasic();
            currentConnection = serialPortManager.Connection;
            return portOpen;
        }

        private bool openPort()
        {
            serialPortManager.SetPortConfiguration(currentPortName, currentBaudRate, currentParity, currentDataBits, currentStopBits);
            serialPortManager.SetTimeouts(timeoutMS, portReadTimeout);
            serialPortManager.DataType = DataType;
            portOpen = serialPortManager.OpenPort();
            currentConnection = serialPortManager.Connection;
            return portOpen;
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
                // send button highlight color: #196ebf
                button1.BackColor = Color.FromArgb(0x19, 0x6E, 0xBF);
                button1.ForeColor = Color.White;
                setComboBoxStates(false);
                setRTSDTR();
                updateLineEnding();
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
                button1.BackColor = SystemColors.Control;
                button1.ForeColor = SystemColors.ControlText;
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
            portOpen = false;
            return serialPortManager.ClosePort();
        }

        private void sendDataToSerialConnection()
        {
            //Send Command - Interacts with the GUI
            string dataSent = serialPortManager.SendData(dataToSend, writeCR || writeNL);
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
            string dataSent = serialPortManager.SendData(data, writeCR || writeNL);
            if (!string.IsNullOrEmpty(dataSent))
            {
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

            if (_historyScrollFilter != null)
            {
                Application.RemoveMessageFilter(_historyScrollFilter);
                _historyScrollFilter = null;
            }
        }

        private void updateTimeout()
        {
            if (currentConnection == null) return;
            float timeoutVal = 0;
            if (float.TryParse(textBoxTimeout.Text, out timeoutVal))
            {
                timeoutMS = (int)(timeoutVal);
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

            //format data using DataFormatter (byte spacing and timestamps)
            dataFormatter.ByteSpacesEnabled = bytespaces;
            dataFormatter.TimestampEnabled = timestampserial;
            dataFormatter.TimestampFormat = timestampformat;
            newText = dataFormatter.FormatReceivedData(newText, DataType);

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

        #endregion Serial Terminal

        #region History Button Methods

        private SmartButton createSmartButton(string buttonCommandText, string buttonDescriptionText, bool displayCMD, int buttonStyle, SmartButton.buttonTypes buttonType)
        {
            var newbutton = smartButtonManager.CreateSmartButton(buttonCommandText, buttonDescriptionText, displayCMD, buttonStyle, buttonType);
            smartButtonManager.SetButtonEventHandlers(newbutton, 
                new EventHandler(this.SmartButton_SendSerial),
                new EventHandler(this.SmartButton_RunScript),
                new EventHandler(this.SmartButton_LogTerminal),
                new EventHandler(this.newbutton_MouseHover),
                new KeyPressEventHandler(this.textBoxTerminal_KeyPress));
            return newbutton;
        }

        private void setButtonEventHandlers(SmartButton newbutton, bool IsNew)
        {
            smartButtonManager.SetButtonEventHandlers(newbutton,
                new EventHandler(this.SmartButton_SendSerial),
                new EventHandler(this.SmartButton_RunScript),
                new EventHandler(this.SmartButton_LogTerminal),
                new EventHandler(this.newbutton_MouseHover),
                new KeyPressEventHandler(this.textBoxTerminal_KeyPress));
        }

        private void setButtonStyle(Button newbutton, int buttonStyle)
        {
            smartButtonManager.SetButtonStyle(newbutton, buttonStyle);
        }

        private void addLastCommandToHistoryButton(String lastCommandSent)
        {
            if (!String.IsNullOrEmpty(lastCommandSent) && writeSmartButton && writeSmartButtonEnabled)
            {
                createSmartButton(lastCommandSent, lastCommandSent, showCMD, 0, SmartButton.buttonTypes.SerialCommand);
                smartButtonManager.RelayoutButtons();
                panelHistory.AutoScrollPosition = new Point(0, 0);
                panelHistory.PerformLayout();
            }
        }

        private void ClearSmartButtons(int reference, bool direction)
        {
            smartButtonManager.ClearSmartButtons(reference, direction);
        }

        private void ClearSmartButtons()
        {
            smartButtonManager.ClearAllSmartButtons();
        }

        private String[] collectAllSmartButtonData()
        {
            return smartButtonManager.CollectAllSmartButtonData();
        }

        private void changeHistoryButtonDisplay(bool ShowCommand)
        {
            smartButtonManager.ChangeHistoryButtonDisplay(ShowCommand);
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
            // Suspend layout updates on the panel where buttons are added (critical optimization)
            panelHistory.SuspendLayout();
            this.SuspendLayout();

            try
            {
                string[] recalledData = dataIn;
                int max_data_length = 200;

                if (recalledData.Length > max_data_length)
                {
                    var response = MessageBox.Show("File is greater than " + max_data_length.ToString() + 
                        " lines long. \nProceed ?", "Large File", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                    if (response == DialogResult.No)
                    {
                        recalledData = null;
                    }
                }

                // Cache the string separator instead of allocating per iteration
                string[] stringSeparator = new string[] { "\t" };
                int buttonStyle = 0;
                bool scriptbutton = false;
                SmartButton tempButton = new SmartButton();

                if (recalledData != null && recalledData.Length != 0)
                {
                    for (int i = 0; i < recalledData.Length; i++)
                    {
                        string[] parsed = recalledData[i].Split(stringSeparator, StringSplitOptions.RemoveEmptyEntries);

                        if (parsed.Length == 0)
                        {
                            buttonStyle = (buttonStyle + 1) % 2;
                            continue;
                        }

                        // Removed redundant null check - Length check is sufficient
                        if (parsed[0].Length == 0)
                            continue;

                        string command = parsed[0];
                        string description = parsed[0];

                        // Cache the trimmed and lowercased command for reuse
                        string commandTrimmed = command.Trim();
                        string commandLower = command.ToLower();

                        if (commandTrimmed.StartsWith("**"))
                        {
                            // Use case-insensitive comparison (cheaper than .ToLower() twice)
                            if (commandLower.Contains("**script"))
                            {
                                scriptbutton = !scriptbutton;

                                if (scriptbutton)
                                {
                                    if ((parsed.Length > 1) && parsed[1].Length > 0)
                                    {
                                        description = parsed[1];
                                    }
                                    tempButton = createSmartButton(description, description, showCMD, 2, SmartButton.buttonTypes.ScriptRunner);
                                    loadScript(false);
                                }
                                else
                                {
                                    tempButton.addScript(serialScript);
                                }
                            }
                        }
                        else if (!commandLower.Contains("**script"))
                        {
                            if (scriptbutton)
                            {
                                serialScript.addCommandIntoCurrentScript(recalledData[i]);
                            }
                            else
                            {
                                if ((parsed.Length > 1) && parsed[1].Length > 0)
                                {
                                    description = parsed[1];
                                }
                                createSmartButton(command, description, showCMD, buttonStyle, SmartButton.buttonTypes.SerialCommand);
                            }
                        }
                    }
                }

                //if everything loaded correctly, display the descriptions
                showCMD = false;
                changeHistoryButtonDisplay(showCMD);
                smartButtonManager.RelayoutButtons();
            }
            catch (Exception)
            {
                Console.WriteLine("Error opening / parsing saved data");
            }
            finally
            {
                panelHistory.ResumeLayout(true);
                this.ResumeLayout(true);
            }
            // Scroll AFTER ResumeLayout so the layout is settled and Maximum is correct
            panelHistory.VerticalScroll.Value = panelHistory.VerticalScroll.Maximum;
            panelHistory.PerformLayout();
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
			bool writeNewLine = false;

			try
			{
				//usage: false or true as the argument
				writeNewLine = Convert.ToBoolean(argument);
			}
			catch { }
			//check the boxes to write CR and NL at end of each command
			checkBoxCR.Checked = writeNewLine;
			checkBoxNL.Checked = writeNewLine;
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
            
            // Update title bar with port description even when port is not open
            if (!string.IsNullOrEmpty(currentPortName))
            {
                string portDescription = serialPortManager.GetPortDescription(currentPortName);
                this.Text = "COMET - " + portDescription;
            }
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
                Edit.ShowDialog(this);
            }
            else if (SmartButtonVar.buttonType == SmartButton.buttonTypes.SerialCommand)
            {
                PromptSmartButtonEdit Edit = new PromptSmartButtonEdit(SmartButtonVar);
                //center the window
                Edit.Location = centerNewWindow(Edit.Width, Edit.Height);
                Edit.ShowDialog(this);
            }
        }

        private void copyCommandToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //SmartButtonVar is populated in the _Opening event of the DropDownOpened event
            //via getSourceSmartButton

            if (SmartButtonVar.buttonType == SmartButton.buttonTypes.ScriptRunner
                && SmartButtonVar.storedScript != null
                && SmartButtonVar.storedScript.currentScript != null)
            {
                // Serialize the script lines to clipboard
                var sb = new StringBuilder();
                foreach (System.Collections.ArrayList item in SmartButtonVar.storedScript.currentScript)
                {
                    if (item.Count == 0) continue;
                    string type = (string)item[0];
                    if (type == "FUNCTION")
                    {
                        string line = "**" + (string)item[1];
                        for (int i = 2; i < item.Count; i++)
                            line += "\t" + (string)item[i];
                        sb.AppendLine(line);
                    }
                    else // SERIAL
                    {
                        sb.AppendLine((string)item[1]);
                    }
                }
                string scriptText = sb.ToString().TrimEnd();
                if (!string.IsNullOrEmpty(scriptText))
                    Clipboard.SetText(scriptText);
            }
            else
            {
                Clipboard.SetText(SmartButtonVar.CommandToSend);
            }
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
                updateLayoutHistoryPanel();
            }
            catch (Exception)
            {
                Console.WriteLine("Failed Loading Files");
            }
        }

        public void updateLayoutHistoryPanel()
        {
            smartButtonManager.RelayoutButtons();
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

        private void checkBoxCR_CheckedChanged(object sender, EventArgs e)
        {
            writeCR = ((CheckBox)sender).Checked;
            updateLineEnding();
            focusInput();
        }

        private void checkBoxNL_CheckedChanged(object sender, EventArgs e)
        {
            writeNL = ((CheckBox)sender).Checked;
            updateLineEnding();
            focusInput();
        }

        private void updateLineEnding()
        {
            string lineEnding = (writeCR ? "\r" : "") + (writeNL ? "\n" : "");
            if (currentConnection != null)
            {
                currentConnection.LineEnding = lineEnding;
            }
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
                //create a new script button if the script loads correctly
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
        }

        private void comboBoxPortName_MouseDown(object sender, MouseEventArgs e)
        {
            //update the available ports
            updateSerialPortList();
        }

        private void comboBoxPortName_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            ComboBox combo = (ComboBox)sender;
            string portName = combo.Items[e.Index].ToString();

            // Update title bar when hovering over an item
            if ((e.State & DrawItemState.Selected) == DrawItemState.Selected || 
                (e.State & DrawItemState.HotLight) == DrawItemState.HotLight)
            {
                string portDescription = serialPortManager.GetPortDescription(portName);
                this.Text = "COMET - " + portDescription;
            }

            // Draw the item
            e.DrawBackground();
            using (Brush brush = new SolidBrush(e.ForeColor))
            {
                e.Graphics.DrawString(portName, e.Font, brush, e.Bounds);
            }
            e.DrawFocusRectangle();
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

        private void panelPortOptions_Click(object sender, EventArgs e)
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
            smartButtonManager.RelayoutButtons();
        }
        private void correctHistoryFrame()
        {
            try
            {
                // Use the stored initial history width instead of the current button width.
                // Using button1.Width here created a feedback loop during resizing that
                // caused the Send button to grow uncontrollably. historyWidth is set once
                // during initialization and provides a stable target for the splitter.
                splitContainer1.SplitterDistance = this.ClientSize.Width - historyWidth - WINDOWMARGINS1;
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
            if (smartButtonManager == null) return;
            correctHistoryFrame();

            //load the .ini file and initial state after the form is fully laid out
            //so panelHistory.ClientSize is correct for button positioning
            loadInitialState();
        }
        // Redirects mouse-wheel events to panelHistory even when a SmartButton (or the panel
        // background) has no keyboard focus, without stealing focus from the terminal input.
        private sealed class PanelScrollFilter : IMessageFilter
        {
            private const int WM_MOUSEWHEEL = 0x020A;
            private readonly Panel _panel;

            public PanelScrollFilter(Panel panel) { _panel = panel; }

            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != WM_MOUSEWHEEL) return false;

                // WM_MOUSEWHEEL is posted to the *focused* window, not the one under
                // the cursor – so m.HWnd is useless here. Check the actual cursor
                // position against the panel's screen rectangle instead.
                Rectangle panelScreen = _panel.RectangleToScreen(_panel.ClientRectangle);
                if (!panelScreen.Contains(Control.MousePosition)) return false;

                // High word of WParam is a signed delta; one detent = ±120
                int delta = (short)(((long)m.WParam >> 16) & 0xFFFF);
                int lines  = SystemInformation.MouseWheelScrollLines;
                int step   = lines * 31; // 29 px button + 2 px gap

                int currentY = -_panel.AutoScrollPosition.Y;
                int newY = Math.Max(0, currentY - (delta / 120) * step);
                _panel.AutoScrollPosition = new Point(0, newY);

                return true; // consumed – do not forward to the focused control
            }
        }

        private IMessageFilter _historyScrollFilter;

        private void SerialWindow_Load(object sender, EventArgs e)
        {
            _historyScrollFilter = new PanelScrollFilter(panelHistory);
            Application.AddMessageFilter(_historyScrollFilter);
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
                    currentConnection.sendData(currentPortName, writeCR || writeNL);

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
                    testport.sendData(currentPortName, writeCR || writeNL);

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
            if (SmartButtonVar == null) return;

            if (SmartButtonVar.buttonType == SmartButton.buttonTypes.ScriptRunner)
            {
                // Ensure the button has a script loaded before opening the editor
                if (SmartButtonVar.storedScript == null)
                {
                    loadScript(false);
                    SmartButtonVar.addScript(serialScript);
                }
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
            Edit.ShowDialog(this);
        }

        private void getSourceSmartButton(object sender, EventArgs e)
        {
            //get the Smart Button that opened the context menu
            ContextMenuStrip CMenu = (ContextMenuStrip)sender;
            SmartButtonVar = CMenu.SourceControl as SmartButton;

            // Update menu text based on button type
            if (SmartButtonVar != null && SmartButtonVar.buttonType == SmartButton.buttonTypes.ScriptRunner)
            {
                copyCommandToolStripMenuItem.Text = "Copy Script";
                toolStripMenuItem4.Text = "Edit Script";
            }
            else
            {
                copyCommandToolStripMenuItem.Text = "Copy Command";
                toolStripMenuItem4.Text = "Edit Command";
            }
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
    }



    public enum enumComparisonType
    {
        stringType,
        between,
        lessThan,
        greaterThan
    }
}