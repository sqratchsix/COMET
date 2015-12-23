using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO.Ports;
using System.IO;
using System.Text.RegularExpressions;



namespace Comet1
{
    public partial class SerialWindow : Form
    {
        ActiveSerialPort currentConnection;

        String currentPortName = "";
        int currentBaudRate = 9600;
        Parity currentParity = (Parity)0;
        int currentDataBits = 8;
        //most hardware does not support StopBits 'None' or 'OnePointFive"; will cause an exception
        StopBits currentStopBits = (StopBits)1;
        int timeoutMS = 2000;
        bool portOpen = false;
        bool textReverse = false;
        bool rememberLastCommand = true;
        bool showCMD = true;
        bool keepText = false;
        bool writeSmartButton = true;
        bool writeSmartButtonEnabled = true;
        bool DiscardDuplicateEntriesInList = true;
        String dataToSend = "";
        Boolean ASCII = true;
        int historyWidth = 0;
        int WINDOWMARGINS1 = 30;//used to set the buttons size small enough that a horizontal scroll won't appear

        System.Collections.ArrayList lastCommandList = new System.Collections.ArrayList(); //list of last typed commands
        int lastCommandIndex = 0;


        //Data for history Buttons
        Button lastButtonForLocation;

        Regex r = new Regex("[\\S][\\S]");
        MatchEvaluator evaluator = new MatchEvaluator(AddSpaceAfter2);

        #region Basic setup & methods

        //Constructor
        public SerialWindow()
        {
            InitializeComponent();
            updateSerialPortList();
            
            initializeHistoryButtons();
            selectDefaults();
            registerEvents();
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

            if (ASCII)
            {
                radioButtonASCII.Select();
            }

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
            newInstance.Show();
            //try to open a new port
            newInstance.openPortActionFirstTime();
        }
        private void registerEvents()
        {
            this.Closing += new CancelEventHandler(CleanClose); 
        }
        #endregion

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
                if (currentConnection.openSerialPort(currentPortName, currentBaudRate, currentParity, currentDataBits, currentStopBits, timeoutMS, ASCII))
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
                //set the connection on the status bar
                toolStripStatusLabel1.Text = currentConnection.getConnectionInfo(1);
                this.Text = "COMET - " + currentConnection.getConnectionInfo(0);
                focusInput();
                return true;
            }
            else
            {
                toolStripStatusLabel1.Text = "No Connection";
                this.Text = "No Connection";
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
            String dataSent = currentConnection.sendData(dataToSend);
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
            String dataSent = currentConnection.sendData(data);
            updateTerminal(dataSent, false);
        }
        private void serialPortDataReadyToRead(object sender, EventArgs e)
        {
            updateTerminal(currentConnection.readData(), true);
        }
        private void CleanClose(object sender, EventArgs e)
        {
            try
            {
                this.currentConnection.closeSerialPort();
            }
            catch { }
        }
        private void updateTimeout()
        {
            float timeoutVal = 0;
            if (float.TryParse(textBoxTimeout.Text, out timeoutVal))
            {
                timeoutMS = (int)(timeoutVal * 1000);
            }
            this.currentConnection.setPortTimeout(timeoutMS, timeoutMS);
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
        #endregion
        
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
                //different color for in/out text
                Color textColor = Color.FromName("Green");

                if (!output)
                {
                    textColor = Color.FromName("DarkTurquoise");

                }
                //Add a newline if the last charaacter is not a newline
                if (!(newText[newText.Length - 1].Equals(Environment.NewLine)))
                {
                    newText = newText + Environment.NewLine;
                }

                //add the new text on top of the previous text
                if (textReverse)
                {
                    //Not implemented
                    //terminalText = newText + terminalText;
                    try
                    {
                        textBoxTerminal.Invoke(new MethodInvoker(delegate { RichTextBoxExtensions.AppendText(textBoxTerminal, newText, textColor); }));
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
                        textBoxTerminal.Invoke(new MethodInvoker(delegate { RichTextBoxExtensions.AppendText(textBoxTerminal, newText, textColor); }));
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
        #endregion

        #region History Button Methods
        private void createHistoryButton(String buttonCommandText, String buttonDescriptionText, Boolean displayCMD, int buttonStyle)
        {
            var newbutton = new SmartButton(buttonCommandText, buttonDescriptionText, displayCMD);

            if (displayCMD)
            {
                this.toolTip1.SetToolTip(newbutton, newbutton.CommandDescription);
            }
            else
            {
                this.toolTip1.SetToolTip(newbutton, newbutton.CommandToSend);
            }

            //change the button style
            switch (buttonStyle)
            {
                case 0:
                    newbutton.BackColor = System.Drawing.SystemColors.Control;
                    break;
                case 1:
                    newbutton.BackColor = System.Drawing.SystemColors.ControlDark;
                    break;
                default:
                    newbutton.BackColor = System.Drawing.SystemColors.Control;
                    break;
            }



            newbutton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));

            //find the relative relative location
            Point basePoint = lastButtonForLocation.Location;
            Point offsetPoint = new Point(0, -29);
            basePoint.Offset(offsetPoint);
            newbutton.Location = basePoint;
            newbutton.Size = lastButtonForLocation.Size;
            lastButtonForLocation = newbutton;
            //set the size

            //add the event handlers
            //clicking the button sends its history data to the terminal
            newbutton.Click += new System.EventHandler(this.newbutton_Click);
            newbutton.MouseHover += new System.EventHandler(this.newbutton_MouseHover);
            newbutton.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textBoxTerminal_KeyPress);
            newbutton.ContextMenuStrip = this.contextMenuSmartButton;
            this.panelHistory.Controls.Add(newbutton);
        }
        private void addLastCommandToHistoryButton(String lastCommandSent)
        {
            if (!(String.IsNullOrEmpty(lastCommandSent)) && writeSmartButton && writeSmartButtonEnabled)
            {
                createHistoryButton(lastCommandSent, lastCommandSent, showCMD, 0);
            }
        }
        private void ClearHistoryButtons()
        {
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
        private String[] collectAllHistoryButtonData()
        {
            String ButtonData;
            String[] HistoryButtonData;

            try
            {
                var historyButtons = panelHistory.Controls.OfType<SmartButton>().ToArray();
                HistoryButtonData = new String[historyButtons.Length];
                int i = 0;
                foreach (var control in historyButtons)
                {
                    //format each line in the file
                    //Command {TAB}{TAB}{TAB}{TAB} Description
                    SmartButton CurrentB = (SmartButton)control;
                    ButtonData = CurrentB.CommandToSend + "\t\t\t\t" + CurrentB.CommandDescription;
                    //add the data to the array
                    HistoryButtonData[i++] = ButtonData;
                }
                return HistoryButtonData;
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
                        if (!(parsed == null || parsed.Length == 0))
                        {
                            if (parsed[0].Length > 0)
                            {
                                String command = parsed[0];
                                String description = parsed[0];
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

            // Process input if the user clicked OK.
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string readThisFile = openFileDialog1.FileName;
                return File.ReadAllLines(readThisFile);
            }
            else
            {
                return null;
            }
        }
        #endregion

        # region GUI Handlers
        private void comboBoxPortName_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.currentPortName = (String)comboBoxPortName.SelectedItem;
        }
        private void comboBoxBaudRate_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxBaudRate.SelectedItem != null)
            {
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
            //if the up key is pressed, then load the last text input - mimics terminal action
            switch (e.KeyCode)
            {
                case Keys.Up:
                    if((lastCommandIndex) > 0)
                    {
                        lastCommandIndex--;
                        textBox1.Text = (String)lastCommandList[lastCommandIndex];
                    }
                    focusInput();
                    e.Handled =true;
                    writeSmartButton = false;
                    break;
                case Keys.Down:
                    if ((lastCommandIndex) <= lastCommandList.Count-2)
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
        private void newbutton_Click(object sender, EventArgs e)
        {
            string storedData = ((SmartButton)sender).CommandToSend;
            sendDataToSerialConnectionBasic(storedData);     
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
        private void button2_Click(object sender, EventArgs e)
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

            PromptSmartButtonEdit Edit= new PromptSmartButtonEdit(SButton);
            
         }
        private void clearAllButtonsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ClearHistoryButtons();
        }
        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            loadSmartButtons(openSavedSmartButtons());
        }
        private void saveToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //collect the history button data
            String[] AllHistoryData = collectAllHistoryButtonData();

            SaveFileDialog save = new SaveFileDialog();
            save.FileName = "COMET_History_01.txt";
            save.Filter = "Text File | *.txt";
            if (save.ShowDialog() == DialogResult.OK)
            {
                StreamWriter writer = new StreamWriter(save.OpenFile());
                for (int i = 0; i < AllHistoryData.Length; i++)
                {
                    writer.WriteLine(AllHistoryData[i].ToString());
                }
                writer.Dispose();
                writer.Close();
            }
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
            updateLayout();
            
        }

