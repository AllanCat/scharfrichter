using System;
using System.IO;
using System.Text;

namespace Scharfrichter.Codec.Sounds
{
    public class SoundElement : IDisposable
    {
        private MemoryStream mSoundStream;
        private string mMagicTag;
        private Int32 mInfoLenth;     //24
        private Int32 mDataLength;
        private Int16 mDummy;         //ASCII "02"
        private Int16 mChannel;       //-1
        private Int16 mPanning;       //0x40
        private Int16 mVolume;        //0x01
        private Int32 mOptions;       //0x00

        public int Length
        {
            get
            {
                return (int)mSoundStream.Length;
            }
        }

        public
        SoundElement(Stream data)
        {
            mInfoLenth = 24;
            mDataLength = (Int32)data.Length;
            mDummy = 0x3230;
            mChannel = -1;
            mPanning = 0x40;
            mVolume = 1;
            mOptions = 0;
            mMagicTag = "2DX9";
            WriteStream(data);
        }

        public
            SoundElement(byte[] data)
        {
            mInfoLenth = 24;
            mDataLength = (Int32)data.Length;
            mDummy = 0x3230;
            mChannel = -1;
            mPanning = 0x40;
            mVolume = 1;
            mOptions = 0;
            mMagicTag = "2DX9";
            WriteData(data);
        }

        public
        MemoryStream
        GetStream()
        {
            return mSoundStream;
        }

        private void WriteData(byte[] data)
        {
            mSoundStream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(mSoundStream);

            writer.Write(Encoding.ASCII.GetBytes(mMagicTag));
            writer.Write(mInfoLenth);
            writer.Write(mDataLength);
            writer.Write(mDummy);
            writer.Write(mChannel);
            writer.Write(mPanning);
            writer.Write(mVolume);
            writer.Write(mOptions);
            writer.Write(data, 0, data.Length);
             
        }

        private
        void
        WriteStream(Stream data)
        {
            mSoundStream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(mSoundStream);

            writer.Write(Encoding.ASCII.GetBytes(mMagicTag));
            writer.Write(mInfoLenth);
            writer.Write(mDataLength);
            writer.Write(mDummy);
            writer.Write(mChannel);
            writer.Write(mPanning);
            writer.Write(mVolume);
            writer.Write(mOptions);
            byte[] buffer = new byte[4096];
            while (true)
            {
                int readCount = data.Read(buffer, 0, buffer.Length);
                if (readCount > 0)
                {
                    writer.Write(buffer, 0, readCount);
                }
                else
                {
                    break;
                }
            }
        }

        public virtual
        void
        Dispose()
        {
            mSoundStream.Dispose();
        }

        #region Document

        /*
         *  if (new string(reader.ReadChars(4)) == "2DX9")
            {
                int infoLength = reader.ReadInt32();
                int dataLength = reader.ReadInt32();
                reader.ReadInt16();
                int channel = reader.ReadInt16();
                int panning = reader.ReadInt16();
                int volume = reader.ReadInt16();
                int options = reader.ReadInt32();

                reader.ReadBytes(infoLength - 24);
         */

        #endregion
    }
}