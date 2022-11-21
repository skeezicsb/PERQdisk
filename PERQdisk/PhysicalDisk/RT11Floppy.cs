//
//  RT11Floppy.cs
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

using PERQemu;
using PERQmedia;

namespace PERQdisk
{
    /// <summary>
    /// Represents a single- or double-sided 8" floppy disk formatted with an
    /// RT11-compatible volume.  This format treats the disk as a series of
    /// logical blocks with a specific skew/interleaving scheme.
    /// </summary>
    public class RT11Floppy : StorageDevice
    {
        public RT11Floppy()
        {
            _skewTable = new int[SkewTableSize];

            for (var i = 0; i < SkewTableSize; i++)
            {
                _skewTable[i] = (1 + i * SkewFactor) % SectorsPerTrack;
                Log.Detail(Category.RT11, "Skew table[{0}] = {1}", i, _skewTable[i]);
            }
        }

        // Highest LBN on the floppy
        public uint MaxLBN => _maxLBN;

        public const int SectorsPerTrack = 26;  // Constant for PERQ floppies

        public const int SkewTableSize = 13;
        public const int SkewFactor = 6;
        public const int Interlace = 2;

        /// <summary>
        /// Get a full 512-byte sector pointed to by a logical block number.
        /// </summary>
        public Sector GetSector(uint block)
        {
            return ReadLogicalBlock(block);
        }

        /// <summary>
        /// Put a full 512-byte sector on the floppy.
        /// </summary>
        public void PutSector(uint block, byte[] data)
        {
            WriteLogicalBlock(block, data);
        }


        /// <summary>
        /// Gets the address of the first physical block of a file from its LBN.
        /// </summary>
        private Block GetFirstPhysicalBlock(uint lbn)
        {
            if (lbn > _maxLBN)
            {
                throw new ArgumentOutOfRangeException(nameof(lbn));
            }

            var tmp = lbn * _sectorsPerBlock;           // Logical sector
            var trk = tmp / SectorsPerTrack;            // Physical track
            var sec = _skewTable[trk % SkewTableSize];  // 1st sector on this track

            tmp = tmp % SectorsPerTrack;                // Relative sector #
            while (tmp > 0)
            {
                sec += Interlace;

                if (sec == (SectorsPerTrack + 1))
                {
                    sec = 1;
                }
                else if (sec > SectorsPerTrack)
                {
                    sec = 2;
                }

                // Wrap from odd sectors to even sectors
                if (Odd(sec) && (sec == _skewTable[trk % SkewTableSize]))
                {
                    sec++;
                }

                tmp--;
            }

            Log.Debug(Category.RT11, "  ==> first:  track {0,3}, sector {1,2}", trk, sec);
            return new Block((ushort)trk, 0, (ushort)sec, true);
        }

        /// <summary>
        /// For a given physical block, compute the skew and interlace to get
        /// the next one in the nutty RT11 interleaving scheme.
        /// </summary>
        private Block GetNextPhysicalBlock(Block cur)
        {
            var trk = cur.Cylinder;
            var sec = cur.Sector;

            var tmp = _skewTable[trk % SkewTableSize];
            sec += Interlace;

            if (sec == (SectorsPerTrack + 1))
            {
                sec = 1;
            }
            else if (sec > SectorsPerTrack)
            {
                sec = 2;
            }

            if (Odd(sec) && sec == tmp)
            {
                sec++;
            }
            else if (!Odd(sec) && sec == (tmp + 1))
            {
                trk++;                                          // Wrap to next track
                sec = (ushort)_skewTable[trk % SkewTableSize];  // New first sector
            }

            cur.Cylinder = trk;
            cur.Sector = sec;

            Log.Debug(Category.RT11, "  ==> contd:  track {0,3}, sector {1,2}", trk, sec);
            return cur;
        }

        /// <summary>
        /// Returns a 512-byte logical block, made from 2 or 4 physical floppy
        /// sectors (depending on density).
        /// </summary>
        private Sector ReadLogicalBlock(uint lbn)
        {
            Log.Debug(Category.RT11, "GetSector: {0}", lbn);

            // Get the first physical block
            var physical = GetFirstPhysicalBlock(lbn);

            // Create a new 512-byte block
            var logical = new Sector(physical.Cylinder, physical.Head, physical.Sector, 512);

            // Now group n physical data blocks together
            for (int i = 0; i < _sectorsPerBlock; i++)
            {
                // Read according to the "track, sector" view
                var sec = Read(physical);

                Log.Detail(Category.RT11, "  ==> copying {0} bytes @ offset {1}",
                                          Geometry.SectorSize, i * Geometry.SectorSize);

                sec.Data.CopyTo(logical.Data, i * Geometry.SectorSize);

                // More to do?  Get next one
                if (i < _sectorsPerBlock - 1)
                {
                    physical = GetNextPhysicalBlock(physical);
                }
            }

            return logical;
        }

