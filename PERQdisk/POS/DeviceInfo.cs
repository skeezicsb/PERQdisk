//
//  DeviceInfo.cs
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

namespace PERQdisk.POS
{
    /// <summary>
    /// Device type code as defined in the (old) POS DiskIO module.  These are
    /// the codes from the DIB/PIB word 250 (bits 2..4) and don't quite line
    /// up with the newer "VolumeSystem" types defined in POS G and enumerated
    /// in the PERQmedia DeviceType.
    /// </summary>
    public enum DeviceCode
    {
        Winch12 = 0,
        Winch24 = 1,
        FloppySingle = 2,
        FloppyDouble = 3,
        CIOMicrop = 4,
        Generic5Inch = 5,
        Undefined0 = 6,
        Undefined1 = 7
    }

    /// <summary>
    /// Represents a Device Information Block (DIB) structure, which is the
    /// "ground zero entry point" into a physical disk's file structure.
    /// 
    /// It contains a table of boot file mappings and the partition table.
    /// 
    /// The structure of this table is described in Section 2.2 of the "File
    /// System Data Structures" documentation in the POS manual.
    /// </summary>
    public class DeviceInformationBlock
    {
        public DeviceInformationBlock(Sector dibSector)
        {
            _bootTable = new Address[26];
            _interpreterTable = new Address[26];
            _pibEntries = new Address[64];

            BuildData(dibSector);
        }

        private void BuildData(Sector dibSector)
        {
            // Read boot and interpreter tables (PDAs)
            for (int i = 0; i < _bootTable.Length; i++)
            {
                _bootTable[i] = new Address(dibSector.ReadDWord(BootTableStart + i * 4), false);
                _interpreterTable[i] = new Address(dibSector.ReadDWord(InterpreterTableStart + i * 4), false);
            }

            _deviceName = dibSector.ReadString(228, 8).TrimEnd();
            _deviceStart = new Address(dibSector.ReadDWord(236), true);
            _deviceEnd = new Address(dibSector.ReadDWord(240), true);

            // Read partition table (LDAs)
            for (int i = 0; i < _pibEntries.Length; i++)
            {
                _pibEntries[i] = new Address(dibSector.ReadDWord(PartitionTableStart + i * 4), true);
            }

            _deviceRoot = new Address(dibSector.ReadDWord(500), true);

            var word = dibSector.ReadWord(504);
            _deviceType = (DeviceCode)((word & 0x1c) >> 2);
            _partType = (PartitionType)(word & 0x03);
        }


        public string DeviceName => _deviceName;

        public DeviceCode DeviceType => _deviceType;
        public PartitionType PartType => _partType;

        public Address DeviceRoot => _deviceRoot;
        public Address DeviceStart => _deviceStart;
        public Address DeviceEnd => _deviceEnd;

        public Address[] BootTable => _bootTable;
        public Address[] InterpreterTable => _interpreterTable;
        public Address[] PIBEntries => _pibEntries;


        // Constants
        private const int BootTableStart = 20;
        private const int InterpreterTableStart = 124;
        private const int PartitionTableStart = 244;

        private Address[] _bootTable;
        private Address[] _interpreterTable;
        private string _deviceName;
        private Address _deviceStart;
        private Address _deviceEnd;
        private Address[] _pibEntries;
        private Address _deviceRoot;
        private DeviceCode _deviceType;
        private PartitionType _partType;
    }

}
