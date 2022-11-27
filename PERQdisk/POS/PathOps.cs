//
//  POS.Volume.PathOps.cs
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

namespace PERQdisk.POS
{
    /// <summary>
    /// Validate and manipulate path names in a POS volume.
    /// </summary>
    public partial class Volume
    {
        /// <summary>
        /// Implements the POS Shell "path" command to set the current working directory.
        /// </summary>
        public void SetPath(string path)
        {
            var dir = ResolveDirectory(path);

            if (dir == null)
            {
                Console.WriteLine($"Can't change path to '{path}'.");
                return;
            }

            _path = dir;
            if (_path.Depth == 0) Console.WriteLine("At device root");
        }

        /// <summary>
        /// Fully resolve the specified pattern.
        /// </summary>
        public POSPath Resolve(string pat)
        {
            return new POSPath(this, pat);
        }

        /// <summary>
        /// Resolve a path to a directory.  If it isn't a valid directory or the
        /// file pattern comes back non-empty (meaning they specified a file, not
        /// a directory) return null.
        /// </summary>
        private Directory ResolveDirectory(string path)
        {
            var dir = Resolve(path);

            // Throw out bad stuff
            if (!dir.IsValid || dir.IsFilePath) return null;

            // Allow pseudo directories
            if (dir.IsDevPath) return Root;

            if (dir.IsPartPath && dir.Partitions.Count == 1)
                return dir.Partitions[0].Root;

            // It's a directory!
            if (dir.IsDirPath)
            {
                // A wildcard that resolves to exactly one directory is OK too
                if (!dir.IsDirWild || (dir.IsDirWild && dir.Directories.Count == 1))
                    return dir.Directories[0];
            }

            // Everything else is a nope
            return null;
        }

