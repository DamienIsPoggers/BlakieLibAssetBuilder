using Hjg.Pngcs;
using Hjg.Pngcs.Chunks;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using ZLibDotNet;

namespace BlakieLibAssetBuilder
{
    internal class DPSpr
    {
        public static void CreateDPSPR(string inPath, string outPath, bool rleCompress = false)
        {
            List<Color[]> paletteData = new List<Color[]>();
            List<ImageHeaderDat> images = new List<ImageHeaderDat>();
            List<byte> imageData = new List<byte>();

            //read pngs
            foreach (string path in Directory.EnumerateFiles(inPath))
            {
                if (path.Substring(path.LastIndexOf('.')) != ".png")
                    continue;
                BinaryReader binReader = new BinaryReader(new FileStream(path, FileMode.Open));
                PngReader png = new PngReader(binReader.BaseStream);

                string imageName = path.Replace('\\', '/');
                imageName = imageName.Substring(imageName.LastIndexOf('/') + 1);
                imageName = imageName.Substring(0, imageName.LastIndexOf('.'));
                ImageHeaderDat headerDat = new ImageHeaderDat();
                headerDat.spriteName = imageName;
                headerDat.width = png.ImgInfo.Cols;
                headerDat.height = png.ImgInfo.Rows;

                List<byte> texDat = new List<byte>();

                if (png.ImgInfo.Indexed)
                {
                    headerDat.indexed = true;
                    PngChunkPLTE plte = (PngChunkPLTE)png.GetChunksList().GetById1("PLTE");
                    int[] paletteAlpha = new int[] { };
                    bool hasTransparency = png.GetChunksList().GetById1("TRNS") != null;
                    if (hasTransparency)
                        paletteAlpha = ((PngChunkTRNS)png.GetChunksList().GetById1("TRNS")).GetPalletteAlpha();
                    Color[] pal = new Color[plte.GetNentries()];
                    for (int i = 0; i < pal.Length; i++)
                    {
                        int colorRaw = plte.GetEntry(i);
                        byte r = (byte)((colorRaw & 0xff0000) >> 16);
                        byte g = (byte)((colorRaw & 0xff00) >> 8);
                        byte b = (byte)(colorRaw & 0xff);
                        byte a = hasTransparency ? (byte)paletteAlpha[i] : (byte)255;
                        pal[i] = new Color(r, g, b, a);
                    }

                    bool alreadyHasPal = false;
                    foreach (Color[] palette in paletteData)
                        if (Enumerable.SequenceEqual(pal, palette))
                        {
                            alreadyHasPal = true;
                            break;
                        }
                    if (alreadyHasPal)
                    {
                        byte palNum = 0;
                        for (byte i = 0; i < paletteData.Count; i++)
                            if (pal == paletteData[i])
                                palNum = i;
                        headerDat.palNum = palNum;
                    }
                    else
                    {
                        paletteData.Add(pal);
                        headerDat.palNum = (byte)(paletteData.Count - 1);
                    }

                    for (int i = 0; i < headerDat.height; i++)
                        foreach (int color in png.ReadRow(i).Scanline)
                            texDat.Add((byte)color);
                }
                else
                {
                    headerDat.indexed = false;
                    headerDat.palNum = 0;
                    for (int i = 0; i < headerDat.height; i++)
                        foreach (int color in png.ReadRow(i).Scanline)
                            texDat.AddRange(BitConverter.GetBytes(color));
                }

                Console.WriteLine("Compressing " + headerDat.spriteName);
                if (rleCompress)
                {
                    byte[] tex = RLECompress(texDat, headerDat.indexed);
                    headerDat.compressedSize = tex.Length;
                    imageData.AddRange(tex);
                }
                else
                {
                    ZLib zlib = new ZLib();
                    Span<byte> compressedData = new byte[zlib.CompressBound((uint)texDat.Count)];
                    int compressedSize;
                    zlib.Compress(compressedData, out compressedSize, texDat.ToArray(), ZLib.Z_BEST_COMPRESSION);
                    byte[] dat = new byte[compressedSize];
                    Array.Copy(compressedData.ToArray(), dat, compressedSize);
                    imageData.AddRange(dat);
                    headerDat.compressedSize = compressedSize;
                }
                images.Add(headerDat);
                binReader.Close();
                binReader.Dispose();
            }

            //write header data
            BinaryWriter file = new BinaryWriter(File.Open(outPath, FileMode.Create));
            file.Write(Encoding.ASCII.GetBytes("DPSpr"));
            file.Write(BitConverter.GetBytes(images.Count));
            file.Write(BitConverter.GetBytes(paletteData.Count));
            file.Write(rleCompress ? (byte)1 : (byte)2); //compressed flag. uncompressed takes way to much space holy shit
            //Write palette data
            for (int i = 0; i < paletteData.Count; i++)
            {
                foreach (Color col in paletteData[i])
                    file.Write(col.GetAsBytes());
                if (paletteData[i].Length < 256)
                    for (int j = paletteData[i].Length; j < 256; j++)
                        file.Write(0);
            }
            //write image data
            foreach (ImageHeaderDat header in images)
                file.Write(header.GetAsBytes());

            //write texture data
            file.Write(imageData.ToArray());

            file.Close();
        }

