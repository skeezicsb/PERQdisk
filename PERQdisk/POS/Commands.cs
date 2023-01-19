//
//  POS.Commands.cs
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
using System.Diagnostics;

namespace PERQdisk.POS
{
    /// <summary>
    /// Commands to interrogate POS Filesystems on hard disk or floppy media.
    /// </summary>
    public class Commands
    {
        public Commands()
        {
            _disk = null;
            _volume = null;
        }

        /// <summary>
        /// Subsystem entered when a disk image is opened.
        /// </summary>
        /// <remarks>
        /// This is cheeky.  When a disk is successfully loaded, we change the
        /// prefix to enable commands that require an open disk image.  That way
        /// every bloody command doesn't have to do a "if (_disk != null) blah"
        /// check.  All the other disk commands "hide" behind this one.
        /// </remarks>
        [Command("pos", Prefix = true, Discreet = true)]
        public void EnterDisk()
        {
            // Thwart cheaters
            if (PERQdisk.Dev == null) return;

            // Make sure we have a valid disk...
            _disk = PERQdisk.Media as LogicalDisk;

            if (!_disk.IsLoaded)
            {
                throw new InvalidOperationException("POS disk not loaded");
            }

            // A bit of cheese
            Console.Title += $" - {_disk.Filename}";

            _volume = new Volume(_disk);
            _volume.ReadPartitionInfo();
            _volume.LoadDirectory();

            Console.WriteLine("POS volume mounted.");

            PERQdisk.CLI.SetPrefix("pos");
            UpdatePrompt();
        }

        [Command("pos unload", "Unload the current media file")]
        [Command("pos done")]
        void LeaveDisk()
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

        [Command("pos commands", "Show available commands")]
        void ShowDiskCommands()
        {
            PERQdisk.CLI.ShowCommands("pos");
        }


        [Command("pos path", "Print the current directory path")]
        void ChangeDirectory()
        {
            Console.WriteLine("Current path is " + _volume.Current.Path);
        }

        [Command("pos path", "Change the current directory path")]
        void ChangeDirectory(string newDir)
        {
            _volume.SetPath(newDir);
            UpdatePrompt();
        }


        [Command("pos details", "Show filesystem summary")]
        void DoDetails()
        {
            _volume.Details();
        }


        [Command("pos directory", "List directory contents")]
        void PrintDirectory(string pattern = "", bool showSize = false)
        {
            _volume.DoDirectory(pattern, showSize);
        }


        [Command("pos tree", "Show the directory tree 'graphically'")]
        void PrintDirTree(string dir = "")
        {
            _volume.DirTree(dir);
        }

        [Command("pos type", "Print contents of a file")]
        [Command("pos more")]
        void TypeFile(string filename)
        {
            _volume.DoTypeFile(filename, true);
        }

        [Command("pos cat", "Dump contents of a file")]
        void CatFile(string filename)
        {
            _volume.DoTypeFile(filename, false);
        }

        [Command("pos copy", "Copy file(s) to the default output directory")]
        void Copy(string source) // , params string[] args)
        {
            // todo: figure out the bloody args parsing
            _volume.DoCopy(source, Paths.OutputDir);
        }

        [Command("pos copy to", "Copy file(s) from the image to the host")]
        void CopyTo(string dest, string source) // , params string[] args)
        {
            _volume.DoCopy(source, dest);
        }

        [Command("pos compare", "Compare a POS file to a host file")]
        void Compare(string file, string hostFile)
        {
            Console.WriteLine("Not yet implemented.");
            return;
        }


        void UpdatePrompt()
        {
            // Trim any trailing '>' (since the editor appends one)
            var prompt = _volume.Current.Path.TrimEnd('>');
            PERQdisk.CLI.SetPrompt(prompt);
        }


        #region Debugging

