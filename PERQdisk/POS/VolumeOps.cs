//
//  POS.VolumeOps.cs
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
using System.Collections.Generic;

using PERQemu;
using PERQmedia;

namespace PERQdisk.POS
{
    /// <summary>
    /// Implements the high-level file operations on a POS volume.
    /// </summary>
    public partial class Volume
    {
        /// <summary>
        /// Implements the POS "directory" command.  In the MOST insane way possible.
        /// </summary>
        public void DoDirectory(string pattern, bool showSize)
        {
            var items = Resolve(pattern);

            if (!items.IsValid)
            {
                Console.WriteLine($"** No matching files matching '{pattern}'.");
                return;
            }

            if (items.IsDevPath)
            {
                // Dir sys: lists the partitions; we have to handle it specially
                // since there isn't a real Directory or FIB at the device level
                items = Resolve(items.DevPart + "*");

                // Fall through!  Re-resolving yields a PartPath.  CHEESE! :-)
            }

            if (items.IsPartPath)
            {
                // POS doesn't allow wildcards in partitions but we do; in this
                // case just list the ones that match
                if (items.IsPartWild)
                {
                    DoOneDir(items.Partitions.ConvertAll((Partition input) => input.Root.DirFile),
                             $"Files matching '{items.PartPart}' :",
                             items.DevPart,
                             showSize);
                    return;
                }

                items = Resolve($"{items.DevPart}{items.PartPart}>*");

                // Fall through!  Re-resolving yields a DirPath!  I have lost my mind!
            }

            if (items.IsDirPath)
            {
                // A wildcard directory spec but no matching files? Print 'em
                if (items.IsDirWild)
                {
                    DoOneDir(items.Directories.ConvertAll(Directory.DirToFile),
                             $"Files matching '{items.DirPart}' :",
                             items.Directories[0].Parent.Path,
                             showSize);
                    return;
                }

                items = Resolve($"{items.DevPart}{items.PartPart}>{items.DirPart}>*");

                // Fall through!  Oh yeah.  This is happening.  We're going there.
            }

            if (items.IsFilePath)
            {
                // At last!  If multiple directories contain matches, we do each
                // one in turn, but tally up the total blocks to display at the end
                var totalBlocks = 0;

                var header = (items.IsDirWild ?
                             $"\n    Files matching '{items.FilePart}' in directories matching '{items.DirPart}' :" :
                             $"Files matching '{items.FilePart}' :");

                // Ugh.  This is ugly.  Should flatten the structure to make the
                // Partitions less of a special case.  If we have "dev:part>dir>*"
                // then walk the list of directories...
                if (items.Directories.Count > 0)
                {
                    foreach (var d in items.Directories)
                    {
                        totalBlocks += DoOneDir(items.Files.FindAll(f => f.Parent == d),
                                                header, d.Path, showSize);

                        header = "";    // Don't repeat if multiple dirs
                    }
                }
                else
                {
                    // But in the form "dev:part>*" the partition IS the directory
                    // and that's just gross and horrible and I should fix it sigh
                    foreach (var d in items.Partitions)
                    {
                        totalBlocks += DoOneDir(items.Files.FindAll(f => f.Parent == d.Root),
                                                header, d.Root.Path, showSize);

                        header = "";
                    }
                }

                if (items.IsDirWild)
                {
                    Console.Write("Grand total: ");
                    if (showSize) Console.Write($"{totalBlocks} blocks in ");
                    Console.WriteLine($"{items.Files.Count} files in {items.Directories.Count} directories scanned.\n");
                }
            }
        }

        private File PartToDirFile(Partition p)
        {
            return p.Root.DirFile;
        }

