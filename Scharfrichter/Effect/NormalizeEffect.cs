using System;
using System.Linq;
using NAudio.Utils;
using NAudio.Wave;

namespace Scharfrichter.Codec.Effect
{
    internal class NormalizeEffect:ISampleProvider
    {
        private readonly ISampleProvider sampleProvider;
        public float linearValue = 0.8f;
        public NormalizeEffect(ISampleProvider sampleProvider)
        {
            this.sampleProvider = sampleProvider;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            var size = sampleProvider.Read(buffer, offset, count);
            
            for (var i = 0; i < count; i++)
            {
                buffer[offset+i] = buffer[offset+i] * linearValue;
            }

            return size;
        }

        public WaveFormat WaveFormat => sampleProvider.WaveFormat;
    }
}