        [Conditional("DEBUG")]
        [Command("pos info", "Show information about a file or directory")]
        void PrintInfo(string file)
        {
            var path = _volume.Resolve(file);

            if (!path.IsValid)
            {
                Console.WriteLine($"File or directory '{file}' does not exist.");
                return;
            }

            // At the root?
            if (path.IsDevPath)
            {
                _volume.PrintDIBInfo();
                return;
            }

            // Specified a partition?
            if (path.IsPartPath)
            {
                Console.WriteLine("Partition:");
                // May be one or more
                foreach (var p in path.Partitions)
                {
                    p.PrintPIBInfo();
                }
                return;
            }

            // Did we match only directories?
            if (path.IsDirPath)
            {
                foreach (var d in path.Directories)
                {
                    d.DirFile.PrintFileInfo();
                    Console.WriteLine("  Output file: " + _volume.CreateOutputPath(d.DirFile));
                    Console.WriteLine();
                }
                return;
            }

            // Must be plain ol' files then
            foreach (var f in path.Files)
            {
                f.PrintFileInfo();
                Console.WriteLine("  Output file: " + _volume.CreateOutputPath(f));
                Console.WriteLine();
            }
        }

        [Conditional("DEBUG")]
        [Command("pos show ranges", "Show disk limits [Debug]")]
        void ShowRanges(bool verbosely = false)
        {
            Console.WriteLine("Physical disk limits:");

            var min = new Block(0, 0, 0, false);
            var max = new Block((ushort)(_disk.Geometry.Cylinders - 1),
                                  (byte)(_disk.Geometry.Heads - 1),
                                (ushort)(_disk.Geometry.Sectors - 1),
                                  false);

            Console.WriteLine("Min: {0} -> {1}", min.ToString(), _disk.BlockToAddress(min));
            Console.WriteLine("Max: {0} -> {1}", max.ToString(), _disk.BlockToAddress(max));

            Console.WriteLine();
            Console.WriteLine("Logical disk limits:");

            var lbmin = _disk.LBNtoLDA(0);
            var lbmax = _disk.LBNtoLDA(_disk.MaxLBN);

            Console.WriteLine("Min: {0} -> {1}", lbmin.ToString(), _disk.AddressToBlock(lbmin));
            Console.WriteLine("Max: {0} -> {1}", lbmax.ToString(), _disk.AddressToBlock(lbmax));

            if (verbosely)
            {
                for (uint l = 0; l <= _disk.MaxLBN; l++)
                {
                    // Do ALL the steps!  And check 'em!
                    Address a = _disk.LBNtoLDA(l);
                    Block b = _disk.AddressToBlock(a);
                    Block c = _disk.LogicalBlockToPhysicalBlock(b);
                    Block d = _disk.PhysicalBlockToLogicalBlock(c);
                    Address e = _disk.BlockToAddress(d);
                    uint f = _disk.LDAtoLBN(e);

                    Console.WriteLine("LBN {0} -> {1} -> {2} -> {3}", l, a, b, c);
                    Console.WriteLine("LBN {0} <- {1} <- {2}", f, e, d);

                    if (f != l) Console.WriteLine($"  LBN mismatch");
                    if (!e.Equals(a)) Console.WriteLine($"  Address a/e mismatch");
                    if (!d.Equals(b)) Console.WriteLine($"  Block b/d mismatch");
                }
            }
        }

        [Conditional("DEBUG")]
        [Command("pos dump directory", "Dump the raw directory structure [Debug]")]
        void DumpDirTree()
        {
            DumpDir(_volume.Root, 0);
        }

        void DumpDir(Directory d, int depth)
        {
            Console.WriteLine($"Node {d.Name} (depth {d.Depth}) has {d.Contents.Count} files, {d.Children.Count} subdirectories");
            Console.WriteLine($"Full path is '{d.Path}'");

            PERQdisk.CLI.Columnify(d.GetFiles().ToArray(), depth, 26);

            foreach (var c in d.Children)
            {
                DumpDir(c, depth + 2);
            }
        }

        [Conditional("DEBUG")]
        [Command("pos check path", "Test path/pattern parsing [Debug]")]
        void PathParse(string pat)
        {
            var p = _volume.Resolve(pat);

            p.Dump();
        }

        [Conditional("DEBUG")]
        [Command("pos check params", "Test 'params' keyword parsing [Debug]")]
        void ParamsParse(params string[] args)
        {
            Console.WriteLine($"ParamsParse() called with {args.Length} arguments!");
        }

        #endregion


        LogicalDisk _disk;
        Volume _volume;
    }
}
