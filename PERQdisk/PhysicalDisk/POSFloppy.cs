//
//  FloppyDisk.cs
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

using PERQemu;
using PERQmedia;

namespace PERQdisk
{
    /// <summary>
    /// Represents a single or double-sided 8" floppy disk formatted with a
    /// POS filesystem.  This lets POS treat the floppy like a small Shugart
    /// hard drive, with 512-byte sectors (and headers).
    /// </summary>
    /// <remarks>
    /// POS did not support double-density filesystem floppies, but there is
    /// no reason why they couldn't have; we'll leave that as an option for a
    /// future PERQ software enhancement by not hardcoding the use of 128-byte
    /// sectors.  So this interface maps four 128-byte OR two 256-byte sectors
    /// to each 512-byte block expected by the higher-level filesystem code.
    ///
    /// To simplify this, we create a logical view of the physical drive with
    /// 154 cylinders, implicitly switching heads to mimic the PERQ's view (and
    /// the hardware's cyl+sec addressing scheme).  Creating this copy takes
    /// care of the de-interleave, grouping floppy sectors into hard disk-sized
    /// sectors (with headers), and "flattening" the C/H/S physical addressing
    /// into just cylinder + sector logical addressing.
    /// </remarks>
    public class POSFloppy : LogicalDisk
    {
        /// <summary>
        /// Convert LDA or floppy PDA format to a Block address.
        /// </summary>
        public override Block AddressToBlock(Address addr)
        {
            ushort c;
            byte h;
            ushort s;

            if (addr.IsLogical)
            {
                // LDA to CHS - uses the geometry of our logical view
                var lbn = LDAtoLBN(addr);

                c = (ushort)(lbn / _logicalDisk.Geometry.Sectors);
                s = (ushort)(lbn % _logicalDisk.Geometry.Sectors);
                h = 0;
            }
            else
            {
                // PDA to CHS - uses the geometry of the physical disk
                c = addr.High;
                s = addr.Low;
                h = 0;

                // Floppy "infers" the head number from the cylinder number;
                // 0..76 => head 0, 77..154 => head 1
                if (c > Geometry.Cylinders)
                {
                    h = 1;
                    c -= Geometry.Cylinders;
                }
            }

            return new Block(c, h, s, addr.IsLogical);
        }

        /// <summary>
        /// Convert a block address to a LDA or floppy PDA.
        /// </summary>
        public override Address BlockToAddress(Block block)
        {
            uint word = 0;
            Address addr;

            if (block.IsLogical)
            {
                // CHS to LDA
                word = (uint)((block.Cylinder * _logicalDisk.Geometry.Sectors) + block.Sector);
                addr = LBNtoLDA(word);
            }
            else
            {
                // CHS to PDA
                word = (uint)((block.Cylinder + (block.Head * Geometry.Cylinders)) << 16);
                word += block.Sector;
                addr = new Address(word, false);
            }

            return addr;
        }

        // Fixed size of floppy boot area
        public override uint BootBlocks => _bootBlocksOffset;

        // Highest LBN on a filesystem floppy
        public override uint MaxLBN => _maxLBN;

        /// <summary>
        /// Returns a logical (512-byte) or physical (128- or 256-byte) sector
        /// based on the block address.  The filesystem code accesses logical
        /// blocks exclusively, while physical addresses are used for reading
        /// .boot/.mboot files, or the (reserved) header blocks.
        /// </summary>
        public override Sector GetSector(Block block)
        {
            Log.Detail(Category.POS, "GetSector {0}", block);

            if (block.IsLogical)
            {
                return _logicalDisk.Read(block.Cylinder, block.Head, block.Sector);
            }

            return Read(block.Cylinder, block.Head, block.Sector);
        }

