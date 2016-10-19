using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
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
        private int portReadTimeout = 3;

        private Boolean Timedout = false;
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
        private String dataToSend = "";
        private Color textReceive = Color.FromName("Lime");
        private Color textSend = Color.FromName("DarkTurquoise");
        private Boolean ASCII = true;
        private ScriptRunner serialScript;
        private DialogWin ResultWindow;
        private Boolean stopThread = false;
        private int historyWidth = 0;
        private int WINDOWMARGINS1 = 30;//used to set the buttons size small enough that a horizontal scroll won't appear
        private Transfer transferData;

        private bool localBuffer = false;
        private string localBufferData = "";

        //variable to let safesleep know that everything is closing and it should quit
        //set in 
        private bool AllWindowsClosed = false;
        private System.Collections.ArrayList lastCommandList = new System.Collections.ArrayList(); //list of last typed commands
        private int lastCommandIndex = 0;
        private string iniFilePath = "COMET.ini";
        private string currentHistoryFile = "";

        //Data for history Buttons
        private Button lastButtonForLocation;

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
        }

        public void updateSerialPortList()
        {
            string[] currentSerialPortList = SerialPort.GetPortNames();

            Array.Sort(currentSerialPortList, new NaturalComparer());
            comboBoxPortName.DataSource = currentSerialPortList;
        }

        public void setComboBoxStates(Boolean newstate)
        {
            comboBoxBaudRate.Enabled = newstate;
            comboBoxPortName.Enabled = newstate;
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

            if (ASCII)
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
            try
            {
                //build the path based on the current directory
                string directory = System.IO.Directory.GetParent(Application.ExecutablePath).FullName;
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
        }

        #endregion Basic setup & methods

        #region Serial Port Methods

        private void updateBaudRates()
        {
            //For the selected serial port, get the available baud rates
            //If there was something selected, try to maintain the selected one
        }

        private Boolean openPortBasic()
        {
            try
            {
                currentConnection = new ActiveSerialPort();
                if (currentConnection.createBasicSerialPort(currentPortName, currentBaudRate, ASCII))
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

        private Boolean openPort()
        {
            try
            {
                currentConnection = new ActiveSerialPort();
                if (currentConnection.openSerialPort(currentPortName, currentBaudRate, currentParity, currentDataBits, currentStopBits, timeoutMS, ASCII, portReadTimeout))
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

        public Boolean openPortActionFirstTime()
        {
            int numberOfComPorts = comboBoxPortName.Items.Count;

            for (int i = 0; i < numberOfComPorts; i++)
            {
                if (openPortAction() == false)
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

        public Boolean openPortAction()
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
                return false;
            }
        }

        public Boolean closePortAction()
        {
            if (closePort())
            {
                buttonOpenSerial.Text = "Open Port";
                setComboBoxStates(true);
                toolStripStatusLabel1.Text = currentConnection.getConnectionInfo(1);
                this.Text = "COMET - " + currentConnection.getConnectionInfo(0);
                return true;
            }
            else
            {
                return false;
            }
        }

        private Boolean closePort()
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
            String dataSent = currentConnection.sendData(dataToSend, endline);
            updateTerminal(dataSent, false);
            AcceptButton = button1;
            if (!keepText) { textBox1.Text = ""; }
            if (rememberLastCommand)
            {
                //add to the history buttons
                addLastCommandToHistoryButton(dataSent);
            }
            //add the command to arraylist of the last commands
            addLastCommand(dataSent);
            focusInput();
        }

        private void sendDataToSerialConnectionBasic(String data)
        {
            //Basic send command - doesn't affect the GUI
            if (currentConnection != null)
            {
                String dataSent = currentConnection.sendData(data, endline);
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
                this.currentConnection.closeSerialPort();
            }
            catch { }

            //close any results window
            try
            {
                ResultWindow.Close();
                ResultWindow.Dispose();
                ResultWindow = null;
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

        public void setPortType(String PortType)
        {
            try
            {
                currentConnection.dataType = PortType;
            }
            catch (Exception)
            {
            }
        }

        private Boolean initTransfer()
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

        private void updateTerminal(String newText, Boolean output)
        {
            if (newText.Length >= 1)
            {
                if (localBuffer)
                {
                    localBufferData += newText + Environment.NewLine;
                }
                Color textColor = Color.White;
                //different color for in/out text
                if (output)
                {
                    textColor = textReceive;
                    if ((!(newText[newText.Length - 1].Equals(Environment.NewLine))) && AddNewLineToTerminal)
                    {
                        newText = newText + Environment.NewLine;
                    }
                }
                else
                {
                    textColor = textSend;

                    if ((!(newText[newText.Length - 1].Equals(Environment.NewLine))))
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
                    catch (Exception)
                    {
                    }
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
                    catch (Exception)
                    {
                    }
                }
            }
        }

        private Boolean addLastCommand(String lastCommandSent)
        {
            if (!(String.IsNullOrEmpty(lastCommandSent)))
            {
                //check out the previous command and only add if this command is new
                if (DiscardDuplicateEntriesInList)
                {
                    //not implemented
                }

                lastCommandList.Add(lastCommandSent);
                lastCommandIndex = lastCommandList.Count;
                return true;
            }
            else
            {
                return false;
            }
        }

        public static string AddSpaceAfter2(Match m)
        {
            string newString = m + "";
            return newString.Insert(2, " ");
        }

        #endregion Serial Terminal

        #region History Button Methods

        private SmartButton createHistoryButton(String buttonCommandText, String buttonDescriptionText, Boolean displayCMD, int buttonStyle)
        {
            var newbutton = new SmartButton(SmartButton.buttonTypes.SerialCommand, buttonCommandText, buttonDescriptionText, displayCMD);

            if (displayCMD)
            {
                this.toolTip1.SetToolTip(newbutton, newbutton.CommandDescription);
            }
            else
            {
                this.toolTip1.SetToolTip(newbutton, newbutton.CommandToSend);
            }

            setButtonStyle(newbutton, buttonStyle);
            setButtonLocation(newbutton);
            setButtonEventHandlers(newbutton);

            return newbutton;
        }

        private SmartButton createScriptButton(String buttonCommandText, String buttonDescriptionText, Boolean displayCMD, int buttonStyle)
        {
            var newbutton = new SmartButton(SmartButton.buttonTypes.ScriptRunner, buttonCommandText, buttonDescriptionText, displayCMD);

            if (displayCMD)
            {
                this.toolTip1.SetToolTip(newbutton, newbutton.CommandDescription);
            }
            else
            {
                this.toolTip1.SetToolTip(newbutton, newbutton.CommandToSend);
            }

            setButtonStyle(newbutton, buttonStyle);
            setButtonLocation(newbutton);
            setButtonEventHandlers(newbutton);

            return newbutton;
        }

        private void setButtonLocation(SmartButton newbutton)
        {
            newbutton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));

            //find the relative relative location
            Point basePoint = lastButtonForLocation.Location;
            Point offsetPoint = new Point(0, -29);
            basePoint.Offset(offsetPoint);
            newbutton.Location = basePoint;
            newbutton.Size = lastButtonForLocation.Size;
            lastButtonForLocation = newbutton;
            //set the size
        }

        private void setButtonEventHandlers(SmartButton newbutton)
        {
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

            newbutton.MouseHover += new System.EventHandler(this.newbutton_MouseHover);
            newbutton.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textBoxTerminal_KeyPress);
            newbutton.ContextMenuStrip = this.contextMenuSmartButton;
            this.panelHistory.Controls.Add(newbutton);
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
            if (!(String.IsNullOrEmpty(lastCommandSent)) && writeSmartButton && writeSmartButtonEnabled)
            {
                createHistoryButton(lastCommandSent, lastCommandSent, showCMD, 0);
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
                    ((SmartButton)control).removeThisButton();
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
                return new String[] { "Error" };
            }
        }

        private void changeHistoryButtonDisplay(Boolean ShowCommand)
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

        private void writeToTextFile(String path, String[] data)
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
            String[] recalledData = dataIn;
            string[] stringSeparator = new string[] { "\t" };
            int buttonStyle = 0;
            //variable to set wheter a script button is being created
            Boolean scriptbutton = false;
            SmartButton tempButton = new SmartButton();
            try
            {
                if (!(recalledData == null || recalledData.Length == 0))
                {
                    for (int i = 0; i < recalledData.Length; i++)
                    {
                        String[] parsed = recalledData[i].Split(stringSeparator, StringSplitOptions.RemoveEmptyEntries);
                        //parse out the data
                        //when encountering an empty line, change the button style
                        //this allows for grouping button appeareance by function
                        if (parsed.Length == 0)
                        {
                            buttonStyle++;
                            buttonStyle = buttonStyle % 2;
                        }
                        //make sure there is data on the line
                        if (!(parsed == null || parsed.Length == 0))
                        {
                            if (parsed[0].Length > 0)
                            {
                                String command = parsed[0];
                                String description = parsed[0];

                                //find out if there is a special command
                                if (command.Trim().StartsWith("**"))
                                {
                                    //command = command.Substring(2).ToLower();
                                    if (command.Contains("**script"))
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
                                            tempButton = createScriptButton(description, description, showCMD, 2);
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
                                if (!(command.Contains("**script")))
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
                                        createHistoryButton(command, description, showCMD, buttonStyle);
                                    }
                                }
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

        private String[] openSavedSmartButtons()
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

        private String openSingleFile()
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

        private bool loadScript(Boolean fromFile)
        {
            try
            {
                serialScript = new ScriptRunner(toggleTime);
                serialScript.clearCurrentScript();
                if (fromFile)
                {
                    if (serialScript.loadScriptFromFile())
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
            return false;
        }

        private void runScript(ScriptRunner scriptToRun)
        { //single threaded at this point....
            try
            {
                if (scriptToRun != null)
                {
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
                        //add on the loop delay AFTER the first time the loop is run
                        if (loopIteration > 0) safeSleep(loopdelay);
                        //The Script
                        for (int current = 0; current < commandArray.Length; current++)
                        {
                            transferProgressChanged((int)(100 * ((float)current / (float)commandArray.Length)));
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
                        catch
                        {
                        }
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
                        catch
                        {
                        }
                        setRTS(serialtermval);
                    }
                    break;

                case "dtr":
                    if (arguments != null)
                    {
                        bool serialtermval = false;

                        try
                        {
                            //usage: false or true as the argument
                            serialtermval = Convert.ToBoolean((string)arguments[0]);
                        }
                        catch
                        {
                        }
                        setDTR(serialtermval);
                    }
                    break;

                case "response_str":
                    //usage:
                    //function name {tab} timeout ms {tab} command {tab} expected response
                    //**response_str   2000 qdver   rqdver
                    ScriptCheckForResponse(arguments, "string");
                    break;

                case "response_int_between":
                    //usage:
                    //function name {tab} timeout ms {tab} command {tab} parsetext {tab} low int {tab} high int
                    //**response_int_between {tab} 2000 {tab} QDAIN,1 {tab} RQDAIN,1, Ain[1] (VBAT) = {tab}7000 {tab} 8000
                    ScriptCheckForResponse(arguments, "between");
                    break;

                case "response_log":
                    //usage:
                    //function name {tab} timeout ms {tab} command {tab} expected response / parseout string
                    //**response_log   500 qdain,1   RQDAIN,1, Ain[1] (VBAT) = 
                    ScriptLogResponse(arguments, "time");
                    break;

                default:
                    break;
            }
        }

        private void ScriptCheckForResponse(object[] arguments, string comparisontype)
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
                long duration = stopWatch.ElapsedMilliseconds;
                bool stopnow = false;
                while (!stopnow)
                {
                    //the the GUI redraw
                    Application.DoEvents();
                    //see if the response is in the buffer
                    switch (comparisontype)
                    {
                        case "string":
                            testcomplete = res.checkForResponse(localBufferData, out passedtest);
                            break;

                        case "between":
                            int lowVal = 0;
                            int.TryParse((string)arguments[3], out lowVal);
                            int highVal = 100;
                            int.TryParse((string)arguments[4], out highVal);
                            testcomplete = res.checkValBetween(localBufferData, lowVal, highVal, out detail, out passedtest);
                            break;

                        default:
                            break;
                    }

                    if (testcomplete)
                    {
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

        private void ScriptLogResponse(object[] arguments, string logtype)
        {
            //valid log types
            // time

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
                string collectedData = "";

                //turn on the local buffer so the data is kept
                localBuffer = true;

                //send the command to ther terminal
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
                        case "time":
                            testcomplete = res.getResponseData(localBufferData, out collectedData);
                            break;

                        default:
                            break;
                    }

                    if (testcomplete)
                    {
                        res.logResponse(collectedData);
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

                if (passedtest)
                {

                }
                else
                {

                }
                //clear the buffer and stop recording
                resetbuffer();
            }
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

        # region GUI Handlers

        private void comboBoxPortName_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.currentPortName = (String)comboBoxPortName.SelectedItem;
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
                    if ((lastCommandIndex) > 0)
                    {
                        lastCommandIndex--;
                        textBox1.Text = (String)lastCommandList[lastCommandIndex];
                    }
                    focusInput();
                    e.Handled = true;
                    writeSmartButton = false;
                    break;

                case Keys.Down:
                    if ((lastCommandIndex) <= lastCommandList.Count - 2)
                    {
                        lastCommandIndex++;
                        textBox1.Text = (String)lastCommandList[lastCommandIndex];
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
                catch (Exception)
                {
                }
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
                Int32.TryParse(comboBoxDataBits.SelectedItem.ToString(), out this.currentDataBits);
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
            findPortWithLoopBack();
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
            ToolStripItem TSItem = (ToolStripItem)sender;
            ContextMenuStrip CMenu = (ContextMenuStrip)TSItem.Owner;
            SmartButton SButton = (SmartButton)CMenu.SourceControl;

            SButton.removeThisButton();
        }

        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            ToolStripItem TSItem = (ToolStripItem)sender;
            ContextMenuStrip CMenu = (ContextMenuStrip)TSItem.Owner;
            SmartButton SButton = (SmartButton)CMenu.SourceControl;
            //pop up a dialog box to change the smart button

            //decide what kind of edit window to open based on the type of button
            if (SButton.buttonType == SmartButton.buttonTypes.ScriptRunner)
            {
                PromptScriptEdit Edit = new PromptScriptEdit(SButton);
                //center the window
                Edit.Location = centerNewWindow(Edit.Width, Edit.Height);
            }
            else if (SButton.buttonType == SmartButton.buttonTypes.SerialCommand)
            {
                PromptSmartButtonEdit Edit = new PromptSmartButtonEdit(SButton);
                //center the window
                Edit.Location = centerNewWindow(Edit.Width, Edit.Height);
            }
        }

        private void copyCommandToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripItem TSItem = (ToolStripItem)sender;
            ContextMenuStrip CMenu = (ContextMenuStrip)TSItem.Owner;
            SmartButton SButton = (SmartButton)CMenu.SourceControl;
            //pop up a dialog box to change the smart button
            Clipboard.SetText(SButton.CommandToSend);
        }

        private void clearAllButtonsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ClearSmartButtons();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            loadSmartButtons(openSavedSmartButtons());
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
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void panelHistory_DragDrop(object sender, DragEventArgs e)
        {
            Console.WriteLine("Loading Files");
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            string[] dataToLoad = File.ReadAllLines(files[0]);
            loadSmartButtons(dataToLoad);

            panelHistory.VerticalScroll.Value = panelHistory.VerticalScroll.Maximum;
            updateLayoutHistoryPanel();
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
            ASCII = false;
            setPortType("HEX");
            focusInput();
        }

        private void radioButtonASCII_CheckedChanged(object sender, EventArgs e)
        {
            ASCII = true;
            setPortType("ASCII");
            focusInput();
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            {
                //HEX validation - not implemented!!!!
                if (!(ASCII))
                {
                    char c = e.KeyChar;
                    if (!((c <= 0x66 && c >= 61) || (c <= 0x46 && c >= 0x41) || (c >= 0x30 && c <= 0x39)))
                    {
                        e.Handled = true;
                    }
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
            MessageBox.Show("Not Tested");

            if (initTransfer())
            {
                String pathToOpen = openSingleFile();
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
                String pathToOpen = openSingleFile();
                YModemSendFile(pathToOpen);
            }
        }

        private void YModemSendFile(String pathToOpen)
        {
            if (pathToOpen != null)
            {
                SetProgress(true, false);
                transferData.YmodemUploadFile(pathToOpen);
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
                Int32.TryParse(testDialog.textBoxDelayTime.Text, out new_timeout);
            }
            else
            {
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

        private void transferProgressChanged(int xferProgress)
        {
            //Action handler for changing the progress from the transfer
            SetProgress(xferProgress);
            //Console.WriteLine(xferProgress);
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            endline = checkBox1.Checked;
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
                SmartButton loaded = createScriptButton(serialScript.scriptName, serialScript.scriptPath, true, 2);
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
            //set DTR low
            currentConnection.setDTR(!((RadioButton)sender).Checked);
        }

        private void DTR_1_CheckedChanged(object sender, EventArgs e)
        {
            //set DTR high
            currentConnection.setDTR(((RadioButton)sender).Checked);
        }

        private void RTS_0_CheckedChanged(object sender, EventArgs e)
        {
            //set RTS low
            currentConnection.setRTS(!((RadioButton)sender).Checked);
        }

        private void RTS_1_CheckedChanged(object sender, EventArgs e)
        {
            //set RTS high
            currentConnection.setRTS(((RadioButton)sender).Checked);
        }

        private void SBREAK_0_CheckedChanged(object sender, EventArgs e)
        {
            //set serial break low
            currentConnection.setSBREAK(!((RadioButton)sender).Checked);
        }

        private void SBREAK_1_CheckedChanged(object sender, EventArgs e)
        {
            //set serial break high
            currentConnection.setSBREAK(((RadioButton)sender).Checked);
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
            focusInput();
        }

        private void panelPortOptions_MouseEnter(object sender, EventArgs e)
        {
            panelPortOptions.Visible = true;
        }

        private void panelPortOptions_MouseLeave(object sender, EventArgs e)
        {
            //panelPortOptions.Visible = false;
        }

        private void panelPortOptions_MouseHover(object sender, EventArgs e)
        {
            panelPortOptions.Visible = true;
        }

        private void textBoxPortReadTimeout_TextChanged(object sender, EventArgs e)
        {
            int.TryParse(textBoxPortReadTimeout.Text, out portReadTimeout);
            if (currentConnection != null)
            {
                try
                {
                    currentConnection.changeReadTimeout(portReadTimeout);
                }
                catch (Exception)
                {
                }
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
            catch (Exception)
            {
            }
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
        private void resizeButtons(Boolean loading)
        {
            button1.Width = panelHistory.ClientSize.Width - WINDOWMARGINS1;
            foreach (Control ctrl in panelHistory.Controls)
            {
                //
                if (loading)
                {
                    if (ctrl is SmartButton) ctrl.Width = button1.Width;
                }
                else
                {
                    if (ctrl is SmartButton) ctrl.Width = panelHistory.ClientSize.Width - WINDOWMARGINS1;
                }
            }
        }
        private void setNoConnection()
        {
            toolStripStatusLabel1.Text = "No Connection";
            this.Text = "No Connection";
        }
        public void SetProgress(int progress)
        {
            try
            {
                toolStripProgressBar1.Visible = true;
                toolStripProgressBar1.Value = progress;
            }
            catch (Exception)
            {
            }
        }
        public void SetProgress(Boolean visible, Boolean busy)
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
            catch
            {
            }
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
                    testport.createBasicSerialPort(currentPortName, currentBaudRate, ASCII);

                    //send the port name as a string and check the response
                    testport.sendData(currentPortName, endline);

                    safeSleep(this.timeoutMS);

                    String response = testport.readData();
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
    }
}