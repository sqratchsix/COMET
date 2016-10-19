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
            bool written = false;
            try
            {
                using (FileStream fs = new FileStream(fileName, FileMode.Append, FileAccess.Write))
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    sw.WriteLine(data);
                    written = true;
                }
            }
            catch (Exception)
            {
                
            }
            return written;
        }
    }
}
