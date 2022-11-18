//
//  RT11.Directory.cs
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

namespace PERQdisk.RT11
{
    /// <summary>
    /// Class for working with an RT-11 floppy directory.  For POS there is room
    /// for four segments of 62 files each, for a maximum of 248 files per floppy.
    /// </summary>
    public class Directory
    {
        public Directory(Volume vol)
        {
            _vol = vol;
            _disk = _vol.Media;

            _files = new List<DirectoryEntry>();
        }

        public List<DirectoryEntry> Files => _files;

        public int NumFiles => _numFiles;
        public int BlocksFree => _blksFree;

        public const int MaxFiles = 248;        // Just precompute this

        /// <summary>
        /// Clear this instance.  Used when zeroing/formatting new disks.
        /// </summary>
        public void Clear()
        {
            _files.Clear();
            _numFiles = 0;
            _blksFree = 0;
        }

        /// <summary>
        /// Loads the directory and recomputes the file offsets, free blocks.
        /// </summary>
        public void Load()
        {
            Clear();

            var size = Volume.DirEntSize + _vol.ExtraBytes;
            var startSeg = _vol.StartSegment;

            for (var curSeg = 0; curSeg < _vol.HighSegment; curSeg++)
            {
                var buf = GetSegment((uint)(Volume.DirStart + (curSeg * 2)));

                var offset = Volume.DirEntOffset;
                var status = (StatusWord)buf.ReadWord(0);

                while (status != StatusWord.Terminator && offset + size < Volume.DirSegSize)
                {
                    _numFiles++;
                    var ent = new DirectoryEntry(Helper.CopyFrom(buf.Data, offset, size));

                    // Add "unused" entries to the total free space
                    if (ent.Status == StatusWord.Unused)
                    {
                        _blksFree += ent.Size;
                    }

                    // Shouldn't really encounter these?  Not sure what they mean...
                    if (ent.Status == StatusWord.Tentative)
                    {
                        Log.Info(Category.RT11, "File {0} has 'tentative' status.", _numFiles);
                    }

                    // This is probably a more serious problem...
                    if (ent.Status == StatusWord.None)
                    {
                        Log.Warn(Category.RT11, "File {0} has unknown/invalid status!", _numFiles);
                    }

                    // Save starting block, and add to the directory!
                    ent.StartBlock = startSeg;
                    _files.Add(ent);

                    // Update offsets for next file
                    startSeg += ent.Size;
                    offset += size;

                    // Status of next entry
                    status = (StatusWord)buf.ReadWord(offset);
                }
            }

            Log.Info(Category.RT11, "Directory loaded: {0} files, {1} blocks free", _numFiles, _blksFree);
        }

        /// <summary>
        /// Based on the current Files list, rewrite the volume/directory structure
        /// using as many RT11 "directory segments" as necessary.  In-memory only.
        /// </summary>
        public void Save()
        {
            var buf = new byte[Volume.DirSegSize];

            // Precompute a buncha stuff
            var size = Volume.DirEntSize + Volume.POSExtraBytes;
            var dirEntPerBlock = ((Volume.DirSegSize - Volume.DirEntOffset) / size) - 1;
            var dirSegs = (_files.Count + dirEntPerBlock - 1) / dirEntPerBlock;

            Log.Debug(Category.RT11, "Updating the directory ({0} files)", _files.Count);
            Log.Debug(Category.RT11, "{0} entries ({1} bytes) in {2} segments", dirEntPerBlock, size, dirSegs);

            if (dirSegs > Volume.DirSegMax)
            {
                Console.WriteLine("** Directory full!");
                dirSegs = Volume.DirSegMax;     // clip it
            }

            var curSeg = Volume.DirStart;       // LBN of current directory segment
            var startSeg = Volume.UserStart;    // LBN of first user data block
            var num = 0;                        // first file entry

            for (var i = 1; i <= dirSegs; i++)
            {
                Log.Debug(Category.RT11, "Building directory segment {0} @ LBN {1}", i, startSeg);

                // Write the volume header in each segment!
                // Yeah, this probably belongs in RT11.Volume.cs
                Helper.WriteWord(buf, 0, Volume.DirSegMax);
                Helper.WriteWord(buf, 2, (ushort)(i == dirSegs ? 0 : i + 1));
                Helper.WriteWord(buf, 4, (ushort)dirSegs);
                Helper.WriteWord(buf, 6, Volume.POSExtraBytes);
                Helper.WriteWord(buf, 8, (ushort)startSeg);

                // Now do a segment's worth of directory entries (62, for POS)
                var offset = Volume.DirEntOffset;
                var j = 0;

                while (j < dirEntPerBlock && num < _files.Count)
                {
                    Log.Detail(Category.RT11,
                               "Writing entry {0} @ offset {1}, file {2} of {3}",
                                j, offset, num + 1, _files.Count);

                    var f = _files[num];            // Get the entry
                    f.ToBuffer(ref buf, offset);    // Save it

                    offset += size;                 // Bump for next entry
                    startSeg += f.Size;             // Bump for next vol header
                    num++;                          // Next file
                    j++;                            // Next entry in segment
                }

                // Close this segment
                Helper.WriteWord(buf, offset, (ushort)StatusWord.Terminator);

                // Write it!
                PutSegment((uint)curSeg, buf);

                // Bump the LBN
                curSeg += 2;
            }
        }

