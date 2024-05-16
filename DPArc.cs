using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BlakieLibAssetBuilder
{
    internal class DPArc
    {
        public static void CreateDPArc(string inPath, string outPath)
        {
            List<HeaderEntry> headerDat = new List<HeaderEntry>();
            List<byte> data = new List<byte>();

            FilesRecursive(inPath, headerDat, data, inPath);

            List<byte> file = new List<byte>();
            file.AddRange(Encoding.ASCII.GetBytes("DPARC"));
            file.AddRange(BitConverter.GetBytes(headerDat.Count));
            file.AddRange(BitConverter.GetBytes(0)); //header size
            foreach(HeaderEntry header in headerDat)
                file.AddRange(header.GetBytes());
            byte[] headerSizeBytes = BitConverter.GetBytes(file.Count);
            file[9] = headerSizeBytes[0]; file[10] = headerSizeBytes[1]; file[11] = headerSizeBytes[2]; file[12] = headerSizeBytes[3];
            file.AddRange(data);
            BinaryWriter outFile = new BinaryWriter(File.Open(outPath, FileMode.OpenOrCreate));
            outFile.Write(file.ToArray());
            outFile.Close();
        }

        static void FilesRecursive(string inPath, List<HeaderEntry> headerDat, List<byte> data, string initPath, string addToPath = "")
        {
            foreach (string path in Directory.EnumerateFiles(inPath))
            {
                byte[] fileData = File.ReadAllBytes(path);
                HeaderEntry header = new HeaderEntry();
                header.fileName = path.Substring(path.LastIndexOf('/') + 1);
                header.fileName = header.fileName.Substring(header.fileName.LastIndexOf('\\') + 1);
                header.fileName = addToPath + header.fileName;
                header.offset = data.Count;
                header.length = fileData.Length;
                headerDat.Add(header);
                data.AddRange(fileData);
            }

            foreach(string path in Directory.EnumerateDirectories(inPath))
                FilesRecursive(path, headerDat, data, initPath, (path.Substring(initPath.Length + 1) + '/').Replace('\\', '/'));
        }

        struct HeaderEntry
        {
            public string fileName;
            public int offset; //offset is from the end of header
            public int length;

            public byte[] GetBytes()
            {
                List<byte> rtrn = new List<byte>();
                rtrn.Add((byte)fileName.Length);
                rtrn.AddRange(Encoding.ASCII.GetBytes(fileName));
                rtrn.AddRange(BitConverter.GetBytes(offset));
                rtrn.AddRange(BitConverter.GetBytes(length));
                return rtrn.ToArray();
            }
        }
    }
}
