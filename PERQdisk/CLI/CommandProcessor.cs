//
//  CommandProcessor.cs
//
//  Author:  S. Boondoggle <skeezicsb@gmail.com>
//
//  Copyright (c) 2022, Boondoggle Heavy Industries, Ltd.
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
using System.Text;
using System.Collections.Generic;

using PERQemu;
using PERQmedia;

namespace PERQdisk
{
    public enum Doit
    {
        Yes = 0,
        No = 1,
        Default = 2,
        Quit = 3
    }

    /// <summary>
    /// CommandProcessor is the top-level console command interpreter.
    /// It wraps the CommandExecutor which does the dirty work.
    /// </summary>
    public class CommandProcessor
    {
        public CommandProcessor()
        {
            // Catch interrupts in the console
            Console.CancelKeyPress += OnCtrlC;

            // List of classes to interrogate for CommandAttributes
            var commandObjects = new List<object> {
                this,
                new POS.Commands(),
                new RT11.Commands(),
                new MediaCommands(),
                new DebugCommands()
            };

            // Build the tree
            _exec = new CommandExecutor(commandObjects);

            // Start up the line editor
            _editor = new CommandPrompt(_exec.CommandTreeRoot);
        }


        /// <summary>
        /// Enter a subsystem.
        /// </summary>
        public void SetPrefix(string pfx)
        {
            _exec.CurrentRoot = _editor.SetPrefix(pfx);
        }

        /// <summary>
        /// Return to the top-level prompt.
        /// </summary>
        public void ResetPrefix()
        {
            _exec.CurrentRoot = _editor.ResetPrefix();
        }

        /// <summary>
        /// Sets the prompt (separate from prefix).
        /// </summary>
        public void SetPrompt(string prompt)
        {
            _editor.Prompt = prompt;
        }

        /// <summary>
        /// Catch ^C on the console and return to the prompt.
        /// </summary>
        void OnCtrlC(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;

            // Abort command, go back to top level the quick & dirty way... :-)
            throw new Exception("^C");
        }

        /// <summary>
        /// Read a script or configuration file, quiet-like.  Always begins
        /// execution at the command tree root, restores prefix when done.
        /// </summary>
        public void ReadScript(string script)
        {
            var curPrefix = _editor.CurrentPrefix;

            try
            {
                ResetPrefix();
                _exec.ExecuteScript(Paths.Canonicalize(script));
                SetPrefix(curPrefix);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to read {script}: {e.Message}");
            }
            finally
            {
                // Restore our prefix
                SetPrefix(curPrefix);
            }
        }

        /// <summary>
        /// Run the main CLI loop.
        /// </summary>
        public void Run()
        {
            // Here we go!
            _running = true;

            while (_running)
            {
                try
                {
                    string cmd = _editor.GetLine();

                    if (cmd != string.Empty)
                    {
                        _exec.ExecuteLine(cmd);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.InnerException?.Message);
                }
            }
        }


        /// <summary>
        /// A hack to run a command without having to root around in the tree
        /// or instantiating the command's class to find the method.  Oof.
        /// </summary>
        private void RunCommand(string cmd)
        {
            try
            {
                _exec.ExecuteLine(cmd);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.InnerException?.Message);
            }
        }


        #region CLI Utility Routines

        /// <summary>
        /// Print a nice columnar list of (reasonably short) strings.
        /// </summary>
        public void Columnify(string[] items, int leftCol = 4, int tabWidth = 15)
        {
            Console.Write(" ".PadLeft(leftCol));
            var col = leftCol;

            foreach (var i in items)
            {
                if (col + tabWidth > Console.BufferWidth)
                {
                    Console.WriteLine();
                    Console.Write(" ".PadLeft(leftCol));
                    col = leftCol;
                }

                // If you want to print long strings, pass a wider tabWidth.
                // But catch the occasional edge case and try to make it pretty.
                if (i.Length > tabWidth)
                {
                    var extra = i.Length % tabWidth;

                    Console.Write(i);
                    col += i.Length;

                    if ((col + tabWidth - extra) < Console.BufferWidth)
                    {
                        Console.Write(" ".PadRight(tabWidth - extra));
                        col += tabWidth - extra;
                    }
                }
                else
                {
                    Console.Write(i.PadRight(tabWidth));
                    col += tabWidth;
                }
            }
            Console.WriteLine();
        }

