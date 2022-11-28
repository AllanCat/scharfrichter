using NAudio;
using NAudio.Wave;
using NAudio.Utils;
using Scharfrichter.Codec.Charts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.MediaFoundation;
using NAudio.Wave.Compression;
using NAudio.Wave.SampleProviders;
using Scharfrichter.Codec.Effect;


namespace Scharfrichter.Codec.Sounds
{
    static public class ChartRenderer
    {
        static private long GetIdentifier(Entry entry)
        {
            long result = (long) entry.Column;
            result <<= 32;
            result |= (long) (entry.Player) & 0xFFFFFFFFL;

            return result;
        }

        static private void Paste(byte[] sourceRendered, ref int[] target, Fraction offset, Fraction cutoffFraction,
            int bitPerSample)
        {
            if (sourceRendered == null)
                return;

            int sourceLength = sourceRendered.Length;

            int desiredOffset = (int) (offset * new Fraction(88200, 1));
            int desiredLength = (sourceRendered.Length / 2) + (int) desiredOffset;
            int cutoff = (int) (cutoffFraction * new Fraction(88200, 1));

            if (cutoff >= 0 && desiredOffset + (sourceLength / 4) > cutoff)
            {
                sourceLength = (cutoff - desiredOffset) * 4;
            }

            if (target.Length < desiredLength)
                Array.Resize(ref target, desiredLength);

            Int32 sourceSampleL = 0;
            Int32 sourceSampleR = 0;
            int sourceIndex = 0;
            int targetIndex = desiredOffset;

            if (bitPerSample == 16)
            {
                while (sourceIndex < sourceLength)
                {
                    sourceSampleL = sourceRendered[sourceIndex++];
                    sourceSampleL |= (int) (sourceRendered[sourceIndex++]) << 8;
                    sourceSampleL <<= 16;
                    sourceSampleL >>= 16;
                    sourceSampleR = sourceRendered[sourceIndex++];
                    sourceSampleR |= (int) (sourceRendered[sourceIndex++]) << 8;
                    sourceSampleR <<= 16;
                    sourceSampleR >>= 16;
                    target[targetIndex++] += sourceSampleL;
                    target[targetIndex++] += sourceSampleR;
                }
            }else if (bitPerSample == 24)
            {
                
            }
        }

        static public byte[] Render(Chart chart, Sound[] sounds, bool trimZero, int duration)
        {
            int[] outputSamples;
            var length = RenderChart(chart, sounds, out outputSamples);
            if (!trimZero) return WriteAdpcmWave(CutoffIntSamples(outputSamples), length);
            for (var i = 0; i < outputSamples.Length; i += 2)
            {
                if (outputSamples[i] != 0 || outputSamples[i + 1] != 0)
                {
                    return WriteAdpcmWave(CutoffIntSamples(outputSamples.Skip(i).ToArray()), duration * 44100 * 2);
                }
            }

            return WriteAdpcmWave(CutoffIntSamples(outputSamples), length);
        }

        static public byte[] RenderToAdpcm(Chart chart, Sound[] sounds, int startTime, int duration, bool crop = false)
        {
            int[] outputSamples;
            var format = new WaveFormat(44100, 2);
            var length = RenderChart(chart, sounds, out outputSamples, (startTime + duration + 10) * 1000);
            for (var i = 0; i < outputSamples.Length; i += 2)
            {
                if (outputSamples[i] != 0 || outputSamples[i + 1] != 0)
                {
                    var samples = outputSamples;
                    if (crop)
                    {
                        samples = outputSamples.Skip(i + startTime * 44100 * 2).Take(duration * 44100 * 2).ToArray();
                    }
                    PostProcess(samples, 15000);
                    return WriteAdpcmWave(CutoffIntSamples(samples), samples.Length);
                }
            }

            return WriteAdpcmWave(CutoffIntSamples(outputSamples), length);
        }


