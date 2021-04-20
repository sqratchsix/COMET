using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;

namespace Comet1
{
    static class Utilities
    {
        
        public static string findUniqueFilepath(string basePathName, string extension)
        {
            string path = "";
            for (int i = 1; i < 999; i++)
            {
                path = basePathName + i.ToString("000") + extension;

                //quit looking if the file doesn't exist yet
                if (!File.Exists(path)) break;
            }
            return path;
        }

        public static void SaveStringArrayToFile(string filePath, String[] data)
        {
            SaveFileDialog save = new SaveFileDialog();
            save.FileName = filePath;
            save.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
            if (save.ShowDialog() == DialogResult.OK)
            {
                StreamWriter writer = new StreamWriter(save.OpenFile());
                for (int i = 0; i < data.Length; i++)
                {
                    writer.WriteLine(data[i].ToString());
                }
                writer.Dispose();
                writer.Close();
            }
        }

        public static bool writeStringToFile(string fileName, string data)
        {
            try
            {
                using (FileStream fs = new FileStream(fileName, FileMode.Append, FileAccess.Write))
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    sw.WriteLine(data);
                    return true;
                }
            }
            catch (Exception)
            {
                
            }
            return false;
        }

        internal static double convertMS(double input, string timeType, bool ToMS)
        {
            //convert to the appropriate time scale
            //timeType = mili sec min hour
            //ToMS : if true, the input is in MS already, so convert to the other type

            switch (timeType)
            {
                case "milli":
                    return input;
                case "sec":
                    if (ToMS)
                    {
                        return input*1000;
                    }
                    else
                    {
                        return input/1000;
                    }
                case "min":
                    if (ToMS)
                    {
                        return input * 60 * 1000;
                    }
                    else
                    {
                        return input / (60 * 1000);
                    }
                case "hour":
                    if (ToMS)
                    {
                        return input * 60 * 60 * 1000;
                    }
                    else
                    {
                        return input / (60 * 60 * 1000);
                    }
                default:
                    return 0;
            }
        }

        public static string ConvertHex(string hexString, bool removeWhitespace)
        {
            if(removeWhitespace)
            {
                //this would remove all whitespace characters, if that's better
                //hexString = RegularExpressions.Replace(hexString, @"\s", "");
                hexString = hexString.Replace(" ", String.Empty);
                hexString = hexString.Replace("\r\n", String.Empty);
                hexString = hexString.Replace("\n", String.Empty);
            }
            try
            {
                string ascii = string.Empty;

                for (int i = 0; i < hexString.Length; i += 2)
                {
                    string hs = string.Empty;

                    hs = hexString.Substring(i, 2);
                    uint decval = System.Convert.ToUInt32(hs, 16);
                    char character = System.Convert.ToChar(decval);
                    ascii += character;

                }

                return ascii;
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }

            return string.Empty;
        }

        public static string ConvertStringToHex(string InputString, bool removeWhitespace)
        {
            string hexOutput = "";

            if (removeWhitespace)
            {
                //this would remove all whitespace characters, if that's better
                //InputString = RegularExpressions.Replace(InputString, @"\s", "");
                InputString = InputString.Replace(" ", string.Empty);
            }

            try
            {
                char[] charValues = InputString.ToCharArray();
                foreach (char _eachChar in charValues)
                {
                    // Get the integral value of the character.
                    int value = Convert.ToInt32(_eachChar);
                    // Convert the decimal value to a hexadecimal value in string form.
                    hexOutput += string.Format("{0:X}", value);
                    // to make output as your eg 
                    //  hexOutput +=" "+ String.Format("{0:X}", value);
                }
            }
            catch (Exception)
            {

            }
            return hexOutput;
        }
    }

    public class RecursiveFileProcessor
    {
        List<string> AllFilePaths = new List<string>();
        string FileExtension = ".txt";
        public string[] GetAllPaths(string[] args)
        {
            foreach (string path in args)
            {
                if (File.Exists(path))
                {
                    // This path is a file
                    ProcessFile(path);
                }
                else if (Directory.Exists(path))
                {
                    // This path is a directory
                    ProcessDirectory(path);
                }
                else
                {
                    Console.WriteLine("{0} is not a valid file or directory.", path);
                    //Add an empty string that something returns
                    AllFilePaths.Add("");
                }
            }
            return AllFilePaths.ToArray();
        }


        // Process all files in the directory passed in, recurse on any directories 
        // that are found, and process the files they contain.
        public void ProcessDirectory(string targetDirectory)
        {
            // Process the list of files found in the directory.
            string[] fileEntries = Directory.GetFiles(targetDirectory);
            foreach (string fileName in fileEntries)
            {
                if (fileName.Contains(FileExtension))
                {
                    ProcessFile(fileName);
                }
            }
            // Recurse into subdirectories of this directory.
            string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory);
            foreach (string subdirectory in subdirectoryEntries)
                ProcessDirectory(subdirectory);
        }

        // Insert logic for processing found files here.
        public void ProcessFile(string path)
        {
            AllFilePaths.Add(path);
            //Console.WriteLine("Processed file '{0}'.", path);
        }
    }

    public static class ToolStripItemExtension
    {
        public static ContextMenuStrip GetContextMenuStrip(this ToolStripItem item)
        {
            ToolStripItem itemCheck = item;

            while (!(itemCheck.GetCurrentParent() is ContextMenuStrip) && itemCheck.GetCurrentParent() is ToolStripDropDown)
            {
                itemCheck = (itemCheck.GetCurrentParent() as ToolStripDropDown).OwnerItem;
            }

            return itemCheck.GetCurrentParent() as ContextMenuStrip;
        }
    }
}