        /// <summary>
        /// Find a Partition by name.  Return null if no such partition exists.
        /// </summary>
        public Partition ResolvePartition(string partName)
        {
            foreach (var p in Partitions)
            {
                if (p.Name.Equals(partName, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }

            return null;
        }
    }


    public class POSPath
    {
        /// <summary>
        /// Represents a fully-qualified POS path name, with each component
        /// (device, partition, subdirectories and files) enumerated separately.
        /// This is wildly over the top.
        /// </summary>
        public POSPath(Volume vol, string pat)
        {
            if (vol == null) throw new InvalidOperationException("Cannot resolve from null volume");
            if (pat == null) throw new InvalidOperationException("Cannot resolve from null path");

            _volume = vol;
            _original = pat;

            _wildParts = new List<Partition>();
            _wildDirs = new List<Directory>();
            _wildFiles = new List<File>();

            Clear();

            // Is the path relative?
            if (pat.IndexOf(':') < 0)
            {
                // Yes: prepend the stringified cwd (guaranteed to include the
                // dev: part at minimum) and resolve as an absolute path
                _original = vol.Current.Path + pat;
            }

            // Does the dirty work
            Qualify();
        }

        public bool IsValid => _isValid;

        public string DevPart => _devPart;
        public bool IsDevPath => _isValid && !string.IsNullOrEmpty(_devPart) && !IsPartPath && !IsDirPath && !IsFilePath;

        public string PartPart => _partPart;
        public bool IsPartPath => _isValid && !string.IsNullOrEmpty(_partPart) && !IsDirPath && !IsFilePath;
        public bool IsPartWild => Paths.IsPattern(_partPart);
        public List<Partition> Partitions => _wildParts;

        public string DirPart => _dirPart;
        public bool IsDirPath => _isValid && !string.IsNullOrEmpty(_dirPart) && !IsFilePath;
        public bool IsDirWild => Paths.IsPattern(_dirPart);
        public List<Directory> Directories => _wildDirs;

        public string FilePart => _filePart;
        public bool IsFilePath => _isValid && !string.IsNullOrEmpty(_filePart);
        public bool IsFileWild => Paths.IsPattern(_filePart);
        public List<File> Files => _wildFiles;


        /// <summary>
        /// Clear and invalidate this instance.
        /// </summary>
        public void Clear()
        {
            _isValid = false;

            _devPart = string.Empty;
            _partPart = string.Empty;
            _dirPart = string.Empty;
            _filePart = string.Empty;
            _resolved = string.Empty;

            _wildParts.Clear();
            _wildDirs.Clear();
            _wildFiles.Clear();
        }

        // debug
        public void Dump()
        {
            if (!_isValid)
            {
                Console.WriteLine($"Path '{_original}' is NOT valid");
                //return;
            }

            Console.WriteLine($"Breakdown of path '{_original}':");
            Console.WriteLine($"  Device:     {DevPart} \tIsDevPath={IsDevPath}");
            Console.WriteLine($"  Partition:  {PartPart}\tIsPartPath={IsPartPath}  Wild={IsPartWild}  Count={_wildParts.Count}");
            if (_wildParts.Count > 0)
            {
                foreach (var p in _wildParts) { Console.WriteLine($"    Matched {p.Name}"); }
            }

            Console.WriteLine($"  Directory:  {DirPart} \tIsDirPath={IsDirPath}  Wild={IsDirWild}  Count={_wildDirs.Count}");
            if (_wildDirs.Count > 0)
            {
                foreach (var d in _wildDirs) { Console.WriteLine($"    Matched {d.Name} ({d.DirFile.FullName})"); }
            }

            Console.WriteLine($"  File part:  {FilePart}\tIsFilePath={IsFilePath}  Wild={IsFileWild}  Count={_wildFiles.Count}");
            if (_wildFiles.Count > 0)
            {
                foreach (var f in _wildFiles) { Console.WriteLine($"    Matched {f.SimpleName} ({f.FullName})"); }
            }

            Console.WriteLine($"  Unresolved: '{_resolved}'");
        }

        /// <summary>
        /// Fully qualifies the original pathname, resolving all wildcards and
        /// producing lists of matching partitions, directories and files.  This
        /// is a Cold War nuclear stockpile level of overkill.
        /// </summary>
        private void Qualify()
        {
            _isValid = false;

            // Get dev: part; if no more left to process, we're done
            if (QualifyDev()) return;

            // Same for the partition(s)
            if (QualifyPart()) return;

            // Still going? Resolve directories and files
            QualifyDir();
        }

        /// <summary>
        /// Make sure the dev: part of the path is valid (if given, matches the
        /// the mounted volume name; otherwise supplies it).  Sets _devPart and
        /// returns true if parsing is complete, false if there's more to parse.
        /// </summary>
        private bool QualifyDev()
        {
            // Get the device separator
            var idx = _original.IndexOf(':');

            if (idx < 0)
            {
                return true;        // No ':' so not an absolute path
            }

            // Isolate the device name, supply default if missing (":part>" form)
            var devStr = (idx == 0 ? _volume.Name : _original.Substring(0, idx));

            // Make sure it matches the loaded volume
            if (!devStr.Equals(_volume.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;        // Nope, wrong dev
            }

            // Simplify what's left
            _resolved = ResolveDots(_original.Substring(idx + 1));

            _devPart = $"{devStr}:";
            _isValid = true;

            return string.IsNullOrEmpty(_resolved);
        }

        /// <summary>
        /// Pull out any ".." or "." elements in a path (string).
        /// </summary>
        private string ResolveDots(string path)
        {
            var stack = new Stack<string>();
            var result = string.Empty;

            foreach (var p in path.Split('>'))
            {
                // Ignore empty parts (i.e., :foo>>bar>)?
                if (p == "" || p == ".") continue;

                // Going up?
                if (p == "..")
                {
                    // At root, should disallow this, but meh
                    if (stack.Count > 0)
                        stack.Pop();

                    continue;
                }

                stack.Push(p);
            }

            // Anything left?
            if (stack.Count > 0)
            {
                var pathElts = stack.ToArray();
                Array.Reverse(pathElts);                // Ugh!
                result = string.Join(">", pathElts);
            }

            return result;
        }

        /// <summary>
        /// Qualify the first level of the directory (partition) structure.  In
        /// a valid POS path, the _minimum_ required is a "dev:part>" form, but
        /// in PERQdisk we allow for just "dev:" since that's handy for operating
        /// on the entire device (i.e., "copy sys:").
        /// </summary>
        /// <returns>
        /// True if a valid partition is present and there is further processing
        /// required, or false if the path has been exhausted.  Sets _isValid to
        /// indicate status and populates _partPart, _wildParts appropriately.
        /// Removes the partition spec from _resolved.
        /// </returns>
        private bool QualifyPart()
        {
            var partStr = string.Empty;

            // Get the first directory separator, if any
            var idx = _resolved.IndexOf('>');

            if (idx < 0)
            {
                // No trailing '>', so dev:part form
                partStr = _resolved;
                _resolved = string.Empty;
            }
            else
            {
                // RemoveDots will have zapped any empty path elements, so POS
                // paths like ">bin" (that rely on the "default partition")
                // aren't valid here, since we don't have a default to use.
                partStr = _resolved.Substring(0, idx);
                _resolved = _resolved.Substring(idx + 1);
            }

            // While not legal in POS (?), we allow wildcards in the partition
            // specification (i.e., "copy :part*" or "info sys:*")
            if (Paths.IsPattern(partStr))
            {
                // Get the matching names
                foreach (var p in _volume.Root.FindDirectories(partStr))
                {
                    // Add the actual Partition entry (not the ROOT.DR Directory)
                    _wildParts.Add(_volume.ResolvePartition(p.Name));
                }

                // No match?  Bad part means YOU GET NOTHING. GOOD DAY SIR.
                if (_wildParts.Count == 0)
                {
                    _isValid = false;
                    return true;
                }

                // Exactly one match?
                if (_wildParts.Count == 1)
                {
                    _partPart = _wildParts[0].Name;         // Resolve as PartPath
                }
                else
                {
                    _partPart = partStr;                    // Save as wildcard
                }
            }
            else
            {
                // Just the one, then
                var p = _volume.ResolvePartition(partStr);

                if (p == null)
                {
                    _isValid = false;
                    return true;
                }

                _wildParts.Add(p);
                _partPart = p.Name;
            }

            return string.IsNullOrEmpty(_resolved);
        }

        /// <summary>
        /// If a directory spec is given (name or wildcard), chase down all of
        /// the matches and build one (flattened) list.  Allows for wildcards
        /// in the partition too.
        /// </summary>
        /// <returns>
        /// Continues from the partition level by level down the hierarchy, and
        /// if at least one match exists considers that a success.  At the final
        /// leaf node, no matches means we need to look for files; if we match
        /// _exactly_ one directory then assume that's what was specified and
        /// we're done, otherwise return false and set _resolved to continue.
        /// </returns>
        private void QualifyDir()
        {
            // Break down the unresolved path into its directory components
            var levels = _resolved.Split('>');
            var foundParts = new List<Partition>();

            _isValid = false;

            // Build a new list of partitions that actually contain a match
            foreach (var p in _wildParts)
            {
                // Depth-first search for each matched partition to fill in
                // matching directories, files
                if (DoOneLevel(p.Root, levels, 0) > 0)
                {
                    foundParts.Add(p);
                }
            }

            _wildParts = foundParts;

            //
            // Figure out what we got.  We were given (at minimum) a dev:part>
            // specification that should have matched at least one subdirectory
            // or file, but if nothing came back then bail out - either the name
            // didn't resolve or the wildcard didn't match anything
            //
            if (_wildParts.Count == 0)
            {
                _partPart = string.Empty;               // No matches, bad path!
                return;
            }

            if (_wildParts.Count == 1)
            {
                _partPart = _wildParts[0].Name;         // Resolve it
            }

            //
            // POS can store files at the partition level, so it's possible we
            // can end up with no matching directories but have files!  If BOTH
            // are zero then we really didn't match anything, and that's a fail.
            //
            if (_wildDirs.Count == 0 && _wildFiles.Count == 0)
            {
                return;
            }

            _isValid = true;
            _resolved = string.Empty;

            if (_wildDirs.Count == 1)
            {
                // Resolve to exactly one matching dir.  Sigh.
                _dirPart = _wildDirs[0].DirFile.FullName.Replace(".DR", "");
            }
            else
            {
                // Reassemble the directory portion of the original pattern
                _dirPart = string.Join(">", levels, 0, levels.Length - 1);
            }

            if (_wildFiles.Count == 0) return;

            // Save the wildcard pattern (leaf element)
            _filePart = levels[levels.Length - 1];
        }

        /// <summary>
        /// Match a directory pattern and return the number of hits.  Starts at
        /// the specified level in the hierarchy and does a depth-first search,
        /// adding directories (or plain files at the leaf node).
        /// </summary>
        private int DoOneLevel(Directory dir, string[] path, int level)
        {
            var count = 0;
            var pad = " ".PadLeft(level * 2 + 5);   // debug
            var atLeafNode = (level == path.Length - 1);
            var fileStr = path[level];

            if (atLeafNode)
            {
                if (Paths.IsPattern(fileStr))
                {
                    // Save any file that matches (including directories)
                    foreach (var f in dir.FindFiles(fileStr))
                    {
                        _wildFiles.Add(f);
                        count++;
                    }
                }
                else
                {
                    // Special case: match as a directory (first) before checking
                    // to see if it's a plain file.  Order matters here, which is
                    // inexcusably bad but is necessary due to the terrible "trim
                    // .DR off directory file names hack" when building the tree.
                    // Ugh.
                    if (dir.ContainsDir(fileStr))
                    {
                        _wildDirs.Add(dir.FindDirectory(fileStr));
                        count++;
                        return count;
                    }

                    if (dir.ContainsFile(fileStr))
                    {
                        _wildFiles.Add(dir.FindFile(fileStr));
                        count++;
                    }
                }

                if (count > 0)
                {
                    // Save our parent dir since it contains a match!
                    _wildDirs.Add(dir);
                }

                return count;
            }

            // Haven't reached the edge, continue the search
            foreach (var d in dir.FindDirectories(path[level]))
            {
                count += DoOneLevel(d, path, level + 1);
            }

            return count;
        }


        private Volume _volume;

        private string _original;
        private string _devPart;
        private string _partPart;
        private string _dirPart;
        private string _filePart;
        private string _resolved;

        private List<Partition> _wildParts;
        private List<Directory> _wildDirs;
        private List<File> _wildFiles;

        private bool _isValid;
    }
}
