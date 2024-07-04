//
//  RT11.Device.cs
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

namespace PERQdisk.RT11
{
    /// <summary>
    /// Top-level implementation of an RT11-formatted floppy disk.
    /// </summary>
    public class Device : IDiskDevice
    {

        public StorageDevice Media => _disk;


        /// <summary>
        /// Probe the specified file for a valid RT-11 floppy volume.
        /// </summary>
        public bool Probe(string path)
        {
            var type = FileUtilities.GetDeviceTypeFromFile(path);

            if (type != DeviceType.Floppy)
            {
                Log.Debug(Category.RT11, "Could not probe {0} for RT11 volume", path);
                return false;
            }

            var dev = new RT11Floppy();

            dev.LoadFrom(path);

            if (dev.IsLoaded)
            {
                var vol = new Volume(dev);

                if (vol.CheckVol())
                {
                    Log.Info(Category.RT11, "Confirmed an RT11 volume for {0}", path);

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
        /// Load the specified RT11 floppy.  Allows reading AND writing!
        /// </summary>
        public bool Mount(string path)
        {
            // If the last thing we probed isn't valid, start over...
            if (_disk == null || (_disk != null && _disk.Filename != path))
            {
                if (!Probe(path)) return false;
            }

            Console.WriteLine("Loading floppy...");
            _disk.LoadFrom(path);

            // For formats that don't store this info, set the filesystem hint
            // in case we store it to a format that does (i.e., .prqm, .pfd)
            if (_disk.IsLoaded && _disk.FileInfo.FSType != FilesystemHint.RT11)
            {
                Log.Info(Category.RT11, "[Corrected filesystem hint]");
                _disk.FileInfo.FSType = FilesystemHint.RT11;
                _disk.IsModified = true;
            }

            return _disk.IsLoaded;
        }

        /// <summary>
        /// Eject the floppy and free it up.
        /// </summary>
        public void Dismount()
        {
            if (_disk != null)
            {
                _disk = null;
                Console.WriteLine("Floppy ejected.");
            }
        }

        RT11Floppy _disk;
    }
}
