using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NIM.FileManagement;

namespace NIM.SkyrimManagement
{
    public class BackupManager
    {
        public static int BackupRetentionCount = 10;

        public static string BackupSuffix = "_backup.zip";

        public static string CreateBackupName(string FileName, int ID)
        {
            string Time = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            return $"{Time}_{ID}_{FileName}";
        }

        public static string AddFile(string Path, ref List<ZipFileInfo> InFos)
        {
            NextCall:

            int ID = 0;

            string FileName = new FileInfo(Path).Name;

            string GetManagePath = new FileInfo(Path).Directory + @"\" + FileName + BackupSuffix;

            if (File.Exists(GetManagePath))
            {
                try
                {
                    InFos = ZipHelper.GetFileList(GetManagePath);

                    if (InFos.Count >= 10)
                    {
                        ZipFileInfo Oldest = InFos.OrderBy(x => x.Time).FirstOrDefault();

                        if (Oldest != null)
                        {
                            ZipHelper.DeleteFileFromZip(
                                GetManagePath,
                                Oldest.Name
                            );

                            InFos.Remove(Oldest);
                        }
                    }

                    ID = InFos.Count;

                    ZipHelper.AddFileToZip(GetManagePath, Path, CreateBackupName(FileName, ID));

                    return GetManagePath;
                }
                catch 
                {
                    //If the compressed archive is corrupted, it needs to be regenerated.
                    if (File.Exists(GetManagePath))
                    {
                        File.Delete(GetManagePath);
                    }
                    
                    goto NextCall;
                }
            }
            else
            {
                ZipHelper.CompressFile(Path, GetManagePath, CreateBackupName(FileName, ID));
            }

            return GetManagePath;
        }

        public static void RestoreLatest(string ManagePath)
        {
            if (!File.Exists(ManagePath))
                throw new FileNotFoundException(
                    "Backup file not found",
                    ManagePath
                );


            List<ZipFileInfo> Infos = ZipHelper.GetFileList(ManagePath);

            if (Infos.Count == 0)
                throw new InvalidDataException(
                    "Backup file is empty"
                );


            ZipFileInfo Latest = Infos
                .OrderByDescending(x => x.Time)
                .FirstOrDefault();


            if (Latest == null)
                throw new InvalidDataException(
                    "No backup entry found"
                );


            string[] Names = Latest.Name.Split('_');

            if ((Names.Length > 3) == false)
                throw new InvalidDataException(
                    "Invalid backup name"
                );


            string FileName = string.Join(
                "_",
                Names.Skip(3)
            );


            string OutputPath = Path.Combine(
                Path.GetDirectoryName(ManagePath),
                FileName
            );


            ZipHelper.DecompressFile(
                ManagePath,
                OutputPath,
                Latest.Name
            );
        }
    }
}
