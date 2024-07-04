//
//  POS.Device.cs
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

namespace PERQdisk.POS
{
    /// <summary>
    /// Top-level implementation of a POS filesystem.  The underlying storage
    /// may be a floppy or hard disk; the logical view masks the implementation
    /// differences (addressing, sector sizes, etc).
    /// </summary>
    public class Device : IDiskDevice
    {

        public StorageDevice Media => _disk;


        /// <summary>
        /// Find the specific subclass for a given media file; for all devices
        /// that can mount a POS filesystem, these will be LogicalDisks, but
        /// with type-specific address translations.
        /// </summary>
        LogicalDisk GetDevForPath(string path)
        {
            var type = FileUtilities.GetDeviceTypeFromFile(path);

            switch (type)
            {
                case DeviceType.Floppy:
                    return new POSFloppy();

                case DeviceType.Disk14Inch:
                    return new ShugartDisk();

                case DeviceType.Disk8Inch:
                    return new MicropolisDisk();

                case DeviceType.Disk5Inch:
                    return new MFMDisk();
            }

            return null;
        }

        /// <summary>
        /// Probe the specified file and verify that it contains a valid POS
        /// filesystem.  The checks are empirical, as there's no one definitive,
        /// distinctive signature to look for.  Probably overkill... but better
        /// than forcing the user to do it, especially if the user is me with a
        /// thousand disks and floppies to search through. :-)
        /// </summary>
        public bool Probe(string path)
        {
            var dev = GetDevForPath(path);

            if (dev == null)
            {
                Log.Debug(Category.POS, "Could not probe {0} for POS filesystem", path);
                return false;
            }

            dev.LoadFrom(path);

            if (dev.IsLoaded)
            {
                var vol = new Volume(dev);

                if (vol.CheckVol())
                {
                    Log.Debug(Category.POS, "Confirmed a POS filesystem for {0}", path);

                    // Cache this result...
                    _disk = dev;
                    return true;
                }
            }

            // Nope
            dev = null;
            return false;
        }

        /// <summary>
        /// Load the specified file and "mount" the filesystem.
        /// </summary>
        public bool Mount(string path)
        {
            // If the last thing we probed isn't valid, start over...
            if (_disk == null || (_disk != null && _disk.Filename != path))
            {
                _disk = GetDevForPath(path);
            }

            Console.WriteLine("Loading disk...");
            _disk.LoadFrom(path);

            // For formats that don't store this info, set the filesystem hint
            // in case we store it to a format that does (i.e., .prqm)
            if (_disk.IsLoaded && _disk.FileInfo.FSType != FilesystemHint.POS)
            {
                Log.Info(Category.POS, "[Corrected filesystem hint]");
                _disk.FileInfo.FSType = FilesystemHint.POS;
                _disk.IsModified = true;
            }

            return _disk.IsLoaded;
        }


        /// <summary>
        /// Dismount this instance and free everything up.
        /// </summary>
        public void Dismount()
        {
            if (_disk != null)
            {
                _disk = null;
                Console.WriteLine("Dismounted.");
            }
        }

        LogicalDisk _disk;
    }
}
