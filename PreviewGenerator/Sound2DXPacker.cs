using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Scharfrichter.Codec.Sounds;

namespace PreviewGenerator
{
    public class Sound2DXPacker : IDisposable
    {
        MemoryStream mSoundStream;
        List<SoundElement> mSoundList;
        string mTag;
        string mTargetPath;

        public
        Sound2DXPacker()
        {
            mSoundStream = new MemoryStream();
            mSoundList = new List<SoundElement>();
           
            mTag = "00000";
        }

        private int HeaderLength
        {
            get { return 0x48 + mSoundList.Count * 4; }
        }

        public
        void
        SetTargetFileName(string fileName)
        {
            mTag = Path.GetFileName(fileName);
            mTargetPath = Path.GetDirectoryName(fileName);
        }

        public
        byte[]
        Pack()
        {
            WriteHeaderToStream();
            WriteSoundToStream();
            return mSoundStream.GetBuffer().Take((int)mSoundStream.Length).ToArray();
        }

        public void Add(SoundElement element)
        {
            mSoundList.Add(element);
        }
        
        private
        void
        WriteHeaderToStream()
        {
            // header length is at 0x10
            // sample count is at 0x14
            // offset list starts at 0x48
            BinaryWriter writer = new BinaryWriter(mSoundStream);
            writer.Write(Encoding.ASCII.GetBytes(mTag.Substring(0, mTag.Length <= 8 ? mTag.Length : 8)));
            writer.Seek(0x08, SeekOrigin.Begin);
            writer.Write((Int32)0);
            writer.Write((Int32)6);//magic numbers
            writer.Write(HeaderLength);
            writer.Write(mSoundList.Count);
            writer.Seek(0x48, SeekOrigin.Begin);
            int currentOffset = HeaderLength;
            foreach (SoundElement sound in mSoundList)
            {
                if (sound == null)
                    continue;
                writer.Write(currentOffset);
                currentOffset += sound.Length;
            }
        }

        private
        void
        WriteSoundToStream()
        {
            BinaryWriter writer = new BinaryWriter(mSoundStream);
            foreach (SoundElement sound in mSoundList)
            {
                if (sound != null)
                    writer.Write(sound.GetStream().ToArray());
            }
        }

        public virtual
        void
        Dispose()
        {
            foreach (SoundElement sound in mSoundList)
            {
                if (sound != null)
                    sound.Dispose();
            }
            mSoundStream.Dispose();
        }
    }
}