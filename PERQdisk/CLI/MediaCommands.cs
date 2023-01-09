//
//  MediaCommands.cs
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
using System.Diagnostics;
using System.Collections.Generic;

using PERQemu;
using PERQmedia;

namespace PERQdisk
{
    /// <summary>
    /// Commands for manipulating the media file (labels, metadata, etc).
    /// Most of these are "global" (apply regardless of filesystem type) but
    /// for now they just get multiply defined for each subsystem.
    /// </summary>
    public class MediaCommands
    {

        bool MediaLoaded()
        {
            if (!PERQdisk.IsLoaded)
            {
                Console.WriteLine("No media loaded.");
                return false;
            }

            return true;
        }

        #region List files

        // todo:  REALLY need to make "Global=true" work to eliminate this redundancy:

        [Command("list", "List media files in the current directory")]
        [Command("ls")]
        [Command("pos list")]
        [Command("pos ls")]
        [Command("rt11 list")]
        [Command("rt11 ls")]
        public void ListImages()
        {
            ListImages(Environment.CurrentDirectory);
        }

        [Command("list", "List media files in a given directory")]
        [Command("ls")]
        [Command("pos list")]
        [Command("pos ls")]
        [Command("rt11 list")]
        [Command("rt11 ls")]
        public void ListImages([PathExpand] string path)
        {
            var extensions = new HashSet<string>(FileUtilities.KnownExtensions);
            var matches = new List<string>();

            // Handle ~ expansion on Unix
            path = Paths.Canonicalize(path);

            // EnumerateFiles requires a path, and doesn't accept a blank path
            // to mean "use the current directory"
            if (string.IsNullOrEmpty(path)) path = ".";

            // Unfortunately, EnumerateFiles also can't take anything beyond a
            // simple wildcard, so we grep for files with known extensions manually.
            // This is grody.  I clearly don't care if it's fast. :-)
            foreach (var file in Directory.EnumerateFiles(path, "*"))
            {
                if (extensions.Contains(Path.GetExtension(file)) && !matches.Contains(file))
                {
                    matches.Add(file);
                }
            }

            if (matches.Count == 0)
            {
                Console.WriteLine($"No PERQ media files found in {path}.");
                return;
            }

            matches = matches.ConvertAll(s => s.Substring(path.Length + 1));

            Console.WriteLine($"PERQ media files in {path}:");
            PERQdisk.CLI.Columnify(matches.ToArray(), tabWidth: 25);
        }

        #endregion

        #region Show file info commands

        [Command("pos show media info", "Show info for the currently loaded media file")]
        [Command("rt11 show media info", "Show info for the currently loaded floppy")]
        void ImageInfo()
        {
            ShowDeviceSpecs(PERQdisk.Media);
        }

        [Command("show media info", "Show info for a media file")]
        void ImageInfo([PathExpand] string path)
        {
            path = Paths.FindFileInPath(path, ".", FileUtilities.KnownExtensions);

            if (string.IsNullOrEmpty(path))
            {
                Console.WriteLine($"No match found for '{path}'.");
                return;
            }

            var dev = new StorageDevice(path);

            if (!dev.CanLoad())
            {
                Console.WriteLine($"Could not load '{path}'.");
                return;
            }

            ShowDeviceSpecs(dev);
        }

        /// <summary>
        /// Shows the detailed info for an archived device.
        /// </summary>
        void ShowDeviceSpecs(StorageDevice dev)
        {
            var capMB = dev.Geometry.TotalBytes / 1000000.0;
            var capMiB = dev.Geometry.TotalBytes / 1048576.0;
            var specs = dev.Specs.ToString().Split('\n');

            Console.WriteLine($"Filename:     {dev.Filename}");
            Console.WriteLine($"IsLoaded:     {dev.IsLoaded}");
            Console.WriteLine($"IsModified:   {dev.IsModified}");

            Console.WriteLine();
            Console.WriteLine($"Name / Type:  {dev.Info.Name} (class {dev.Info.Type})");
            Console.WriteLine($"Description:  {dev.Info.Description}");
            Console.WriteLine($"Capacity:     {capMB:N2} MB ({capMiB:N2} MiB), formatted");
            Console.WriteLine($"Geometry:     {dev.Geometry}");
            Console.WriteLine($"Performance:  {specs[0]}");
            Console.WriteLine($"              {specs[1]}");
            Console.WriteLine("Device Flags: {0} {1} {2}",
                              dev.Info.IsRemovable ? "removable," : "fixed,",
                              dev.Info.IsWritable ? "writable," : "readonly,",
                              dev.Info.IsBootable ? "bootable" : "no floppy boot signature");

            Console.WriteLine();
            Console.WriteLine($"File format:  {dev.FileInfo.Format}");
            Console.WriteLine($"File version: {dev.FileInfo.Version}");
            Console.WriteLine($"FSType hint:  {dev.FileInfo.FSType}");
            Console.WriteLine($"Archive date: {dev.FileInfo.ArchiveDate}");
            Console.WriteLine($"Archived by:  {dev.FileInfo.ArchivedBy}");
            Console.WriteLine();

            // Show everything
            ShowTextLabel(dev);
            ShowImageLabel(dev);
        }


