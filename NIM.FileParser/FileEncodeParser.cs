using System;
using System.Text;

namespace ModFileParser
{
    public class FileEncodeParser
    {
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
    }
}
