using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.ComponentModel;

namespace Comet1
{
    public class SmartButtonManager
    {
        private Button lastButtonForLocation;
        private int historyWidth = 0;
        private Panel panelHistory;
        private ToolTip toolTip;
        private ContextMenuStrip contextMenuSmartButton;
        private bool showCMD = true;
        private ScriptRunner serialScript;

        public event EventHandler<SmartButton> ButtonCreated;
        public event EventHandler<SmartButton> ButtonClicked;

        public SmartButtonManager(Panel historyPanel, Button referenceButton, ToolTip tooltip, ContextMenuStrip contextMenu)
        {
            panelHistory = historyPanel;
            toolTip = tooltip;
            contextMenuSmartButton = contextMenu;
            historyWidth = referenceButton.Width;
            // lastButtonForLocation initialized lazily on first button creation
            lastButtonForLocation = null;
        }

        private void ResetAnchor()
        {
            // Dummy anchor sits at the panel bottom. First button is placed 31px above it.
            lastButtonForLocation = new Button
            {
                Size = new Size(panelHistory.ClientSize.Width, 29),
                Location = new Point(0, panelHistory.ClientSize.Height)
            };
        }

        public void SetShowCommand(bool show)
        {
            showCMD = show;
        }

        public SmartButton CreateSmartButton(string buttonCommandText, string buttonDescriptionText, 
            bool displayCMD, int buttonStyle, SmartButton.buttonTypes buttonType)
        {
            if (lastButtonForLocation == null)
                ResetAnchor();

            var newbutton = new SmartButton(buttonType, buttonCommandText, buttonDescriptionText, displayCMD);

            SetButtonStyle(newbutton, buttonStyle);
            SetButtonLocation(newbutton);

            if (toolTip != null && LicenseManager.UsageMode != LicenseUsageMode.Designtime)
            {
                if (displayCMD)
                {
                    toolTip.SetToolTip(newbutton, newbutton.CommandDescription);
                }
                else
                {
                    toolTip.SetToolTip(newbutton, newbutton.CommandToSend);
                }
            }

            newbutton.ContextMenuStrip = contextMenuSmartButton;
            panelHistory.Controls.Add(newbutton);

            ButtonCreated?.Invoke(this, newbutton);
            return newbutton;
        }

        public void SetButtonEventHandlers(SmartButton button, EventHandler serialHandler, 
            EventHandler scriptHandler, EventHandler logHandler, 
            EventHandler mouseHoverHandler, KeyPressEventHandler keyPressHandler)
        {
            button.ClearAllEvents();

            if (button.buttonType == SmartButton.buttonTypes.SerialCommand)
            {
                button.Click += serialHandler;
            }
            if (button.buttonType == SmartButton.buttonTypes.ScriptRunner)
            {
                button.Click += scriptHandler;
            }
            if (button.buttonType == SmartButton.buttonTypes.Log)
            {
                button.Click += logHandler;
            }

            button.ContextMenuStrip = contextMenuSmartButton;
            button.MouseHover += mouseHoverHandler;
            button.KeyPress += keyPressHandler;
        }

        public void SetButtonLocation(SmartButton newbutton)
        {
            // Top anchor is required for AutoScroll compatibility – Bottom-anchored
            // controls shift with the scroll position, which cancels out scrolling.
            // RelayoutButtons() called on every resize keeps the "stack from bottom" visual.
            newbutton.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            Point basePoint = lastButtonForLocation.Location;
            basePoint.Offset(new Point(0, -31));  // stack upward
            newbutton.Location = basePoint;
            newbutton.Size = new Size(panelHistory.ClientSize.Width, 29);
            lastButtonForLocation = newbutton;
        }

