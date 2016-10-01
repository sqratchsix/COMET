using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;


namespace Comet1
{
    class ResponseAnalyzer
    {
        string expectedResponse = "";

        public ResponseAnalyzer(string input)
        {
            //constructor
            expectedResponse = input;
        }
        
        public void loadNewData(string input)
        {
            expectedResponse = input;
        }

        public bool checkForResponse(String input, out string lineOfData)
        {
            string expResLC = expectedResponse.ToLower();
            string inputLC = input.ToLower();
            lineOfData = "";

            string[] responseStrings = inputLC.Split(new string[]{Environment.NewLine}, StringSplitOptions.None);
            foreach (string line in responseStrings)
            {
                if (line.Contains(expResLC))
                {
                    lineOfData = line;
                    return true;
                }
            }
            return false;
        }

        public bool checkForResponse(String input)
        {
            string expResLC = expectedResponse.ToLower();
            string inputLC = input.ToLower();

                if (inputLC.Contains(expResLC))
                {
                    return true;
                }
            return false;
        }

        public bool parseOutInt(String input, out int intval)
        {
            string inputLC = input.ToLower();
            string lineOfData = "";
            checkForResponse(inputLC,out lineOfData);

            string intValString = lineOfData.Replace(expectedResponse.ToLower(), "");
            intval = 0;
            return(int.TryParse(intValString,out intval));
        }

        public bool checkValBetween(String input, int lowVal, int highVal)
        {
            //search for the line that has the data
            string dataLC = expectedResponse.ToLower();
            string inputLC = input.ToLower();
            if (inputLC.Contains(dataLC))
            {
                int foundvalInt = 0;

                //find the data first
                if (parseOutInt(inputLC, out foundvalInt))
                {
                    if ((lowVal < foundvalInt) && (highVal > foundvalInt))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void showFailure()
        {
            DialogWin Error = new DialogWin();
            Error.setFail();
        }

        public void showPass()
        {
            DialogWin Error = new DialogWin();
            Error.setPass();
        }

  
    }
}
