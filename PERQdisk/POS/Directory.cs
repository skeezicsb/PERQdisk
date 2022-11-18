//
//  POS.Directory.cs
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
using System.Text.RegularExpressions;

namespace PERQdisk.POS
{
    /// <summary>
    /// Represents the structure of a single directory entry in a directory file.
    /// </summary>
    public class DirectoryEntry
    {
        public DirectoryEntry(byte[] data, int offset)
        {
            var tmp = Helper.ReadWord(data, offset);
            _inUse = (tmp & 0x1) != 0;          // This one counts
            _deleted = (tmp & 0x2) != 0;        // I'm just curious to see if
            _archived = (tmp & 0x4) != 0;       // these were ever used...

            _fibAddress = new Address(Helper.ReadDWord(data, offset + 2), true);
            _simpleName = Helper.ReadString(data, offset + 7, data[offset + 6]);
        }

        public bool InUse => _inUse;
        public bool Deleted => _deleted;
        public bool Archived => _archived;
        public Address FIBAddress => _fibAddress;
        public string SimpleName => _simpleName;


        bool _inUse;
        bool _deleted;
        bool _archived;
        Address _fibAddress;
        string _simpleName;
    }

    /// <summary>
    /// Expands a flat POS directory file into a hierarchical structure that is
    /// cached in memory and can be easily navigated from the CLI.
    /// </summary>
    public class Directory
    {
        /// <summary>
        /// Load a directory for fast access to its contents (by name): all files
        /// are cached in the Contents list, while subdirectories are also split
        /// into the Children list.  This lets the CLI do full tab completion in
        /// some cases, while the "path" command can complete on just directory
        /// names.  It's geeeenyus I tell ya.
        /// </summary>
        public Directory(LogicalDisk disk, File dirFile, Directory parent, int depth)
        {
            _disk = disk;
            _file = dirFile;
            _parent = parent;
            _depth = depth;

            _name = _file.SimpleName;
            _fullName = _parent.Path + _name + ">";

            Load();
        }

        /// <summary>
        /// Ok, now this is hideous and embarassing and you should take pity and
        /// look away.  This constructor sets up a new root instance to avoid
        /// a bunch of hoops to fake up a root directory, which POS doesn't have
        /// at the device level.  So this semi-grotesque workaround, born of sheer
        /// laziness, is arguably a fairly elegant solution.  (Naw, I don't believe
        /// that either.  Meh.)
        /// </summary>
        public Directory(LogicalDisk disk, string name)
        {
            _disk = disk;
            _file = null;
            _name = name;
            _fullName = _name + ":";            // Our story so far
            _depth = 0;
            _parent = this;                     // I'm my own grandpa...
            _children = new List<Directory>();  // Populated by partitions
            _contents = new List<File>();       // Empty at the device level
        }

        public Directory Parent => _parent;
        public List<File> Contents => _contents;
        public List<Directory> Children => _children;

        public File DirFile => _file;
        public string Name => _name;
        public string Path => _fullName;
        public int Depth => _depth;


        /// <summary>
        /// Reads the child files of this directory into memory.
        /// </summary>
        public void Load()
        {
            if (_file == null)
            {
                throw new InvalidOperationException("Cannot load from null directory");
            }

            if (!_file.IsDirectory)
            {
                throw new InvalidOperationException("Attempt to load directory from a non-directory file");
            }

            _children = new List<Directory>();
            _contents = new List<File>();

            for (int i = 0; i < _file.Data.Length; i += 32)
            {
                var entry = new DirectoryEntry(_file.Data, i);

                if (entry.InUse)
                {
                    var f = new File(_disk, this, entry.FIBAddress);
                    _contents.Add(f);

                    if (f.IsDirectory)
                    {
                        _depth++;
                        _children.Add(new Directory(_disk, f, this, _depth));
                        _depth--;
                    }
                }
            }
        }

        public bool ContainsFile(string name)
        {
            return _contents.Exists(x => x.SimpleName.ToLower() == name.ToLower());
        }

        public bool ContainsDir(string dir)
        {
            return _children.Exists(x => x.Name.ToLower() == dir.ToLower());
        }

        public File FindFile(string name)
        {
            return _contents.Find(x => x.SimpleName.ToLower() == name.ToLower());
        }

        public List<File> FindFiles(string pattern)
        {
            var match = Paths.MakePattern(pattern);

            return _contents.FindAll(x => Regex.IsMatch(x.SimpleName, match, RegexOptions.IgnoreCase));
        }

        public Directory FindDirectory(string dir)
        {
            return _children.Find(x => x.Name.ToLower() == dir.ToLower());
        }

        public List<Directory> FindDirectories(string pattern)
        {
            var match = Paths.MakePattern(pattern);

            return _children.FindAll(x => Regex.IsMatch(x.Name, match, RegexOptions.IgnoreCase));
        }

        public List<string> GetFiles()
        {
            return _contents.ConvertAll(new Converter<File, string>(ContentName));
        }

        public List<string> GetDirectories()
        {
            return _children.ConvertAll(new Converter<Directory, string>(ChildName));
        }

        public static string ContentName(File f)
        {
            return f.SimpleName;
        }

        public static string ChildName(Directory d)
        {
            return d.Name;
        }

        public static File DirToFile(Directory d)
        {
            return d.DirFile;
        }

        private LogicalDisk _disk;
        private File _file;
        private Directory _parent;
        private List<Directory> _children;
        private List<File> _contents;

        private string _name;
        private string _fullName;
        private int _depth;
    }
}
