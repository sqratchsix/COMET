using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Comet1
{
    class ProgramWithContext : ApplicationContext
    {
        ProgramWithContext()
        {
            // When the program goes Idle, check to see if there are any windows open
            Application.Idle += new EventHandler(this.OnApplicationIsIdle);
        }

        private void OnApplicationIsIdle(object sender, EventArgs e){
            //If no forms are open, then close the program 
            if (Application.OpenForms.Count == 0)
            {
                Application.Exit();
            }
        }
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            //SetCompatibleTextRending dictates if the text in Smartbuttons is word wrapped
            Application.SetCompatibleTextRenderingDefault(false);
            // Ensure runtime has access to build metadata properties
            // These will be generated into Properties\BuildInfo.cs at build time
            // and are safe to read at runtime.
            SerialWindow serialWin = new SerialWindow();
            try
            {
                //try to open the first serial port when the program starts
                serialWin.openPortActionFirstTime();
            }
            catch (Exception)
            {
                //do nothing
            }
            // Show the main window and run it as the application's main form.
            // Using Application.Run(serialWin) ensures the form's layout and message
            // loop are tied directly to the window (fixes certain release-mode
            // rendering/layout differences seen when using a custom ApplicationContext).
            serialWin.Show();
            serialWin.updateLayoutHistoryPanel();
            Application.Run(serialWin);


       }
    }

    public enum enumDataType
    {
        ASCII,
        HEX
    };
}
