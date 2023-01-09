//
//  POS.Partition.cs
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

namespace PERQdisk.POS
{
    /// <summary>
    /// Partition type as defined in the (old) POS DiskIO module.  These codes
    /// are used in the DIB/PIB (low bits of word 250).
    /// </summary>
    public enum PartitionType
    {
        Root = 0,
        Unused = 1,
        Leaf = 2,
        Unknown = 3
    }

    /// <summary>
    /// Represents a Partition Information Block (PIB) structure, which defines a
    /// disk partition.  It's very similar to a DIB, so there's some duplication
    /// of code here.  Meh.
    /// 
    /// The structure of this table is described in Section 2.3 of the "File System
    /// Data Structures" documentation in the POS manual.
    /// </summary>
    public class PartitionInformationBlock
    {
        public PartitionInformationBlock(Sector pibSector)
        {
            _freeHead = new Address(pibSector.ReadDWord(0), true);
            _freeTail = new Address(pibSector.ReadDWord(4), true);
            _numberFree = pibSector.ReadDWord(8);
            _rootDirectoryID = new Address(pibSector.ReadDWord(12), true);
            _badSegmentID = new Address(pibSector.ReadDWord(16), true);
            _partitionName = pibSector.ReadString(228, 8).TrimEnd();
            _partitionStart = new Address(pibSector.ReadDWord(236), true);
            _partitionEnd = new Address(pibSector.ReadDWord(240), true);
            _partitionRoot = new Address(pibSector.ReadDWord(500), true);
            _partitionType = pibSector.ReadWord(504);
        }

        public string PartitionName => _partitionName;
        public uint PartitionType => _partitionType;
        public uint NumberFree => _numberFree;
        public Address PartitionStart => _partitionStart;
        public Address PartitionEnd => _partitionEnd;
        public Address PartitionRoot => _partitionRoot;
        public Address RootDirectoryID => _rootDirectoryID;
        public Address BadSegmentID => _badSegmentID;
        public Address FreeHead => _freeHead;
        public Address FreeTail => _freeTail;


        Address _freeHead;
        Address _freeTail;
        uint _numberFree;
        Address _rootDirectoryID;
        Address _badSegmentID;
        string _partitionName;
        Address _partitionStart;
        Address _partitionEnd;
        Address _partitionRoot;
        ushort _partitionType;
    }


    /// <summary>
    /// Represents a Partition on a POS Volume.  The Partition is described by a
    /// PartitionInformatinBlock (PIB) and contains a directory (ROOT.DR) that is
    /// the root of the directory structure.
    /// </summary>
    public class Partition
    {
        public Partition(LogicalDisk disk, Address pibAddr)
        {
            _disk = disk;
            _pib = new PartitionInformationBlock(_disk.GetSector(pibAddr));
            _root = null;
        }

        public PartitionInformationBlock PIB => _pib;
        public string Name => _pib.PartitionName;

        public Directory Root => _root;


        /// <summary>
        /// Prints the PIB contents in a nice one-line summary.
        /// </summary>
        /// <remarks>
        /// Technically the partition type word contains a copy of the device 
        /// code, too.  And we don't print the root dir lbn here either.  Meh.
        /// </remarks>
        public void PrintPIBInfo()
        {
            Console.WriteLine("    {0,-8}  Start: {1,6}  End: {2,6}  Free: {3,6}  Flags: {4}",
                              _pib.PartitionName,
                              _disk.LDAtoLBN(_pib.PartitionStart),
                              _disk.LDAtoLBN(_pib.PartitionEnd),
                              _pib.NumberFree,
                              (PartitionType)(_pib.PartitionType & 0x3));
        }


        /// <summary>
        /// Load the root directory for this partition, which then recursively
        /// traverses the entire filesystem to populate the tree.  Sets _root
        /// when complete.
        /// </summary>
        public void LoadDirectory(Directory parent)
        {
            // Load the FIB for ROOT.DR
            var rootDir = new File(_disk, _root, _pib.RootDirectoryID);

            // Make sure this is a valid root!
            if (!rootDir.IsDirectory)
            {
                throw new InvalidOperationException($"Partition {Name} has invalid ROOT.DR!");
            }

            // Change ROOT.DR -> <part>.DR
            rootDir.FIB.SimpleName = _pib.PartitionName;

            // And away we go!
            _root = new Directory(_disk, rootDir, parent, 1);
        }


        LogicalDisk _disk;
        PartitionInformationBlock _pib;
        Directory _root;
    }
}
