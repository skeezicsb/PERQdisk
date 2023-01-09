//
//  Scavenger.cs
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

namespace PERQdisk.POS
{
    /// <summary>
    /// Implements a version of the POS "Scavenger" utility that thoroughly
    /// checks for and repairs filesystem corruption.  Someday.
    /// </summary>
    public class Scavenger
    {
        public Scavenger()
        {
        }

        /// <summary>
        /// Run a series of tests and try to find/fix any corruption in the DIB,
        /// PIBs or directory blocks for a POS disk image.  The goal is to fix
        /// problems with archived disks that don't mount successfully on the
        /// emulator, where the real Scavenger can do its magic.
        /// </summary>
        public void Scavenge(Partition p)
        {
            // todo: requires a partition to work on; boolean flag to allow
            // writes, otherwise "dry run" only?

            // todo: maybe snoop around the headers looking for deleted files
            // like josh's original perqdisk did?

            // todo: verify integrity of logical headers?

            // todo: look for improperly closed files?
            Console.WriteLine("Not implemented.");
        }

    }
}