        /// <summary>
        /// Write a logical block (512 bytes) into as many physical floppy
        /// sectors as required (2 for double density, 4 for single density).
        /// Applies all the nutty interleaving/skew calculations.  Returns
        /// false if anything goes haywire.
        /// </summary>
        private void WriteLogicalBlock(uint lbn, byte[] data)
        {
            Log.Debug(Category.RT11, "PutSector: {0}", lbn);

            // sanity check: debugging
            if (data.Length / _sectorsPerBlock != Geometry.SectorSize)
            {
                Log.Error(Category.RT11,
                          "Data size wrong (lbn {0}), got {1} bytes but expected {2} bytes!",
                          lbn, data.Length, Geometry.SectorSize * _sectorsPerBlock);
                return;
            }

            // Get the first physical block
            var physical = GetFirstPhysicalBlock(lbn);

            // Break the data into n physical sectors
            for (int i = 0; i < _sectorsPerBlock; i++)
            {
                Log.Detail(Category.RT11, "  ==> copying {0} bytes @ offset {1}",
                                          Geometry.SectorSize, i * Geometry.SectorSize);

                var buf = Helper.CopyFrom(data, i * Geometry.SectorSize, Geometry.SectorSize);
                Write(physical, buf);

                if (i < _sectorsPerBlock - 1)
                {
                    physical = GetNextPhysicalBlock(physical);
                }
            }
        }


        /// <summary>
        /// Read a physical sector with RT11's weird logical translations applied.
        /// </summary>
        /// <remarks>
        /// Ugh.  Serious sausage making here.  Should probably reconsider doing a
        /// "logical disk" and mapping things like with POS floppies...
        /// 
        /// Adjust cyl/head here based on RT11's view where cyl > 76 "implies"
        /// head 1.  Always adjust by one cylinder, since RT11 doesn't use cyl 0
        /// (unless that's just a POS/PERQ thing?).  We don't transform these in
        /// the overloaded Read() since that's used at media loading time, which
        /// would be a problem.  This ain't pretty. :-|
        /// </remarks>
        private Sector Read(Block blk)
        {
            // Apply transforms: skip cyl 0, map sector from 1..26 -> 0..25
            var trk = (ushort)(blk.Cylinder + 1);
            var sec = (ushort)(blk.Sector - 1);
            byte head = 0;

            // Check for wrap, and adjust head
            if (trk >= Geometry.Cylinders)
            {
                trk -= Geometry.Cylinders;
                head = 1;
            }

            // We'll have to be more conscientious about Writes, but the values
            // returned in the Sector read will have the wrong CylID, SecID...
            return base.Read(trk, head, sec);
        }

        /// <summary>
        /// Do a physical write with the RT11 translations applied.
        /// </summary>
        private void Write(Block blk, byte[] data)
        {
            Log.Debug(Category.RT11, "Writing block {0}", blk);

            // Same as read: skip cyl 0, map sector from 1..26 -> 0..25
            blk.Cylinder++;
            blk.Sector--;

            // Check for wrap, and adjust head
            if (blk.Cylinder >= Geometry.Cylinders)
            {
                blk.Cylinder -= Geometry.Cylinders;
                blk.Head = 1;
            }

            // Create a new Sector and shove it at the floppy
            var sec = new Sector(blk.Cylinder, blk.Head, blk.Sector, Geometry.SectorSize, 0);
            data.CopyTo(sec.Data, 0);
            Log.Debug(Category.RT11, "Writing sector {0}", sec);

            base.Write(sec);
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
            Sector fudge = base.Read(cyl, head, (ushort)(sec - 1));

            // But fudge the returned id from 0..25 -> 1..26
            fudge.SectorID++;

            return fudge;
        }

        /// <summary>
        /// Writes a sector to the given address, accounting for offset.
        /// </summary>
        public override void Write(Sector sec)
        {
            // Map from 1..26 -> 0..25
            sec.SectorID--;

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
        /// </summary>
        /// <remarks>
        /// For floppies we generally say "tracks" rather than cylinders, and
        /// "head" is implied;  tracks 0..76 are side/head 0 and tracks 77..153
        /// are side/head 1 (double-sided floppies).  For RT11 cylinder 0 isn't
        /// used (is that a PERQ-thing only or common to the PDP-11 too?)
        /// </remarks>
        public override void OnLoad()
        {
            _sides = Geometry.Heads;
            _density = Geometry.SectorSize / 128;           // if this is bad...
            _sectorsPerBlock = 4 / _density;                // ...boom!
            _tracks = (Geometry.Cylinders * _sides) - 1;

            _maxPhys = Geometry.Cylinders * _sides * SectorsPerTrack;
            _maxLBN = (uint)(_tracks * SectorsPerTrack / _sectorsPerBlock);

            Log.Debug(Category.RT11, "OnLoad: sides={0} dens={1} secPerBlk={2}", _sides, _density, _sectorsPerBlock);
            Log.Debug(Category.RT11, "OnLoad: maxPhys={0} maxLBN={1}", _maxPhys, _maxLBN);
        }

        private bool Odd(int num)
        {
            return ((num & 0x1) != 0);                      // i am so lazy
        }

        private int[] _skewTable;

        private int _sides;
        private int _density;
        private int _tracks;
        private int _sectorsPerBlock;
        private int _maxPhys;

        private uint _maxLBN;
    }
}
