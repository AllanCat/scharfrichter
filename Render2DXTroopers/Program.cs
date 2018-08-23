using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Render2DXTroopers
{
    class Program
    {
        static void Main(string[] args)
        {
            // debug args (if applicable)
            if (System.Diagnostics.Debugger.IsAttached && args.Length == 0)
            {
                Console.WriteLine();
                Console.WriteLine("Debugger attached. Input file name:");
                string baseName = Console.ReadLine();
                args = new string[] { baseName + ".1", baseName + ".2dx",baseName+".s3p" };
            }

           var results  = ConvertHelper.Render.RenderWAV(args, 1, 1000);
            foreach (var renderResult in results)
            {
                Console.WriteLine($"{string.Join(",", renderResult.Difficulties)}-->{renderResult.OutputFileName}");
                File.Move(renderResult.OutputFileName,
                    $"{renderResult.OutputFileName.Substring(0,renderResult.OutputFileName.IndexOf('-'))}" +
                    $" {string.Join(",",renderResult.Difficulties)}.wav");
            }
           
        }
    }
}
