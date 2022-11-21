//
//  RT11.VolumeOps.cs
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

namespace PERQdisk.RT11
{
    /// <summary>
    /// High-level RT11 volume operations (file operations).
    /// </summary>
    public partial class Volume
    {
        /// <summary>
        /// Gets a file 'src' from the floppy to the host as 'dst'.  Assumes the
        /// supplied destination file name is fully qualified.  On Unix hosts
        /// will convert line endings depending on the file type and current Mode
        /// setting, potentially prompting for confirmation.
        /// </summary>
        public Doit GetFile(DirectoryEntry src, string dst, bool ask, bool confirm, Mode mode)
        {
            // Sanity check
            if (src.Status != StatusWord.Permanent)
            {
                Console.WriteLine($"** Bad status {src.Status} for {src.Filename}; skipped.");
                return Doit.No;
            }

            // Tidy up the output a bit
            var host = Paths.Canonicalize(dst);

            Doit ans;

            // Ask to copy?
            if (ask)
            {
                ans = PERQdisk.CLI.Yes($"Get [Floppy] {src.Filename} to {host} [y]? ");
                if (ans == Doit.No || ans == Doit.Quit) return ans;
            }

            // Confirm if overwriting an existing file?
            if (System.IO.File.Exists(dst) && confirm)
            {
                Console.WriteLine($"* Destination {host} already exists!");
                if ((ans = PERQdisk.CLI.Yes("Overwrite [n]? ")) != Doit.Yes) return ans;
            }

            // Do translation of line endings based on the mode
            byte[] buf = _dir.ReadFile(src);
            bool convert = false;

            // On Unix, check mode/file mismatches to decide if line endings need
            // convertin'.  Prompts if the "ask" switch is true.  Kinda silly, but
            // since Accent uses Unix-style LFs there's a reason to not always do it
            if (PERQdisk.HostIsUnix)
            {
                if (PERQdisk.CLI.IsBinaryFile(buf))
                {
                    if (mode == Mode.Text)
                    {
                        if (ask)
                        {
                            Console.WriteLine($"* Mode is TEXT but {src.Filename} appears to be BINARY.");
                            if ((ans = PERQdisk.CLI.Yes("Really convert line endings [n]? ")) != Doit.Yes)
                                return ans;

                            // Assume the user knows best?
                            convert = true;
                        }
                        else
                        {
                            Console.WriteLine($"** Binary file {src.Filename} in {mode} mode, skipped.");
                            return Doit.No;
                        }
                    }
                    // Fall through!  (In all other modes we just write the file as is)
                }
                else
                {
                    if (mode == Mode.Binary && ask)
                    {
                        Console.WriteLine($"* Mode is BINARY but {src.Filename} appears to be TEXT.");
                        if ((ans = PERQdisk.CLI.Yes("Convert line endings [y]? ")) == Doit.Quit)
                            return ans;

                        convert = (ans != Doit.No);
                    }
                    else
                    {
                        // Mode == text or auto, or we aren't asking (so just bloody do it)
                        convert = true;
                    }
                }
            }

            // So, what did we learn on the show tonight Craig?
            if (convert)
            {
                // Convert to a string and do the replacement
                var bufStr = Encoding.UTF8.GetString(buf);
                buf = Encoding.UTF8.GetBytes(bufStr.Replace("\r\n", "\n"));

                mode = Mode.Text;   // Aesthetics
            }

            // Now let's try to actually write the file out!
            Console.Write($"  [Floppy] {src.Filename} => [{mode}] {host} ... ");

            try
            {
                if (_dryrun)
                {
                    Console.WriteLine("(dryrun)");
                }
                else
                {
                    var fs = new System.IO.FileStream(dst, System.IO.FileMode.Create);

                    fs.Write(buf, 0, buf.Length);
                    fs.Close();

                    Console.WriteLine("Done ({0:n1}KB).", buf.Length / 1024);
                }

                // todo: if confirm, check before overwriting?
                // todo: set file times?

            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.WriteLine($"** Failed to create '{dst}': {e.Message}");
                return Doit.No;
            }

            return Doit.Yes;
        }