        /// <summary>
        /// Prompt for a yes or no response.  If "strict", repeat until a valid
        /// response (yes, no, quit) is given; otherwise any non-response is
        /// taken to mean "no".  If "quiet" don't echo the response.
        /// </summary>
        public Doit Yes(string prompt, bool strict = false, bool quiet = false, bool reverse = false)
        {
            // Would be awfully nice if there was a "Console.ClearInputLine()"
            var clear = "\r" + " ".PadLeft(prompt.Length) + "\r";

            while (true)
            {
                if (reverse)
                {
                    Console.BackgroundColor = ConsoleColor.Green;
                    Console.ForegroundColor = ConsoleColor.Black;
                }
                Console.Write(prompt);
                Console.ResetColor();

                var key = Console.ReadKey(true).KeyChar;

                if (key == 'y' || key == 'Y' || (!strict && key == ' '))
                {
                    Console.Write(quiet ? clear : "Yes\n");
                    return Doit.Yes;
                }

                if (key == 'n' || key == 'N')
                {
                    Console.Write(quiet ? clear : "No\n");
                    return Doit.No;
                }

                if (key == CR || key == LF)
                {
                    Console.Write(quiet ? clear : "\n");
                    return Doit.Default;
                }

                if (key == 'q' || key == 'Q')
                {
                    Console.WriteLine(quiet ? clear : "Quit\n");
                    return Doit.Quit;
                }

                if (!strict)
                {
                    Console.Write(quiet ? clear : "\n");
                    return Doit.No;
                }

                Console.WriteLine("xxx  (Please answer 'y' or 'n')");
            }
        }

        /// <summary>
        /// Do "more mode" on a file.  If it looks like a binary, ask the user
        /// first (test for >10% non-printing chars in first 1K?).  Converts any
        /// non-printing chars to a reasonable form/reverse video so they don't
        /// cause terminal glitches or excessive beeps.
        /// </summary>
        public Doit Paginate(string fileName, byte[] fileData, bool pause = true)
        {
            if (fileData.Length == 0)
            {
                Console.WriteLine($"{fileName} is empty.");
                return Doit.No;
            }

            var bin = IsBinaryFile(fileData);

            if (bin)
            {
                var check = Yes($"** {fileName} looks like a binary file.  Continue [n]? ", true);
                if (check != Doit.Yes)
                    return check;
            }

            Console.WriteLine($"--- {fileName} ---");

            var pct = 0;
            var row = 1;
            var sb = new StringBuilder();

            foreach (var b in fileData)
            {
                pct++;

                // Accumulate characters and look for line endings
                switch (b)
                {
                    case LF:
                        break;

                    case CR:
                        sb.Append(bin ? "^M" : "");
                        continue;

                    default:
                        if (char.IsControl((char)b))
                        {
                            sb.Append('^');
                            sb.Append((b & 0x7f) == 0x7f ? '?' : (char)((b & 0x7f) + '@'));
                        }
                        else
                        {
                            sb.Append((char)b);
                        }
                        continue;
                }

                // Here at the end of a line!
                var line = sb.ToString();
                row += (line.Length / Console.BufferWidth) + 1;

                // Pause on ^L or if we've done a page full
                if (pause && ((line == "^L" && !bin) || row >= Console.BufferHeight - 1))
                {
                    // Update the prompt and pause
                    var progress = (pct * 100 / fileData.Length);
                    var prompt = $" {fileName} {progress}% -- More -- ";

                    var more = Yes(prompt, false, true, true);

                    if (more == Doit.No || more == Doit.Quit)
                    {
                        Console.WriteLine();
                        return more;
                    }

                    // Default does one more line; Yes does another page
                    row = (more == Doit.Default ? Console.BufferHeight : 0);
                }

                // This is a little cheesy
                if (line == "^L" && !bin)
                {
                    Console.Clear();
                    Console.BackgroundColor = ConsoleColor.Green;
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.WriteLine(line);
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine(line);
                }
                sb.Clear();
            }

            // Incomplete last line?
            if (sb.Length > 0) Console.WriteLine(sb);

            Console.WriteLine();
            return Doit.Yes;
        }


