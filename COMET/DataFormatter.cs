using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Comet1
{
    public class DataFormatter
    {
        private Regex byteSpacingRegex;
        private MatchEvaluator evaluator;
        private bool timestampEnabled = false;
        private string timestampFormat = "HH:mm:ss:ffff";
        private bool byteSpacesEnabled = true;
        private bool addNewLineToTerminal = true;

        public bool TimestampEnabled
        {
            get => timestampEnabled;
            set => timestampEnabled = value;
        }

        public string TimestampFormat
        {
            get => timestampFormat;
            set => timestampFormat = value;
        }

        public bool ByteSpacesEnabled
        {
            get => byteSpacesEnabled;
            set => byteSpacesEnabled = value;
        }

        public bool AddNewLineToTerminal
        {
            get => addNewLineToTerminal;
            set => addNewLineToTerminal = value;
        }

        public DataFormatter()
        {
            byteSpacingRegex = new Regex("[\\S][\\S]");
            evaluator = new MatchEvaluator(AddSpaceAfter2);
        }

        public string FormatReceivedData(string data, enumDataType dataType)
        {
            if (string.IsNullOrEmpty(data))
                return string.Empty;

            string formattedData = data;

            if (byteSpacesEnabled && dataType == enumDataType.HEX)
            {
                formattedData = AddByteSpacing(formattedData);
            }

            if (timestampEnabled)
            {
                string timeStamp = DateTime.Now.ToString(timestampFormat);
                formattedData = timeStamp + " " + formattedData;
            }

            return formattedData;
        }

        public string AddByteSpacing(string unspacedByteString)
        {
            int msgsize = unspacedByteString.Length;
            if (msgsize == 0)
            {
                return "";
            }

            if (msgsize % 2 != 0)
            {
                unspacedByteString = unspacedByteString.PadLeft(unspacedByteString.Length + 1, '0');
                msgsize = unspacedByteString.Length;
            }

            StringBuilder addingSpacing = new StringBuilder();
            int indexChar = 0;
            while (true)
            {
                addingSpacing.Append(unspacedByteString[indexChar]);
                addingSpacing.Append(unspacedByteString[indexChar + 1]);
                indexChar = indexChar + 2;
                if (indexChar >= msgsize)
                    break;
                addingSpacing.Append(" ");
            }
            return addingSpacing.ToString().ToUpper();
        }

        public static string AddSpaceAfter2(Match m)
        {
            string newString = m + "";
            return newString.Insert(2, " ");
        }

        public string RemoveTrailingEndlines(string data)
        {
            if (!addNewLineToTerminal)
            {
                return Regex.Replace(data, @"\t|\n|\r", "");
            }
            return data;
        }

        public static string ConvertHexToAscii(string hexString, bool removeWhitespace)
        {
            return Utilities.ConvertHex(hexString, removeWhitespace);
        }

        public static string ConvertAsciiToHex(string inputString, bool removeWhitespace)
        {
            return Utilities.ConvertStringToHex(inputString, removeWhitespace);
        }

        public static string ConvertUnicodeFromHex(string hexString)
        {
            try
            {
                int code = Int32.Parse(hexString, System.Globalization.NumberStyles.HexNumber);
                return char.ConvertFromUtf32(code);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