        /// <summary>
        /// Puts a file from the host to the floppy.  Same as Get, in reverse.
        /// </summary>
        public Doit PutFile(string src, string dst, bool ask, bool confirm, Mode mode)
        {
            // Generate the floppy name
            var flopName = Radix50.Format6dot3(Paths.Basename(dst));

            if (string.IsNullOrEmpty(flopName))
            {
                Console.WriteLine($"** Could not generate valid floppy name for '{dst}', skipped.");
                return Doit.No;
            }

            // Clean up the host name and try to read in the file
            var hostName = Paths.Canonicalize(src);

            byte[] buf;

            try
            {
                var fs = new System.IO.FileStream(src, System.IO.FileMode.Open, System.IO.FileAccess.Read);

                // Let's not try to read something stupidly large
                if (fs.Length > _disk.Geometry.TotalBytes)
                {
                    Console.WriteLine($"** {hostName} ({fs.Length} bytes) is too big for this floppy; skipped.");
                    return Doit.No;
                }

                buf = new byte[fs.Length];

                // Slurp it up!
                var len = fs.Read(buf, 0, (int)fs.Length);
                fs.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine($"** Failed to read '{src}': {e.Message}");
                return Doit.No;
            }

            // Make sure we got some data; POS/RT-11 doesn't do so well with zero-length files!?
            if (buf.Length == 0)
            {
                Console.WriteLine($"** File '{hostName}' is empty; skipped.");
                return Doit.No;
            }

            Doit ans;

            // Ask to copy?
            if (ask)
            {
                ans = PERQdisk.CLI.Yes($"Put {hostName} to [Floppy] {flopName} [y]? ");
                if (ans == Doit.No || ans == Doit.Quit) return ans;
            }

            // Determine if line endings should be converted (as with Get, above)
            bool convert = false;

            if (PERQdisk.HostIsUnix)
            {
                if (PERQdisk.CLI.IsBinaryFile(buf))
                {
                    if (mode == Mode.Text)
                    {
                        if (ask)
                        {
                            Console.WriteLine($"* Mode is TEXT but {hostName} appears to be BINARY.");
                            if ((ans = PERQdisk.CLI.Yes("Really convert line endings [n]? ")) != Doit.Yes)
                                return ans;

                            // Assume the user knows best?
                            convert = true;
                        }
                        else
                        {
                            Console.WriteLine($"** Binary file {hostName} in {mode} mode, skipped.");
                            return Doit.No;
                        }
                    }
                }
                else
                {
                    if (mode == Mode.Binary && ask)
                    {
                        Console.WriteLine($"* Mode is BINARY but {hostName} appears to be TEXT.");
                        if ((ans = PERQdisk.CLI.Yes("Convert line endings [y]? ")) == Doit.Quit)
                            return ans;

                        convert = (ans != Doit.No);
                    }
                    else
                    {
                        // Mode == text or auto, or we aren't asking (so just bloody do it)
                        convert = true;
                    }
                }
            }

            if (convert)
            {
                // Same as above, in reverse (LF -> CRLF)
                var bufStr = Encoding.UTF8.GetString(buf);
                buf = Encoding.UTF8.GetBytes(bufStr.Replace("\n", "\r\n"));

                mode = Mode.Text;   // Show we changed the mode (aesthetics)
            }

            var blocks = (buf.Length + 511) / 512;

            // Are we trying to overwrite an existing file?
            if (FileExists(flopName))
            {
                if (confirm)
                {
                    Console.WriteLine($"* File {flopName} already exists on the floppy.");
                    if ((ans = PERQdisk.CLI.Yes("Overwrite [n]? ")) != Doit.Yes) return ans;

                    // Overwrite!
                    _dir.DeleteFile(FindFile(flopName));
                }
                else
                {
                    Console.WriteLine($"** File {flopName} already exists; skipped.");
                    return Doit.No;
                }
            }

            // Okay!  Let's do this thing
            Console.Write($"  [{mode}] {hostName} => {flopName} ... ");

            var status = _dir.EnterFile(flopName, buf);

            // Success!
            if (status == 0)
            {
                Console.WriteLine($"Done ({blocks} blocks).");
                return Doit.Yes;
            }

            Console.WriteLine();

            // Failed: report the error
            switch (status)
            {
                // Bad filename got through?
                case -1:
                    Console.WriteLine($"** Could not store {flopName} on the floppy; skipped.");
                    break;

                // Not enough space
                case -2:
                    if (blocks > _dir.BlocksFree)
                    {
                        Console.WriteLine($"** Not enough free blocks to put {hostName}; skipped.");
                    }
                    else
                    {
                        Console.WriteLine($"** Not enough contiguous free blocks for {hostName}.");
                        Console.WriteLine("   Consider using the COMPRESS command to coalesce free space and try again.");
                    }
                    break;

                // Directory full
                case -3:
                    Console.WriteLine($"** Floppy directory is full!  Can't store any more files.");
                    return Doit.Quit;

                // Unknown error!?
                default:
                    Console.WriteLine($"** Unknown error {status}!  File skipped.");
                    break;
            }

            return Doit.No;
        }

