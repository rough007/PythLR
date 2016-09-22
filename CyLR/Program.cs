﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CyLR.read;
using CyLR.write;
using DiscUtils;

namespace CyLR
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            Arguments arguments;
            try
            {
                arguments = new Arguments(args);
            }
            catch (ArgumentException e)
            {
                Console.WriteLine(e.Message);
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unknown error while parsing arguments: {e.Message}");
                return;
            }

            if (arguments.HelpRequested)
            {
                Console.WriteLine(arguments.GetHelp(arguments.HelpTopic));
                return;
            }

            string[] paths;
            try
            {
                paths = CollectionPaths.GetPaths(arguments);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error occured while collecting files:\n{e}");
                return;
            }

            
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                var system = FileSystem.GetFileSystem('C', FileAccess.Read);

                var files = paths.SelectMany(path => system.GetFilesFromPath(path));

                if (arguments.SFTPCheck)
                {
                    var archiveStream = arguments.SFTPInMemory ? new MemoryStream() : OpenFileStream(system, $@"{arguments.OutputPath}\{Environment.MachineName}.zip");
                    using (archiveStream)
                    {
                        int port;
                        string[] server = arguments.SFTPServer.Split(':');
                        try
                        {
                            port = Int32.Parse(server[1]);
                        }
                        catch (Exception)
                        {
                            port = 22;
                        }

                        files.CollectFilesToArchive(archiveStream);

                        archiveStream.Seek(0, SeekOrigin.Begin); //rewind the stream

                        Sftp.SendUsingSftp(archiveStream, server[0], port, arguments.UserName, arguments.UserPassword, $@"{arguments.OutputPath}/{Environment.MachineName}.zip");
                    }
                }
                else
                {
                    var zipPath = $@"{arguments.OutputPath}\{Environment.MachineName}.zip";
                    var archiveStream = OpenFileStream(system, zipPath);
                    using (archiveStream)
                    {
                        files.CollectFilesToArchive(archiveStream);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error occured while collecting files:\n{e}");
            }

            stopwatch.Stop();
            Console.WriteLine("Extraction complete. {0} elapsed", new TimeSpan(stopwatch.ElapsedTicks).ToString("g"));
        }

        private static Stream OpenFileStream(IFileSystem system, string path)
        {
            var archiveFile = new FileInfo(path);
            if (archiveFile.Directory != null && !archiveFile.Directory.Exists)
            {
                archiveFile.Directory.Create();
            }
            return File.Open(archiveFile.FullName, FileMode.Create, FileAccess.ReadWrite); //TODO: Replace with non-api call
        }
    }
}
