//
//  Helper.cs
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

using System.Text;

namespace PERQdisk
{
    /// <summary>
    /// Helper functions that are used in various places in the code...
    /// </summary>
    public static class Helper
    {
        public static ushort ReadWord(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        public static void WriteWord(byte[] data, int offset, ushort value)
        {
            data[offset] = (byte)(value & 0xff);
            data[offset + 1] = (byte)(value >> 8);
        }

        // Todo: add WriteDWord, WriteString if needed

        public static uint ReadDWord(byte[] data, int offset)
        {
            return (uint)(ReadWord(data, offset) | (ReadWord(data, offset + 2) << 16));
        }

        public static string ReadString(byte[] data, int offset, int count)
        {
            var sb = new StringBuilder();

            for (int i = offset; i < offset + count; i++)
            {
                sb.Append((char)data[i]);
            }

            return sb.ToString();
        }


        /// <summary>
        /// Returns n bytes copied from an array starting at offset.
        /// </summary>
        public static byte[] CopyFrom(byte[] data, int offset, int length)
        {
            var copy = new byte[length];

            for (var i = 0; i < length; i++)
                copy[i] = data[offset + i];

            return copy;
        }

        /// <summary>
        /// Returns exactly n bytes copied from an array starting at offset,
        /// padding the end with zeros if necessary.
        /// </summary>
        public static byte[] FillFrom(byte[] data, int offset, int length)
        {
            var copy = new byte[length];

            for (var i = 0; i < length; i++)
                copy[i] = (offset + i < data.Length ? data[offset + i] : (byte)0);

            return copy;
        }
    }
}
