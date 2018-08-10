using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using NAudio.Wave;
using NAudio.WindowsMediaFormat;

namespace Scharfrichter.Codec.Sounds
{
    public class BenamiS3VSound
    {
        // sample volume table
        // TODO: determine correctness.
        static private float[] volTab;
        static public float[] VolumeTable
        {
            get
            {
                if (volTab == null)
                {
                    volTab = new float[256];
                    for (int i = 0; i < 256; i++)
                        volTab[i] = (float)Math.Pow(10.0f, (-36.0f * i / 64f) / 20.0f);
                }
                return volTab;
            }
        }

        static public Sound Read(Stream source)
        {
            Sound result = new Sound();
            BinaryReader reader = new BinaryReader(source);
            var samplePos = source.Position;
            
            if (new string(reader.ReadChars(4)) == "S3V0")
            {
                int infoLength = reader.ReadInt32();
                int dataLength = reader.ReadInt32();
                reader.BaseStream.Position = samplePos + infoLength;
                byte[] wmaData = reader.ReadBytes(dataLength);
                File.WriteAllBytes("audio.asf",wmaData);
                
                using (var wmaStream = new WmaStream("audio.asf"))
                {
                    var wavData = new byte[wmaStream.Length];
                    
                        var size = wmaStream.Read(wavData, 0, (int)wmaStream.Length);
                        Debug.Assert(size == wmaStream.Length,"size == wmaStream.Length");
                        result.Data = wavData;
                        result.Format = wmaStream.Format;
                   
                }
                
            }

            return result;
        }
    }
}