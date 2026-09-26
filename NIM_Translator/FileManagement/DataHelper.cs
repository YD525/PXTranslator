using System;
using System.IO;
using System.Text;

namespace NIM.SkyrimModManager
{
    public class DataHelper
    {
        public static string ShowSaveFileDialog(string defaultFileName, string filter = "All files (*.*)|*.*")
        {
            using (var SaveFileDialog = new System.Windows.Forms.SaveFileDialog())
            {
                SaveFileDialog.FileName = defaultFileName;
                SaveFileDialog.Filter = filter;
                SaveFileDialog.RestoreDirectory = true;

                if (SaveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    return SaveFileDialog.FileName;
                }
                else
                {
                    return null;
                }
            }
        }       
        public static byte[] ReadFile(string Path)
        {
            byte[] Data = null;

            if (File.Exists(Path))
            {
                using (FileStream FS =
                          new FileStream(Path, FileMode.Open, FileAccess.Read))
                {
                    using (BinaryReader BR = new BinaryReader(FS))
                    {
                        Data = BR.ReadBytes((int)FS.Length);
                    }
                }
            }
            else
            {
                return new byte[0];
            }

            return Data;
        }
        public static void WriteFile(string TargetPath, byte[] Data)
        {
            FileStream FS = new FileStream(TargetPath, FileMode.Create);
            FS.Write(Data, 0, Data.Length);
            FS.Close();
            FS.Dispose();
        }    
        public static System.Text.Encoding GetFileEncodeType(string FilePath)
        {
            try
            {
                System.IO.FileStream FileStream = new System.IO.FileStream(FilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                System.IO.BinaryReader BinaryReader = new System.IO.BinaryReader(FileStream);
                Byte[] buffer = BinaryReader.ReadBytes(2);
                if (buffer[0] >= 0xEF)
                {
                    if (buffer[0] == 0xEF && buffer[1] == 0xBB)
                    {
                        FileStream.Close();
                        BinaryReader.Close();
                        return System.Text.Encoding.UTF8;
                    }
                    else if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                    {
                        FileStream.Close();
                        BinaryReader.Close();
                        return System.Text.Encoding.BigEndianUnicode;
                    }
                    else if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                    {
                        FileStream.Close();
                        BinaryReader.Close();
                        return System.Text.Encoding.Unicode;
                    }
                    else
                    {
                        FileStream.Close();
                        BinaryReader.Close();
                        return System.Text.Encoding.UTF8;
                    }
                }
                if (buffer[0] == 0x3c)//UTF-8 without BOM
                {
                    FileStream.Close();
                    BinaryReader.Close();
                    return System.Text.Encoding.UTF8;
                }
                else
                {
                    FileStream.Close();
                    BinaryReader.Close();
                    return System.Text.Encoding.UTF8;
                }
            }
            catch { return Encoding.UTF8; }
        }
        public static void CopyDir(string Path, string TargetPath)
        {
            try
            {
                if (TargetPath[TargetPath.Length - 1] != System.IO.Path.DirectorySeparatorChar)
                {
                    TargetPath += System.IO.Path.DirectorySeparatorChar;
                }
                if (!System.IO.Directory.Exists(TargetPath))
                {
                    System.IO.Directory.CreateDirectory(TargetPath);
                }

                string[] FileList = System.IO.Directory.GetFileSystemEntries(Path);

                foreach (string File in FileList)
                {
                    if (System.IO.Directory.Exists(File))
                    {
                        CopyDir(File, TargetPath + System.IO.Path.GetFileName(File));
                    }
                    else
                    {
                        System.IO.File.Copy(File, TargetPath + System.IO.Path.GetFileName(File), true);
                    }
                }
            }
            catch (Exception e)
            {
                throw;
            }
        }
        
    }
}
