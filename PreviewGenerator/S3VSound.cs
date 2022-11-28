using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using Scharfrichter.Codec;

namespace PreviewGenerator
{
    public class S3VSound
    {
        private readonly byte[] mAudioFile;
        private byte[] mGuid = new byte[16];
        private static string Magic = "S3V0 \0\0\0";
        private byte[] mFlags = new byte[4];
        private byte[] mUnknow1 = new byte[12];
        public int HeaderSize { get; private set; }
        public byte[] Flags => mFlags;

        public byte[] Unknow1 => mUnknow1;

        public bool IsBase;
        public S3VSound(byte[] audioFile)
        {
            mAudioFile = audioFile;
        }

        public byte[] Md5 => mGuid;

        public byte[] Pack()
        {
            using (var mem = new MemoryStream())
            {
                mem.Write(Encoding.ASCII.GetBytes(Magic),0,Magic.Length);
                mem.Write(BitConverter.GetBytes(mAudioFile.Length),0,4);
                mem.Write(mGuid,0,4);
                mem.Write(mUnknow1, 0, 12);
                mFlags[3] = IsBase ? (byte)1 : (byte)0;
                mem.Write(mFlags,0,mFlags.Length);
                mem.Write(mAudioFile, 0, mAudioFile.Length);
                return mem.ToArray();
            }
        }

        public int Size => mAudioFile.Length + 0x20;

        public static S3VSound FromPack(byte[] span)
        {
            using (MemoryStream mem = new MemoryStream(span))
            {
                BinaryReaderEx reader = new BinaryReaderEx(mem);
                var stringFromSpan = GetStringFromSpan(reader.ReadBytes(4));
                if (stringFromSpan.Equals("S3V0"))
                {
                    int infoLength = reader.ReadInt32();
                    int dataLength = reader.ReadInt32();

                    reader.BaseStream.Seek(infoLength, SeekOrigin.Begin);
                    var wmaData = reader.ReadBytes(dataLength);
                    var s3VSound = new S3VSound(wmaData);
                    s3VSound.HeaderSize = infoLength;

                    reader.BaseStream.Seek(12, SeekOrigin.Begin);
                    s3VSound.mGuid = reader.ReadBytes(4);
                    s3VSound.mUnknow1 = reader.ReadBytes(12);
                    s3VSound.mFlags = reader.ReadBytes(4);
                    return s3VSound;
                }
            }
            throw new S3PFileException("S3V magic error!");
        }

        private static unsafe string GetStringFromSpan(byte[] span)
        {
            string result;
            var signed = (sbyte[])(Array)span;
            fixed (sbyte* ptr = signed)
            {
                result = new string(ptr, 0, span.Length);
            }

            return result;
        }
        public class S3PFileException : Exception
        {
            public S3PFileException(string exception) : base(exception) { }
        }
    }

    public class S3PPack
    {
        private Header mHeader;
        private List<S3VSound> mSounds = new List<S3VSound>();

        public S3PPack()
        {
            mHeader = new Header();
        }

        public S3PPack(Stream fileStream)
        {
            mHeader = new Header();
            mSounds = Unpack(fileStream);
            fileStream.Close();
        }

        public void Add(S3VSound s)
        {
            mSounds.Add(s);
        }

        public byte[] Pack()
        {
            using (var mem = new MemoryStream())
            {
                using (var writer = new BinaryWriter(mem))
                {
                    mem.Write(Encoding.ASCII.GetBytes(mHeader.Magic), 0, 4);
                    mem.Write(BitConverter.GetBytes(mSounds.Count), 0, 4);
                    var offset = 8 + mSounds.Count * 8;
                    for (var i = 0; i < mSounds.Count; i++)
                    {
                        writer.Write(offset);
                        writer.Write(mSounds[i].Size);
                        offset += mSounds[i].Size;
                    }
                    for (var i = 0; i < mSounds.Count; i++)
                    {
                        writer.Write(mSounds[i].Pack());
                    }
                    writer.Flush();
                    return mem.ToArray();
                }
            }
        }

        private List<S3VSound> Unpack(Stream stream)
        {
            using (var reader = new BinaryReaderEx(stream)) {
                var magicSlice = reader.ReadBytes(4);
                if (Encoding.Default.GetString(magicSlice) != mHeader.Magic)
                {
                    throw new S3PFileException("Magic number error!");
                }
                var count = reader.ReadInt32();

                mHeader.Offsets = new List<int>(count);
                mHeader.Sizes = new List<int>(count);
                for (var i = 0; i < count; i++)
                {
                    var offset = reader.ReadInt32();
                    var size = reader.ReadInt32();
                    mHeader.Offsets.Add(offset);
                    mHeader.Sizes.Add(size);
                }

                var result = new List<S3VSound>(count);
                for (var i = 0; i < mHeader.Offsets.Count; i++)
                {
                    reader.BaseStream.Seek(mHeader.Offsets[i], SeekOrigin.Begin);
                    var buffer = reader.ReadBytes(mHeader.Sizes[i]);
                    result.Add(S3VSound.FromPack(buffer));
                }

                return result;
            }
            throw new S3PFileException("S3V magic error!");
        }

        class Header
        {
            public string Magic = "S3P0";
            public List<int> Offsets;
            public List<int> Sizes;
            public int CurrentTail = 8;
        }

        public class S3PFileException : Exception
        {
            public S3PFileException(string exception) : base(exception) { }
        }
    }
}