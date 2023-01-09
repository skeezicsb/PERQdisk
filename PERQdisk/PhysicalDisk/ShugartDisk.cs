//
//  ShugartDisk.cs
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

namespace PERQdisk
{
    /// <summary>
    /// Represents a classic Shugart SA4000-series hard disk.
    /// </summary>
    public class ShugartDisk : LogicalDisk
    {
        /// <summary>
        /// Convert Shugart LDA or PDA to Block.
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
                c = (ushort)((addr.Low & 0xff00) >> 8);
                h = (byte)((addr.Low & 0x00e0) >> 5);
                s = (ushort)(addr.Low & 0x001f);
#if DEBUG
                if (addr.High != 0)
                    Console.WriteLine($"Warning: Shugart high word not zero! ({addr.High})");
#endif
            }

            return new Block(c, h, s, addr.IsLogical);
        }

        /// <summary>
        /// Convert a Block address (C/H/S) to a Shugart PDA or LDA.
        /// </summary>
        public override Address BlockToAddress(Block block)
        {
            uint word = 0;
            Address addr;

            if (block.IsLogical)
            {
                // CHS to LDA
                word = (uint)((block.Cylinder * (Geometry.Heads * Geometry.Sectors)) +
                              (block.Head * Geometry.Sectors) +
                               block.Sector);

                addr = LBNtoLDA(word);
            }
            else
            {
                // CHS to PDA
                word = (uint)(((block.Cylinder & 0xff) << 8) |
                                 ((block.Head & 0x07) << 5) |
                                 (block.Sector & 0x1f));
                
                addr = new Address(word, false);
            }

            return addr;
        }

        // Fixed size of boot area (how many tracks to offset logical blocks
        // from the physical start of disk).  For Shugart, 1 track (30 sectors)
        public override uint BootBlocks => 30;

        // Highest logical block number (LBN)
        public override uint MaxLBN => (uint)(Geometry.TotalBlocks - BootBlocks - 1);

        /// <summary>
        /// Convert a logical block to a physical block (offset over boot area).
        /// </summary>
        public override Block LogicalBlockToPhysicalBlock(Block logBlock)
        {
            VerifyLogical(logBlock);

            // Move this forward one track to compensate for the boot area
            logBlock.Head++;

            if (logBlock.Head >= Geometry.Heads)
            {
                logBlock.Head = 0;
                logBlock.Cylinder++;

                if (logBlock.Cylinder >= Geometry.Cylinders)
                {
                    throw new InvalidOperationException("Went off the end of the disk converting to physical.");
                }
            }

            logBlock.IsLogical = false;

            return logBlock;
        }

        public override Block PhysicalBlockToLogicalBlock(Block physBlock)
        {
            VerifyPhysical(physBlock);

            // Move back one track to compensate for the boot area
            if (physBlock.Head == 0)
            {
                physBlock.Head = (byte)(Geometry.Heads - 1);

                if (physBlock.Cylinder == 0)
                {
                    throw new InvalidOperationException("Went off the beginning of the disk converting to logical.");
                }
                physBlock.Cylinder--;
            }
            else
            {
                // Adjust by the size of the boot area (in tracks)
                physBlock.Head--;    // = (byte)(physBlock.Head - 1);
            }

            physBlock.IsLogical = true;

            return physBlock;
        }
    }
}
