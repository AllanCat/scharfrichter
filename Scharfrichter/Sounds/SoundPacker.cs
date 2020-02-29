using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Scharfrichter.Codec.Sounds
{
    public class SoundPacker : IDisposable
    {
        MemoryStream mSoundStream;
        List<SoundElement> mSoundList;
        //int mSampleCount;
        int mHeaderLength;
        string mTag;
        string mTargetPath;

        public
        SoundPacker()
        {
            mSoundStream = new MemoryStream();
            mSoundList = new List<SoundElement>();
            mTag = "00000";
        }

        public SoundPacker SetTag(string tag)
        {
            mTag = tag;
            return this;
        }

        public SoundPacker
        SetTargetFileName(string fileName)
        {
            mTag = Path.GetFileName(fileName);
            mTargetPath = Path.GetDirectoryName(fileName);
            return this;
        }

        public SoundPacker Add(SoundElement s)
        {
            mSoundList.Add(s);
            return this;
        }

        public
        MemoryStream
        Pack()
        {
            WriteHeaderToStream();
            WriteSoundToStream();
            return mSoundStream;
        }

        private
        void
        WriteHeaderToStream()
        {
            // header length is at 0x10
            // sample count is at 0x14
            // offset list starts at 0x48
            mHeaderLength = 0x48 + mSoundList.Count * 4;
            BinaryWriter writer = new BinaryWriter(mSoundStream);
            writer.Write(Encoding.ASCII.GetBytes(mTag.Substring(0, mTag.Length <= 8 ? mTag.Length : 8)));
            writer.Seek(0x08, SeekOrigin.Begin);
            writer.Write((Int32)0);
            writer.Write((Int32)6);//magic numbers
            writer.Write(mHeaderLength);
            writer.Write(mSoundList.Count);
            writer.Seek(0x48, SeekOrigin.Begin);
            int currentOffset = mHeaderLength;
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