        /// <summary>
        /// Print a formatted directory listing for one directory and return the
        /// total size (in blocks).  Displays the blocks if showSize is true.
        /// </summary>
        private int DoOneDir(List<File> files, string header, string thisDir, bool showSize)
        {
            const int leftCol = 2;
            const int tabWidth = 26;

            var col = Console.BufferWidth;
            var totalBlocks = 0;

            // Quietly exit if nothing to do.  This is probably a bug.
            if (files.Count == 0) return 0;

            if (files.Count > 1)    // && doSort
            {
                files.Sort(delegate (File x, File y)
                {
                    return x.SimpleName.CompareTo(y.SimpleName);
                });
            }

            if (!string.IsNullOrEmpty(header))
                Console.WriteLine($"\nDIRECT V1.0  {DateTime.Now}  {header}");

            Console.WriteLine($"\n--- In {thisDir} ---");

            foreach (var file in files)
            {
                if (col + tabWidth > Console.BufferWidth)
                {
                    Console.WriteLine();
                    Console.Write(" ".PadLeft(leftCol));
                    col = leftCol;
                }

                // Make certain file types stand out
                switch ((FileTypes)file.FIB.FileType)
                {
                    case FileTypes.DirFile:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;

                    case FileTypes.MBootFile:
                    case FileTypes.SBootFile:
                        Console.ForegroundColor = ConsoleColor.Blue;
                        break;

                    default:
                        Console.ForegroundColor = ConsoleColor.Gray;
                        break;
                }

                var name = file.IsDirectory ? file.SimpleName + ">" : file.SimpleName;

                if (showSize)
                {
                    name = name.Insert(0, $"{file.FIB.FileSize,4} ");
                    totalBlocks += file.FIB.FileSize;
                }

                // Catch strings longer than tabWidth...
                if (name.Length > tabWidth)
                {
                    var extra = name.Length % tabWidth;

                    Console.Write(name);
                    col += name.Length;

                    if ((col + tabWidth - extra) < Console.BufferWidth)
                    {
                        Console.Write(" ".PadRight(tabWidth - extra));
                        col += tabWidth - extra;
                    }
                }
                else
                {
                    Console.Write(name.PadRight(tabWidth));
                    col += tabWidth;
                }
            }
            Console.ResetColor();
            Console.WriteLine("\n");

            if (showSize) Console.WriteLine($"{totalBlocks} total blocks.");
            Console.WriteLine($"{files.Count} files found.\n");

            return totalBlocks;
        }


        /// <summary>
        /// Implements the POS "typefile" command.
        /// </summary>
        public void DoTypeFile(string filePat, bool pause)
        {
            var found = Resolve(filePat);

            if (found.Files.Count == 0)
            {
                Console.WriteLine($"** No files matching '{filePat}' found.");
                return;
            }

            var count = 0;
            foreach (var file in found.Files)
            {
                if (pause && (count++ > 0))
                {
                    var prompt = $" -- Next file {file.SimpleName} ({count} of {found.Files.Count}) -- ";
                    var doit = PERQdisk.CLI.Yes(prompt, false, true);
                    if (doit == Doit.Quit) return;
                    if (doit == Doit.No) continue;
                }

                if (PERQdisk.CLI.Paginate(file.SimpleName, file.Data, pause) == Doit.Quit)
                    return;
            }
        }


        /// <summary>
        /// Do a POS-like "details" command showing filesystem info.
        /// </summary>
        public void Details()
        {
            // Device
            Console.WriteLine($"\nDisk image:   {_disk.Filename}");
            Console.WriteLine($"Description:  {_disk.Info.Description}");

            // Volume
            Console.WriteLine("\nDevice name:");
            Console.WriteLine("    {0,-8}  Start: {1,6}  End: {2,6}  Flags: {3},{4}",
                              _dib.DeviceName,
                              _disk.LDAtoLBN(_dib.DeviceStart),
                              _disk.LDAtoLBN(_dib.DeviceEnd),
                              _dib.DeviceType,
                              _dib.PartType);

            // Partitions
            Console.WriteLine("\nPartitions:");

            foreach (var part in _partitions)
            {
                part.PrintPIBInfo();
            }

            // Boots
            var validBoots = 0;

            for (int i = 0; i < DIB.BootTable.Length; i++)
            {
                var bootChar = (char)(i + 'a');
                var bootFile = "";
                var interpFile = "";

                var addr = DIB.BootTable[i];

                if (addr.Value != 0)
                {
                    // If the FIB is on a floppy, force conversion of the physical
                    // address to logical so we get a full 512-byte sector.  This
                    // is a little unwieldy but there's no PDAtoLDA directly...
                    if (_disk.Info.Type == DeviceType.Floppy)
                    {
                        addr = _disk.BlockToAddress(
                                    _disk.PhysicalBlockToLogicalBlock(
                                        _disk.AddressToBlock(addr)));
                    }
                    var bootFib = new File(_disk, _root, addr);     // XXX
                    bootFile = bootFib.SimpleName;
                }

                addr = DIB.InterpreterTable[i];
                if (addr.Value != 0)
                {
                    if (_disk.Info.Type == DeviceType.Floppy)
                    {
                        addr = _disk.BlockToAddress(
                                    _disk.PhysicalBlockToLogicalBlock(
                                        _disk.AddressToBlock(addr)));
                    }
                    var interpFib = new File(_disk, _root, addr);   // XXX
                    interpFile = interpFib.SimpleName;
                }

                if (bootFile == "" && interpFile == "") continue;

                if (bootFile != "" && interpFile != "") validBoots++;

                if (validBoots == 1) Console.WriteLine("\nBoots:");

                Console.WriteLine("    Char: {0}      System: {1,-18}  File: {2}",
                                  bootChar,
                                  _disk.AddressToBlock(DIB.BootTable[i]),
                                  (bootFile == "" ? "** Missing .Boot" : bootFile));

                Console.WriteLine("            Interpreter: {0,-18}  File: {1}",
                                  _disk.AddressToBlock(DIB.InterpreterTable[i]),
                                  (interpFile == "" ? "** Missing .MBoot" : interpFile));
            }

            if (validBoots == 0)
            {
                Console.WriteLine("\n** No valid boots on this device.");
            }

            Console.WriteLine();
        }


