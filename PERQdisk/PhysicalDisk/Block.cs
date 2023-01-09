//
//  Block.cs
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

using PERQmedia;

namespace PERQdisk
{
    /// <summary>
    /// Represents a PERQ hard disk Block (sector) Header. (Unused)
    /// </summary>
    //public struct BlockHeader
    //{
    //    uint SerialNum;
    //    ushort LogBlock;
    //    ushort Filler;
    //    uint NextAdr;
    //    uint PrevAdr;
    //}

    /// <summary>
    /// Represents a [cyl, track, sector] triad; additionally contains a bit
    /// indicating whether this block is "logical" (excluding boot sector) or
    /// "physical" (representing actual disk layout).  This could (should)
    /// probably be factored into two classes, inheriting from a common
    /// abstract base class.  But I'm lazy.
    /// </summary>
    public struct Block
    {
        public Block(ushort cylinder, byte head, ushort sector, bool isLogical)
        {
            Cylinder = cylinder;
            Head = head;
            Sector = sector;
            IsLogical = isLogical;
        }

        public ushort Cylinder;
        public byte Head;
        public ushort Sector;
        public bool IsLogical;

        public override string ToString()
        {
            if (IsLogical)
            {
                return $"Logical {Cylinder}/{Head}/{Sector}";
            }

            return $"Physical {Cylinder}/{Head}/{Sector}";
        }
    }

    /// <summary>
    /// Represent a 32-bit disk address as two 16-bit halves.  IsLogical
    /// indicates that this is an LDA (device independent, logical address)
    /// or a PDA (device dependent, physical address including the boot area).
    /// </summary>
    public struct Address
    {
        public Address(uint addr, bool isLogical)
        {
            High = (ushort)((addr & 0xffff0000) >> 16);
            Low = (ushort)(addr & 0x0000ffff);
            IsLogical = isLogical;
        }

        public uint Value => (uint)((High << 16) | Low);

        public ushort Low;
        public ushort High;
        public bool IsLogical;

        public override string ToString()
        {
            if (IsLogical)
            {
                return $"LDA Value={Value:x8} Low={Low:x4} High={High:x4}";
            }

            return $"PDA Value={Value:x8} Low={Low:x4} High={High:x4}";
        }
    }

    /// <summary>
    /// Sector helper: read a word, double word or string from a Sector.
    /// These are likely to be useful to other clients (PERQfloppy, Stut) and
    /// should probably find their way into PERQmedia directly.
    /// </summary>
    public static class SectorHelper
    {

        public static ushort ReadWord(this Sector sec, int offset)
        {
            return Helper.ReadWord(sec.Data, offset);
        }

        public static uint ReadDWord(this Sector sec, int offset)
        {
            if (offset < 0 || offset >= sec.Data.Length - 3)    // need 4 bytes
            {
                throw new ArgumentOutOfRangeException(nameof(offset), offset, "ReadDWord");
            }

            return Helper.ReadDWord(sec.Data, offset);
        }

        public static string ReadString(this Sector sec, int offset, int count)
        {
            if (count < 0 || offset < 0 || offset + count >= sec.Data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset),
                                    $"ReadString bad offset {offset} or count {count}");
            }

            return Helper.ReadString(sec.Data, offset, count);
        }
    }
}
