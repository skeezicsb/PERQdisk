//
//  RT11.Commands.cs
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
using System.Diagnostics;
using System.Collections.Generic;

namespace PERQdisk.RT11
{
    public enum Mode
    {
        Auto = 0,
        Binary = 1,
        Text = 2,
        ASCII = 2       // Synonym!
    }

    /// <summary>
    /// Commands to manipulate RT-11 floppies.
    /// </summary>
    public class Commands
    {

        public Commands()
        {
            _mode = Mode.Auto;
            _ask = true;
            _confirm = true;
        }


        [Command("rt11", Prefix = true, Discreet = true)]
        private void EnterFloppy()
        {
            // Prevent sneakiness
            if (PERQdisk.Dev == null) return;

            _disk = PERQdisk.Media as RT11Floppy;

            if (_disk.IsLoaded)
            {
                // A bit of cheese
                Console.Title += $" - {_disk.Filename}";

                _volume = new Volume(_disk);
                _volume.LoadDirectory();

                Console.WriteLine("RT11 volume mounted.");

                PERQdisk.CLI.SetPrefix("rt11");
                PERQdisk.CLI.SetPrompt("FLOPPY");
            }
        }

        [Command("rt11 unload", "Unload the current floppy")]
        [Command("rt11 done")]
        private void LeaveFloppy()
        {
            Console.WriteLine("Dismounting volume...");

            _volume = null;
            _disk = null;

            PERQdisk.Dev = null;
            PERQdisk.CLI.ResetPrefix();
            PERQdisk.CLI.SetPrompt("");

            Console.Title = PERQdisk.Version;
            Console.WriteLine("Disk unloaded.");
        }

        [Command("rt11 commands", "Show available commands")]
        private void ShowFloppyCommands()
        {
            PERQdisk.CLI.ShowCommands("rt11");
        }

        private bool WriteCheck()
        {
            if (!_disk.Info.IsWritable)
            {
                Console.WriteLine("** This floppy is read-only.  Please affix a sticker to the write notch");
                Console.WriteLine("   and reinsert the diskette in the drive.");
                return false;
            }

            return true;
        }

        #region Housekeeping commands

        //
        // Housekeeping - a bunch of little commands for compatibility with
        // POS FLOPPY command files; set flags or no-ops, mostly
        //

        [Command("rt11 ask", "Set ASK flag to true")]
        private void SetAsk()
        {
            _ask = true;
            Console.WriteLine("Ask ON.");
        }

        [Command("rt11 noask", "Set ASK flag to false")]
        private void NoAsk()
        {
            _ask = false;
            Console.WriteLine("Ask OFF.");
        }


        [Command("rt11 confirm", "Set CONFIRM flag to true")]
        private void SetConfirm()
        {
            _confirm = true;
            Console.WriteLine("Confirm ON.");
        }

        [Command("rt11 noconfirm", "Set CONFIRM flag to false")]
        private void NoConfirm()
        {
            _confirm = false;
            Console.WriteLine("Confirm OFF.");
        }

        [Command("rt11 fast", "Set flags for speed!")]
        private void GoFast()
        {
            _ask = false;
            _confirm = false;
            Console.WriteLine("FAST mode.");
        }

        [Command("rt11 safe", "Set flags for safety")]
        private void BeSafe()
        {
            _ask = true;
            _confirm = true;
            Console.WriteLine("SAFE mode.");
        }

        [Command("rt11 mode", "Show the current transfer mode")]
        private void ShowMode()
        {
            Console.WriteLine($"Mode is {_mode.ToString().ToUpper()}.");
        }

        [Command("rt11 mode", "Set the current transfer mode")]
        private void SetMode(Mode mode)
        {
            _mode = mode;
            ShowMode();
        }

        [Command("rt11 auto", "Set the current transfer mode to AUTO")]
        private void SetAuto()
        {
            _mode = Mode.Auto;
            ShowMode();
        }

        [Command("rt11 binary", "Set the current transfer mode to BINARY")]
        private void SetBinary()
        {
            _mode = Mode.Binary;
            ShowMode();
        }

        [Command("rt11 text", "Set the current transfer mode to TEXT")]
        [Command("rt11 ascii")]
        private void SetTextMode()
        {
            _mode = Mode.Text;
            ShowMode();
        }

        [Command("rt11 verify", "No-op for POS FLOPPY compatibility")]
        [Command("rt11 noverify")]
        [Command("rt11 sides")]
        [Command("rt11 density")]
        [Command("rt11 duplicate")]
        [Command("rt11 floppyget")]
        [Command("rt11 floppyput")]
        [Command("rt11 fsfloppy")]
        [Command("rt11 mount")]
        [Command("rt11 dismount")]
        private void CompatNoop()
        {
#if DEBUG
            Console.WriteLine("[Compatibility command, ignored.]");
#else
            Console.WriteLine("Okay.");
#endif
        }

