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
        //Serial Port Parameters
        string portName = "COM1";
        int baudRate = 115200;
        Parity parity = (Parity)0;
        int dataBits = 8;
        StopBits stopBits = (StopBits)1;
        int portTimeoutWriteMS = 2000;
        int portTimeoutReadMS = 2000;
        public Boolean isOpen = false;
        public SerialPort activeSerial;
        public String PortName { get { return portName; } set { portName = value; } }
        public int BaudRate { get { return baudRate; } set { baudRate = value; } }
        //public int Parity { get { return parity; } set { parity = (Parity)value; } }
        public int DataBits { get { return dataBits; } set { dataBits = value; } }
        //public int StopBits { get { return stopBits; } set { stopBits = (StopBits)value; } }
        public string dataType = "ASCII"; //Can be "ASCII" or "HEX"
        //Data handling
        public event EventHandler DataReadyToRead;
        private System.Timers.Timer aTimer;
        int portReadTimeoutMS = 30;
        string newData = "";
        string currentData = "";
        string lineEnd = System.Environment.NewLine;



        public Boolean createBasicSerialPort(String portName_in, int baudRate_in, Boolean DataTypeASCII)
        {
            this.portName = portName_in;
            this.baudRate = baudRate_in;
            setDataType(DataTypeASCII);
            return createSerialPort();
        }

        private void setDataType(bool DataTypeASCII)
        {
            if (DataTypeASCII)
            {
                this.dataType = "ASCII";
            }
            else
            {
                this.dataType = "HEX";
            }
        }

        public Boolean createSerialPort()
        {
            try
            {
                //most hardware does not support StopBits 'None' or 'OnePointFive"; will cause an exception
                activeSerial = new SerialPort(portName, baudRate, parity, dataBits, stopBits);
                activeSerial.Open();
                //set the port timeouts
                updateReadWriteTimeout();

                isOpen = activeSerial.IsOpen;
                //register the event handler
                activeSerial.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
                //create the timeout timer for reading data
                aTimer = new System.Timers.Timer(portReadTimeoutMS);
                this.aTimer.AutoReset = false;
                // Hook up the Elapsed event for the timer. 
                aTimer.Elapsed += OnTimedEvent;

                return isOpen;
            }
            catch (Exception)
            {
                isOpen = false;
                return isOpen;
            }
        }

        public Boolean openSerialPort(string m_portName, int m_baudRate, Parity m_parity, int m_dataBits, StopBits m_stopBits, int m_timeoutMS, Boolean DataTypeASCII)
        {
            setDataType(DataTypeASCII);
            portName = m_portName;
            baudRate = m_baudRate;
            parity = m_parity;
            dataBits = m_dataBits;
            stopBits = m_stopBits;
            portTimeoutReadMS = m_timeoutMS;
            portTimeoutWriteMS = m_timeoutMS;

            return createSerialPort();
        }

        public int closeSerialPort()
        {
            int returnVal = -1;

            try
            {
                activeSerial.Close();
                isOpen = activeSerial.IsOpen;
                returnVal = 1;
            }
            catch (Exception)
            {
                //C
            }
            return returnVal;
        }

        public void setPortTimeout(int writeTimeout, int readTimeout)
        {
            this.portTimeoutReadMS = readTimeout;
            this.portTimeoutWriteMS = writeTimeout;
            if (isOpen)
            {
                updateReadWriteTimeout();
            }

        }

        private void updateReadWriteTimeout()
        {
            activeSerial.WriteTimeout = portTimeoutWriteMS;
            activeSerial.ReadTimeout = portTimeoutReadMS;
        }
        public String getConnectionInfo(int returnformat)
        {
            //A concise String that has all the parameters of the open port
            //longer version returnformat =1
            String connectionInfo = "";
            if (isOpen)
            {
                if(returnformat == 1)
                {
                    connectionInfo = portName + ":  " + baudRate.ToString() + "," + parity.ToString() + "," + dataBits.ToString() + "," + stopBits.ToString();
                }
                else
                {
                    connectionInfo = portName + "  (" + baudRate.ToString() + ")";
                }
            }
                else
                {
                    connectionInfo = "No Connection";
                }
            return connectionInfo;
        }

        //TODO need to decode the enumeration returned from dwSettableBaud
        public static Int32 getAvailableBaudRates(string m_portName)
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
                Int32 dwSettableBaud = (Int32)p.GetType().GetField("dwSettableBaud",
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
            if(dataType.Equals("ASCII"))
            {
                this.newData = activeSerial.ReadExisting();

            }else{
                int bytes = activeSerial.BytesToRead;
                byte[] buffer = new byte[bytes];
                activeSerial.Read(buffer, 0, bytes);

                this.newData = BitConverter.ToString(buffer);
               
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
                String dataReceived = currentData;
                //clear the buffer
                this.currentData = "";
                if (dataType.Equals("ASCII"))
                {
                    return dataReceived;
                }
                else
                {
                    return addByteSpacing(validateStringIsHex(dataReceived));
                }
   
        }

        public String sendData(String dataToSend)
        {
            //only send actual data
            if (isOpen & dataToSend.Length > 0)
            {
                try
                {
                    if (dataType.Equals("ASCII"))
                    {
                        //ASCII DATA
                        string dataToSendToPort = dataToSend + lineEnd;
                        activeSerial.Write(dataToSendToPort);
                        return dataToSend;
                        //Console.Write("ASCII");
                    }
                    else
                    {
                        //HEX Data
                        //Validate the String is hex

                        //Console.Write("HEX");
                        String formattedByteString = addByteSpacing(validateStringIsHex(dataToSend));
                        byte[] bytesToSend = convertStringHEXToHEXBytes(formattedByteString);
                        int offsetBytes = 0;
                        activeSerial.Write(bytesToSend, offsetBytes, bytesToSend.Length);

                        return formattedByteString;

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
            int bitsToBreak = this.dataBits + 2 + extraBitTime;
            int timeToBreakMS = 1;
            try
            {
                int calcBreakTime = bitsToBreak / this.baudRate;
                if (calcBreakTime > 0)
                    timeToBreakMS = calcBreakTime;
            }
            catch (Exception)
            {
                Console.Write("error calculating break time");
            }

            this.activeSerial.BreakState = true;
            System.Threading.Thread.Sleep(timeToBreakMS);
            this.activeSerial.BreakState = false;

        }

        public void changeReadTimeout(int newReadTimeout)
        {
            this.portReadTimeoutMS = newReadTimeout;
            //re-create the timeout timer for reading data
            this.aTimer = new System.Timers.Timer(portReadTimeoutMS);
            this.aTimer.AutoReset = false;
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
            EventHandler handler = DataReadyToRead;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        private byte[] convertStringHEXToHEXBytes(String hexValuesString)
        {
            //string must be delimited by " " {space}
            //"AA 11 22 33 44 55 66 77 88 99 AA BB CC DD EE FF"
            return hexValuesString.Split().Select(s => Convert.ToByte(s, 16)).ToArray();
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

                if (!(outputHexString.Length % 2 == 0))
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
        private string addByteSpacing(String unspacedByteString)
        {
            int msgsize = unspacedByteString.Length;
            if (msgsize == 0)
            {
                return "";
            }
                
            //makes sure it's even
            if (!(msgsize % 2 == 0))
            {
                unspacedByteString = unspacedByteString.PadLeft(unspacedByteString.Length + 1, '0');
            }

            StringBuilder addingSpacing = new StringBuilder();
            Boolean complete = false;
            int indexChar = 0;
            while (!complete)
            {
                addingSpacing.Append(unspacedByteString[indexChar]);
                addingSpacing.Append(unspacedByteString[indexChar+1]);
                indexChar = indexChar + 2;
                if (indexChar >= (msgsize))
                {
                    complete = true;
                }
                else
                {
                    addingSpacing.Append(" ");
                }
            }

            return addingSpacing.ToString().ToUpper();
        }
    }
}
