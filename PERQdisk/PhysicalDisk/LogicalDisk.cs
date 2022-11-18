//
//  LogicalDisk.cs
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

using PERQmedia;

namespace PERQdisk
{
    /// <summary>
    /// Wraps the physical, block-addressed StorageDevice with the address
    /// translation mechanisms necessary to read and interpret POS filesystems.
    /// </summary>
    public abstract class LogicalDisk : StorageDevice
    {
        public virtual uint BootBlocks { get; }
        public virtual uint MaxLBN { get; }

        /// <summary>
        /// Get the sector pointed to by a physical or logical block address.
        /// </summary>
        public virtual Sector GetSector(Block block)
        {
            // Get physical block if this is a logical one
            if (block.IsLogical)
            {
                block = LogicalBlockToPhysicalBlock(block);
            }

            return Read(block.Cylinder, block.Head, block.Sector);
        }

        /// <summary>
        /// Get the sector pointed to by a physical or logical address.
        /// </summary>
        public virtual Sector GetSector(Address addr)
        {
            return GetSector(AddressToBlock(addr));
        }

        /// <summary>
        /// Convert a logical block number (LBN) into a device independent
        /// logical disk address (LDA).
        /// </summary>
        public virtual Address LBNtoLDA(uint lbn)
        {
            var lda = ((Info.Type == DeviceType.Floppy ? 0xe0000000 : 0xc0000000) |
                       ((lbn & 0x7ffff) << 8));

            return new Address(lda, true);
        }

        /// <summary>
        /// Convert a logical disk address (LDA) to a logical block number (LBN).
        /// </summary>
        public virtual uint LDAtoLBN(Address lda)
        {
            return (uint)(((lda.High & 0x07ff) << 8) | ((lda.Low & 0xff00) >> 8));
        }

        /// <summary>
        /// Convert a block to an address:
        ///     CHStoPDA for physical blocks,
        ///     CHStoLDA for logical blocks.
        /// </summary>
        public abstract Address BlockToAddress(Block block);

        /// <summary>
        /// Go back thuther way:
        ///     PDAtoCHS for physical addrs,
        ///     LDAtoCHS for logical blocks.
        /// </summary>
        public abstract Block AddressToBlock(Address addr);

        /// <summary>
        /// Adjust for boot sector (CHS -> CHS translation).
        /// </summary>
        public abstract Block PhysicalBlockToLogicalBlock(Block physBlock);

        /// <summary>
        /// Adjust for boot sector, in reverse (CHS -> CHS translation).
        /// </summary>
        public abstract Block LogicalBlockToPhysicalBlock(Block logBlock);


        protected void VerifyPhysical(Block physBlock)
        {
            if (physBlock.IsLogical)
            {
                throw new InvalidOperationException("This block is not physical.");
            }
        }

        protected void VerifyLogical(Block logBlock)
        {
            if (!logBlock.IsLogical)
            {
                throw new InvalidOperationException("This block is not logical.");
            }
        }
    }
}