        static public byte[] RenderToWma(Chart chart, Sound[] sounds, int startTime, int duration, bool crop = false)
        {
            int[] outputSamples;
            var format = new WaveFormat(44100, 2);
            var length = RenderChart(chart, sounds, out outputSamples, (startTime + duration + 10) * 1000);
            for (var i = 0; i < outputSamples.Length; i += 2)
            {
                if (outputSamples[i] != 0 || outputSamples[i + 1] != 0)
                {
                    var samples = outputSamples;
                    if (crop)
                    {
                        samples = outputSamples.Skip(i + startTime * 44100 * 2).Take(duration * 44100 * 2).ToArray();
                    }
                    PostProcess2(samples);
                    return WriteWma(format, CutoffIntSamples(samples), samples.Length);
                }
            }
            return WriteWma(format, CutoffIntSamples(outputSamples), length);
        }

        private static object lockObj = new Object();

        private static byte[] WriteWma(WaveFormat format, short[] outputSamples, int length)
        {
            var media = MediaFoundationEncoder.SelectMediaType(AudioSubtypes.MFAudioFormat_WMAudioV9, format, 192000);
            var tempFile = Path.GetTempFileName() + ".asf";
            File.Delete(tempFile);
            lock (lockObj)
            {
                MediaFoundationApi.Startup();
            }

            using (var stream = GetWaveStream(outputSamples, length))
            using (var encoder = new MediaFoundationEncoder(media))
            {
                encoder.Encode(tempFile, stream);
            }

            var bytes = File.ReadAllBytes(tempFile);
            File.Delete(tempFile);
            return bytes;
        }


        static public byte[] Render(Chart chart, Sound[] sounds, int targetMilliSecond = Int32.MaxValue)
        {
            int[] outputSamples;
            var length = RenderChart(chart, sounds, out outputSamples, targetMilliSecond);

            return WriteWave(CutoffIntSamples(outputSamples), length);
        }

        static public short[] CutoffIntSamples(int[] sample)
        {
            return sample.Select(CutOff16Bit).ToArray();
        }

        private static int RenderChart(Chart chart, Sound[] sounds, out int[] outputSamples,
            int targetTimeMilliSecond = Int32.MaxValue)
        {
            Dictionary<long, Entry> lastNote = new Dictionary<long, Entry>();
            Dictionary<int, Fraction> noteCutoff = new Dictionary<int, Fraction>();
            Dictionary<int, byte[]> renderedSamples = new Dictionary<int, byte[]>();

            int[] buffer = new int[targetTimeMilliSecond == Int32.MaxValue
                ? 0
                : (int) (((targetTimeMilliSecond) / 1000 + 30) * 44100 * 2)];

            //chart.Entries.Reverse();

            foreach (Entry entry in chart.Entries)
            {
                if (entry.LinearOffset.Numerator > targetTimeMilliSecond) break;
                if (entry.Type == EntryType.Sample)
                {
                    lastNote[GetIdentifier(entry)] = entry;
                }
                else if (entry.Type == EntryType.Marker)
                {
                    Sound sound;

                    if (entry.Value.Numerator > 0)
                    {
                        byte[] soundData = null;
                        int soundIndex = (int) entry.Value - 1;
                        sound = sounds[(int) entry.Value - 1];

                        if (renderedSamples.ContainsKey(soundIndex))
                        {
                            soundData = renderedSamples[soundIndex];
                        }
                        else if (sound != null)
                        {
                            soundData = sound.Render(1.0f);
                            renderedSamples[soundIndex] = soundData;
                        }

                        Fraction cutoff = new Fraction(-1, 1);
                        if (sound.Channel >= 0 && noteCutoff.ContainsKey(sound.Channel))
                        {
                            cutoff = noteCutoff[sound.Channel];
                        }

                        if (soundData != null)
                        {
                            Paste(soundData, ref buffer, entry.LinearOffset * chart.TickRate, cutoff * chart.TickRate,
                                sound.Format.BitsPerSample);
                        }

                        if (sound.Channel >= 0)
                            noteCutoff[sound.Channel] = entry.LinearOffset;
                    }
                }
            }

            //chart.Entries.Reverse();

            int length = buffer.Length;
            outputSamples = buffer;
            int normalization = 1;
            long maxpressure = 0;
            var windowSize = 100;
            var energy = 0L;

            return length;
        }

