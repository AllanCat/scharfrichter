using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
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
                        volTab[i] = (float) Math.Pow(10.0f, (-36.0f * i / 64f) / 20.0f);
                }

                return volTab;
            }
        }

        [ThreadStatic] private static byte[] ReadBuffer;

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
                var tempFile = Path.GetTempFileName();
               
                File.WriteAllBytes(tempFile, wmaData);
                //var wmaTemp = tempFile+".wma";
                //File.Move(tempFile, wmaTemp);

                using (var wmaStream = new MediaFoundationReader(tempFile))
                {
                    var sampleProvider = new SampleToWaveProvider16(wmaStream.ToSampleProvider());
                    if (ReadBuffer == null || ReadBuffer.Length < wmaStream.Length)
                    {
                        ReadBuffer = new byte[wmaStream.Length];
                    }
                   
                    var size = sampleProvider.Read(ReadBuffer, 0, (int) wmaStream.Length);
                    //Debug.Assert(size == wmaStream.Length,"size == wmaStream.Length");
                    
                    
                    var waveData = new byte[size];
                    Array.Copy(ReadBuffer, 0, waveData, 0, size);
                    result.Data = waveData;
                    result.Format = sampleProvider.WaveFormat;
                    
                }
                File.Delete(tempFile);
            }

            return result;
        }
    }
}