        /// <summary>
        /// Return true if a buffer appears to contain binary data (has > 10%
        /// unprintable characters).  Not foolproof but good enough.
        /// </summary>
        public bool IsBinaryFile(byte[] buf)
        {
            var len = Math.Min(buf.Length, 1024);
            var count = 0;

            for (var i = 0; i < len; i++)
            {
                // Don't count these as non-printable
                if (buf[i] == CR || buf[i] == LF || buf[i] == FF) continue;

                if (char.IsControl((char)buf[i])) count++;
            }

            return (count > len * .1);
        }

        public bool IsPrintable(char c)
        {
            return (char.IsLetterOrDigit(c) ||
                    char.IsSymbol(c) ||
                    char.IsPunctuation(c));
        }

        public void UpdateKeywordMatchHelpers(string key, string[] values)
        {
            _exec.UpdateKeywordMatchHelpers(key, values);
        }

        #endregion

        #region Top-level commands

        [Command("about", "About PERQdisk")]
        private void About()
        {
            PERQdisk.PrintBanner();
        }

        [Command("commands", "Show console commands and their descriptions")]
        public void ShowCommands()
        {
            _exec.ShowCommands("root");
        }

        public void ShowCommands(string prefix)
        {
            _exec.ShowCommands(prefix);
        }

        [Command("pwd", "Show the local (host) working directory")]
        [Command("pos pwd")]
        [Command("rt11 pwd")]
        public void LocalDirectory()
        {
            Console.WriteLine($"Current working directory is {Environment.CurrentDirectory}");
        }

        [Command("cd", "Change local (host) working directory")]
        [Command("pos cd")]
        [Command("rt11 cd")]
        public void LocalDirectory([PathExpand] string dir)
        {
            if (System.IO.Directory.Exists(dir))
            {
                Environment.CurrentDirectory = dir;
            }
            else
            {
                Console.WriteLine($"No such directory: {dir}");
            }
        }

        [Command("load", "Load a media file")]
        public void LoadImage([PathExpand] string path)
        {
            IDiskDevice dev;

            path = Paths.FindFileInPath(path, ".", FileUtilities.KnownExtensions);

            var type = FileUtilities.GetDeviceTypeFromFile(path);

            switch (type)
            {
                case DeviceType.Floppy:
                    //
                    // For floppies, determine what kind of volume it is.
                    // Check for POS first, since an empty boot area might be
                    // mistaken for a blank RT11 data floppy!?
                    //
                    dev = new POS.Device();

                    if (dev.Probe(path))
                    {
                        if (dev.Mount(path))
                        {
                            PERQdisk.Dev = dev;
                            RunCommand("pos");
                            return;
                        }
                        // Fall through to check for an RT11 volume
                    }

                    dev = new RT11.Device();

                    if (dev.Probe(path))
                    {
                        if (dev.Mount(path))
                        {
                            PERQdisk.Dev = dev;
                            RunCommand("rt11");
                            return;
                        }
                    }

                    Console.WriteLine("Could not determine volume type; Are you sure this a PERQ floppy?");
                    break;

                case DeviceType.Disk14Inch:
                case DeviceType.Disk8Inch:
                case DeviceType.Disk5Inch:
                    //
                    // Only POS filesystems are supported; probe for PNX in future
                    //
                    dev = new POS.Device();

                    if (dev.Probe(path))
                    {
                        if (dev.Mount(path))
                        {
                            PERQdisk.Dev = dev;
                            RunCommand("pos");
                            return;
                        }
                    }
                    break;

                case DeviceType.Unused:
                    Console.WriteLine("Unknown device type or can't read file.");
                    break;

                default:
                    Console.WriteLine($"Unimplemented device type '{type}'!");
                    break;
            }

            Console.WriteLine($"Failed to load from '{path}'.");
        }


