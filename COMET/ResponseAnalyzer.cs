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
        DialogWin Error;
        string expectedResponse = "";
        string logfilename = "COMETResponseLog.txt";
        string timeNow = "";

        public ResponseAnalyzer(string input)
        {
            //constructor where each instance creates a new dialog window
            expectedResponse = input;
            Error = new DialogWin(false);
        }

        public ResponseAnalyzer(string input, DialogWin Error_in)
        {
            //constructor that uses a single dialg from the parent
            expectedResponse = input;
            Error = Error_in;
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

        public bool getResponseData(String input, out string responseData)
        {
            string expResLC = expectedResponse.ToLower();
            string inputLC = input.ToLower();
            responseData = "";

            string[] responseStrings = inputLC.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
                foreach (string line in responseStrings)
            {
                //response is found OR there is no expected response
                if (line.Contains(expResLC))
                {
                    //if you are looking for as response to parse out
                    if (!expResLC.Equals(""))
                    {
                        responseData = line.Replace(expResLC, "");
                        return true;
                    } 
                    else if((expResLC.Equals("")) && (line.Length > 0))
                    {
                        //if there's actually some data to look at
                        responseData = line;
                        return true;
                    }
                }
            }
            return false;
        }

        public bool checkForResponse(String input, out bool passed)
        {
            passed = false;
            string expResLC = expectedResponse.ToLower();
            string inputLC = input.ToLower();

                if (inputLC.Contains(expResLC))
                {
                    passed = true;
                    return true;
                }
            return false;
        }

        public bool logResponse(String input, String collectedData)
        {
            //get the current time
            timeNow = DateTime.Now.ToString();
            string log = input + "\t" + timeNow + "\t" + collectedData;
            return Utilities.writeStringToFile(logfilename, log);
        }


        public void setLogFilename(string newFilename)
        {
            this.logfilename = newFilename;
        }

        public string getLogFilename()
        {
            return this.logfilename;
        }

        public bool parseOutInt(String input, out int intval)
        {
            string inputLC = input.ToLower();
            string lineOfData = "";
            checkForResponse(inputLC, out lineOfData);

            string intValString = lineOfData.Replace(expectedResponse.ToLower(), "");
            intval = 0;
            return int.TryParse(intValString,out intval);
        }

        public bool checkValBetween(String input, int lowVal, int highVal, out string detail, out bool passed)
        {
            detail = "";
            passed = false;
            //search for the line that has the data
            string expectedLC = expectedResponse.ToLower();
            string inputLC = input.ToLower();
            if (inputLC.Contains(expectedLC))
            {
                int foundvalInt = 0;

                //find the data first and return true if the comparison is made
                if (parseOutInt(inputLC, out foundvalInt))
                {
                    detail = "Measured: " + foundvalInt + " :between: " + lowVal + " & " + highVal;

                    passed = (lowVal < foundvalInt) && (highVal > foundvalInt);
                    return true;
                }
                //this will always be overwritten before returning
                //maybe follow with return false
                detail = "ERROR Parsing Response";
            }
            detail = expectedLC + " :: NOT FOUND ";
            return false;
        }

        public void showFailure(string detailtext)
        {
            Error.setFail(detailtext);
        }

        public void showPass(string detailtext)
        {
            Error.setPass(detailtext);
        }

  
    }
}
