//ADAPTED FROM: https://sites.google.com/site/adamficsor1024/home/programming/xymodem   

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Ports;

namespace Comet1
{
    public enum InitialCrcValue { Zeros, NonZero1 = 0xffff, NonZero2 = 0x1D0F }
    
    class Transfer
    {
        SerialPort serialPort;

        public event Action<int> ProgressChanged;
        
        //constructor
        public Transfer(ActiveSerialPort currentConnection){
            this.serialPort = currentConnection.activeSerial;
            Console.WriteLine(serialPort.ReadTimeout);
            Console.WriteLine(serialPort.WriteTimeout);
            serialPort.ReadTimeout = 10000;
            Console.WriteLine(serialPort.ReadTimeout);
        }

        private void OnProgressChanged(int progress)
        {
            var eh = ProgressChanged;
            if (eh != null)
            {
                eh(progress);
            }
        }

        #region YMODEM
        /*
         * Upload file via Ymodem protocol to the device
         * ret: is the transfer succeeded? true is if yes
         */
        public bool YmodemUploadFile(string path)
        {
            /* control signals */
            const byte STX = 2;  // Start of TeXt 
            const byte EOT = 4;  // End Of Transmission
            const byte ACK = 6;  // Positive ACknowledgement
            const byte C = 67;   // capital letter C

            /* sizes */
            const int dataSize = 1024;
            const int crcSize = 2;

            /* THE PACKET: 1029 bytes */
            /* header: 3 bytes */
            // STX
            int packetNumber = 0;
            int invertedPacketNumber = 255;
            /* data: 1024 bytes */
            byte[] data = new byte[dataSize];
            /* footer: 2 bytes */
            byte[] CRC = new byte[crcSize];

            /* get the file */
            FileStream fileStream = new FileStream(@path, FileMode.Open, FileAccess.Read);
            long totalFileSize = fileStream.Length;
            double currentdata = 0;
            Console.WriteLine(totalFileSize);
            OnProgressChanged((int)((currentdata / totalFileSize) * 100));
            try
            {
                /* send the initial packet with filename and filesize */
                if (serialPort.ReadByte() != C)
                {
                    Console.WriteLine("Can't begin the transfer.");
                    return false;
                }

                sendYmodemInitialPacket(STX, packetNumber, invertedPacketNumber, data, dataSize, path, fileStream, CRC, crcSize);
                if (serialPort.ReadByte() != ACK)
                {
                    Console.WriteLine("Can't send the initial packet.");
                    return false;
                }

                if (serialPort.ReadByte() != C)
                    return false;

                /* send packets with a cycle until we send the last byte */
                int fileReadCount;
                do
                {
                    /* if this is the last packet fill the remaining bytes with 0 */
                    fileReadCount = fileStream.Read(data, 0, dataSize);
                    if (fileReadCount == 0) break;
                    if (fileReadCount != dataSize)
                        for (int i = fileReadCount; i < dataSize; i++)
                            data[i] = 0;

                    /* calculate packetNumber */
                    packetNumber++;
                    if (packetNumber > 255)
                        packetNumber -= 256;
                    Console.WriteLine(packetNumber);

                    /* calculate invertedPacketNumber */
                    invertedPacketNumber = 255 - packetNumber;

                    /* calculate CRC */
                    Crc16Ccitt crc16Ccitt = new Crc16Ccitt(InitialCrcValue.Zeros);
                    CRC = crc16Ccitt.ComputeChecksumBytes(data);

                    /* send the packet */
                    sendYmodemPacket(STX, packetNumber, invertedPacketNumber, data, dataSize, CRC, crcSize);
                    /* wait for ACK */
                    //is this necessary - no ACK>?!?!?!?!
                    /*
                    if (serialPort.ReadByte() != ACK)
                    {
                        Console.WriteLine("Couldn't send a packet.");
                        return false;
                    }
                     * */
                    
                    //update how much data was sent
                    currentdata = currentdata + dataSize;

                    OnProgressChanged((int)((currentdata / totalFileSize) * 100));
                    Console.WriteLine("currentdata");
                    Console.WriteLine(currentdata);
                    Console.WriteLine("totalFilesize");
                    Console.WriteLine(totalFileSize);
                    Console.WriteLine("percent");
                    Console.WriteLine((currentdata / totalFileSize));
                } while (dataSize == fileReadCount);

                /* send EOT (tell the downloader we are finished) */
                serialPort.Write(new byte[] { EOT }, 0, 1);
                /* send closing packet */
                packetNumber = 0;
                invertedPacketNumber = 255;
                data = new byte[dataSize];
                CRC = new byte[crcSize];
                sendYmodemClosingPacket(STX, packetNumber, invertedPacketNumber, data, dataSize, CRC, crcSize);
                /* get ACK (downloader acknowledge the EOT) */
                if (serialPort.ReadByte() != ACK)
                {
                    Console.WriteLine("Can't complete the transfer.");
                    return false;
                }
            }
            catch (TimeoutException)
            {
                Console.WriteLine("No Answer - Aborting Transfer");
            }
            finally
            {
                fileStream.Close();
            }

            Console.WriteLine("End transfer Session");
            
            return true;
        }