        /// <summary>
        /// Convert a logical floppy block to the physical address of its first
        /// constituent block.
        /// </summary>
        public override Block LogicalBlockToPhysicalBlock(Block logBlock)
        {
            VerifyLogical(logBlock);

            // Move forward n tracks to compensate for the boot area
            logBlock.Cylinder += _bootCylsOffset;

            if (logBlock.Cylinder >= Geometry.Cylinders)
            {
                // Deal with the head wrap calculation
                logBlock.Cylinder -= Geometry.Cylinders;
                logBlock.Head++;

                // If we went negative it'll be out of range
                if (logBlock.Cylinder >= Geometry.Cylinders)
                {
                    throw new InvalidOperationException("Went off the end of the disk converting to physical.");
                }
            }

            if (logBlock.Head >= Geometry.Heads)
            {
                throw new InvalidOperationException("Too many heads converting logical to physical.");
            }

            if (logBlock.Sector >= _logicalDisk.Geometry.Sectors)
            {
                throw new InvalidOperationException("Too many sectors converting logical to physical.");
            }

            // There are n physical blocks per logical block, interleaved and
            // offset from the start of the track to leave room for the reserved
            // logical header block; return the first physical sector in the
            // group, accounting for the shift to indexing the zero-based array
            logBlock.Sector = _sectorOrder[logBlock.Sector, 0];

            logBlock.IsLogical = false;

            return logBlock;
        }

        public override Block PhysicalBlockToLogicalBlock(Block physBlock)
        {
            VerifyPhysical(physBlock);

            if (physBlock.Head > 0)
            {
                physBlock.Cylinder += Geometry.Cylinders;
                physBlock.Head--;
            }

            if (physBlock.Head >= _logicalDisk.Geometry.Heads)
            {
                throw new InvalidOperationException("Too many heads converting physical to logical.");
            }

            if ((physBlock.Cylinder - _bootCylsOffset) < 0)
            {
                throw new InvalidOperationException("Went off the beginning of the disk converting to logical.");
            }

            // Move back to compensate for the boot area
            physBlock.Cylinder -= _bootCylsOffset;

            // De-skew the sector to reconvert!
            physBlock.Sector = _deskewTable[(physBlock.Sector / _sectorsPerBlock)];

            if (physBlock.Sector >= Geometry.Sectors)
            {
                throw new InvalidOperationException("Went off the beginning of the disk converting to logical.");
            }

            physBlock.IsLogical = true;

            return physBlock;
        }

        /// <summary>
        /// Returns a 512-byte logical block, made from 2 or 4 physical floppy
        /// sectors (depending on density).
        /// </summary>
        /// <remarks>
        /// All filesystem floppies set aside sectors 1-2 to store the logical
        /// headers that are present in hard disk formats.
        /// </remarks>
        Sector CreateLogicalBlock(Block b)
        {
            // Create a new block with hard disk geometry
            var logical = new Sector(b.Cylinder, b.Head, b.Sector, 512, 16);

            // Get the data block containing the logical headers for this track
            var blk = LogicalBlockToPhysicalBlock(new Block(b.Cylinder, b.Head, 0, true));

            //
            // blk computes the boot area offset and returns the address of the
            // first logical sector on the track we care about, but the special
            // header block is just physical sector #1 -- in the original
            // physical sector size -- NOT the grouped logical (512-byte) size.
            //
            blk.Sector = 1;
            var physical = GetSector(blk);

            // Fill in the header for our logical block!
            for (uint i = 0; i < 16; i++)
            {
                logical.WriteHeaderByte(i, physical.ReadByte((uint)(b.Sector * 16 + i)));
            }

            // Now group the data blocks together
            for (int i = 0; i < _sectorsPerBlock; i++)
            {
                // Get the physical sector based on the interleave table;
                // Use the block address we computed above!
                blk.Sector = _sectorOrder[b.Sector, i];

                physical = GetSector(blk);
                physical.Data?.CopyTo(logical.Data, i * Geometry.SectorSize);
            }

            return logical;
        }

        //
        // StorageDevice overrides for floppies
        //

        /// <summary>
        /// Returns sector data for the given address, but map the (annoying)
        /// floppy sector numbering (1..26) to the underlying device (0..25).
        /// </summary>
        public override Sector Read(ushort cyl, byte head, ushort sec)
        {
            // Read from 1..26 -> 0..25
            var fudge = base.Read(cyl, head, (ushort)(sec - 1));

            // But fudge the returned id from 0..25 -> 1..26
            fudge.SectorID++;

            Log.Detail(Category.POS, "Read {0} (actual {1})", sec - 1, fudge.SectorID);
            return fudge;
        }