        /// <summary>
        /// Deletes a file from the floppy, updates the directory.
        /// </summary>
        public Doit DeleteFile(DirectoryEntry file, bool confirm)
        {
            if (confirm)
            {
                var ans = PERQdisk.CLI.Yes($"Delete file [Floppy] {file.Filename} [y]? ", true);
                if (ans == Doit.No || ans == Doit.Quit) return ans;
            }

            Console.Write($"  [Floppy] {file.Filename} ... ");

            _dir.DeleteFile(file);

            Console.WriteLine("Deleted.");
            return Doit.Yes;
        }

        /// <summary>
        /// Rename a file on the floppy.
        /// </summary>
        public void RenameFile(DirectoryEntry src, string dst, bool ask)
        {
            var dstName = Radix50.Format6dot3(dst);
            if (string.IsNullOrEmpty(dstName))
            {
                Console.WriteLine($"** Could not create floppy name for '{dst}'.");
                return;
            }

            if (FileExists(dstName))
            {
                Console.WriteLine($"** File {dstName} alreadys exists, cannot rename.");
                return;
            }

            if (ask)
            {
                var ans = PERQdisk.CLI.Yes($"Rename file [Floppy] {src.Filename} to {dstName} [y]? ", true);
                if (ans == Doit.No || ans == Doit.Quit) return;
            }

            Console.Write($"  [Floppy] {src.Filename} => {dstName} ... ");

            var index = FindFileIndex(src.Filename);
            if (index < 0) throw new InvalidOperationException($"Index failure for {src.Filename}");

            // Tweak the name in all the right places
            var file = "";
            var ext = "";
            Radix50.Split6dot3(dstName, ref file, ref ext);

            // Get back the encoded version and update src
            var words = Radix50.StringToRad50(file + ext);
            src.Name0 = words[0];
            src.Name1 = words[1];
            src.Ext = words[2];
            src.Filename = dstName;

            // Write it back
            _dir.Files[index] = src;
            _dir.Save();

            Console.WriteLine("Renamed.");
        }

        /// <summary>
        /// "Compress" (defragment) the floppy by reorganizing the files to squeeze
        /// out all the free blocks, moving files around as needed.  The result is
        /// just one large contiguous chunk of free space at the end.  WAY faster
        /// than on actual hardware!
        /// </summary>
        public void Compress()
        {
            // Count up blocks to coalesce; bail if there aren't at least two
            var chunks = _dir.NumFiles - FindFiles("*").Count;

            if (chunks < 2)
            {
                Console.WriteLine("No compression possible.");
                return;
            }

            Console.Write("Compressing the floppy's free space ... ");

            _dir.Compress();

            Console.WriteLine("Done.");
        }

        /// <summary>
        /// Effectively erase the contents by creating a new, empty directory
        /// and volume headers.  Doesn't wipe the data blocks.
        /// </summary>
        public void Zero()
        {
            // Zap the volume header
            InitVolumeInfo();

            // Blow away the file list
            _dir.Clear();

            // Create one new entry to contain the whole disk
            var f = new DirectoryEntry((ushort)(_disk.MaxLBN - UserStart));
            f.DateVal = DateWord.Encode(DateTime.Today);
            f.Date = DateWord.Decode(f.DateVal);
            f.StartBlock = UserStart;

            // Recreate the directory
            _dir.Files.Add(f);
            _dir.Save();

            // I say we take off and reload the entire volume from orbit.
            // It's the only way to be sure.
            ReadVolumeInfo();
            LoadDirectory();

            Console.WriteLine("Floppy zeroed.");
        }

        /// <summary>
        /// For RT11, the default output path is <appDir>/Output/<disk>/.  We do
        /// by default organize them in a subdirectory so that files extracted
        /// from each floppy land in their own destination folder.
        /// </summary>
        public string DefaultOutputPath()
        {
            // Get and prepend the media file's basename to our default dir!
            return Paths.BuildOutputPath(System.IO.Path.GetFileNameWithoutExtension(_disk.Filename));
        }

        public bool MakeDestPath(string dst)
        {
            // Is it already there?
            if (System.IO.Directory.Exists(dst)) return true;

            // Nope, try to create it (with intermediate dirs as needed)
            try
            {
                if (!_dryrun)
                {
                    var di = System.IO.Directory.CreateDirectory(dst);
                }
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"** Could not create '{dst}': {e.Message}");
                return false;
            }
        }

        private bool _dryrun = true;
    }
}
