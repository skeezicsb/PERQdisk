//
//  Program.cs
//
//  Author:  S. Boondoggle <skeezicsb@gmail.com>
//
//  Copyright (c) 2022-2023, Boondoggle Heavy Industries, Ltd.
//
//  This file is part of PERQdisk and/or PERQemu, originally written by
//  and Copyright (c) 2006, Josh Dersch <derschjo@gmail.com>
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.IO;
using System.Reflection;

using PERQemu;
using PERQmedia;

namespace PERQdisk
{
    static class PERQdisk
    {
        static PERQdisk()
        {
            // Some useful globals
            _baseDir = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            _baseDir = Path.GetDirectoryName(_baseDir);

            // Set a platform flag
            _hostIsUnix = (Environment.OSVersion.Platform == PlatformID.Unix ||
                           Environment.OSVersion.Platform == PlatformID.MacOSX);

            Version vers = Assembly.GetCallingAssembly().GetName().Version;
            _version = $"PERQdisk v{vers.Major}.{vers.Minor}.{vers.Build}";
        }


        public static void Main(string[] args)
        {
            // Parse command line arguments
            _switches = ParseArgs(args);

            if (_switches.printVersion)
            {
                Console.WriteLine(Version);
                return;
            }

            if (_switches.printHelp)
            {
                Console.WriteLine("Usage:  PERQdisk [-h] [-v] [-b] [-s <file>] [<disk>]");
                Console.WriteLine();
                Console.WriteLine("\t-h\tprint this help message");
                Console.WriteLine("\t-v\tprint version information");
                Console.WriteLine("\t-b\tbatch mode (ignore 'pause' in command files)");
                Console.WriteLine("\t-s file\tread startup commands from file");
                Console.WriteLine("\tdisk\tDisk image to load");
                return;
            }

            if (_switches.debug)
            {
                Log.Categories = Category.All;
            }

            if (_switches.batchMode)
            {
                _batchMode = true;
            }

            // Ack!  Make sure we have an actual console to work with
            if (Console.BufferWidth == 0)
            {
                // This may not actually be seen, but try logging it anyway
                Console.WriteLine("** Cannot run on zero-width console!  Please run PERQdisk");
                Console.WriteLine("   from a terminal or console with a reasonable geometry.");
                return;
            }

            // Set up command-line parser and GUI manager
            _cli = new CommandProcessor();

            Console.Title = _version;

            PrintBanner();

            // If a media file was specified, try loading it
            if (!string.IsNullOrEmpty(_switches.disk))
            {
                _cli.LoadImage(_switches.disk);
            }

            // If the user requested a start-up script, read it now
            if (!string.IsNullOrEmpty(_switches.runScript))
            {
                _cli.ReadScript(_switches.runScript);
            }

            // Run the CLI
            _cli.Run();
        }


        public static void PrintBanner()
        {
            Console.WriteLine(Version);
            Console.WriteLine(Copyright);
#if DEBUG
            Console.WriteLine();
            Console.WriteLine("[DEBUG version]");
            Console.WriteLine($"[Working directory is {Environment.CurrentDirectory}]");
            Console.WriteLine($"[Console is {Console.BufferWidth}x{Console.BufferHeight}]");
#endif
        }

        public static string Copyright = "Copyright (c) 2006-2023, J. Dersch (derschjo@gmail.com)\n" +
                                         "Rewritten in 2022 by S. Boondoggle (skeezicsb@gmail.com)";

        public static string Version => _version;
        public static string BaseDir => _baseDir;

        public static bool HostIsUnix => _hostIsUnix;
        public static bool BatchMode => _batchMode;

        public static CommandProcessor CLI => _cli;

        public static IDiskDevice Dev
        {
            get { return _dev; }
            set { _dev = value; }
        }

        public static StorageDevice Media => _dev?.Media;

        public static bool IsLoaded => (_dev != null && _dev.Media != null);

        /// <summary>
        /// Quick and dirty command line parsing.
        /// </summary>
        static CmdLineArgs ParseArgs(string[] args)
        {
            var sw = new CmdLineArgs();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-h")
                {
                    sw.printHelp = true;
                }
                else if (args[i] == "-v")
                {
                    sw.printVersion = true;
                }
                else if (args[i] == "-b")
                {
                    sw.batchMode = true;
                }
                else if (args[i] == "-d")
                {
                    Log.Level = sw.debug ? Severity.Verbose : Severity.Debug;
                    sw.debug = true;
                }
                else if (args[i] == "-s")
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.WriteLine("Missing script argument");
                        sw.printHelp = true;
                    }
                    else
                    {
                        i++;
                        sw.runScript = args[i];
                    }
                }
                else if (args[i].StartsWith("@", StringComparison.InvariantCulture))
                {
                    // "@foo" is alternate form of "-s foo"
                    sw.runScript = args[i].Substring(1);
                }
                else if (!args[i].StartsWith("-", StringComparison.InvariantCulture) &&
                         string.IsNullOrEmpty(sw.disk))
                {
                    sw.disk = args[i];
                }
                else
                {
                    Console.WriteLine("Unknown argument: " + args[i]);
                    sw.printHelp = true;
                }
            }
            return sw;
        }

        internal struct CmdLineArgs
        {
            public bool printHelp;
            public bool printVersion;
            public bool batchMode;
            public bool debug;
            public string disk;
            public string runScript;
        }

        static string _version;
        static string _baseDir;

        static bool _hostIsUnix;
        static bool _batchMode;

        static CmdLineArgs _switches;
        static CommandProcessor _cli;
        static IDiskDevice _dev;

    }
}
