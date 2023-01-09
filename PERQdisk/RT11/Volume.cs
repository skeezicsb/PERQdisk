//
//  RT11.Volume.cs
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
using System.Collections.Generic;
using System.Text.RegularExpressions;

using PERQemu;
using PERQmedia;

namespace PERQdisk.RT11
{

    /// <summary>
    /// Represents the high-level structure of an RT-11 floppy image.
    /// </summary>
    public partial class Volume
    {
        public Volume(RT11Floppy disk)
        {
            if (disk == null) throw new ArgumentNullException(nameof(disk));

            _disk = disk;
            _dir = new Directory(this);
            _dryrun = false;

            if (disk.IsLoaded)
            {
                ReadVolumeInfo();
            }
            else
            {
                InitVolumeInfo();
            }
        }

        public RT11Floppy Media => _disk;

        public ushort AvailableSegments => _segsAvail;
        public ushort NextSegment => _nextSeg;
        public ushort HighSegment => _highSeg;
        public ushort ExtraBytes => _extraBytes;
        public ushort StartSegment => _startSeg;

        public Directory Dir => _dir;

        public bool DryRun
        {
            get { return _dryrun; }
            set { _dryrun = value; }
        }

        //
        // RT11 constants
        //
        public const int DirStart = 6;
        public const int DirSegMax = 4;
        public const int DirSegSize = 1024;
        public const int DirEntSize = 14;
        public const int DirEntOffset = 10;
        public const int MaxExtraBytes = 8;
        public const int POSExtraBytes = 2;
        public const int UserStart = DirStart + (DirSegMax * 2);

        /// <summary>
        /// Initialize an empty volume with default values.  This allows us to
        /// create new, blank floppies from thin air (outside of PERQemu).
        /// </summary>
        void InitVolumeInfo()
        {
            _segsAvail = DirSegMax;
            _nextSeg = 0;
            _highSeg = 1;
            _extraBytes = POSExtraBytes;
            _startSeg = UserStart;
            Log.Info(Category.RT11, "New volume initialized");
        }

        /// <summary>
        /// Load and cache the floppy directory.  There are up to four directory
        /// "segments" of 1KB each, starting at LBN 6.  We read the volume header
        /// for the initial segment, then repeat if necessary for the next.
        /// </summary>
        public void ReadVolumeInfo()
        {
            var buf = _disk.GetSector(DirStart);  // Just need the header part

            _segsAvail = buf.ReadWord(0);
            _nextSeg = buf.ReadWord(2);
            _highSeg = buf.ReadWord(4);
            _extraBytes = buf.ReadWord(6);
            _startSeg = buf.ReadWord(8);
        }


        public void PrintVolInfo()
        {
            Console.WriteLine("RT11 Volume Header:");
            Console.WriteLine($"  Available segs:  {AvailableSegments}");
            Console.WriteLine($"  Next segment:    {NextSegment}");
            Console.WriteLine($"  High segment:    {HighSegment}");
            Console.WriteLine($"  Extra bytes:     {ExtraBytes}");
            Console.WriteLine($"  Start segment:   {StartSegment}");
        }


        /// <summary>
        /// Verify the volume header and make sure the loaded device looks like
        /// it contains a valid RT11-formatted filesystem.
        /// </summary>
        public bool CheckVol()
        {
            if ((AvailableSegments < HighSegment) || (StartSegment != UserStart))
            {
                Log.Debug(Category.RT11, "Bad directory - not a valid RT-11 image");
                return false;
            }

            if (ExtraBytes > MaxExtraBytes)
            {
                // This might be an Accent "superfloppy"?  Have to create one and see
                Log.Debug(Category.RT11, "Too many extra bytes - not a valid RT-11 image");
                return false;
            }

            if (ExtraBytes == 0)
            {
                Log.Debug(Category.RT11, "Too few extra bytes = can't save 'BitsInLastBlk'");
                return false;
            }

            if (ExtraBytes != POSExtraBytes)
            {
                // warning
                Log.Info(Category.RT11, "WARNING: Extra bytes != {0}, not a PERQ floppy?", POSExtraBytes);
                // try to continue...
            }

            // Must be good then!
            return true;
        }

        /// <summary>
        /// Do this separately so that Probe() doesn't have to do all that work
        /// (and get bogus results) when not reading an RT11 floppy!
        /// </summary>
        public void LoadDirectory()
        {
            _dir.Load();
        }

        
        /// <summary>
        /// Returns status of a floppy file lookup.
        /// </summary>
        public bool FileExists(string name)
        {
            return _dir.Files.Exists(x => x.Filename.Equals(name));
        }

        /// <summary>
        /// Finds a named file on the floppy.  Exact match, no wildcards.
        /// </summary>
        public DirectoryEntry FindFile(string name)
        {
            var match = Radix50.Format6dot3(name);

            return _dir.Files.Find(x => x.Filename.Equals(match));
        }

        /// <summary>
        /// Find a file on the floppy and return its index in the directory.
        /// The name is assumed to be in proper floppy 6.3 format.
        /// </summary>
        /// <returns>
        /// >= 0 - index of file
        ///   -1 - not found
        /// </returns>
        public int FindFileIndex(string name)
        {
            for (var i = 0; i < _dir.Files.Count; i++)
            {
                if (_dir.Files[i].Filename.Equals(name)) return i;
            }
            return -1;
        }

        /// <summary>
        /// Finds files that match a given wildcard pattern.
        /// </summary>
        public List<DirectoryEntry> FindFiles(string pattern)
        {
            var match = Paths.MakePattern(pattern);

            return _dir.Files.FindAll(x => (x.Status == StatusWord.Permanent) &&
                              Regex.IsMatch(x.Filename, match, RegexOptions.IgnoreCase));
        }

        ushort _segsAvail;
        ushort _nextSeg;
        ushort _highSeg;
        ushort _extraBytes;
        ushort _startSeg;

        RT11Floppy _disk;
        Directory _dir;

        bool _dryrun;

    }
}
