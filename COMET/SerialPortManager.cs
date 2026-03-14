using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;

namespace Comet1
{
    public class SerialPortManager
    {
        private ActiveSerialPort currentConnection;
        private string currentPortName = "";
        private int currentBaudRate = 9600;
        private Parity currentParity = (Parity)0;
        private int currentDataBits = 8;
        private StopBits currentStopBits = (StopBits)1;
        private int timeoutMS = 500;
        private int portReadTimeout = 20;
        private enumDataType dataType = enumDataType.ASCII;
        private bool portOpen = false;

        public event EventHandler<string> DataReceived;
        public event EventHandler<string> DataSent;

        public bool IsPortOpen => portOpen;
        public string CurrentPortName => currentPortName;
        public int CurrentBaudRate => currentBaudRate;
        internal ActiveSerialPort Connection => currentConnection;
        public enumDataType DataType
        {
            get => dataType;
            set
            {
                dataType = value;
                if (currentConnection != null)
                {
                    currentConnection.DataType = value;
                }
            }
        }

        public SerialPortManager()
        {
        }

        private Dictionary<string, string> portDescriptions = new Dictionary<string, string>();

        public string[] GetAvailablePortNames()
        {
            string[] currentSerialPortList = SerialPort.GetPortNames();
            portDescriptions.Clear();
            
            try
            {
                // Use Win32_PnPEntity to get all devices including virtual/disconnected COM ports
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%'"))
                {
                    var ports = searcher.Get().Cast<ManagementBaseObject>().ToList();
                    foreach (var port in ports)
                    {
                        string caption = port["Caption"]?.ToString();
                        if (!string.IsNullOrEmpty(caption))
                        {
                            // Extract COM port number from caption like "USB Serial Port (COM3)"
                            int startIndex = caption.IndexOf("(COM");
                            int endIndex = caption.IndexOf(")", startIndex);
                            if (startIndex >= 0 && endIndex > startIndex)
                            {
                                string portName = caption.Substring(startIndex + 1, endIndex - startIndex - 1);
                                portDescriptions[portName] = caption;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            
            Array.Sort(currentSerialPortList, new NaturalComparer());
            return currentSerialPortList;
        }

        public string GetPortDescription(string portName)
        {
            if (!string.IsNullOrEmpty(portName) && portDescriptions.ContainsKey(portName))
            {
                return portDescriptions[portName];
            }
            // Return port name if no description found
            return string.IsNullOrEmpty(portName) ? "Unknown Port" : portName;
        }

        public void SetPortConfiguration(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            currentPortName = portName;
            currentBaudRate = baudRate;
            currentParity = parity;
            currentDataBits = dataBits;
            currentStopBits = stopBits;
        }

        public void SetTimeouts(int timeoutMilliseconds, int readTimeout)
        {
            timeoutMS = timeoutMilliseconds;
            portReadTimeout = readTimeout;
            
            if (currentConnection != null)
            {
                currentConnection.setPortTimeout(timeoutMS, timeoutMS);
            }
        }

        public bool OpenPortBasic()
        {
            try
            {
                currentConnection = new ActiveSerialPort();
                if (currentConnection.createBasicSerialPort(currentPortName, currentBaudRate, dataType))
                {
                    currentConnection.DataReadyToRead += SerialPortDataReadyToRead;
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

        public bool OpenPort()
        {
            try
            {
                currentConnection = new ActiveSerialPort();
                if (currentConnection.openSerialPort(currentPortName, currentBaudRate, currentParity, 
                    currentDataBits, currentStopBits, timeoutMS, dataType, portReadTimeout))
                {
                    currentConnection.DataReadyToRead += SerialPortDataReadyToRead;
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

        public bool ClosePort()
        {
            try
            {
                if (currentConnection != null)
                {
                    currentConnection.closeSerialPort();
                }
                portOpen = false;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public string SendData(string data, bool endline)
        {
            if (currentConnection != null)
            {
                string dataSent = currentConnection.sendData(data, endline);
                DataSent?.Invoke(this, dataSent);
                return dataSent;
            }
            return string.Empty;
        }

        public string GetConnectionInfo(int infoType)
        {
            if (currentConnection != null)
            {
                return currentConnection.getConnectionInfo(infoType);
            }
            return string.Empty;
        }

        public void SetRTSDTR(bool rtsEnabled, bool dtrEnabled)
        {
            if (currentConnection != null && currentConnection.ActiveSerial != null)
            {
                try
                {
                    currentConnection.ActiveSerial.RtsEnable = rtsEnabled;
                    currentConnection.ActiveSerial.DtrEnable = dtrEnabled;
                }
                catch (Exception)
                {
                }
            }
        }

        internal Transfer InitializeTransfer()
        {
            if (currentConnection != null)
            {
                return new Transfer(currentConnection);
            }
            return null;
        }

        private void SerialPortDataReadyToRead(object sender, EventArgs e)
        {
            if (currentConnection != null)
            {
                string data = currentConnection.readData();
                DataReceived?.Invoke(this, data);
            }
        }
    }
}