        /// <summary>
        /// Implements the POS "dirtree" command to print a "graphical" dump
        /// of the directory structure.
        /// </summary>
        public void DirTree(string start)
        {
            var dir = (start == "" ? Root : ResolveDirectory(start));

            if (dir == null)
            {
                Console.WriteLine($"** Can't start tree at {start}: bad directory.");
                return;
            }

            PrintOneLevel(dir, "", "");
        }

        private void PrintOneLevel(Directory dir, string prefix, string next)
        {
            // Alternates:
            const char T = (char)0x251c;    // 2560 or +
            const char V = (char)0x2502;    // 2551 or |
            const char H = (char)0x2500;    // 2550 or -
            const char C = (char)0x2514;    // 255a or `

            string tee = $"{T}{H}{H}{H} ";
            string vert = $"{V}    ";
            string horz = "     ";
            string corner = $"{C}{H}{H}{H} ";

            // First pass?
            if (prefix == "")
            {
                prefix = "".PadLeft(dir.Name.Length);
                next = prefix;
                Console.WriteLine($"{dir.Name}" + (dir.Depth == 0 ? ":" : ">"));
            }
            else
            {
                Console.WriteLine($"{prefix}{dir.Name}");
            }

            if (dir.Children.Count > 0)
            {
                var nextPrefix = next + tee;
                var nextNext = next + vert;

                for (var i = 0; i < dir.Children.Count; i++)
                {
                    // Last one?
                    if (i == dir.Children.Count - 1)
                    {
                        nextPrefix = next + corner;
                        nextNext = next + horz;
                    }

                    PrintOneLevel(dir.Children[i], nextPrefix, nextNext);
                }

                // Pure cheese: make partitions stand out a bit
                if (dir.Depth == 1) Console.WriteLine(next);
            }
        }


        /// <summary>
        /// Copies files from the POS filesystem to the host.  Can copy from a
        /// file pattern (flat, one directory) or a directory (including a whole
        /// device or partition at a time).  Directory copies are (currently)
        /// always recursive.
        /// </summary>
        /// <remarks>
        /// For now DoCopy operates as if the following (unimplemented) switches
        /// are set:
        ///     /preserve       - tries to preserve original timestamps
        ///     /nocaseconvert  - writes files with original capitalization
        ///     /binary         - writes exact binary copies
        ///     /recurse        - descends into subdirectories
        ///     /noconfirm      - overwrites destination files!
        ///     /noflatten      - keeps original directory structure
        /// 
        /// For now, if no destination is given the default is the Output dir
        /// plus the name of the media file (sans extension) with the full POS
        /// path, so a copy of the G.6:Boot partition from 1412203.IMD will end
        /// up (on Unix) in [...PERQdisk/bin/Release/]Output/1412203/G.6/Boot/
        /// </remarks>
        public void DoCopy(string src, string dst)
        {
            // See if the source is valid
            var items = Resolve(src);

            if (!items.IsValid)
            {
                Console.WriteLine($"** No files matching '{src}' found.");
                return;
            }

            // If the source is dev:, expand that to all partitions
            if (items.IsDevPath)
            {
                items = Resolve($"{items.DevPart}*");
                // Resolve all the partitions and fall through to execute 'em
            }

            // If the source is one or more partitions, recurse over their root
            // directories
            if (items.IsPartPath)
            {
                foreach (var p in items.Partitions)
                {
                    CopyRecursive(p.Root, dst);
                }
                return;
            }

            // If the source resolves to a specific directory, do it
            if (items.IsDirPath)
            {
                foreach (var d in items.Directories)
                {
                    CopyRecursive(d, dst);
                }
                return;
            }

            // Otherwise, do just the list of given files (which might include
            // directories, if the wildcard matched!)
            foreach (var f in items.Files)
            {
                if (f.IsDirectory)
                    CopyRecursive(f.Parent, dst);
                else
                    CopyOne(f, dst);
            }
            return;
        }

