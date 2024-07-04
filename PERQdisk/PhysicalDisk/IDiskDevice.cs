//
//  IDiskDevice.cs
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

using PERQmedia;

namespace PERQdisk
{
    public interface IDiskDevice
    {
        /// <summary>
        /// Return true if 'path' contains a filesystem of the specific type.
        /// </summary>
        bool Probe(string path);

        /// <summary>
        /// Load the image and initialize the appropriate volume type.
        /// </summary>
        bool Mount(string path);

        /// <summary>
        /// Unload the image.
        /// </summary>
        void Dismount();

        /// <summary>
        /// Access to the underlying storage.
        /// </summary>
        StorageDevice Media { get; }
    }
}
