using Scharfrichter.Codec;
using Scharfrichter.Codec.Archives;
using Scharfrichter.Codec.Charts;
using Scharfrichter.Codec.Sounds;
using Scharfrichter.Common;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ConvertHelper
{
    public enum ChartDifficulty
    {
        SPH,
        SPN,
        SPA,
        DPH = 6,
        DPN,
        DPA
    }
    public class RenderResult
    {
        public readonly List<ChartDifficulty> Difficulties = new List<ChartDifficulty>();
        public string OutputFileName;
    }

    static public class Render
    {
        static public RenderResult[] RenderWAV(string[] inArgs, long unitNumerator, long unitDenominator)
        {
            Splash.Show("Render");
            Console.WriteLine("Timing: " + unitNumerator.ToString() + "/" + unitDenominator.ToString());

            string[] args;

            if (inArgs.Length > 0)
                args = Subfolder.Parse(inArgs);
            else
                args = inArgs;

            if (System.Diagnostics.Debugger.IsAttached && args.Length == 0)
            {
                Console.WriteLine();
                Console.WriteLine("Debugger attached. Input file name:");
                args = new string[] { Console.ReadLine() };
            }

            if (args.Length == 0)
            {
                Console.WriteLine();
                Console.WriteLine("Usage: Render2DX <files..>");
                Console.WriteLine();
                Console.WriteLine("Drag and drop with files and folders is fully supported for this application.");
                Console.WriteLine();
                Console.WriteLine("You must have both the chart file (.1) and the sound file (.2dx).");
                Console.WriteLine("Supported formats:");
                Console.WriteLine("1, 2DX");
            }

            Sound[] sounds = null;
            Chart[] charts = null;
            bool cancel = false;
            string outFile = null;
            List<RenderResult> results = new List<RenderResult>();
            foreach (string filename in args)
            {
                if (cancel)
                    break;

                if (File.Exists(filename))
                {
                    switch (Path.GetExtension(filename).ToUpper())
                    {
                        case @".1":
                            if (charts == null)
                            {
                                Console.WriteLine();
                                Console.WriteLine("Valid charts:");
                                outFile = Path.Combine(Path.GetDirectoryName(filename), Path.GetFileNameWithoutExtension(filename));
                                using (MemoryStream mem = new MemoryStream(File.ReadAllBytes(filename)))
                                {
                                    charts = Bemani1.Read(mem, unitNumerator, unitDenominator).Charts;
                                    for (int i = 0; i < charts.Length; i++)
                                    {
                                        if (charts[i] != null)
                                            Console.Write(i.ToString() + "  ");
                                    }
                                }
                                Console.WriteLine();
                            }
                            break;
                        case @".2DX":
                            if (sounds == null)
                            {
                                using (var stream = File.OpenRead(filename))
                                {
                                    sounds = Bemani2DX.Read(stream).Sounds;
                                }
                            }
                            break;
                        case @".S3P":
                            if (sounds == null)
                            {
                                using (var stream = File.OpenRead(filename))
                                {
                                    sounds = Bemani2DX.Read(stream).Sounds;
                                }
                            }
                            break;
                    }
                }
            }

            if (!cancel && (sounds != null) && (charts != null))
            {
                List<byte[]> rendered = new List<byte[]>();
                List<int> renderedIndex = new List<int>();

                for (int k = 0; k < charts.Length; k++)
                {
                    Chart chart = charts[k];

                    if (chart == null)
                        continue;

                    Console.WriteLine("Rendering " + k.ToString());
                    byte[] data = ChartRenderer.Render(chart, sounds);

                    int renderedCount = rendered.Count;
                    int matchIndex = -1;
                    bool match = false;

                    for (int i = 0; i < renderedCount; i++)
                    {
                        int renderedLength = rendered[i].Length;
                        if (renderedLength == data.Length)
                        {
                            byte[] renderedBytes = rendered[i];
                            match = true;
                            for (int j = 0; j < renderedLength; j++)
                            {
                                if (renderedBytes[j] != data[j])
                                {
                                    match = false;
                                    break;
                                }
                            }
                            if (match)
                            {
                                matchIndex = i;
                                break;
                            }
                        }
                    }

                    if (!match)
                    {
                        Console.WriteLine("Writing unique " + k.ToString());
                        
                        var path = outFile + "-" + Util.ConvertToDecimalString(k, 2) + ".wav";
                        File.WriteAllBytes(path, data);
                        rendered.Add(data);
                        renderedIndex.Add(k);
                        var result = new RenderResult();
                        result.Difficulties.Add((ChartDifficulty)k);
                        result.OutputFileName = path;
                        results.Add(result);
                    }
                    else
                    {
                        Console.WriteLine("Matches " + renderedIndex[matchIndex].ToString());
                        var difficulty = (ChartDifficulty)k;
                        var renderedResult = results.Find(r => r.Difficulties.Contains((ChartDifficulty)matchIndex));
                        Debug.Assert(renderedResult!=null,"renderedResult!=null");
                        renderedResult.Difficulties.Add(difficulty);
                    }
                }
            }
            return results.ToArray();
        }

        private static object lockObj = new object();

        public enum Codec
        {
            Wma,
            Adpcm
        }
        static public byte[] RenderPreview(string[] inArgs, long unitNumerator, long unitDenominator, Codec codec = Codec.Wma)
        {
            //Splash.Show("Render");
            //Console.WriteLine("Timing: " + unitNumerator.ToString() + "/" + unitDenominator.ToString());

            string[] args;

           // if (inArgs.Length > 0)
           //     args = Subfolder.Parse(inArgs);
          //  else
                args = inArgs;

            if (args.Length == 0)
            {
                Console.WriteLine();
                Console.WriteLine("Usage: Render2DX <files..>");
                Console.WriteLine();
                Console.WriteLine("Drag and drop with files and folders is fully supported for this application.");
                Console.WriteLine();
                Console.WriteLine("You must have both the chart file (.1) and the sound file (.2dx).");
                Console.WriteLine("Supported formats:");
                Console.WriteLine("1, 2DX");
            }

            Sound[] sounds = null;
            Chart[] charts = null;
            bool cancel = false;
            string outFile = null;
            List<RenderResult> results = new List<RenderResult>();
            foreach (string filename in args)
            {
                if (cancel)
                    break;

                if (File.Exists(filename))
                {
                    switch (Path.GetExtension(filename).ToUpper())
                    {
                        case @".1":
                            if (charts == null)
                            {
                                //Console.WriteLine();
                                Console.WriteLine("Valid charts: "+filename);
                                outFile = Path.Combine(Path.GetDirectoryName(filename), Path.GetFileNameWithoutExtension(filename));
                                using (MemoryStream mem = new MemoryStream(File.ReadAllBytes(filename)))
                                {
                                    charts = Bemani1.Read(mem, unitNumerator, unitDenominator).Charts;
                                }
                                //Console.WriteLine();
                            }
                            break;
                        case @".2DX":
                            if (sounds == null)
                            {
                                Console.WriteLine("Read Sound: " + filename);
                                using (var mem = File.OpenRead(filename))
                                {
                                    sounds = Bemani2DX.Read(mem).Sounds;
                                }
                            }
                            break;
                        case @".S3P":
                            if (sounds == null)
                            {
                                Console.WriteLine("Read Sound: " + filename);
                                //lock(lockObj)
                                using(var mem = File.OpenRead(filename))
                                {
                                    sounds = Bemani2DX.Read(mem).Sounds;
                                }
                            }
                            break;
                    }
                }
            }

            if (!cancel && (sounds != null) && (charts != null))
            {
                for (int k = 0; k < charts.Length; k++)
                {
                    Chart chart = charts[k];

                    if (chart == null)
                        continue;
                    try
                    {
                        //Console.WriteLine("Rendering " + k.ToString());
                        if(codec == Codec.Wma) return ChartRenderer.RenderToWma(chart, sounds, 50, 10);
                        else if(codec == Codec.Adpcm) return ChartRenderer.RenderToAdpcm(chart, sounds, 50, 10);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        Console.WriteLine(e.StackTrace);
                        return null;
                    }
                }
            }
            sounds = null;
            throw new FileNotFoundException("No chart to render");

        }
    }
}
