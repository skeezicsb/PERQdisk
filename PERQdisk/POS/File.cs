//
//  POS.File.cs
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
    /// Represents a File in a POS directory structure.  No interpretation of the
    /// contents is done here; if the File's Information Block indicates that it
    /// is a directory or some other kind of structured file, other classes may
    /// parse the contents as necessary.
    /// </summary>
    public class File
    {
        /// <summary>
        /// Creates a new file given the address of its FIB.  The file's data is
        /// only read (and is cached) when it is accessed.  That's kind of silly
        /// given how the underlying StorageDevice operates from memory and there
        /// isn't any real reason why we couldn't just re-read the file instead.
        /// </summary>
        public File(LogicalDisk disk, Directory parent, Address fib)
        {
            _disk = disk;
            _address = fib;
            _parent = parent;
            _fib = new FileInformationBlock(_disk.GetSector(fib));
            _data = null;
        }

        public FileInformationBlock FIB => _fib;
        public Directory Parent => _parent;
        public string FullName => _fib.FileName;
        public string SimpleName => _fib.SimpleName;
        public bool IsDirectory => _fib.FileType == 3;    // from POS FileTypes.pas

        public byte[] Data
        {
            get
            {
                if (_data == null) ReadFile();      // Lazily cache it when accessed
                return _data;
            }
        }

        /// <summary>
        /// Prints the raw file information.
        /// </summary>
        public void PrintFileInfo()
        {
            Console.WriteLine($"Information for '{SimpleName}':");
            Console.WriteLine($"  Full name: '{FullName}'");
            Console.WriteLine($"  File type:  {_fib.FileType} ({(FileTypes)_fib.FileType})" +
                              (_fib.IsSparse ? "  Flags: IsSparse" : ""));
            Console.WriteLine($"  File size:  {_fib.FileSize} ({Data.Length} bytes)" +
                              $"  Blocks: {_fib.BlocksInUse}  Bits: {_fib.FileBits}");
            Console.WriteLine($"  Segment type:  {(SegmentKind)_fib.SegmentKind}");
            Console.WriteLine($"  Start address: {_address}");
            Console.WriteLine($"  Creation date: {_fib.CreationDate}");
            Console.WriteLine($"  Last written:  {_fib.LastWriteDate}");
            Console.WriteLine($"  Last accessed: {_fib.LastAccessDate}");
        }

        /// <summary>
        /// Reads the disk blocks for this file into memory.
        /// </summary>
        void ReadFile()
        {
            // This is based on trial and error -- FileSize is _not_ used for
            // Directories, but is accurate for Files.  LastBlock+2 _seems_ to
            // be the correct size for directories, but I don't know if it's the
            // Right Thing.  POS documentation is _Very Vague_ about this stuff.
            int numBlocks = IsDirectory ? ((short)_fib.LastBlock) + 2 : _fib.FileSize; // hack!

            // todo: should numBlocks use _fib.BlocksInUse?  does that properly
            // account for any "negative blocks"?  does POS include the FIB as
            // part of the file's size?!?

            // This is a stupid way to convey "if we have more than one block
            // allocated to a file, then subtract off the unused bytes of the
            // last block; otherwise add the used bytes to the current allocation
            // (0 blocks)."
            int dataSize = Math.Abs(numBlocks * 512 - (512 - (_fib.FileBits >> 3)));

            // Allocate some space for the data in this file
            _data = new byte[dataSize];

            int blockCount = 0;

            // Read the direct indexed blocks
            blockCount += ReadBlocks(_fib.DirectIndices, 0, numBlocks - blockCount);

            if (blockCount >= numBlocks) return;

            // Read the indirect indexed blocks
            blockCount += ReadIndexedBlocks(_fib.IndirectIndices, blockCount * 512, numBlocks - blockCount);

            if (blockCount >= numBlocks) return;

            // Read the double-indirect indexed blocks
            ReadDoubleIndexedBlocks(_fib.DoubleIndirectIndices, blockCount * 512, numBlocks - blockCount);

            // We're done regardless at this point.  Not sure I've ever seen a POS
            // file big enough to actually require double indirect blocks! :-)
        }

        /// <summary>
        /// Get an array of uints (lda's) out of a sector.
        /// </summary>
        Address[] SectorDataToLDAs(Sector s)
        {
            var addrs = new Address[128];

            for (int i = 0; i < 128; i++)
            {
                addrs[i] = new Address(s.ReadDWord(i * 4), true);
            }

            return addrs;
        }

        int ReadDoubleIndexedBlocks(Address[] indices, int offset, int maxBlocks)
        {
            int blockCount = 0;

            // Read the double-indirect blocks
            for (int i = 0; i < _fib.DoubleIndirectIndices.Length; i++)
            {
                if (indices[i].Value <= 0)
                    continue;

                var doubleIndirectSector = _disk.GetSector(indices[i]);

                // The above points to a sector of indirect blocks
                blockCount += ReadIndexedBlocks(SectorDataToLDAs(doubleIndirectSector), offset + blockCount * 512, maxBlocks - blockCount);

                if (blockCount >= maxBlocks)
                {
                    return blockCount;
                }
            }

            return blockCount;
        }

        int ReadIndexedBlocks(Address[] indices, int offset, int maxBlocks)
        {
            int blockCount = 0;
            for (int i = 0; i < indices.Length; i++)
            {
                if (indices[i].Value <= 0)
                    continue;

                var indirectSector = _disk.GetSector(indices[i]);

                blockCount += ReadBlocks(SectorDataToLDAs(indirectSector), offset + blockCount * 512, maxBlocks - blockCount);

                if (blockCount >= maxBlocks)
                {
                    return blockCount;
                }
            }

            return blockCount;
        }

        int ReadBlocks(Address[] indices, int offset, int maxBlocks)
        {
            // Read direct blocks
            for (int i = 0; i < Math.Min(indices.Length, maxBlocks); i++)
            {
                if (indices[i].Value <= 0)
                    continue;

                var fileSector = _disk.GetSector(indices[i]);

                if (offset + i * 512 + 512 <= _data.Length)
                {
                    fileSector.Data.CopyTo(_data, offset + i * 512);
                }
                else
                {
                    // Copy incomplete last sector by hand...
                    for (int j = offset + i * 512; j < _data.Length; j++)
                    {
                        _data[j] = fileSector.Data[j - (offset + i * 512)];
                    }

                    return i + 1;
                }
            }

            return Math.Min(indices.Length, maxBlocks);
        }


        LogicalDisk _disk;
        Address _address;
        Directory _parent;
        FileInformationBlock _fib;
        byte[] _data;
    }
}