        [ThreadStatic] private static byte[] postProcessSourceBuffer;
        [ThreadStatic] private static byte[] postProcessDestBuffer;
        private static void PostProcess2(int[] samples)
        {
            postProcessSourceBuffer = new byte[samples.Length * 4];
            postProcessDestBuffer = new byte[samples.Length * 4];
            //ResizeBuffer(ref postProcessSourceBuffer, samples.Length*4);
            //ResizeBuffer(ref postProcessDestBuffer, samples.Length*4);
            var min = int.MaxValue;
            var max = int.MinValue;
            
            foreach (var s in samples)
            {
                min = Math.Min(min, s);
                max = Math.Max(max, s);
            }

            if (max > short.MaxValue)
            {
                Console.WriteLine($"found max sample too large: {max}");
            }

            if (min < short.MinValue)
            {
                Console.WriteLine($"found min sample too small: {min}");
            }

            var absMax = Math.Max(Math.Abs(min), Math.Abs(max));
            if (max > short.MaxValue || min < short.MinValue)
            {
                var linearQuantizer = short.MaxValue / (double)absMax;
                for (var i = 0; i < samples.Length; i++)
                {
                    samples[i] = (int)Math.Round(samples[i]* linearQuantizer*0.95);
                }
            }
            
            Buffer.BlockCopy(samples,0,postProcessSourceBuffer,0,postProcessSourceBuffer.Length);
            var sampleProvider = CreatePostProcessSampleProvider(absMax);
            var size = sampleProvider.ToWaveProvider16().Read(postProcessDestBuffer, 0, postProcessDestBuffer.Length);
            //Debug.Assert(size*2 == postProcessSourceBuffer.Length, "size == postProcessSourceBuffer.Length");
            Buffer.BlockCopy(postProcessDestBuffer, 0, samples,0,size);
        }

        private static ISampleProvider CreatePostProcessSampleProvider(double maxSampleReference)
        {
            var waveStream = new RawSourceWaveStream(postProcessSourceBuffer, 0, postProcessSourceBuffer.Length,
                new WaveFormat(44100, 16, 2));
            //var normalizer = new NormalizeEffect(waveStream.ToSampleProvider()) {linearValue = 0.5f};
           // var preVolume = new VolumeSampleProvider(waveStream.ToSampleProvider()) {Volume = 1f};
            var limiter = new SoftLimiter(waveStream.ToSampleProvider());
            var boost = (float)(1/Math.Pow(Math.Log(maxSampleReference), 2)*500);
            Console.WriteLine($"maxSample: {maxSampleReference} boost: {boost}");
            limiter.Boost.CurrentValue = boost;
            limiter.Brickwall.CurrentValue = -0.1f;
            var volume = new VolumeSampleProvider(limiter) {Volume = 1};
            return volume;
        }

        private static void ResizeBuffer(ref byte[] buffer, int length)
        {
            if (buffer == null || buffer.Length < length)
            {
                buffer = new byte[length];
            }
        }

        private static void PostProcess(int[] samples, int targetPressure, int pass = 0)
        {
            var length = samples.Length;
            long energy = 0;
            long maxpressure = 0;
            var windowSize = 5000;
            var gain = 0f;
            var maxSample = 0;
            var burstCnt = 0;
            var buffer = new float[samples.Length];

            for (int i = 0; i < length; i++)
            {
                //energy++;
                var absSample = Math.Abs(samples[i]);
                energy += absSample;
                if (i - windowSize >= 0)
                {
                    energy -= Math.Abs(samples[i - windowSize]);
                    var pressure = energy / windowSize;
                    if (pressure > maxpressure)
                        maxpressure = pressure;
                }

                if (maxSample < absSample)
                    maxSample = absSample;
                if (absSample >= 32767)
                    burstCnt++;
                buffer[i] = samples[i] / 32767f;
            }

            if (pass == 0)
            {
                var factor = (float) 32767 / maxSample;
                if (maxSample > 32767)
                {
                    for (int i = 0; i < length; i++)
                    {
                        buffer[i] = (buffer[i] * factor);
                    }

                    gain = (float) Math.Log((double) targetPressure / (maxpressure * factor), 2);
                    gain = (gain + 1) * (gain + 1);
                }

                Console.WriteLine(
                    $"MaxSample: {maxSample} Pressure: {maxpressure} Burst: {burstCnt} Gain: {gain} factor: {factor}");
            }

            if (pass >= 1)
            {
                return;
            }


            var compressor = new FastAttackCompressor1175(44100);

            compressor.thresh = 0;
            compressor.makeup = (float) gain;
            compressor.Init();
            compressor.Apply();


            for (int i = 0; i < length; i += 2)
            {
                if (i + 1 >= length) break;
                var left = Limiter(buffer[i]);
                var right = Limiter(buffer[i + 1]);
                compressor.Sample(ref left, ref right);
                samples[i] = (int) (Limiter(left) * 32767);
                samples[i + 1] = (int) (Limiter(right) * 32767);
            }

            PostProcess(samples, targetPressure, ++pass);
        }

