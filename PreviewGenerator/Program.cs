using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConvertHelper;
using Scharfrichter.Codec.Sounds;

namespace PreviewGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            
            Preview(args);
            //SingleFile(args);
        }

        private static void SingleFile(string[] args)
        {
            //var args = new string[2];
            //args[0] = @"‪Q:\LDJ-20180724\data\sound\00000\00000.1";
           // args[1] = @"‪Q:\LDJ-20180724\data\sound\00000\00000.2dx";
            var bytes =  ConvertHelper.Render.RenderPreview(args, 1, 1000);
            File.WriteAllBytes("tmp.wma",bytes);
        }

        private static void Preview(string[] args)
        {
            if (args.Length == 3 && args.Contains("-d"))
            {
                var source = args[0];
                var output = args[1];
                var removeDir = Directory.EnumerateDirectories(source).Where((directory) =>
                {
                    var info = new DirectoryInfo(directory);
                    var chartFile = new FileInfo($"{directory}\\{info.Name}.1");
                    var previewFile = new FileInfo($"{directory}\\{info.Name}_pre.2dx");
                    var sound2DX = new FileInfo($"{directory}\\{info.Name}.2dx");
                    var soundS3P = new FileInfo($"{directory}\\{info.Name}.s3p");
                    var outputDir = $"{output}\\{info.Name}";
                    var outPath = $"{output}\\{info.Name}\\{info.Name}_pre.2dx";
                    var outPath2 = $"{output}\\{info.Name}\\{info.Name}_pre.asf";
                    return chartFile.Exists && (sound2DX.Exists || soundS3P.Exists) && (File.Exists(outPath) || File.Exists(outPath2));
                }).ToArray();
                foreach (var s in removeDir)
                {
                    var info = new DirectoryInfo(output);
                    var previewName = $"{s}\\{Path.GetFileName(s)}_pre.2dx";
                    //Console.WriteLine(previewName);
                    var previewFile = new FileInfo(previewName);
                    if (File.Exists(previewFile.FullName))
                    {
                        Console.WriteLine($"Remove {previewFile.FullName}");
                        File.Delete(previewFile.FullName);
                    }
                }
                return;
            }
            if (args.Length != 3)
            {
                Console.WriteLine("Usage: arg1: sound folder arg2:outputFolder arg3: s3p/2dx");
                return;
            }
            var soundFolder = args[0];
            var outputFolder = args[1];

            var dirs = Directory.EnumerateDirectories(soundFolder).Where((directory) =>
            {
                var info = new DirectoryInfo(directory);
                var chartFile = new FileInfo($"{directory}\\{info.Name}.1");
                var previewFile = new FileInfo($"{directory}\\{info.Name}_pre.2dx");
                var sound2DX = new FileInfo($"{directory}\\{info.Name}.2dx");
                var soundS3P = new FileInfo($"{directory}\\{info.Name}.s3p");
                var outputDir = $"{outputFolder}\\{info.Name}";
                var outPath = $"{outputFolder}\\{info.Name}\\{info.Name}_pre.2dx";
                return chartFile.Exists && (sound2DX.Exists || soundS3P.Exists) && !File.Exists(outPath);
            }).ToArray();
            var option = new ParallelOptions();
            option.MaxDegreeOfParallelism = 12;
            Parallel.ForEach(dirs, option, (dir) =>
            {

                if(args[2]=="s3p")
                    GeneratePreview(dir, outputFolder, Render.Codec.Wma);
                else if(args[2]=="2dx")
                    GeneratePreview(dir, outputFolder, Render.Codec.Adpcm);
                else throw new ArgumentException("arg3: 2dx/s3p");
            });
        }

        private static void GeneratePreview(string directory, string outputFolder, Render.Codec codec)
        {
            var param = new string[2];
            var info = new DirectoryInfo(directory);
            var chartFile = new FileInfo($"{directory}\\{info.Name}.1");
            var previewFile = new FileInfo($"{directory}\\{info.Name}_pre.2dx");
            var sound2DX = new FileInfo($"{directory}\\{info.Name}.2dx");
            var soundS3P = new FileInfo($"{directory}\\{info.Name}.s3p");
            if (chartFile.Exists && (sound2DX.Exists || soundS3P.Exists))
            {
                if (previewFile.Exists)
                {
                    Console.WriteLine($"Skip: {directory}");
                    return;
                }
                var outputDir = $"{outputFolder}\\{info.Name}";
                var outPath = $"{outputFolder}\\{info.Name}\\{info.Name}_pre.2dx";
                Directory.CreateDirectory(outputDir);
                param[0] = chartFile.FullName;
                param[1] = soundS3P.Exists ? soundS3P.FullName : sound2DX.FullName;
#if DEBUG
                RenderSoundDebug(param, outPath);
#else
                if(codec==Render.Codec.Wma)
                    RenderSound(param, outPath);
                else if(codec == Render.Codec.Adpcm)
                    RenderSoundLegacy(param, outPath);
                
#endif
                GC.Collect();
                return;
            }
            Console.WriteLine($"Skip: {directory}");
        }

        private static void RenderSound(string[] args, string fileName)
        {
            try
            {
                var bytes = ConvertHelper.Render.RenderPreview(args, 1, 1000);
                if (bytes == null)
                {
                    Console.WriteLine($"Generate {fileName} failed.");
                    return;
                }
                File.WriteAllBytes(fileName+".asf", bytes);
                var s3v = new S3VSound(bytes);
                var s3p = new S3PPack();
                s3p.Add(s3v);
                File.WriteAllBytes(fileName, s3p.Pack());

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
            
        }

        private static void RenderSoundLegacy(string[] args, string fileName)
        {
            try
            {
                var bytes = ConvertHelper.Render.RenderPreview(args, 1, 1000,Render.Codec.Adpcm);
                if (bytes == null)
                {
                    Console.WriteLine($"Generate {fileName} failed.");
                    return;
                }
                var soundElement = new SoundElement(bytes);
                var packer = new Sound2DXPacker();
                packer.Add(soundElement);
                File.WriteAllBytes(fileName, packer.Pack());

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        private static void RenderSoundDebug(string[] args, string fileName)
        {
            try
            {
                var bytes = ConvertHelper.Render.RenderPreview(args, 1, 1000);
                if (bytes == null)
                {
                    Console.WriteLine($"Generate {fileName} failed.");
                    return;
                }
                fileName = fileName.Replace(".2dx", ".asf");
                
                File.WriteAllBytes(fileName, bytes);

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }
    }
}