        // Repositions all SmartButtons so their Y coordinates are always >= 0.
        // Oldest button sits at the bottom, newest at the top of the used area.
        // This keeps AutoScroll able to detect overflow and show a scrollbar.
        public void RelayoutButtons()
        {
            var buttons = panelHistory.Controls.OfType<SmartButton>().ToArray();
            if (buttons.Length == 0)
            {
                lastButtonForLocation = null;
                return;
            }
            int N = buttons.Length;
            int startY = Math.Max(0, panelHistory.ClientSize.Height - N * 31);
            panelHistory.SuspendLayout();
            for (int i = 0; i < N; i++)
            {
                // buttons[0] = oldest (first added) → highest Y (bottom of stack)
                // buttons[N-1] = newest (last added) → startY (top of stack)
                buttons[i].Location = new Point(0, startY + (N - 1 - i) * 31);
            }
            lastButtonForLocation = buttons[N - 1];
            panelHistory.ResumeLayout(false);
            panelHistory.PerformLayout();
        }

        public void SetButtonStyle(Button newbutton, int buttonStyle)
        {
            switch (buttonStyle)
            {
                case 0:
                    newbutton.BackColor = SystemColors.Control;
                    break;
                case 1:
                    newbutton.BackColor = SystemColors.ControlDark;
                    break;
                case 2:
                    newbutton.BackColor = SystemColors.ControlLight;
                    newbutton.Font = new Font(newbutton.Font.Name, newbutton.Font.Size, FontStyle.Bold);
                    break;
                default:
                    newbutton.BackColor = SystemColors.Control;
                    break;
            }
        }