        #endregion

        #region Informational commands

        /// <summary>
        /// Prints the directory in PERQ "FLOPPY" program format.
        /// </summary>
        /// <remarks>
        /// Eventually provide all that the floppy.pl implementation offers:
        /// 
        /// Usage:      DIRECTORY [/switches] [filepat] [outfile]
        /// 
        /// Arguments:
        ///     <none>      Print a long directory listing of all files to the screen
        /// 	filepat     (optional) List only files matching this pattern
        ///     outfile     (optional) Write the output to "outfile"
        /// 
        /// Switches:
        ///     LONG        List all file information [Default]
        ///     SHORT       Only list file names
        ///     HELP        Print this message
        ///
        /// For now, LONG format with no switches or options.
        /// </remarks>
        [Command("rt11 directory", "List contents of the directory")]
        private void PrintDirectory()
        {
            var nFiles = 0;
            var nFree = 0;

            foreach (var f in _volume.Dir.Files)
            {
                var tName = "";
                var tDate = "";

                if (f.Status == StatusWord.Permanent || f.Status == StatusWord.Tentative)
                {
                    tName = f.Filename;
                    tDate = f.Date;
                    nFiles++;
                }
                else
                {
                    tName = "<unused>";
                    tDate = "---------";
                    nFree += f.Size;
                }

                Console.WriteLine($"\t{tName,-10}  {tDate,9}  Block: {f.StartBlock,4} Size: {f.Size,4} Bits: {f.BitsInLastBlock,4}");
            }

            Console.WriteLine($"\t{nFiles} File" + (nFiles != 1 ? "s" : "") + " in Use");
            Console.WriteLine($"\t{nFree} Free Block" + (nFree != 1 ? "s" : ""));
            Console.WriteLine();

            // debug
            if (nFree != _volume.Dir.BlocksFree)
                Console.WriteLine($"** ERROR: free block count wrong, vol={_volume.Dir.BlocksFree}");
        }

        [Command("rt11 type", "Print contents of floppy file(s)")]
        [Command("rt11 more")]
        private void TypeFile(string filename)
        {
            DoTypeFile(filename, true);
        }

        [Command("rt11 cat", "Dump contents of a file")]
        private void CatFile(string filename)
        {
            DoTypeFile(filename, false);
        }

        private void DoTypeFile(string filePat, bool pause)
        {
            var found = new List<DirectoryEntry>();

            if (Paths.IsPattern(filePat))
            {
                found = _volume.FindFiles(filePat);
            }
            else
            {
                var f = _volume.FindFile(filePat);
                if (f.Status == StatusWord.Permanent) found.Add(f);
            }

            if (found.Count == 0)
            {
                Console.WriteLine($"No files matching '{filePat}' found on the floppy.");
                return;
            }

            var count = 0;
            foreach (var file in found)
            {
                // Pause between files, but give a chance to bail out
                if (pause && (count++ > 0))
                {
                    var prompt = $" -- Next file {file.Filename} ({count} of {found.Count}) -- ";
                    var doit = PERQdisk.CLI.Yes(prompt, false, true, true);
                    if (doit == Doit.Quit) return;
                    if (doit == Doit.No) continue;
                }

                if (PERQdisk.CLI.Paginate(file.Filename, _volume.Dir.ReadFile(file), pause) == Doit.Quit)
                    return;
            }
        }

        #endregion

        #region Transfer commands