        private void sendYmodemInitialPacket(byte STX, int packetNumber, int invertedPacketNumber, byte[] data, int dataSize, string path, FileStream fileStream, byte[] CRC, int crcSize)
        {
            string fileName = System.IO.Path.GetFileName(path);
            string fileSize = fileStream.Length.ToString();

            /* add filename to data */
            int i;
            for (i = 0; i < fileName.Length && (fileName.ToCharArray()[i] != 0); i++)
            {
                data[i] = (byte)fileName.ToCharArray()[i];
            }
            data[i] = 0;

            /* add filesize to data */
            int j;
            for (j = 0; j < fileSize.Length && (fileSize.ToCharArray()[j] != 0); j++)
            {
                data[(i + 1) + j] = (byte)fileSize.ToCharArray()[j];
            }
            data[(i + 1) + j] = 0;

            /* fill the remaining data bytes with 0 */
            for (int k = ((i + 1) + j) + 1; k < dataSize; k++)
            {
                data[k] = 0;
            }

            /* calculate CRC */
            Crc16Ccitt crc16Ccitt = new Crc16Ccitt(InitialCrcValue.Zeros);
            CRC = crc16Ccitt.ComputeChecksumBytes(data);

            /* send the packet */
            sendYmodemPacket(STX, packetNumber, invertedPacketNumber, data, dataSize, CRC, crcSize);
        }

        private void sendYmodemClosingPacket(byte STX, int packetNumber, int invertedPacketNumber, byte[] data, int dataSize, byte[] CRC, int crcSize)
        {
            /* calculate CRC */
            Crc16Ccitt crc16Ccitt = new Crc16Ccitt(InitialCrcValue.Zeros);
            CRC = crc16Ccitt.ComputeChecksumBytes(data);

            /* send the packet */
            sendYmodemPacket(STX, packetNumber, invertedPacketNumber, data, dataSize, CRC, crcSize);
        }

        private void sendYmodemPacket(byte STX, int packetNumber, int invertedPacketNumber, byte[] data, int dataSize, byte[] CRC, int crcSize)
        {
            serialPort.Write(new byte[] { STX }, 0, 1);
            serialPort.Write(new byte[] { (byte)packetNumber }, 0, 1);
            serialPort.Write(new byte[] { (byte)invertedPacketNumber }, 0, 1);
            serialPort.Write(data, 0, dataSize);
            serialPort.Write(CRC, 0, crcSize);
        }
        #endregion

        #region XMODEM