        public void updateLayout()
        {
            panelHistory.PerformLayout();
            resizeButtons();
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

            Console.Out.WriteLine(r.Replace(textBox1.Text, evaluator));

        }
        private void removeAllButtonsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ClearHistoryButtons();
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
            splitContainer1.SplitterDistance = this.ClientSize.Width - button1.Width - WINDOWMARGINS1;
        }
        private void SerialWindow_ResizeEnd(object sender, EventArgs e)
        {
            correctHistoryFrame();
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
                    resizeButtons();
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
        private void resizeButtons()
        {
            button1.Width = panelHistory.ClientSize.Width - WINDOWMARGINS1;
            foreach (Control ctrl in panelHistory.Controls)
            {
                //if (ctrl is SmartButton) ctrl.Width = panelHistory.ClientSize.Width - WINDOWMARGINS1;
                if (ctrl is SmartButton) ctrl.Width = button1.Width;
            }
            
        }
        #endregion

        //unused - timer starts on load nut is not used ... yet :-)
        private void SerialWindow_Load(object sender, EventArgs e)
        {
            //start a scroll timer
            Timer MyTimer = new Timer();
            MyTimer.Interval = (200);
            MyTimer.Tick += new EventHandler((sender2, e2) => MyTimer_Tick(sender, e));
            MyTimer.Start();
        }
        private void MyTimer_Tick(object sender, EventArgs e)
        {

            //((SmartButton)sender).Text = ((SmartButton)sender).Text.Substring(1, ((SmartButton)sender).Text.Length - 1) + ((SmartButton)sender).Text.Substring(0, 1);
        }
    }
    }
