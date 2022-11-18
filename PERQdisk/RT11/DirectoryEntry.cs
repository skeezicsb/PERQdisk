//
//  RT11.DirectoryEntry.cs
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

namespace PERQdisk.RT11
{
    /// <summary>
    /// RT11 directory entry status word.
    /// </summary>
    public enum StatusWord : ushort
    {
        None = 0,
        Tentative = 0x100,
        Unused = 0x200,
        Permanent = 0x400,
        Terminator = 0x800
    }

    /// <summary>
    /// An RT11-formatted date encoded in a 16-bit int.  Pointlessly limited to
    /// a very narrow range and aggressively NOT Y2K compliant.
    /// </summary>
    /// <remarks>
    /// RT-11 dates are packed in one 16-bit int:
    ///    bits 14..10   0076000   Mon  ( 0..31 )    31 months?
    ///    bits  9.. 5   0001740   Day  ( 0..31 )
    ///    bits  4.. 0   0000037   Year ( 0..31 )    from 1972!!
    /// 
    /// DEC could have easily shaved a bit off the Month to extend year range
    /// from 1972..2035, or until the end of the 21st century using the sign
    /// bit too.  Insted, they clip the range to 2003 (but I try to limit them
    /// to 1999 so that POS doesn't format >2000 as years 100-102).  Nutty.
    /// </remarks>
    public class DateWord
    {
        public DateWord()
        {
            _raw = Encode(DateTime.Today);
            _cooked = Decode(_raw);
        }

        public DateWord(ushort encoded)
        {
            _raw = encoded;
            _cooked = Decode(_raw);
        }

        public string Decoded => _cooked;
        public ushort Encoded => _raw;

        public static ushort Encode(DateTime date)
        {
            var mon = (date.Month & 0x1f) << 10;
            var day = (date.Day & 0x1f) << 5;
            var yr = date.Year - 1972;

            // Nudge the year into range
            while (yr > 27) { yr -= 10; }

            return (ushort)(mon | day | (yr & 0x1f));
        }

        public static string Decode(ushort encoded)
        {
            var mon = (encoded & 0x7c00) >> 10;
            if (mon > 12) mon = 0;                  // ignore months 13-31 :-/

            var day = (encoded & 0x3e0) >> 5;       // no error/leap yr checks
            var yr = (encoded & 0x1f) + 72;         // 0 == 1972

            return $"{day:#0}-{_months[mon]}-{yr:00}";
        }

        private ushort _raw;
        private string _cooked;

        static readonly string[] _months = {
            "BAD", "JAN", "FEB", "MAR", "APR", "MAY", "JUN",
            "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"
        };
    }

    /// <summary>
    /// One entry in the RT11 floppy directory.
    /// </summary>
    public struct DirectoryEntry
    {
        public DirectoryEntry(ushort size)
        {
            Status = StatusWord.Unused;
            Name0 = 0;
            Name1 = 0;
            Ext = 0;
            Size = size;
            DateVal = 0;
            JobChan = 0;
            StartBlock = 0;
            BitsInLastBlock = 4096;

            _dataBytes = null;
            _extraBytes = null;

            Filename = string.Empty;
            Basename = string.Empty;
            Extension = string.Empty;
            Date = string.Empty;
        }

        public DirectoryEntry(byte[] buf)
        {
            // Populate the entry from the sector data
            Status = (StatusWord)Helper.ReadWord(buf, 0);
            Name0 = Helper.ReadWord(buf, 2);
            Name1 = Helper.ReadWord(buf, 4);
            Ext = Helper.ReadWord(buf, 6);
            Size = Helper.ReadWord(buf, 8);
            JobChan = Helper.ReadWord(buf, 10);
            DateVal = Helper.ReadWord(buf, 12);

            // Copy the "extra" bytes that RT11 allows for customization
            _extraBytes = Helper.CopyFrom(buf, 14, buf.Length - 14);

            // POS uses the first two for the BitsInLastBlock field, so there
            // has to be at least that many!
            BitsInLastBlock = Helper.ReadWord(_extraBytes, 0);

            Filename = string.Empty;
            Basename = string.Empty;
            Extension = string.Empty;
            Date = string.Empty;

            // Pass this in? 
            StartBlock = 0;

            // If this is a "permanent" entry, decode these
            if (Status == StatusWord.Permanent || Status == StatusWord.Tentative)
            {
                Basename = Radix50.Rad50ToString(new ushort[] { Name0, Name1 });
                Extension = Radix50.Rad50ToString(new ushort[] { Ext });
                Filename = Radix50.Join6dot3(Basename, Extension);
                Date = DateWord.Decode(DateVal);
            }

            // The file's contents if we want to read or write it;
            // don't see much point in a separate RT11.File class that
            // just wraps a byte[]... but that could change?
            _dataBytes = null;
        }

        public StatusWord Status;
        public ushort Name0;                // Rad50 encoded filename
        public ushort Name1;                // Rad50 filename, 2nd word
        public ushort Ext;                  // Rad50 extension word
        public ushort Size;
        public ushort JobChan;
        public ushort DateVal;
        public ushort BitsInLastBlock;
        public ushort StartBlock;

        public string Basename;             // Filename part only
        public string Extension;            // Extension only
        public string Filename;             // Complete filename
        public string Date;                 // Readable form

        public byte[] Data
        {
            get { return _dataBytes; }
            set { _dataBytes = value; }
        }

        public void ToBuffer(ref byte[] buf, int offset)
        {
            Helper.WriteWord(buf, offset, (ushort)Status);
            Helper.WriteWord(buf, offset + 2, Name0);
            Helper.WriteWord(buf, offset + 4, Name1);
            Helper.WriteWord(buf, offset + 6, Ext);
            Helper.WriteWord(buf, offset + 8, Size);
            Helper.WriteWord(buf, offset + 10, JobChan);
            Helper.WriteWord(buf, offset + 12, DateVal);
            Helper.WriteWord(buf, offset + 14, BitsInLastBlock);
        }

        private byte[] _extraBytes;
        private byte[] _dataBytes;
    }
}