        static byte[] RLECompress(List<byte> data, bool indexed)
        {
            List<byte> rtrn = new List<byte>();

            int pixelCount = data.Count;
            if (!indexed)
                pixelCount /= 4;

            int continuousCount = 1;
            int randomCount = 1;
            int randomStart = 0;
            bool inRandom = false;

            for (int i = 1; i < pixelCount; i++)
            {
                int curPixel, prevPixel;
                bool writeBuffer = false;
                if (indexed)
                {
                    curPixel = data[i];
                    prevPixel = data[i - 1];
                }
                else
                {
                    curPixel = BitConverter.ToInt32(data.ToArray(), i * 4);
                    prevPixel = BitConverter.ToInt32(data.ToArray(), (i - 1) * 4);
                }

                if (curPixel == prevPixel)
                    continuousCount++;
                else if (continuousCount < 5)
                {
                    if (!inRandom)
                    {
                        inRandom = true;
                        randomCount = continuousCount;
                        randomStart = i - randomCount;
                        continuousCount = 1;
                    }
                    else
                    {
                        randomCount += continuousCount;
                        continuousCount = 1;
                    }
                }
                else
                    writeBuffer = true;

                if (i + 1 == pixelCount)
                    writeBuffer = true;

                if (writeBuffer)
                {
                    if (inRandom)
                    {
                        int randomCountDiv = randomCount / 255;
                        while (randomCountDiv > 0)
                        {
                            rtrn.Add(255);
                            for (int j = 0; j < 256; j++)
                                if (indexed)
                                    rtrn.Add(data[randomStart + j]);
                                else
                                {
                                    int pixel = BitConverter.ToInt32(data.ToArray(), (randomStart + j) * 4);
                                    rtrn.AddRange(BitConverter.GetBytes(pixel));
                                }
                            randomStart += 255;
                            randomCountDiv--;
                        }

                        rtrn.Add((byte)randomCount);
                        for (int j = 0; j < randomCount; j++)
                            if (indexed)
                                rtrn.Add(data[randomStart + j]);
                            else
                            {
                                int pixel = BitConverter.ToInt32(data.ToArray(), (randomStart + j) * 4);
                                rtrn.AddRange(BitConverter.GetBytes(pixel));
                            }
                        inRandom = false;
                        randomCount = 1;
                    }

                    int contCountDiv = continuousCount / ushort.MaxValue;
                    for (int j = 0; j < contCountDiv; j++)
                    {
                        rtrn.Add(0);
                        rtrn.AddRange(BitConverter.GetBytes(ushort.MaxValue));
                        if (indexed)
                            rtrn.Add((byte)prevPixel);
                        else
                            rtrn.AddRange(BitConverter.GetBytes(prevPixel));
                    }
                    rtrn.Add(0);
                    rtrn.AddRange(BitConverter.GetBytes((ushort)continuousCount));
                    if (indexed)
                        rtrn.Add((byte)prevPixel);
                    else
                        rtrn.AddRange(BitConverter.GetBytes(prevPixel));
                    continuousCount = 1;
                }
            }

            return rtrn.ToArray();
        }

        struct Color
        {
            public byte r;
            public byte g;
            public byte b;
            public byte a;

            public Color(byte R, byte G, byte B, byte A)
            {
                r = R;
                g = G;
                b = B;
                a = A;
            }

            public byte[] GetAsBytes()
            {
                byte[] rtrn = new byte[4];
                rtrn[0] = r;
                rtrn[1] = g;
                rtrn[2] = b;
                rtrn[3] = a;
                return rtrn;
            }
        }

        struct ImageHeaderDat
        {
            public string spriteName;
            public bool indexed;
            public byte palNum;
            public int width;
            public int height;
            public int compressedSize; //if zlib compressed this is uncompressed size

            public byte[] GetAsBytes()
            {
                List<byte> data = new List<byte>();
                data.Add((byte)spriteName.Length);
                data.AddRange(Encoding.ASCII.GetBytes(spriteName));
                data.Add(indexed ? (byte)1 : (byte)0);
                data.Add(palNum);
                data.AddRange(BitConverter.GetBytes(width));
                data.AddRange(BitConverter.GetBytes(height));
                data.AddRange(BitConverter.GetBytes(compressedSize));
                return data.ToArray();
            }
        }
    }
}
