//
//  MFMDisk.cs
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

namespace PERQdisk
{
    /// <summary>
    /// Represents a generic 5.25" MFM/ST-506 hard disk.
    /// </summary>
    public class MFMDisk : LogicalDisk
    {
        /// <summary>
        /// Convert a logical block number (LBN) into a device independent
        /// logical disk address (LDA).  Note that this is 12 bits for MFM,
        /// not 11 as defined by the Shugart/Micropolis documentation.
        /// </summary>
        public override Address LBNtoLDA(uint lbn)
        {
            var lda = ((Info.Type == DeviceType.Floppy ? 0xe0000000 : 0xc0000000) |
                       ((lbn & 0xfffff) << 8));

            return new Address(lda, true);
        }

        /// <summary>
        /// Convert a logical disk address (LDA) to a logical block number (LBN).
        /// Note the subtle change to 12 significant bits in the high word.
        /// </summary>
        public override uint LDAtoLBN(Address lda)
        {
            return (uint)(((lda.High & 0x0fff) << 8) | ((lda.Low & 0xff00) >> 8));
        }

        /// <summary>
        /// Convert MFM LDA or PDA to Block.
        /// </summary>
        public override Block AddressToBlock(Address addr)
        {
            ushort c;
            byte h;
            ushort s;

            if (addr.IsLogical)
            {
                // LDA to CHS
                var lbn = LDAtoLBN(addr);
                c = (ushort)(lbn / (Geometry.Heads * Geometry.Sectors));
                h = (byte)((lbn - (c * Geometry.Heads * Geometry.Sectors)) / Geometry.Sectors);
                s = (ushort)(lbn % Geometry.Sectors);
            }
            else
            {
                // PDA to CHS
                c = addr.High;
                h = (byte)((addr.Low & 0xff00) >> 8);
                s = (ushort)(addr.Low & 0x00ff);
            }

            return new Block(c, h, s, addr.IsLogical);
        }

        /// <summary>
        /// Convert a Block address (C/H/S) to a MFM PDA or LDA.
        /// </summary>
        public override Address BlockToAddress(Block block)
        {
            uint word = 0;
            Address addr;

            if (block.IsLogical)
            {
                // CHS to LDA
                word = (uint)((block.Cylinder * (block.Head * block.Sector)) +
                              (block.Head * block.Sector) +
                               block.Sector);

                addr = LBNtoLDA(word);
            }
            else
            {
                // CHS to PDA
                word = (uint)((block.Cylinder << 16) |      // high word
                             ((block.Head & 0xff) << 8) |   // low word, high byte
                              (block.Sector & 0xff));       // low word, low byte

                addr = new Address(word, false);
            }

            return addr;
        }

        // Fixed size of boot area (how many tracks to offset logical blocks
        // from the physical start of disk).  For MFM 2 tracks x 16 sectors
        public override uint BootBlocks => 32;

        // Highest logical block number (LBN)
        public override uint MaxLBN => (uint)(Geometry.TotalBlocks - BootBlocks - 1);

    }
}