        /// <summary>
        /// Prints the "raw" RT11 directory (for debugging, mostly).
        /// </summary>
        public void PrintDirInfo()
        {
            var num = 0;
            Console.WriteLine("RT11 Directory:");

            foreach (var f in _files)
            {
                num++;
                Console.WriteLine($"\nDirectory entry #{num}:");
                Console.WriteLine($"  Status:     {f.Status}");
                Console.WriteLine($"  Filename:   {f.Filename}");
                Console.WriteLine($"  File size:  {f.Size}");
                Console.WriteLine($"  File date:  {f.Date} ({f.DateVal})");
                Console.WriteLine($"  Job chan#:  {f.JobChan}");
                Console.WriteLine($"  POS bits:   {f.BitsInLastBlock}");
                Console.WriteLine($"  1st block:  {f.StartBlock}");
            }

            Console.WriteLine();
            Console.WriteLine($"  Total files:  {_numFiles}");
            Console.WriteLine($"  Free blocks:  {_blksFree}");
        }

        /// <summary>
        /// Reads a file into memory and returns its contents (cached so next
        /// read just returns the data immediately).
        /// </summary>
        public byte[] ReadFile(DirectoryEntry file)
        {
            // Has this file been read in yet?
            if (file.Data == null)
            {
                var first = file.StartBlock;
                var last = first + file.Size - 1;
                var lastBytes = file.BitsInLastBlock / 8;

                // Assume n full blocks...
                var totalBytes = (file.Size * 512);

                // ... but if the last block isn't full, trim it!
                if (file.BitsInLastBlock < 4096)
                {
                    totalBytes -= (512 - lastBytes);
                }

                file.Data = new byte[totalBytes];

                var offset = 0;

                for (var blk = first; blk <= last; blk++)
                {
                    if (blk == last)
                    {
                        var buf = Helper.CopyFrom(_disk.GetSector(blk).Data, 0, (file.BitsInLastBlock / 8));
                        buf.CopyTo(file.Data, offset);
                    }
                    else
                    {
                        _disk.GetSector(blk).Data.CopyTo(file.Data, offset);
                    }

                    offset += 512;
                }
            }

            return file.Data;
        }

