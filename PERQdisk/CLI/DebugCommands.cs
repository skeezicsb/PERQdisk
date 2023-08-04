//
//  DebugCommands.cs
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
using System.IO;
using System.Diagnostics;

using PERQmedia;

namespace PERQdisk
{
    /// <summary>
    /// Boneyard for Debug mode, test commands, other remnants.  Likely to be
    /// removed before release.
    /// </summary>
    public class DebugCommands
    {
        [Conditional("DEBUG")]
        [Command("debug crc speed test", "Check CRC speed [requires 'file'.short]")]
        public void CRCSpeedCheck(string file)
        {
            Console.WriteLine("Starting speedcheck, reading " + file);

            var sw = new Stopwatch();

            using (var fs = new FileStream($"{file}.short", FileMode.Open, FileAccess.Read))
            {
                using (var test = new CRC32Stream(fs))
                {
                    test.ResetChecksum();

                    var buf = new byte[65536];
                    sw.Restart();
                    while (test.Read(buf, 0, buf.Length) > 0) { };
                    sw.Stop();

                    Console.WriteLine("Read {0} bytes in {1}ms", test.Position, sw.ElapsedMilliseconds);
                    Console.WriteLine("Checksum = {0:x8}", test.ReadCRC);
                }
            }
        }
    }
}