        /// <summary>
        /// Recursively copies directories (including partitions) from the disk
        /// image to the local filesystem.
        /// </summary> 
        private void CopyRecursive(Directory srcDir, string destDir)
        {
            Log.Debug(Category.POS, "Starting recursive copy of '{0}' to '{1}'",
                                     srcDir.Name, Paths.Canonicalize(destDir));

            // If there are children, do them first
            foreach (var d in srcDir.Children)
            {
                CopyRecursive(d, destDir);
            }

            // Now do all the plain files
            foreach (var f in srcDir.Contents)
            {
                CopyOne(f, destDir);
            }
        }

        private void CopyOne(File srcFile, string destDir)
        {
            // Skip directories; we only do files!
            if (srcFile.IsDirectory) return;

            var newPath = System.IO.Path.Combine(destDir, CreateOutputPath(srcFile));

            // Make sure the output directory exists on the host
            var dirPath = System.IO.Path.GetDirectoryName(newPath);

            if (!System.IO.Directory.Exists(dirPath))
            {
                // Try to create it, but bail if it fails
                if (!Paths.MakeDestPath(dirPath, _dryrun)) return;
            }

            // Attempt to create the file; catch and report exceptions in case
            // the host can't handle something funky in the POS filename
            try
            {
                Console.Write($"  Copying {srcFile.SimpleName} => ");

                var outPath = Paths.Canonicalize(newPath);  // Once
                if ((Console.CursorLeft + outPath.Length + 18) > Console.BufferWidth)
                    Console.Write("\n          ");          // Aesthetics hack
                Console.Write($"{outPath} ... ");

                if (_dryrun)
                {
                    Console.WriteLine("(dryrun)");
                    return;
                }

                // Do it for realsies
                var fs = new System.IO.FileStream(newPath, System.IO.FileMode.Create);

                fs.Write(srcFile.Data, 0, srcFile.Data.Length);
                fs.Close();

                Console.WriteLine("Done ({0:n1}KB)", srcFile.Data.Length / 1024.0);
            }
            catch (Exception e)
            {
                Console.WriteLine($"** Failed to create '{newPath}': {e.Message}");
                // Skip and continue
            }
        }

        /// <summary>
        /// Creates the output path for a given file relative to the "standard"
        /// disk naming scheme so that the desired (or default) output directory
        /// can be prepended.  Format is DiskName/Dev/Path, where DiskName is the
        /// original image file read (without its extension), Dev is the volume
        /// name, and Path is the full POS path from the Partition on down.
        /// Returns a path using the current host syntax.
        /// </summary>
        public string CreateOutputPath(File sourceFile)
        {
            // Qualify and split the POS path into chunks, then assemble as host path
            var fullPath = sourceFile.Parent.Path + ">" + sourceFile.SimpleName;
            var parts = fullPath.Split(':', '>');
            fullPath = System.IO.Path.Combine(parts);

            // This oughtta be a flag/switch, but for now just force it...
            if (PERQdisk.HostIsUnix) fullPath = fullPath.ToLower();

            // Get and prepend the media file's basename
            var hostPath = System.IO.Path.GetFileNameWithoutExtension(_disk.Filename);
            hostPath = System.IO.Path.Combine(hostPath, fullPath);

            Log.Detail(Category.POS, "CreateOutPath {0} => {1}", sourceFile.SimpleName, hostPath);
            return hostPath;
        }

    }
}
