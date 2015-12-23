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
            Application.SetCompatibleTextRenderingDefault(false);
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
            serialWin.Show();
            serialWin.updateLayout();
            //Application.Run(serialWin);
            ProgramWithContext context = new ProgramWithContext();
            Application.Run(context);

       }
    }
}