        [Command("create floppy", "Create a new RT-11 data floppy")]
        [Command("rt11 format", "Format a new floppy")]
        private void Create(string filename, bool doubleSided = true, bool doubleDensity = false)
        {
            var geom = (doubleSided ?
                            (doubleDensity ? DeviceGeometry.DSDD : DeviceGeometry.DSSD) :
                            (doubleDensity ? DeviceGeometry.SSDD : DeviceGeometry.SSSD));

            // Create the empty shell
            var dev = new RT11Floppy();
            dev.Filename = Paths.QualifyPathname(filename, ".", ".prqm", true);

            // Give it shape
            dev.Info = DeviceInfo.SA851;
            dev.Specs = DevicePerformance.SA851;
            dev.Geometry = geom;

            // Devil in the details
            dev.FileInfo = new MediaInfo();
            dev.FileInfo.Format = Formatters.PRQFormat;
            dev.FileInfo.FSType = FilesystemHint.RT11;

            // Fake a load event to set up some parameters
            dev.OnLoad();

            // Create the actual sectors and set up the RT11 volume
            dev.Format();

            var vol = new RT11.Volume(dev);
            vol.Zero();

            // Save it to disk!
            dev.Save();
        }

        /// <summary>
        /// Pause for user interaction.  Used in POS and (RT11) FLOPPY command files.
        /// </summary>
        [Command("pos pause", "Pause execution and wait for user to press RETURN")]
        [Command("rt11 pause", "Pause execution and wait for user to press RETURN")]
        private void Pause(string message = "")
        {
            Console.WriteLine(message);
            Console.Write("Press RETURN to continue: ");
            Console.ReadLine();
        }

        [Command("done", Discreet = true)]
        private void Done()
        {
            Console.WriteLine("Already at top-level.");
        }

        [Command("quit", "Quit PERQdisk")]
        [Command("pos quit")]
        [Command("rt11 quit")]
        private void Quit()
        {
            _running = false;
        }

        [Command("quit without save", "Quit PERQdisk without committing changes")]
        private void QuitNow()
        {
            _running = false;
        }

        #endregion

        #region CLI Help (for now)

        [Command("help", "Show PERQdisk help")]
        private void Help()
        {
            Console.WriteLine("This is PERQdisk, a standalone utility for reading PERQ POS filesystems\n" +
                              "from archived floppy or hard disk images.  It supports all of the media\n" +
                              "formats that PERQemu can read and can translate between them.\n");
            Console.WriteLine("Type 'commands' at the prompt to see which commands are available.\n");
        }

        #endregion


#if DEBUG
        // use of Conditional() here puts the empty method in the tree (oops!)
        [Command("debug dump command tree")]
        public void DumpCommandTree()
        {
            Console.WriteLine("\nCommand tree:");
            _exec.DumpCommandTree(_exec.CurrentRoot, 0);
        }

        [Command("debug level")]
        public void DebugLevel(Severity level)
        {
            Log.Level = level;
            Console.WriteLine($"Debug level now {level}.");
        }
#endif

        const byte CR = (byte)'\r';
        const byte LF = (byte)'\n';
        const byte FF = (byte)'\f';


        private bool _running;
        private CommandExecutor _exec;
        private CommandPrompt _editor;
    }
}