        public bool XmodemUploadFile(string path)
        {
            /* control signals */
            const byte SOH = 1;  // Start of Header
            const byte EOT = 4;  // End of Transmission
            const byte ACK = 6;  // Positive Acknowledgement
            const byte NAK = 21; // Negative Acknowledgement

            /* sizes */
            const byte dataSize = 128;

            /* THE PACKET: 132 bytes */
            /* header: 3 bytes */
            // SOH
            int packetNumber = 0;
            int invertedPacketNumber = 255;
            /* data: 128 bytes */
            byte[] data = new byte[dataSize];
            /* footer: 1 byte */
            int checkSum = 0;

            /* get the file */
            FileStream fileStream = new FileStream(@path, FileMode.Open, FileAccess.Read);

            try
            {
                /* get NAK */
                if (serialPort.ReadByte() != NAK)
                {
                    Console.WriteLine("Can't start the transfer");
                    return false;
                }

                /* send packets with a cycle until we send the last byte */
                int fileReadCount;
                do
                {
                    /* if this is the last packet fill the remaining bytes with 0 */
                    fileReadCount = fileStream.Read(data, 0, dataSize);
                    if (fileReadCount == 0) break;
                    if (fileReadCount != dataSize)
                        for (int i = fileReadCount; i < dataSize; i++)
                            data[i] = 0;

                    /* calculate packetNumber */
                    packetNumber++;
                    if (packetNumber > 255)
                        packetNumber -= 256;
                    Console.WriteLine(packetNumber);

                    /* calculate invertedPacketNumber */
                    invertedPacketNumber = 255 - packetNumber;

                    /* calculate checkSum */
                    checkSum = 1;
                    checkSum += packetNumber;
                    checkSum += invertedPacketNumber;
                    for (int i = 0; i < dataSize; i++)
                        checkSum += data[i];

                    /* send the packet */
                    serialPort.Write(new byte[] { SOH }, 0, 1);
                    serialPort.Write(new byte[] { (byte)packetNumber }, 0, 1);
                    serialPort.Write(new byte[] { (byte)invertedPacketNumber }, 0, 1);
                    serialPort.Write(data, 0, dataSize);
                    serialPort.Write(new byte[] { (byte)checkSum }, 0, 1);

                    /* wait for ACK */
                    if (serialPort.ReadByte() != ACK)
                    {
                        Console.WriteLine("Couldn't send a packet.");
                        return false;
                    }
                } while (dataSize == fileReadCount);

                /* send EOT (tell the downloader we are finished) */
                serialPort.Write(new byte[] { EOT }, 0, 1);
                /* get ACK (downloader acknowledge the EOT) */
                if (serialPort.ReadByte() != ACK)
                {
                    Console.WriteLine("Can't complete the transfer.");
                    return false;
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Exception");
                return false;
            }
            return false;
        }
        #endregion


        #region Helper Methods
        public class Crc16Ccitt
        {
            const ushort poly = 0x1021;
            ushort[] table = new ushort[256];
            ushort initialValue = 0;

            public ushort ComputeChecksum(byte[] bytes)
            {
                ushort crc = this.initialValue;
                for (int i = 0; i < bytes.Length; i++)
                {
                    crc = (ushort)((crc << 8) ^ table[((crc >> 8) ^ (0xff & bytes[i]))]);
                }
                return crc;
            }

            public byte[] ComputeChecksumBytes(byte[] bytes)
            {
                ushort crc = ComputeChecksum(bytes);
                return new byte[] { (byte)(crc >> 8), (byte)(crc & 0x00ff) };
            }

            public Crc16Ccitt(InitialCrcValue initialValue)
            {
                this.initialValue = (ushort)initialValue;
                ushort temp, a;
                for (int i = 0; i < table.Length; i++)
                {
                    temp = 0;
                    a = (ushort)(i << 8);
                    for (int j = 0; j < 8; j++)
                    {
                        if (((temp ^ a) & 0x8000) != 0)
                        {
                            temp = (ushort)((temp << 1) ^ poly);
                        }
                        else
                        {
                            temp <<= 1;
                        }
                        a <<= 1;
                    }
                    table[i] = temp;
                }
            }
        }
        #endregion
    }
 }
