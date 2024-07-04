//
//  POS.FileInfo.cs
//
//  Author:  S. Boondoggle <skeezicsb@gmail.com>
//
//  Copyright (c) 2022-2024, Boondoggle Heavy Industries, Ltd.
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

using PERQmedia;

namespace PERQdisk.POS
{
    /// <summary>
    /// These are taken from the POS module FileTypes.Pas.  Only a few of these
    /// were consistently used/set by the operating system, but it's fun to have
    /// them here (shown by the "info" command).
    /// </summary>
    public enum FileTypes
    {
        UnknownFile = 0,
        SegFile = 1,        // Pascal module (compiled)
        PasFile = 2,        // Pascal source (text)
        DirFile = 3,        // Directory
        ExDirFile = 4,      // Like an ex parrot?
        FontFile = 5,       // .Kst
        RunFile = 6,        // Executable
        TextFile = 7,       // For non-Pas text files
        CursorFile = 8,     // Cursor bin files
        BinaryFile = 9,     // A binary data file?
        BinFile = 10,       // Microcode binary
        MicroFile = 11,     // Microcode source
        ComFile = 12,       // Temp file used by microassembler/placer
        RelFile = 13,       // Temp file used by microassembler/placer
        IncludeFile = 14,   // Included in a .Pas file
        SBootFile = 15,     // System part of boot file
        MBootFile = 16,     // Microcode part
        SwapFile = 17,      // Used for swapping by compiler or editor; length not set
        BadFile = 18,       // Created by the Scavenger
        ForFile = 19,       // Fortran source file
        DatFile = 20,       // Fortran unformatted data file
        PsgFile = 21,       // Fortran pre-seg file
        ExtFile = 22,       // Fortran external definition file
        LibFile = 23,       // Fortran library file
        TempFile = 24       // Created by Temper
    }

    /// <summary>
    /// May be useful if I implement a Scavenger function?  From diskio.pas.
    /// </summary>
    public enum SegmentKind
    {
        Temporary = 0,
        Permanent = 1,
        Bad = 2
    }

    public class TimeStamp
    {
        public TimeStamp(uint value)
        {
            _hour = Limit((value & 0x0000001f), 0, 23);
            _day = Limit((value & 0x000003e0) >> 5, 0, 31);
            _second = Limit((value & 0x0000fc00) >> 10, 0, 59);
            _minute = Limit((value & 0x003f0000) >> 16, 0, 59);
            _month = Limit(((value & 0x003c0000) >> 22) - 1, 0, 11);
            _year = (ushort)(((value & 0xfc000000) >> 26) + 1980);
        }

        byte Limit(uint value, byte low, byte high)
        {
            return (byte)Math.Max(Math.Min(value, high), low);
        }

        /// <summary>
        /// Return a POS standard date string (but with 4-digit years).
        /// </summary>
        public override string ToString()
        {
            return $"{_day:d2}-{_months[_month]}-{_year:d4} {_hour:d2}:{_minute:d2}:{_second:d2}";
        }

        byte _hour;
        byte _day;
        byte _second;
        byte _minute;
        byte _month;
        ushort _year;

        static readonly string[] _months = {
            "Jan", "Feb", "Mar", "Apr", "May", "Jun",
            "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
        };
    }