        /// <summary>
        /// Commit the file's data to backing storage device (in memory!).
        /// </summary>
        /// <remarks>
        /// Makes sure the Data is properly broken into sectors and stored in the
        /// underlying StorageDevice.  IF the Data array is null, should we call
        /// ReadFile to populate it automagically, as might be the case when the
        /// Compress operation is moving things around?  For now, NO, the caller
        /// must do that.  Hmm.
        /// </remarks>
        public void WriteFile(DirectoryEntry file)
        {
            // Sanity check.  For now, throw an error.
            if (file.Data == null)
                throw new InvalidOperationException("Null data in WriteFile");

            var first = file.StartBlock;
            var last = first + file.Size - 1;
            var lastBytes = file.BitsInLastBlock / 8;

            Console.WriteLine($"{file.Filename} is {file.Size} blocks @ {first}, {lastBytes} bytes @ {last}");

            var offset = 0;

            for (var blk = first; blk <= last; blk++)
            {
                // Copy each sector-sized chunk of data, zero-filling the
                // last one if necessary.  This is silly.  Could just FillFrom
                // but the Copy loop is marginally faster, as if that could
                // possibly make a difference on a modern multi-GHz processor...
                _disk.PutSector(blk, (blk == last ?
                                      Helper.FillFrom(file.Data, offset, 512) :
                                      Helper.CopyFrom(file.Data, offset, 512)));

                offset += 512;
            }
        }

        /// <summary>
        /// Add a new file to the directory.  On success, updates the directory
        /// and volume info to reflect new file count and remaining free space.
        /// </summary>
        /// <returns>
        ///  0 = successfully added the file
        /// -1 = name was invalid
        /// -2 = not enough free space
        /// -3 = directory is full
        /// </returns>
        public int EnterFile(string name, byte[] data)
        {
            var blocks = (data.Length + 511) / 512;
            var bits = ((data.Length % 512) * 8);

            // Find a hole!  Bail out if there's not enough room
            var slot = FindHole(blocks);
            if (slot < 0) return -2;


            // Is the found hole the correct size?
            var freeBlocks = _files[slot].Size - blocks;

            // Is there room in the directory?  If we have to split the current
            // block (adding a new entry) but the directory is full, bail out
            if (freeBlocks != 0 && _numFiles >= MaxFiles) return -3;

            // Convert the name and make sure it's valid
            var file = "";
            var ext = "";
            if (!Radix50.Split6dot3(name, ref file, ref ext)) return -1;

            // Get back the encoded version
            var words = Radix50.StringToRad50(file + ext);  // Space-padded!

            // If necessary, create the new free segment and add it,
            // shrinking the found slot and updating it in place
            if (freeBlocks > 0)
            {
                var free = new DirectoryEntry((ushort)freeBlocks);
                free.StartBlock = (ushort)(_files[slot].StartBlock + blocks);
                _files.Add(free);
                _numFiles++;

                Log.Detail(Category.RT11, "EnterFile: New empty slot has {0} blocks free @ {1}",
                                           free.Size, free.StartBlock);
            }

            Log.Detail(Category.RT11, "EnterFile: Updating slot {0} with new file info", slot);
            var entry = _files[slot];

            // Update the record
            entry.Status = StatusWord.Permanent;
            entry.Name0 = words[0];
            entry.Name1 = words[1];
            entry.Ext = words[2];
            entry.Size = (ushort)blocks;
            entry.BitsInLastBlock = (ushort)(bits == 0 ? 4096 : bits);    // POS quirk
            entry.JobChan = 0;
            entry.DateVal = DateWord.Encode(DateTime.Today);
            entry.Data = data;
            entry.Basename = Radix50.Rad50ToString(new ushort[] { entry.Name0, entry.Name1 });
            entry.Extension = Radix50.Rad50ToString(new ushort[] { entry.Ext });
            entry.Filename = Radix50.Join6dot3(entry.Basename, entry.Extension);
            entry.Date = DateWord.Decode(entry.DateVal);

            // Save it back and update the directory
            _files[slot] = entry;
            WriteFile(entry);

            Log.Debug(Category.RT11, "EnterFile: {0} ({1} blocks, {2} bits)",
                                      name, entry.Size, entry.BitsInLastBlock);

            _blksFree -= blocks;
            SortDirectory();
            Save();
            return 0;
        }