        [Command("pos show text label", "Show the loaded file's text label")]
        [Command("rt11 show text label", "Show the loaded floppy's text label")]
        void ShowTextLabel()
        {
            ShowTextLabel(PERQdisk.Media);
        }

        void ShowTextLabel(StorageDevice dev)
        {
            Console.Write("Text label:   ");

            if (dev.FileInfo.TextLabel != null)
            {
                if (dev.FileInfo.TextLabel.Length > 0)
                {
                    Console.WriteLine("\n-----");
                    Console.WriteLine(dev.FileInfo.DecodeTextLabel());
                    Console.WriteLine("-----");
                    return;
                }
            }

            if (dev.FileInfo.Format != Formatters.IMDFormat &&
                dev.FileInfo.Format != Formatters.PRQFormat)
            {
                Console.WriteLine("<Not supported by this format>");
                return;
            }

            Console.WriteLine("<unassigned>");
        }

        [Command("pos show image label", "Show the loaded file's image label")]
        [Command("rt11 show image label", "Show the loaded floppy's image label")]
        void ShowImageLabel()
        {
            ShowImageLabel(PERQdisk.Media);
        }

        void ShowImageLabel(StorageDevice dev)
        {
            Console.Write("Image label:  ");

            if (dev.FileInfo.Format != Formatters.PRQFormat)
            {
                Console.WriteLine("<Not supported by this format>");
                return;
            }

            if (dev.FileInfo.ImageLabel != null)
            {
                // Todo: provide a gui panel/window/popup... or at least try to
                // determine/show the type of image that's attached
                Console.WriteLine($"{dev.FileInfo.ImageLabel.Length} bytes");
                return;
            }

            Console.WriteLine("<unassigned>");
        }

        #endregion

        #region Set file options

        [Command("pos set filesystem type", "Set flag indicating filesystem type")]
        [Command("rt11 set filesystem type", "Set flag indicating filesystem type")]
        void SetFSHint(FilesystemHint fsType)
        {
            if (fsType != PERQdisk.Media.FileInfo.FSType)
            {
                Console.WriteLine($"Filesystem type set to {fsType}.");
                PERQdisk.Media.FileInfo.FSType = fsType;
                PERQdisk.Media.IsModified = true;
            }
        }

        [Command("pos set removable", "Set or clear removable media flag")]
        void SetRemovable(bool isRemovable)
        {
            if (isRemovable != PERQdisk.Media.Info.IsRemovable)
            {
                Console.WriteLine($"Removable media flag set to {isRemovable}.");
                PERQdisk.Media.Info.IsRemovable = isRemovable;
                PERQdisk.Media.IsModified = true;

                if (PERQdisk.Media.Info.Type == DeviceType.Floppy && !isRemovable)
                {
                    Console.WriteLine("Do you REALLY want to flag a floppy disk as non-removable?");
                }
            }
        }

        [Command("pos set archive user", "Set archive's creator name or contact info")]
        [Command("rt11 set archive user", "Set archive's creator name or contact info")]
        void SetArchiveBy(string user)
        {
            if (PERQdisk.Media.FileInfo.ArchivedBy != user)
            {
                Console.WriteLine($"Archive creator set to '{user}'.");
                PERQdisk.Media.FileInfo.ArchivedBy = user;
                PERQdisk.Media.IsModified = true;
            }
        }

