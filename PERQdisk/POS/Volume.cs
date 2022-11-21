//
//  POS.Volume.cs
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
using System.Collections.Generic;

using PERQemu;
using PERQmedia;

namespace PERQdisk.POS
{

    /// <summary>
    /// Represents a POS volume, a logical abstraction that sits on top of a 
    /// PhysicalDisk.  Loads the Device Information Block (DIB) which anchors
    /// the entire filesystem structure.
    /// </summary>
    /// <remarks>
    /// A POS logical volume consists of at least 1 to as many as 64 partitions,
    /// which are identified by entries in the DIB's partition table.  Note that
    /// POS does not allow allocation of files outside of partitions; PERQdisk
    /// creates a "fake" directory at the top level to allow for navigation from
    /// a single root.
    /// </remarks>
    public partial class Volume
    {
        public Volume(LogicalDisk disk)
        {
            _disk = disk;

            // Read the DIB from LBN 0
            Sector dibSector = _disk.GetSector(new Address(0, true));
            _dib = new DeviceInformationBlock(dibSector);

            // Partitions loaded when needed (not necessary for device Probe())
            _partitions = null;

            // Root of the directory tree deferred until partitions initialized
            _root = null;

            _dryrun = false;
        }


        public DeviceInformationBlock DIB => _dib;
        public string Name => _dib.DeviceName;

        public List<Partition> Partitions => _partitions;

        public Directory Root => _root;

        public Directory Current
        {
            get { return _path; }
            set { _path = value; }
        }

        public bool DryRun
        {
            get { return _dryrun; }
            set { _dryrun = value; }
        }

        /// <summary>
        /// Dumps interesting info from the Volume's DIB to screen.
        /// </summary>
        public void PrintDIBInfo()
        {
            Console.WriteLine("Device Name:  '{0}'", DIB.DeviceName);
            Console.WriteLine("  Device Type: {0}", DIB.DeviceType);
            Console.WriteLine("  Part Type:   {0}", DIB.PartType);

            Console.WriteLine("  Start Block: {0}", _disk.LDAtoLBN(DIB.DeviceStart));
            Console.WriteLine("  Last Block:  {0}", _disk.LDAtoLBN(DIB.DeviceEnd));
            Console.WriteLine("  Device Root: {0}", _disk.LDAtoLBN(DIB.DeviceRoot));

            Console.WriteLine("\nBoot Table:");

            for (int i = 0; i < DIB.BootTable.Length; i++)
            {
                if (DIB.BootTable[i].Value != 0)
                {
                    Console.WriteLine("  {0} - {1}", (char)((i < 26 ? 'a' : 'A' - 26) + (char)i),
                                      _disk.AddressToBlock(DIB.BootTable[i]));
                }
            }

            Console.WriteLine("\nInterpreter Table:");
            for (int i = 0; i < DIB.InterpreterTable.Length; i++)
            {
                if (DIB.InterpreterTable[i].Value != 0)
                {
                    Console.WriteLine("  {0} - {1}", (char)((i < 26 ? 'a' : 'A' - 26) + (char)i),
                                      _disk.AddressToBlock(DIB.InterpreterTable[i]));
                }
            }
            Console.WriteLine();
        }

        /// <summary>
        /// Interrogates the DIB and makes sure the values look sane for a POS
        /// filesystem.  Confines its checks to the DIB so that Probe() doesn't
        /// do a ton of wasted work trying to load bogus partitions for non-POS
        /// disks.  Not foolproof, but sturdy enough.  See: Scavenger().
        /// </summary>
        public bool CheckVol()
        {
            var weight = 0;

            // Range check some DIB values and balk if they look bogus
            weight += Check((_dib.PartType == PartitionType.Root), $"PartType {_dib.PartType}", 10);

            weight += Check((_disk.LDAtoLBN(DIB.DeviceStart) == 0), $"DevStart {_dib.DeviceStart}", 10);

            weight += Check((_disk.LDAtoLBN(DIB.DeviceEnd) > _disk.LDAtoLBN(DIB.DeviceStart) &&
                            _disk.LDAtoLBN(DIB.DeviceEnd) <= _disk.Geometry.TotalBlocks),
                            $"DevEnd {_dib.DeviceEnd}", 10);

            weight += Check(_disk.LDAtoLBN(DIB.DeviceRoot) == 0, $"DevRoot {_dib.DeviceRoot}", 10);

            var goodParts = 0;
            for (int i = 0; i < DIB.PIBEntries.Length; i++)
            {
                var pib = DIB.PIBEntries[i];
                if (_disk.LDAtoLBN(pib) != 0)
                {
                    var blk = _disk.AddressToBlock(pib);
                    goodParts += Check(_disk.Validate(blk.Cylinder, blk.Head, blk.Sector),
                                       $"PartAddr[{i}] {pib}", 5);
                }
            }
            weight += goodParts;

            // Have to have at least one valid partition!
            weight += Check((goodParts > 0), "MinValidParts", 10);

            if (weight > 50)
            {
                Log.Debug(Category.POS, "Probe looks good to me... weight = {0}", weight);
                return true;
            }

            Log.Debug(Category.POS, "POS probe below probability threshold, weight = {0}", weight);
            return false;
        }

        private int Check(bool test, string name, int weight)
        {
            if (test)
            {
                Log.Debug(Category.POS, "Check {0} passed", name);
                return weight;
            }

            Log.Debug(Category.POS, "Check {0} failed", name);
            return -weight;
        }


        /// <summary>
        /// Read the partition table and load the partitions.  (Separate so that
        /// Probe() can verify the DIB in case we aren't reading a POS disk.)
        /// </summary>
        public void ReadPartitionInfo()
        {
            _partitions = new List<Partition>(16);

            for (int i = 0; i < DIB.PIBEntries.Length; i++)
            {
                var lbn = _disk.LDAtoLBN(DIB.PIBEntries[i]);

                // In a properly initialized Partition Table, empty entries are
                // LBN 0, but range check them in case they're wacky
                if (lbn != 0 && lbn < _disk.LDAtoLBN(DIB.DeviceEnd))
                {
                    // Go for it...
                    _partitions.Add(new Partition(_disk, DIB.PIBEntries[i]));
                }
            }
        }

        /// <summary>
        /// Initialize the (fake) root of the directory tree.  POS doesn't let
        /// you "path sys:" since it's not a real directory; to make navigation
        /// behave more reasonably, we fake up a directory for the device name
        /// and add the partition names as its children.  That way the tree is
        /// anchored by a single root and makes pathname hacking much simpler.
        /// </summary>
        public void LoadDirectory()
        {
            if (_partitions == null) ReadPartitionInfo();

            if (_partitions.Count == 0)
            {
                throw new InvalidOperationException("Device has no partitions!");
            }

            // Set up the fake root
            _root = new Directory(_disk, _dib.DeviceName);

            // Now load each partition and splice it in
            foreach (var part in _partitions)
            {
                part.LoadDirectory(_root);
                _root.Children.Add(part.Root);
            }

            _path = _root;

            Log.Info(Category.POS, "Directory loaded: {0} partitions", _partitions.Count);
        }


        private LogicalDisk _disk;
        private DeviceInformationBlock _dib;
        private List<Partition> _partitions;
        private Directory _root;
        private Directory _path;

        private bool _dryrun;
    }
}