        public void ClearSmartButtons(int reference, bool direction)
        {
            try
            {
                var historyButtons = panelHistory.Controls.OfType<SmartButton>().ToArray();
                foreach (var control in historyButtons)
                {
                    if (direction)
                    {
                        if (control.Location.Y > reference)
                        {
                            control.removeThisButton();
                        }
                    }
                    else
                    {
                        if (control.Location.Y < reference)
                        {
                            control.removeThisButton();
                        }
                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Error Clearing History Buttons");
            }
        }

        public void ClearAllSmartButtons()
        {
            try
            {
                panelHistory.SuspendLayout();

                // Get all SmartButtons before clearing
                var historyButtons = panelHistory.Controls.OfType<SmartButton>().ToArray();

                // Remove only SmartButton controls, not all controls (preserves button1, etc.)
                foreach (var control in historyButtons)
                {
                    panelHistory.Controls.Remove(control);
                }

                // Dispose all SmartButtons
                foreach (var control in historyButtons)
                {
                    control.Dispose();
                }

                // Reset so next batch of buttons starts fresh from bottom
                lastButtonForLocation = null;

                panelHistory.ResumeLayout(true);
            }
            catch (Exception)
            {
                Console.WriteLine("Error Clearing History Buttons");
            }
        }

        public string[] CollectAllSmartButtonData()
        {
            var historyButtonData = new List<string>();

            try
            {
                var historyButtons = panelHistory.Controls.OfType<SmartButton>().ToArray();
                foreach (var control in historyButtons)
                {
                    SmartButton currentB = control;

                    if (currentB.buttonType == SmartButton.buttonTypes.SerialCommand)
                    {
                        string buttonData = currentB.CommandToSend + "\t\t\t\t" + currentB.CommandDescription;
                        historyButtonData.Add(buttonData);
                    }

                    if (currentB.buttonType == SmartButton.buttonTypes.ScriptRunner)
                    {
                        object[] scriptItems = currentB.storedScript.currentScript.ToArray();
                        historyButtonData.Add("**script" + "\t\t\t\t" + currentB.CommandToSend);
                        
                        for (int j = 0; j < scriptItems.Length; j++)
                        {
                            object[] currentItem = (object[])((ArrayList)scriptItems[j]).ToArray();
                            
                            if ((string)currentItem[0] == "SERIAL")
                            {
                                historyButtonData.Add((string)currentItem[1]);
                            }
                            if ((string)currentItem[0] == "FUNCTION")
                            {
                                string commandAndArgs = "**" + (string)currentItem[1];
                                int lengthItems = currentItem.Length;
                                
                                for (int i = 2; i < lengthItems; i++)
                                {
                                    commandAndArgs = commandAndArgs + "\t" + currentItem[i];
                                }
                                historyButtonData.Add(commandAndArgs);
                            }
                        }
                        historyButtonData.Add("**script" + "\t\t\t\t" + currentB.CommandDescription);
                    }
                }
                return historyButtonData.ToArray();
            }
            catch (Exception)
            {
                Console.WriteLine("Error Collecting History Data");
                return new string[] { "Error" };
            }
        }

        public void ChangeHistoryButtonDisplay(bool showCommand)
        {
            try
            {
                var historyButtons = panelHistory.Controls.OfType<SmartButton>().ToArray();
                foreach (var control in historyButtons)
                {
                    SmartButton currentB = control;
                if (showCommand)
                {
                    currentB.Text = currentB.CommandToSend;
                    if (toolTip != null && LicenseManager.UsageMode != LicenseUsageMode.Designtime)
                        toolTip.SetToolTip(currentB, currentB.CommandDescription);
                }
                else
                {
                    currentB.Text = currentB.CommandDescription;
                    if (toolTip != null && LicenseManager.UsageMode != LicenseUsageMode.Designtime)
                        toolTip.SetToolTip(currentB, currentB.CommandToSend);
                }
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Error Changing Button displays");
            }
        }

        public void LoadSmartButtons(string[] dataIn, Func<bool, bool> loadScriptCallback, 
            Action<ScriptRunner> setSerialScriptCallback, Action<string> addCommandCallback, Button scrollButton)
        {
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
                            buttonStyle++;
                            buttonStyle = buttonStyle % 2;
                        }
                        
                        if (parsed != null && parsed.Length != 0 && parsed[0].Length > 0)
                        {
                            string command = parsed[0];
                            string description = parsed[0];

                            if (command.Trim().StartsWith("**"))
                            {
                                if (command.ToLower().Contains("**script"))
                                {
                                    scriptbutton = !scriptbutton;
                                    
                                    if (scriptbutton)
                                    {
                                        if ((parsed.Length > 1) && parsed[1].Length > 0)
                                        {
                                            description = parsed[1];
                                        }
                                        tempButton = CreateSmartButton(description, description, showCMD, 2, 
                                            SmartButton.buttonTypes.ScriptRunner);
                                        loadScriptCallback(false);
                                    }
                                    else
                                    {
                                        setSerialScriptCallback(serialScript);
                                        tempButton.addScript(serialScript);
                                    }
                                }
                            }
                            
                            if (!command.ToLower().Contains("**script"))
                            {
                                if (scriptbutton)
                                {
                                    addCommandCallback(recalledData[i]);
                                }
                                else
                                {
                                    if ((parsed.Length > 1) && parsed[1].Length > 0)
                                    {
                                        description = parsed[1];
                                    }
                                    CreateSmartButton(command, description, showCMD, buttonStyle, 
                                        SmartButton.buttonTypes.SerialCommand);
                                }
                            }
                        }
                    }
                }

                    showCMD = false;
                    ChangeHistoryButtonDisplay(showCMD);
                    RelayoutButtons();
                    // Scroll to the bottom so the first (oldest) button is visible
                    panelHistory.VerticalScroll.Value = panelHistory.VerticalScroll.Maximum;
                    panelHistory.PerformLayout();
                }
                catch (Exception)
                {
                    Console.WriteLine("Error opening / parsing saved data");
            }
        }

        public void SetSerialScript(ScriptRunner script)
        {
            serialScript = script;
        }

        public static void WriteToTextFile(string path, string[] data)
        {
            if (!File.Exists(path))
            {
                File.WriteAllLines(path, data, Encoding.UTF8);
            }
        }

        public static string[] OpenSavedSmartButtons()
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Filter = "Text Files (.txt)|*.txt|All Files (*.*)|*.*";
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.Multiselect = false;
            openFileDialog1.ShowHelp = true;
            openFileDialog1.AutoUpgradeEnabled = true;

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

        public static string OpenSingleFile()
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Multiselect = false;
            openFileDialog1.ShowHelp = true;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                return openFileDialog1.FileName;
            }
            else
            {
                return null;
            }
        }
    }
}