        /// <summary>
        /// Deletes a file from the directory and replaces it with an unused
        /// entry (or coalesces the vacated spot with an adjacent free entry).
        /// Updates the directory and volume info.
        /// </summary>
        public void DeleteFile(DirectoryEntry file)
        {
            // Convert to an index and sanity check
            var index = _files.IndexOf(file);
            if (index < 0 || index > _files.Count - 1 || file.Status != StatusWord.Permanent)
            {
                Log.Error(Category.RT11, "DeleteFile: bad index {0} or status for file {1}",
                                          index, file.Filename);
                return;
            }

            // Change the status and zap the relevant bits
            file.Status = StatusWord.Unused;
            file.Name0 = 0;
            file.Name1 = 0;
            file.Ext = 0;
            file.Filename = string.Empty;
            file.BitsInLastBlock = 4096;

            // Save it back to the list!?
            _files[index] = file;

            Log.Debug(Category.RT11, "DeleteFile: Freed slot {0}, {1} blocks",
                                      index, _files[index].Size);

            // Update the block count
            _blksFree += file.Size;

            // Combine free blocks if there are adjacent unused slots
            Coalesce();
        }

        /// <summary>
        /// Find free space of at least <param name="size">size</param> blocks.
        /// </summary>
        /// <returns>
        /// >=0 = index of a suitable unused block
        ///  -1 = no hole found that can accommodate the request
        ///  -2 = bad size, file too large for the floppy
        /// </returns>
        public int FindHole(int size)
        {
            if (size > _blksFree) return -2;

            for (int i = 0; i < _files.Count; i++)
            {
                if (_files[i].Status == StatusWord.Unused && _files[i].Size >= size)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Coalesce adjacent free blocks after a delete operation.  Rebuilds
        /// the directory as necessary.  Always succeeds even if no changes made.
        /// </summary>
        private void Coalesce()
        {
            if (_files.Count < 2) return;   // Nothing to do

            var needsRebuilt = false;       // the Pittsburgh variable
            var curId = 0;

            while (curId < _files.Count - 1)
            {
                var nextId = curId + 1;

                if (_files[curId].Status == StatusWord.Unused &&
                    _files[nextId].Status == StatusWord.Unused)
                {
                    // Merge!  Do this silliness (struct DirEntry)
                    var foo = _files[curId];
                    foo.Size += _files[nextId].Size;
                    _files[curId] = foo;

                    // Consume the next entry and update
                    _files.RemoveAt(nextId);
                    _numFiles--;
                    needsRebuilt = true;

                    Log.Debug(Category.RT11,
                              "Coalesced entries {0} + {1}, new size = {2}",
                              curId, nextId, _files[curId].Size);
                }
                else
                {
                    curId++;
                }
            }

            if (needsRebuilt) Save();
            return;
        }

        /// <summary>
        /// Stitch together two logical blocks into a 1 Kbyte directory "segment".
        /// Each one contains a copy of the volume info and up to 62 file entries.
        /// </summary>
        private Sector GetSegment(uint lbn)
        {
            var segment = new Sector(0, 0, (ushort)lbn, 1024);

            var tmp = _disk.GetSector(lbn);
            tmp.Data.CopyTo(segment.Data, 0);

            tmp = _disk.GetSector(lbn + 1);
            tmp.Data.CopyTo(segment.Data, 512);

            return segment;
        }

        /// <summary>
        /// Split a 1 Kbyte directory segment into two logical blocks and write
        /// 'em back to the underlying storage.
        /// </summary>
        private void PutSegment(uint lbn, byte[] data)
        {
            _disk.PutSector(lbn, Helper.CopyFrom(data, 0, 512));
            _disk.PutSector(lbn + 1, Helper.CopyFrom(data, 512, 512));
        }

        /// <summary>
        /// Sorts the directory by StartBlock.  Should only be needed after new
        /// files are added?
        /// </summary>
        private void SortDirectory()
        {
            // Nothing to sort
            if (_files.Count < 2) return;

            _files.Sort(delegate (DirectoryEntry x, DirectoryEntry y)
            {
                return x.StartBlock.CompareTo(y.StartBlock);
            });
        }

        private Volume _vol;
        private RT11Floppy _disk;
        private List<DirectoryEntry> _files;

        private int _numFiles;
        private int _blksFree;
    }
}
