using System;
using System.IO;

namespace Bingosoft.Net.IfcMetadata;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (!TryParseArguments(args, out var ifcSourceFile, out var jsonTargetFile, out var preserveOrder))
        {
            PrintUsage();
            Environment.Exit(1);
        }

        if (!ifcSourceFile.Exists)
        {
            Console.WriteLine($"File: {ifcSourceFile} does not exist.");
            Environment.Exit(1);
        }

        try
        {
            IfcStreamingJsonExporter.Export(ifcSourceFile, jsonTargetFile, preserveOrder);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Environment.Exit(1);
        }
    }

    private static bool TryParseArguments(string[] args, out FileInfo ifcSourceFile, out FileInfo jsonTargetFile, out bool preserveOrder)
    {
        ifcSourceFile = null;
        jsonTargetFile = null;
        preserveOrder = true;

        string sourcePath = null;
        string targetPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--preserve-order":
                    {
                        if (i + 1 >= args.Length || !bool.TryParse(args[i + 1], out preserveOrder))
                        {
                            return false;
                        }

                        i++;
                        break;
                    }
                case "--no-preserve-order":
                    {
                        preserveOrder = false;
                        break;
                    }
                default:
                    {
                        if (arg.StartsWith("--", StringComparison.Ordinal))
                        {
                            return false;
                        }

                        if (sourcePath is null)
                        {
                            sourcePath = arg;
                            break;
                        }

                        if (targetPath is null)
                        {
                            targetPath = arg;
                            break;
                        }

                        return false;
                    }
            }
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return false;
        }

        ifcSourceFile = new FileInfo(sourcePath);
        jsonTargetFile = targetPath is null
            ? new FileInfo(Path.ChangeExtension(sourcePath, ".json"))
            : new FileInfo(targetPath);

        return true;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Please specify the path to the IFC and optional output json.");
        Console.WriteLine("Usage: ifc_metadata /path_to_file.ifc [/path_to_file.json] [--preserve-order true|false]");
        Console.WriteLine("Usage: ifc_metadata /path_to_file.ifc --no-preserve-order");
        Console.WriteLine("Default: preserve order is true.");
        Console.WriteLine("If output path is not passed, target defaults to source name with .json extension.");
    }
}