        /// <summary>
        /// Writes a sector to the given address, accounting for offset.
        /// </summary>
        public override void Write(Sector sec)
        {
            // Map from 1..26 -> 0..25
            sec.SectorID--;

            Log.Detail(Category.POS, "Write {0} (actual {1})", sec.SectorID, sec.SectorID + 1);
            base.Write(sec);
        }

        /// <summary>
        /// Validate a write, accounting for sector offset.
        /// </summary>
        public override bool WriteCheck(Sector sec)
        {
            return Validate(sec.CylinderID, sec.HeadID, (ushort)(sec.SectorID - 1));
        }

        /// <summary>
        /// Initialize parameters for the loaded disk based on its geometry.
        /// This will allow PERQdisk to read double-density POS filesystem
        /// floppies!  I think POS sorta kinda could support those?  But NOT
        /// for boot floppies due to ROM restrictions, probably.
        /// </summary>
        /// <remarks>
        /// The orderings for physical sectors for each logical sector.  These
        /// are not in linear order (probably for speed optimization) and the
        /// order isn't actually documented anywhere that I can find... These
        /// numbers were deduced by seeing how the POS code accesses the disk
        /// through PERQemu.
        /// </remarks>
        public override void OnLoad()
        {
            if (Geometry.SectorSize == 256)
            {
                // Double density:
                //      two x 256-byte sectors per block
                //      3 tracks x 26 sectors for boot area (19.5KB)
                _bootCylsOffset = 3;
                _sectorOrder = new byte[12, 2] {
                    {  3,  8 },
                    { 13, 18 },
                    { 23,  4 },
                    {  9, 14 },
                    { 19, 24 },
                    {  5, 10 },
                    { 15, 20 },
                    { 25,  6 },
                    { 11, 16 },
                    { 21, 26 },
                    {  7, 12 },
                    { 17, 22 }
                };
                // this one might not be right; try a dd pos floppy and see!
                //  3 13 23  9 19  5 15 25 11 21  7 17
                //  1  6 11  4  9  2  7 12  5 10  3 8
                _deskewTable = new ushort[] { 1, 6, 11, 4, 9, 2, 7, 12, 5, 10, 3, 8 };
            }
            else
            {
                // Single density:
                //      four x 128-bye sectors per block
                //      5 tracks x 26 sectors for boot area (16.25KB)
                _bootCylsOffset = 5;
                _sectorOrder = new byte[6, 4] {
                    {  3,  8, 13, 18 },
                    { 23,  4,  9, 14 },
                    { 19, 24,  5, 10 },
                    { 15, 20, 25,  6 },
                    { 11, 16, 21, 26 },
                    {  7, 12, 17, 22 }
                };

                // LBNs only point to the starting block, so map the sector #s
                // from the first column back to their logical sector number
                // (this makes sense for PDA -> LDA conversion)
                _deskewTable = new ushort[] { 0, 5, 4, 3, 2, 1 };
            }

            _logicalSectors = (byte)_sectorOrder.GetLength(0);
            _sectorsPerBlock = (byte)_sectorOrder.GetLength(1);
            _bootBlocksOffset = (byte)(_bootCylsOffset * _logicalSectors);

            // Create a StorageDevice with the geometry of our logical view
            _logicalDisk = new StorageDevice(
                            new DeviceGeometry((ushort)(Geometry.Cylinders * Geometry.Heads),
                                               1, _logicalSectors, 512, 16));

            _maxLBN = (uint)(_logicalDisk.Geometry.TotalBlocks - _bootBlocksOffset - 1);

            _logicalDisk.CreateSectors();

            // Assemble the logical disk blocks from physical floppy sectors
            for (uint lbn = 0; lbn <= _maxLBN; lbn++)
            {
                var b = AddressToBlock(LBNtoLDA(lbn));
                _logicalDisk.Write(CreateLogicalBlock(b));
            }

            base.OnLoad();
        }

        // A container for our logical (filesystem) view of the floppy
        StorageDevice _logicalDisk;

        byte[,] _sectorOrder;
        ushort[] _deskewTable;

        uint _maxLBN;

        byte _bootBlocksOffset;
        byte _bootCylsOffset;
        byte _logicalSectors;
        byte _sectorsPerBlock;
    }
}
