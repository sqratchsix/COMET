using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Comet1
{
    class ActiveSerialPort
    {
        public string PortName { get; set; } = "COM1";
        public int BaudRate { get; set; } = 115200;



        Parity parity = (Parity)0;
        StopBits stopBits = (StopBits)1;
        int portTimeoutWriteMS = 2000;
        int portTimeoutReadMS = 2000;
        public bool IsOpen { get; set; } = false;
        public SerialPort ActiveSerial { get; set; }
        public int DataBits { get; set; } = 8;
        //public int Parity { get { return parity; } set { parity = (Parity)value; } }

        //public int StopBits { get { return stopBits; } set { stopBits = (StopBits)value; } }
        public enumDataType DataType { get; set; } = enumDataType.ASCII; //Can be "ASCII" or "HEX"
        //Data handling
        public event EventHandler DataReadyToRead;
        private System.Timers.Timer aTimer;
        //the timeout to wait until the read data is written to the terminal
        //must be less than the senders period between messages, or the terminal will never update
        int portReadTimeoutMS = 500;
        string newData = "";
        string currentData = "";
        string lineEnd = System.Environment.NewLine;
        public bool addByteSpaces = true;

        public bool createBasicSerialPort(string portName_in, int baudRate_in, enumDataType dataType)
        {
            this.PortName = portName_in;
            this.BaudRate = baudRate_in;
            this.DataType = dataType;
            return createSerialPort();
        }

        public bool createSerialPort()
        {
            try
            {
                //most hardware does not support StopBits 'None' or 'OnePointFive"; will cause an exception
                ActiveSerial = new SerialPort(PortName, BaudRate, parity, DataBits, stopBits);
                ActiveSerial.Open();
                //set the port timeouts
                updateReadWriteTimeout();

                IsOpen = ActiveSerial.IsOpen;
                //register the event handler
                ActiveSerial.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
                //create the timeout timer for reading data
                aTimer = new System.Timers.Timer(portReadTimeoutMS);
                this.aTimer.AutoReset = false;
                // Hook up the Elapsed event for the timer. 
                aTimer.Elapsed += OnTimedEvent;

                return IsOpen;
            }
            catch (Exception)
            {
                this.IsOpen = false;
                return this.IsOpen;
            }
        }

        public bool openSerialPort(string m_portName, int m_baudRate, Parity m_parity, int m_dataBits, StopBits m_stopBits, int m_timeoutMS, enumDataType dataType, int portReadTimeout)
        {
            this.DataType = dataType;
            this.PortName = m_portName;
            this.BaudRate = m_baudRate;
            this.parity = m_parity;
            this.DataBits = m_dataBits;
            this.stopBits = m_stopBits;
            this.portTimeoutReadMS = m_timeoutMS;
            this.portTimeoutWriteMS = m_timeoutMS;
            this.portReadTimeoutMS = portReadTimeout;

            return createSerialPort();
        }

        public int closeSerialPort()
        {
            try
            {
                ActiveSerial.Close();
                IsOpen = ActiveSerial.IsOpen;
                return 1;
            }
            catch (Exception)
            {
                //C
            }
            return -1;
        }

        public void setPortTimeout(int writeTimeout, int readTimeout)
        {
            this.portTimeoutReadMS = readTimeout;
            this.portTimeoutWriteMS = writeTimeout;
            if (IsOpen)
            {
                updateReadWriteTimeout();
            }
        }

        private void updateReadWriteTimeout()
        {
            ActiveSerial.WriteTimeout = portTimeoutWriteMS;
            ActiveSerial.ReadTimeout = portTimeoutReadMS;
        }
        public int getReadTimeout()
        {
            return portTimeoutReadMS;
        }

        public int getWriteTimeout()
        {
            return portTimeoutWriteMS;
        }

        public string getConnectionInfo(int returnformat)
        {
            //A concise String that has all the parameters of the open port
            //longer version returnformat =1
            if (IsOpen)
            {
                if(returnformat == 1)
                {
                    return PortName + ":  " + BaudRate.ToString() + "," + parity.ToString() + "," + DataBits.ToString() + "," + stopBits.ToString();
                }
                else
                {
                    return PortName + "  (" + BaudRate.ToString() + ")";
                }
            }
            else
            {
                return "No Connection";
            }
        }

        //TODO need to decode the enumeration returned from dwSettableBaud
        public static int getAvailableBaudRates(string m_portName)
        {
            //make a new serial port just to check the available baudrates
            SerialPort tempSerialPort = null;
            try
            {
                tempSerialPort = new SerialPort(m_portName);
                tempSerialPort.Open();
                //Getting COMMPROP structure, and its property dwSettableBaud.
                object p = tempSerialPort.BaseStream.GetType().GetField("commProp",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tempSerialPort.BaseStream);
                int dwSettableBaud = (int)p.GetType().GetField("dwSettableBaud",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(p);

                tempSerialPort.Close();
                return dwSettableBaud;
            }
            catch (Exception)
            {
                //ignore any errors opening the serial port in this simple way                
            }
            return 0;

        }
        private void DataReceivedHandler(
            object sender,
            SerialDataReceivedEventArgs e)
        {
            //restart the timer each time that new data is received
            aTimer.Stop();
            aTimer.Start();
            SerialPort sp = (SerialPort)sender;
            //read the data
            switch(DataType)
            {
                case enumDataType.ASCII:
                    this.newData = ActiveSerial.ReadExisting();
                    break;
                case enumDataType.HEX:
                    int bytes = ActiveSerial.BytesToRead;
                    byte[] buffer = new byte[bytes];
                    ActiveSerial.Read(buffer, 0, bytes);
                    this.newData = BitConverter.ToString(buffer);
                    break;
                default:
                    throw new System.ComponentModel.InvalidEnumArgumentException("DataType");
            }
            continueDataRead();
        }

        private void continueDataRead()
        {
            //add the new data to the previously read data
            this.currentData += this.newData;
        }

        public string readData()
        {
            //prepare the data for output
            string dataReceived = currentData;
            //clear the buffer
            this.currentData = "";
            switch(DataType)
            {
                case enumDataType.ASCII:
                    return dataReceived;
                case enumDataType.HEX:
                    return validateStringIsHex(dataReceived);
                default:
                    throw new System.ComponentModel.InvalidEnumArgumentException("DataType");
            }
        }

        public int ReadByte()
        {
            return ActiveSerial.ReadByte();
        }

        public string sendData(string dataToSend, bool endline)
        {
            //only send actual data
            if (IsOpen & dataToSend.Length > 0)
            {
                try
                {
                    switch(DataType)
                    {
                        case enumDataType.ASCII:
                            //ASCII data
                            string dataToSendToPort;

                            if (endline)
                            {
                                dataToSendToPort = dataToSend + lineEnd;
                            }
                            else
                            {
                                dataToSendToPort = dataToSend;
                            }
                            ActiveSerial.Write(dataToSendToPort);
                            return dataToSend;
                            //Console.Write("ASCII");
                        case enumDataType.HEX:
                            //HEX Data
                            //Validate the String is hex

                            //Console.Write("HEX");
                            string formattedByteString = validateStringIsHex(dataToSend);
                            byte[] bytesToSend = convertStringHEXToHEXBytes(formattedByteString);
                            int offsetBytes = 0;
                            ActiveSerial.Write(bytesToSend, offsetBytes, bytesToSend.Length);
                            return formattedByteString;
                        default:
                            throw new System.ComponentModel.InvalidEnumArgumentException("DataType");
                    }
                }
                catch (Exception)
                {
                    Console.Write("Serial Write Exception");
                    return "ERROR";
                }
            }
            return "";
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            //passthru method to write byte arrays directly yo serial port
            ActiveSerial.Write(buffer, offset, count);
        }

        public void serialBreak()
        {
            sendBreak();
        }

        private void sendBreak()
        {
            /* send the break signal based on the bits and the data rate
            * makes sure that that the TX line is held low for longer
            * than one frame time, including start and stop bits but less than 2 frames
             * Sometimes this has to be sent twice to actually break ?
             */
            int extraBitTime = 2;
            int bitsToBreak = this.DataBits + 2 + extraBitTime;
            int timeToBreakMS = 1;
            try
            {
                
                int calcBreakTime = bitsToBreak / this.BaudRate;
                if (calcBreakTime > 0)
                    timeToBreakMS = calcBreakTime;
            }
            catch (Exception)
            {
                Console.Write("error calculating break time");
            }

            SerialBreakToggle(timeToBreakMS);
            Console.Write("Serial Break: " + timeToBreakMS);

        }

        public void SerialBreakToggle(int timeToBreakMS)
        {
            this.ActiveSerial.BreakState = true;
            System.Threading.Thread.Sleep(timeToBreakMS);
            this.ActiveSerial.BreakState = false;
        }

        public void setSBREAK(bool breakState)
        {
            try
            {
                ActiveSerial.BreakState = breakState;
            }
            catch { }
        }

        public void changeReadTimeout(int newReadTimeout)
        {
            this.portReadTimeoutMS = newReadTimeout;
            //re-create the timeout timer for reading data
            aTimer.Interval = this.portReadTimeoutMS;
        }

        private void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            //Raise an event to let the GUI know that all the data has been received and that it should be read
            OnThresholdReached(EventArgs.Empty);
            //debug
            //Console.WriteLine("The Elapsed event was raised at {0}", e.SignalTime);
        }

        protected virtual void OnThresholdReached(EventArgs e)
        {
            if (DataReadyToRead != null)
            {
                DataReadyToRead(this, e);
            }
        }

        private byte[] convertStringHEXToHEXBytes(string hexValuesString)
        {
            //string must be delimited by " " {space}
            //"AA 11 22 33 44 55 66 77 88 99 AA BB CC DD EE FF"
            //return hexValuesString.Split().Select(s => Convert.ToByte(s, 16)).ToArray();
            //Not Necessary as the string to be sent doesn't contain the spaces anymore
            return Enumerable.Range(0, hexValuesString.Length)
                 .Where(x => x % 2 == 0)
                 .Select(x => Convert.ToByte(hexValuesString.Substring(x, 2), 16))
                 .ToArray();
        }

        private string validateStringIsHex(string inputHexString)
        {
            //Just remove white space
            //String outputHexString = inputHexString.Replace(" ","");
     
          
                //Regex explained:
            //  @
            //  "  "        string
            //  (  )        regex expression is inside
            //
            //  \s          whitespace
            //  |           logical OR
            //  -           hyphen


                String outputHexString = Regex.Replace(inputHexString, @"(\s|-)", "");

                if (outputHexString.Length % 2 != 0)
                {
                    outputHexString = outputHexString.PadLeft(outputHexString.Length + 1, '0');
                }
                //Check if all characters are hex 
                if (Regex.IsMatch(outputHexString, @"\A\b[0-9a-fA-F]+\b\Z"))
                {
                    return outputHexString;
                }
                else
                {
                    //hopefully it doesn't ever get here
                    return "DEADC0DE";
                }
            
        
        }

        public bool getDTR()
        {
            bool val = false;
            try
            {
                val = ActiveSerial.DtrEnable;
            }
            catch { }
            return val;
        }
        public void setDTR(bool setVal)
        {
            try
            {
                ActiveSerial.DtrEnable = setVal;
            }
            catch { }
        }
        public bool getRTS()
        {
            try
            {
                return ActiveSerial.RtsEnable;
            }
            catch { }
            return false;
        }
        public void setRTS(bool setVal)
        {
            try
            {
                ActiveSerial.RtsEnable = setVal;
            }
            catch { }
        }
    }
}