        /// <summary>
        /// Implements the POS FLOPPY commmand's GET function to retrieve files
        /// from the floppy to the host.  Allows a single file to be transferred
        /// (optionally saved with a specific filename on the host) or a wildcard
        /// and a destination directory to be named.
        /// </summary>
        [Command("rt11 get", "Get one or more files from the floppy")]
        private void GetFiles(string filePat, string hostName = "")
        {
            // Set the default destination directory
            var hostDir = _volume.DefaultOutputPath();
            var fold = PERQdisk.HostIsUnix;

            // Is it a pattern?
            if (Paths.IsPattern(filePat))
            {
                var found = _volume.FindFiles(filePat);

                if (found.Count == 0)
                {
                    Console.WriteLine($"No files matching '{filePat}' found on the floppy.");
                    return;
                }

                if (hostName != "") hostDir = hostName;

                // When given a file pattern, a given destination must be a valid
                // directory (NOT a plain file).  If it's already there, proceed,
                // otherwise see if it should be created
                if (!System.IO.Directory.Exists(hostDir))
                {
                    if (System.IO.File.Exists(hostDir))
                    {
                        Console.WriteLine("** Destination exists and is a plain file!  When getting multiple");
                        Console.WriteLine("   files with a wildcard, destination must be a directory.");
                        return;
                    }

                    if (_confirm)
                    {
                        Console.WriteLine($"* Destination directory {hostDir} does not exist.");
                        var ans = PERQdisk.CLI.Yes("Create it [y]? ", true);
                        if (ans == Doit.No || ans == Doit.Quit) return;

                        // If fails we bail
                        if (!_volume.MakeDestPath(hostDir)) return;
                    }
                }

                // Okay, got a destination directory, start copying files
                foreach (var file in found)
                {
                    // Set the destination filename; lowercase it if folding
                    var dst = System.IO.Path.Combine(hostDir, fold ? file.Filename.ToLower() : file.Filename);

                    // Copy one at a time (but bail early if told to quit)
                    if (_volume.GetFile(file, dst, _ask, _confirm, _mode) == Doit.Quit)
                        return;
                }
            }
            else
            {
                // One matching file?  Do it this way so we get the filename munging
                // for free; that way "get default.profile" properly matches DEFAUL.PRO
                // on the floppy but is written to the host as the given full name.
                // Yeah, it's janky, but that's how POS does it too... 
                var file = _volume.FindFile(filePat);

                if (file.Status != StatusWord.Permanent)
                {
                    Console.WriteLine($"** No file matching '{filePat}' found on the floppy.");
                    return;
                }

                // Set up default destination filename if none given
                var dst = fold ? file.Filename.ToLower() : file.Filename;

                // If the destination was given, see if it's an existing directory
                if (hostName != "")
                {
                    if (System.IO.Directory.Exists(hostName))
                    {
                        // Prepend to our default filename
                        dst = System.IO.Path.Combine(hostName, dst);
                    }
                    else
                    {
                        // Not a dir, so go ahead and just use what's given
                        dst = hostName;
                    }
                }
                else
                {
                    // Prepend the default dir to our default (folded) filename
                    dst = System.IO.Path.Combine(hostDir, filePat);
                }

                // Go for it
                _volume.GetFile(file, dst, _ask, _confirm, _mode);
            }
        }

        /// <summary>
        /// PUT command to copy files from the host TO the floppy.  Generates
        /// valid RAD50-encoded names if a specific name isn't given.  Allows
        /// a wildcard to select files to copy.
        /// </summary>
        [Command("rt11 put", "Put one or more files onto the floppy")]
        private void PutFiles(string hostPat, string fileName = "")
        {
            if (!WriteCheck()) return;

            if (Paths.IsPattern(hostPat))
            {
                // Could do the crazy POS thing of mapping wildcards but nah (for now)
                if (fileName != "")
                {
                    Console.WriteLine("** Cannot specify a destination file name when multiple host files");
                    Console.WriteLine("   are specified.  To put a file on the floppy with a specific name,");
                    Console.WriteLine("   please choose one file at a time.");
                    return;
                }

                // If the hostPat is relative, we can EnumerateFiles just fine;
                // if it's absolute, we have to break down the path to provide a
                // starting directory?  Otherwise
                //      "Second path fragment must not be a drive or UNC name."
                // Will have to test this on windows.

                var files = System.IO.Directory.EnumerateFiles(".", hostPat);
                var count = 0;

                foreach (var file in files)
                {
                    count++;

                    if (_volume.PutFile(file, file, _ask, _confirm, _mode) == Doit.Quit)
                        return;
                }

                if (count == 0)
                {
                    Console.WriteLine($"No files matching '{hostPat}' found.");
                }
            }
            else
            {
                // For a non-wildcard path, make sure it is NOT a directory...
                if (System.IO.Directory.Exists(hostPat))
                {
                    Console.WriteLine($"** '{hostPat}' is a directory; please specify a filename.");
                    return;
                }

                // And that it IS an existing file
                if (!System.IO.File.Exists(hostPat))
                {
                    Console.WriteLine($"** No file matching '{hostPat}' found.");
                    return;
                }

                // If destination fileName not given, use the source pattern
                if (fileName == "") fileName = hostPat;

                // Do it!
                _volume.PutFile(hostPat, fileName, _ask, _confirm, _mode);
            }
        }

        #endregion

        #region Utility commands

        /// <summary>
        /// Renames a file on the floppy.
        /// </summary>
        [Command("rt11 rename", "Rename a file on the floppy")]
        private void RenameFile(string from, string to)
        {
            if (!WriteCheck()) return;

            if (Paths.IsPattern(from) || Paths.IsPattern(to))
            {
                Console.WriteLine("** Sorry, can't rename with wildcards.");
                return;
            }

            var fromFile = _volume.FindFile(from);

            if (fromFile.Status != StatusWord.Permanent)
            {
                Console.WriteLine($"** No file matching '{from}' found on the floppy.");
                return;
            }

            _volume.RenameFile(fromFile, to, _ask);
        }

