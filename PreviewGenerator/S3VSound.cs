using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PreviewGenerator
{
    public class S3VSound
    {
        private readonly byte[] mAudioFile;
        private static readonly byte[] mGuid = new byte[16];
        private static string Magic = "S3V0 \0\0\0";
        private readonly byte[] mFlags = new byte[4];

        public bool IsBase;
        public S3VSound(byte[] audioFile)
        {
            mAudioFile = audioFile;
        }

        public byte[] Pack()
        {
            using (var mem = new MemoryStream())
            {
                mem.Write(Encoding.ASCII.GetBytes(Magic),0,Magic.Length);
                mem.Write(BitConverter.GetBytes(mAudioFile.Length),0,4);
                mem.Write(mGuid,0,16);
                mFlags[3] = IsBase ? (byte)1 : (byte)0;
                mem.Write(mFlags,0,mFlags.Length);
                mem.Write(mAudioFile, 0, mAudioFile.Length);
                return mem.ToArray();
            }
        }

        public int Size => mAudioFile.Length + 0x20;
    }

    public class S3PPack
    {
        private Header mHeader;
        private List<S3VSound> mSounds = new List<S3VSound>();

        public S3PPack()
        {
            mHeader = new Header();
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

        class Header
        {
            public string Magic = "S3P0";
            public List<int> Offsets;
            public int CurrentTail = 8;
        }
    }
}