        private static short CutOff16Bit(float currentSample)
        {
            if (currentSample > 32767) currentSample = 32767;
            if (currentSample < -32768) currentSample = -32768;
            return (short) currentSample;
        }

        private static short CutOff16Bit(int currentSample)
        {
            if (currentSample > 32767) currentSample = 32767;
            if (currentSample < -32768) currentSample = -32768;
            return (short) currentSample;
        }

        private static float Limiter(float sample)
        {
            const float LIM_THRESH = 0.9f;
            const float LIM_RANGE = (1f - LIM_THRESH);
            const float M_PI_2 = (float) (Math.PI / 2);
            float res;
            if ((LIM_THRESH < sample))
            {
                res = (sample - LIM_THRESH) / LIM_RANGE;
                res = (float) ((Math.Atan(res) / M_PI_2) * LIM_RANGE + LIM_THRESH);
            }
            else if ((sample < -LIM_THRESH))
            {
                res = -(sample + LIM_THRESH) / LIM_RANGE;
                res = -(float) ((Math.Atan(res) / M_PI_2) * LIM_RANGE + LIM_THRESH);
            }
            else
            {
                res = sample;
            }

            return res;
        }

        private static byte[] WriteWave(short[] outputSamples, int length)
        {
            var zero = new float[4096];
            using (MemoryStream mem = new MemoryStream())
            {
                using (WaveFileWriter writer = new WaveFileWriter(new IgnoreDisposeStream(mem),
                    WaveFormat.CreateCustomFormat(WaveFormatEncoding.Pcm, 44100, 2, 44100 * 4, 4, 16)))
                {
                    writer.WriteSamples(outputSamples, 0, length);
                    writer.WriteSamples(zero, 0, 4096 - length % 4096);
                }

                mem.Flush();
                return mem.ToArray();
            }
        }

        private static WaveStream GetWaveStream(short[] outputSamples, int length)
        {
            MemoryStream mem = new MemoryStream();
            {
                var customFormat = WaveFormat.CreateCustomFormat(WaveFormatEncoding.Pcm, 44100, 2, 44100 * 4, 4, 16);
                using (WaveFileWriter writer = new WaveFileWriter(new IgnoreDisposeStream(mem),
                    customFormat))
                {
                    writer.WriteSamples(outputSamples, 0, length);
                }

                mem.Flush();
                mem.Seek(0, SeekOrigin.Begin);
                var waveFileReader = new WaveFileReader(mem);
                return waveFileReader;
            }
        }


        private static byte[] ConvertToAdpcm(byte[] wavFile)
        {
            var wavformat = new AdpcmWaveFormat(44100, 2);
            var buffer = new byte[4096];
            var zero = new byte[4096];
            using (var inputStream = new MemoryStream(wavFile))
            using (var wavStream = new WaveFileReader(inputStream))
            using (MemoryStream mem = new MemoryStream())
            {
                var length = 0;
                using (var conversionStream = new WaveFormatConversionStream(wavformat, wavStream))
                using (WaveFileWriter writer = new WaveFileWriter(new IgnoreDisposeStream(mem), wavformat))
                {
                    while (true)
                    {
                        var readCount = conversionStream.Read(buffer, 0, buffer.Length);
                        if (readCount == 0)
                        {
                            break;
                        }

                        writer.Write(buffer, 0, readCount);
                        length += readCount;
                    }
                }

                mem.Flush();
                return mem.GetBuffer().Take(length).ToArray();
            }
        }

        static byte[] WriteAdpcmWave(short[] outputSamples, int length)
        {
            return ConvertToAdpcm(WriteWave(outputSamples, length));
        }
    }
}