        // todo: set archive date - parsing this could be a real pain...
        // todo: set image label - for now, read in a file and save the bytes?

        #endregion

        #region Edit text label

        [Command("pos set text label", "Edit text label of loaded media file")]
        [Command("rt11 set text label", "Edit text label of loaded floppy")]
        void EditTextLabel()
        {
            EditTextLabel(PERQdisk.Media);
        }

        /// <summary>
        /// Invoke an external editor to update the text label.  For Unix, looks
        /// at the $EDITOR environment var and uses it if set, otherwise falls
        /// back to 'vi'.  For Windows, notepad.exe is probably the safest bet.
        /// </summary>
        void EditTextLabel(StorageDevice dev)
        {
            // Create a unique temp file
            var tmp = Path.ChangeExtension(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()), ".txt");
            Log.Debug(Category.UI, "Temp file is '{0}'", tmp);

            // Set up the editor to use
            var editor = string.Empty;

            if (PERQdisk.HostIsUnix)
            {
                editor = Environment.GetEnvironmentVariable("EDITOR");

                if (string.IsNullOrEmpty(editor))
                {
                    editor = "vi";      // But "'ed' is the Standard Unix Editor..." :-)
                }
            }
            else
            {
                editor = "notepad.exe";
            }

            // Copy the existing label, if any, to the temp file
            var label = dev.FileInfo.DecodeTextLabel();
            Log.Debug(Category.UI, "Writing label ({0} bytes) to temp file", label.Length);

            // todo: wrap a try/catch around this just in case...
            File.WriteAllText(tmp, label);

            Console.WriteLine($"Launching external editor '{editor}'...");

            var p = new Process();
            p.StartInfo.FileName = editor;          // what to run
            p.StartInfo.Arguments = tmp;            // what to edit
            p.StartInfo.UseShellExecute = true;

            p.Start();                  // do it!
            p.WaitForExit();            // exit status??

            Log.Write($"Editor returned exit status {p.ExitCode}");

            // Read it back in
            label = File.ReadAllText(tmp);

            // Clean up the temp file
            Log.Debug(Category.UI, "Cleaning up temp file...");
            File.Delete(tmp);

            // If the label has been changed, set IsModified
            if (dev.FileInfo.DecodeTextLabel() != label)
            {
                // Set it and show it
                dev.FileInfo.EncodeTextLabel(label);
                dev.IsModified = true;
                ShowTextLabel(dev);
            }
            else
            {
                Console.WriteLine("Label is unchanged.");
            }
        }

        #endregion

        #region Save commands

        [Command("pos save", "Save changes to the current media file")]
        [Command("rt11 save", "Save changes to the current floppy")]
        void SaveMedia()
        {
            if (!PERQdisk.Media.IsModified)
            {
                Console.WriteLine("No changes.");
                return;
            }

            PERQdisk.Media.Save();
        }

        [Command("pos save with", "Save current media file in a different format")]
        [Command("rt11 save with", "Save current floppy in a different format")]
        void SaveMediaWith(Formatters fmt)
        {
            SaveMediaAs(PERQdisk.Media.Filename, fmt);
        }

        [Command("pos save as", "Save current media file with a new name")]
        [Command("rt11 save as", "Save current floppy with a new name")]
        void SaveMediaAs([PathExpand] string file)
        {
            SaveMediaAs(file, PERQdisk.Media.FileInfo.Format);
        }

        [Command("pos save as", "Save current media file with a new name and format")]
        [Command("rt11 save as", "Save current floppy with a new name and format")]
        void SaveMediaAs([PathExpand] string file, Formatters fmt)
        {
            if (!PERQdisk.Media.CanSaveWithFormat(fmt))
            {
                Console.WriteLine($"Can't save this image using {fmt}!");
                return;
            }

            var ext = FileUtilities.GetExtensionForFormat(fmt);
            var newFile = Paths.QualifyPathname(file, Environment.CurrentDirectory, ext, true);

            Console.WriteLine($"Saving to {newFile}...");

            PERQdisk.Media.Filename = newFile;
            PERQdisk.Media.SaveAsWithFormat(newFile, fmt);

            // Update our window title :-)
            Console.Title = $"{PERQdisk.Version} - {newFile}";
        }

        #endregion

    }
}
