using System;
using System.IO;

namespace BlakieLibAssetBuilder
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if(args.Length < 1)
            {
                Console.WriteLine("No args, please supply arg.");
                return;
            }

            switch(args[0])
            {
                default:
                    Console.WriteLine("Unknown arg");
                    return;
                case "-dpspr":
                    if (Directory.Exists(args[1]))
                        DPSpr.CreateDPSPR(args[1], args[2]);
                    break;
                case "-dparc":
                    if (Directory.Exists(args[1]))
                        DPArc.CreateDPArc(args[1], args[2]);
                    break;
            }
        }
    }
}