    /// <summary>
    /// Represents a POS File Information Block (FIB).
    /// </summary>
    public class FileInformationBlock
    {
        public FileInformationBlock(Sector fibSector)
        {
            _fileSize = fibSector.ReadWord(0);

            var tmp = fibSector.ReadWord(2);
            _fileBits = (ushort)(tmp & 0x1fff);     // Bits in last block
            _isSparse = (tmp & 0x2000) != 0;        // True if sparsely allocated
            _isOpen = (tmp & 0xc000) != 0;          // File status: not used in POS!?

            _creationDate = new TimeStamp(fibSector.ReadDWord(4));
            _lastWriteDate = new TimeStamp(fibSector.ReadDWord(8));
            _lastAccessDate = new TimeStamp(fibSector.ReadDWord(12));

            _fileType = fibSector.ReadWord(16);
            _fileName = fibSector.ReadString(23, fibSector.ReadByte(22));
            _simpleName = MakeSimple();

            // Direct indices
            _directIndices = new Address[64];

            for (int i = 0; i < 64; i++)
            {
                _directIndices[i] = new Address(fibSector.ReadDWord(i * 4 + 104), true);
            }

            // Indirect indices
            _indirectIndices = new Address[32];

            for (int i = 0; i < 32; i++)
            {
                _indirectIndices[i] = new Address(fibSector.ReadDWord(i * 4 + 360), true);
            }

            // Double-indirect indices
            _doubleIndirectIndices = new Address[2];

            for (int i = 0; i < 2; i++)
            {
                _doubleIndirectIndices[i] = new Address(fibSector.ReadDWord(i * 4 + 488), true);
            }

            _segmentKind = fibSector.ReadWord(496);
            _blocksInUse = fibSector.ReadWord(498);
            _lastBlock = fibSector.ReadWord(500);
            _lastAddress = fibSector.ReadDWord(502);
            _lastNegativeBlock = fibSector.ReadWord(506);
            _lastNegativeAddress = fibSector.ReadDWord(508);
        }

        // Small hack: when faking partition entries, set this manually 
        public string SimpleName
        {
            get { return _simpleName; }
            set { _simpleName = value; }
        }

        public string FileName => _fileName;
        public ushort FileSize => _fileSize;
        public ushort FileBits => _fileBits;
        public ushort FileType => _fileType;

        public bool IsSparse => _isSparse;
        public bool IsOpen => _isOpen;

        public TimeStamp CreationDate => _creationDate;
        public TimeStamp LastWriteDate => _lastWriteDate;
        public TimeStamp LastAccessDate => _lastAccessDate;

        public Address[] DirectIndices => _directIndices;
        public Address[] IndirectIndices => _indirectIndices;
        public Address[] DoubleIndirectIndices => _doubleIndirectIndices;

        public ushort SegmentKind => _segmentKind;
        public ushort BlocksInUse => _blocksInUse;
        public ushort LastBlock => _lastBlock;
        public ushort LastNegativeBlock => _lastNegativeBlock;
        public uint LastAddress => _lastAddress;
        public uint LastNegativeAddress => _lastNegativeAddress;

        /// <summary>
        /// Derive SimpleName from the full name.  For directories, remove the
        /// .DR extension to make parsing simpler.  The full name is unmodified.
        /// </summary>
        string MakeSimple()
        {
            var simple = _fileName;

            if (_fileName.EndsWith(".DR", StringComparison.Ordinal))
            {
                simple = _fileName.Substring(0, _fileName.Length - 3);
            }

            var dirParts = simple.Split('>');

            if (dirParts.Length > 1)
            {
                simple = dirParts[dirParts.Length - 1];
            }

            // Sanity check!  Flag bad entries (disk requires a Scavenge)
            if (string.IsNullOrWhiteSpace(simple))
            {
                simple = "!!BLANK_FILENAME!!";  // Something that will stand out
            }

            return simple;
        }

        string _fileName;
        string _simpleName;
        ushort _fileType;
        ushort _fileSize;
        ushort _fileBits;
        bool _isSparse;
        bool _isOpen;
        Address[] _directIndices;
        Address[] _indirectIndices;
        Address[] _doubleIndirectIndices;
        TimeStamp _creationDate;
        TimeStamp _lastWriteDate;
        TimeStamp _lastAccessDate;
        ushort _segmentKind;
        ushort _blocksInUse;
        ushort _lastBlock;
        ushort _lastNegativeBlock;
        uint _lastAddress;
        uint _lastNegativeAddress;
    }
}
