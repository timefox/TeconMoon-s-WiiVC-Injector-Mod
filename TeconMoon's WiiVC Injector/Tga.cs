using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace ImageUtils
{

    class Tga
    {

        public static Image loadTga(string path)
        {
            using (Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return loadTga(stream);
            }
        }

        public static Image loadTga(Stream stream)
        {
            Bitmap result = null;

            BinaryReader reader = new BinaryReader(stream);

            byte idFieldLength = (byte)stream.ReadByte();
            byte colorMap = (byte)stream.ReadByte();
            byte imageType = (byte)stream.ReadByte();
            reader.BaseStream.Position = 0xC;
            int width = reader.ReadUInt16();
            reader.BaseStream.Position = 0xE;
            int height = reader.ReadUInt16();
            reader.BaseStream.Position = 0x10;
            byte bpp = (byte)stream.ReadByte();
            reader.BaseStream.Position = 0x11;
            byte flags = (byte)stream.ReadByte();

            if(colorMap > 0 || bpp < 16 || imageType > 3) {
                throw new InvalidDataException("Unsupported TGA file.");
            }

            if (idFieldLength > 0)
            {
                stream.Read(new byte[idFieldLength], 0, idFieldLength);
            }

            byte[] line = new byte[width * (bpp / 8)];

            result = new Bitmap(width, height);
            for (int y = height - 1; y >= 0; y--)
            {
                switch (bpp)
                {
                    case 16:
                        int hi, lo;
                        for (int x = 0; x < width; x++)
                        {
                            hi = stream.ReadByte();
                            lo = stream.ReadByte();

                            Color pixel = Color.FromArgb(255,
                                                        (byte)(((lo & 0x7F) >> 2) << 3),
                                                        (byte)((((lo & 0x3) << 3) + ((hi & 0xE0) >> 5)) << 3),
                                                        (byte)((hi & 0x1F) << 3)
                            );
                            result.SetPixel(x, y, pixel);
                        }
                        break;
                    case 24:
                        stream.Read(line, 0, line.Length);
                        for (int x = 0; x < width; x++)
                        {
                            Color pixel = Color.FromArgb(255,
                                                        line[x * 3 + 2],
                                                        line[x * 3 + 1],
                                                        line[x * 3]
                            );
                            result.SetPixel(x, y, pixel);
                        }
                        break;
                    case 32:
                        stream.Read(line, 0, line.Length);
                        for (int x = 0; x < width; x++)
                        {
                            Color pixel = Color.FromArgb(line[x * 4 + 3],
                                                        line[x * 4 + 2],
                                                        line[x * 4 + 1],
                                                        line[x * 4]);
                            result.SetPixel(x, y, pixel);
                        }
                        break;
                }
            }

            switch((flags >> 4) & 0x3)
            {
                case 1: 
                    result.RotateFlip(RotateFlipType.RotateNoneFlipX);
                    break;
                case 2: 
                    result.RotateFlip(RotateFlipType.RotateNoneFlipY);
                    break;
                case 3: 
                    result.RotateFlip(RotateFlipType.RotateNoneFlipXY);
                    break;
            }


            return result;
        }

        public static void saveTGA(Image image, PixelFormat pixelFormat, string path)
        {
            Bitmap bitmap = (Bitmap)image;

            byte entryPerPixel;
            byte bpp;

            switch (pixelFormat)
            {
                case PixelFormat.Format32bppArgb:
                    entryPerPixel = 8;
                    bpp = 32;
                    break;
                case PixelFormat.Format24bppRgb:
                    entryPerPixel = 0;
                    bpp = 24;
                    break;
                default:
                    throw new InvalidDataException("Unsupported bpp.");
            }

            using (Stream file = File.Create(path))
            {
                using (BinaryWriter writer = new BinaryWriter(file))
                {
                    // Header
                    writer.Write(new byte[]
                    {
                    0, // ID length
                    0, // no color map
                    2, // uncompressed, true color
                    0, 0, 0, 0,
                    0,
                    0, 0, 0, 0, // x and y origin
                    (byte)(bitmap.Width & 0x00FF),
                    (byte)((bitmap.Width & 0xFF00) >> 8),
                    (byte)(bitmap.Height & 0x00FF),
                    (byte)((bitmap.Height & 0xFF00) >> 8),
                    bpp,
                    entryPerPixel
                    });

                    // Image
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            Color c = bitmap.GetPixel(x, bitmap.Height - y - 1);
                            writer.Write(new[]
                            {
                            c.B,
                            c.G,
                            c.R
                            });

                            if (bpp == 32)
                            {
                                writer.Write(c.A);
                            }
                        }
                    }

                    // Footer
                    writer.Write(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 });
                    writer.Write(Encoding.ASCII.GetBytes("TRUEVISION-XFILE"));
                    writer.Write(new byte[] { 46, 0 });
                }
            }
        }

    }

}