        /// <summary>
        /// Deletes one or more files from the floppy.  Should we mark them as
        /// "tentative" so they can be undeleted?  Is that what that's for?
        /// </summary>
        [Command("rt11 delete", "Delete files from the floppy")]
        private void DeleteFiles(string filePat)
        {
            if (!WriteCheck()) return;

            if (Paths.IsPattern(filePat))
            {
                var found = _volume.FindFiles(filePat);

                if (found.Count == 0)
                {
                    Console.WriteLine($"No files matching '{filePat}' found on the floppy.");
                    return;
                }

                foreach (var file in found)
                {
                    if (_volume.DeleteFile(file, _confirm) == Doit.Quit) return;
                }
            }
            else
            {
                var found = _volume.FindFile(filePat);

                if (found.Status != StatusWord.Permanent)
                {
                    Console.WriteLine($"** No file matching '{filePat}' found on the floppy.");
                    return;
                }

                _volume.DeleteFile(found, _confirm);
            }
        }

        /// <summary>
        /// Compare a floppy file to a host file.  A nice addition to add...
        /// </summary>
        [Command("rt11 compare", "Compare a floppy file to a host file")]
        private void CompareFile()
        {
            Console.WriteLine("Not yet implemented.");
        }

        /// <summary>
        /// Coalesce free space on the floppy by moving files around so that
        /// there's just one contiguous chunk available when done.  On the real
        /// hardware this is painfully slow, as you might imagine!
        /// </summary>
        [Command("rt11 compress", "Coalesce free space on the floppy")]
        private void CompressFloppy()
        {
            if (!WriteCheck()) return;

            Console.WriteLine("Not yet implemented.");
        }

        /// <summary>
        /// Erase a floppy by blasting the directory and creating a new one.
        /// Does not wipe the data blocks; use Format for that...
        /// </summary>
        [Command("rt11 zero", "Erase the floppy and create a new, empty directory")]
        private void ZeroFloppy()
        {
            if (!WriteCheck()) return;

            if (_ask || _confirm)
            {
                Console.WriteLine("* WARNING: Zeroing the floppy will destroy its contents!");
                if (PERQdisk.CLI.Yes("Zero the floppy [n]? ", true) != Doit.Yes)
                    return;
            }

            // Allllrightee then
            _volume.Zero();
        }

        #endregion

        #region Debugging

        [Conditional("DEBUG")]
        [Command("rt11 dump volume info", "Dump a full debug listing of the volume info")]
        public void DumpVolume()
        {
            _volume.PrintVolInfo();
        }

        [Conditional("DEBUG")]
        [Command("rt11 dump directory", "Dump a full debug listing of the RT11 directory")]
        public void DumpDir()
        {
            _volume.Dir.PrintDirInfo();
        }

        [Conditional("DEBUG")]
        [Command("rt11 update directory", "Force a rewrite of the volume/directory info")]
        public void RewriteDir()
        {
            _volume.Dir.Save();
        }

        [Conditional("DEBUG")]
        [Command("rt11 check radix50 encoder", "Run Radix50 codec tests")]
        private void CheckRad50Encoder()
        {
            Radix50.DumpEncoder();
        }

        [Conditional("DEBUG")]
        [Command("rt11 check regex", "Match files with a regular expression")]
        private void CheckRegex(string pat)
        {
            foreach (var f in _volume.FindFiles(pat))
            {
                Console.WriteLine($"Match: {f.Filename}");
            }
        }

        [Conditional("DEBUG")]
        [Command("rt11 check dates", "Test the RT11 date encoding routines")]
        public void CheckRT11Dates()
        {
            Console.WriteLine("RT11 date encoder range checks (invalid dates):");
            Console.WriteLine("      0 decodes as " + DateWord.Decode(0));
            Console.WriteLine("  32767 decodes as " + DateWord.Decode(32767));
            Console.WriteLine("  32768 decodes as " + DateWord.Decode(32768));
            Console.WriteLine("  65535 decodes as " + DateWord.Decode(65535));

            var dt = new DateTime(1970, 1, 1);
            Console.WriteLine("  Unix epoch is " + DateWord.Decode(DateWord.Encode(dt)));

            Console.WriteLine("\nValid dates:");
            dt = new DateTime(1972, 1, 1);
            Console.WriteLine("  Earliest valid date is " + DateWord.Decode(DateWord.Encode(dt)));

            dt = new DateTime(2003, 12, 31);
            Console.WriteLine("  Last valid date (clipped) is " + DateWord.Decode(DateWord.Encode(dt)));

            ushort lim = (12 << 10) + (31 << 5) + 31;
            Console.WriteLine("  Last valid date decodes as " + DateWord.Decode(lim));

            Console.WriteLine("  Today in RT11 is " + DateWord.Decode(DateWord.Encode(DateTime.Today)));
        }

        #endregion

        private RT11Floppy _disk;
        private Volume _volume;
        private Mode _mode;

        private bool _ask;
        private bool _confirm;
    }
}
