using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;


namespace NIM.FileManagement
{
    public class ZipFileInfo
    {
        public string Name { get; set; }
        public DateTime Time { get; set; }
        public long Size { get; set; }
    }

    public class ZipHelper
    {
        public static void CompressFile(
            string SourceFile,
            string ZipFile,
            string NewName = null)
        {
            if (!File.Exists(SourceFile))
                throw new FileNotFoundException("Source file not found", SourceFile);

            if (File.Exists(ZipFile))
                File.Delete(ZipFile);


            using (FileStream ZipStream = new FileStream(ZipFile, FileMode.Create))
            using (ZipArchive Archive = new ZipArchive(ZipStream, ZipArchiveMode.Create))
            {
                Archive.CreateEntryFromFile(
                    SourceFile,
                    string.IsNullOrEmpty(NewName)
                        ? Path.GetFileName(SourceFile)
                        : NewName,
                    CompressionLevel.Optimal
                );
            }
        }


        public static void DecompressFile(
            string ZipFile,
            string OutputFile,
            string EntryName = null)
        {
            if (!File.Exists(ZipFile))
                throw new FileNotFoundException("Zip file not found", ZipFile);


            using (FileStream ZipStream = new FileStream(ZipFile, FileMode.Open))
            using (ZipArchive Archive = new ZipArchive(ZipStream, ZipArchiveMode.Read))
            {
                ZipArchiveEntry Entry;


                if (string.IsNullOrEmpty(EntryName))
                {
                    if (Archive.Entries.Count == 0)
                        throw new InvalidDataException("Zip file is empty");

                    Entry = Archive.Entries[0];
                }
                else
                {
                    Entry = Archive.GetEntry(EntryName);

                    if (Entry == null)
                        throw new FileNotFoundException(
                            "Entry not found in zip",
                            EntryName
                        );
                }


                Entry.ExtractToFile(OutputFile, true);
            }
        }
        public static List<ZipFileInfo> GetFileList(string ZipFile)
        {
            if (!File.Exists(ZipFile))
                throw new FileNotFoundException("Zip file not found", ZipFile);


            List<ZipFileInfo> Files = new List<ZipFileInfo>();

            using (FileStream ZipStream = new FileStream(ZipFile, FileMode.Open))
            using (ZipArchive Archive = new ZipArchive(ZipStream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry Entry in Archive.Entries)
                {
                    Files.Add(new ZipFileInfo
                    {
                        Name = Entry.FullName,
                        Time = Entry.LastWriteTime.DateTime,
                        Size = Entry.Length
                    });
                }
            }

            return Files;
        }


        public static void DeleteFileFromZip(
            string ZipFile,
            string FileName)
        {
            if (!File.Exists(ZipFile))
                throw new FileNotFoundException("Zip file not found", ZipFile);


            using (FileStream ZipStream = new FileStream(ZipFile, FileMode.Open))
            using (ZipArchive Archive = new ZipArchive(ZipStream, ZipArchiveMode.Update))
            {
                ZipArchiveEntry Entry = Archive.GetEntry(FileName);

                if (Entry != null)
                {
                    Entry.Delete();
                }
            }
        }


        public static void AddFileToZip(
            string ZipFile,
            string SourceFile,
            string NewName = null)
        {
            if (!File.Exists(ZipFile))
                throw new FileNotFoundException("Zip file not found", ZipFile);

            if (!File.Exists(SourceFile))
                throw new FileNotFoundException("Source file not found", SourceFile);


            using (FileStream ZipStream = new FileStream(ZipFile, FileMode.Open))
            using (ZipArchive Archive = new ZipArchive(ZipStream, ZipArchiveMode.Update))
            {
                Archive.CreateEntryFromFile(
                    SourceFile,
                    string.IsNullOrEmpty(NewName)
                        ? Path.GetFileName(SourceFile)
                        : NewName,
                    CompressionLevel.Optimal
                );
            }
        }
    }
}
