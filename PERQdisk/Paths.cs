//
//  Paths.cs
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

namespace PERQdisk
{
    /// <summary>
    /// Helpers for manipulating pathnames.
    /// </summary>
    public static class Paths
    {
        /// <summary>
        /// Default directory (besides ".") to look for PERQ hard and floppy disk
        /// images.  For PERQdisk this should be a stored default, or possibly
        /// even a stack or $PATH-like variable?  That way it could be pointed at
        /// a local PERQemu/Disks directory, or a CIFS share or NFS mount on the
        /// NetApp.  (Or is that just me?)
        /// </summary>
        public static string DiskDir
        {
            get { return Path.Combine(PERQdisk.BaseDir, "Disks"); }
        }

        /// <summary>
        /// Default directory for saving files extracted from media.
        /// </summary>
        public static string OutputDir
        {
            get { return Path.Combine(PERQdisk.BaseDir, "Output"); }
        }

        /// <summary>
        /// Settings dir is the root of the user's home directory.
        /// </summary>
        public static string SettingsDir
        {
            get
            {
                return PERQdisk.HostIsUnix
                       ? Environment.GetEnvironmentVariable("HOME")
                       : Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
            }
        }

        /// <summary>
        /// Return the path to the default configuration file, appropriate to
        /// the platform.   We save settings in a common format and avoid any
        /// platform-specific mechanisms.
        /// </summary>
        public static string SettingsPath
        {
            get
            {
                return Path.Combine(SettingsDir, (PERQdisk.HostIsUnix
                                                  ? ".PERQdisk_cfg"
                                                  : "PERQdisk.cfg"));
            }
        }


        public static string BuildDiskPath(string file)
        {
            return Path.Combine(DiskDir, file);
        }

        public static string BuildOutputPath(string file)
        {
            return Path.Combine(OutputDir, file);
        }


        /// <summary>
        /// Try to qualify a file name by applying a few simple search rules:
        ///     1. Try 'file' as given
        ///     2. Try 'file' in 'dir'
        ///     3. Try 'file' with each of the 'extensions' in current, 'dir'
        /// Returns the canonicalized pathname upon the first successful match,
        /// otherwise an empty string.
        /// </summary>
        public static string FindFileInPath(string file, string dir, params string[] extensions)
        {
            var canonical = Canonicalize(file);

            // Try exact match, as given
            if (File.Exists(file))
            {
                return file;
            }

            // Try with possible ~ expansion (Unix)
            if (File.Exists(canonical))
            {
                return canonical;
            }

            // Exact match with 'dir' prepended
            if (File.Exists(Path.Combine(dir, file)))
            {
                return Canonicalize(Path.Combine(dir, file));
            }

            // Try the canonicalized path with alternate extensions?
            if (extensions.Length > 0)
            {
                // Back to the beginning
                var root = file;

                // Remove the current one
                if (Path.HasExtension(file))
                {
                    root = Path.GetFileNameWithoutExtension(root);
                }

                foreach (var ext in extensions)
                {
                    var found = Path.ChangeExtension(root, ext);

                    // Do ~ expansion and check with current extension
                    canonical = Canonicalize(found);
                    if (File.Exists(canonical)) return canonical;

                    // Try again, but in dir
                    found = Path.Combine(dir, found);
                    if (File.Exists(found)) return Canonicalize(found);
                }
            }

            // Special case: if the file isn't found after all the transformations
            // and we're on a Unix host, scan for the Windows directory separator.
            // If something like Disks\g7.prqm slipped through, replace with '/'
            // and try again...
            if (PERQdisk.HostIsUnix && (file.Contains("\\") || dir.Contains("\\")))
            {
                return FindFileInPath(file.Replace('\\', '/'), dir.Replace('\\', '/'), extensions);
            }

            return string.Empty;
        }

        /// <summary>
        /// Return a (presumed) simple filename with a directory and extension.
        /// </summary>
        /// <remarks>
        /// Attempts to transform 'file' into the form 'dir/file.extension'.  If
        /// file already has a directory specification it is not changed; if the
        /// file does not have any extension the one given is appended, otherwise
        /// an existing extension is only replaced if replaceExt is true.  The
        /// return string is "canonicalized".
        /// </remarks>
        public static string QualifyPathname(string file, string dir, string extension, bool replaceExt)
        {
            var path = Path.GetDirectoryName(file);

            if (path == string.Empty)
            {
                path = Path.Combine(dir, file);
            }
            else
            {
                path = file;    // Gotta go with what they gave us
            }

            if ((Path.HasExtension(path) && replaceExt) || !Path.HasExtension(path))
            {
                path = Path.ChangeExtension(path, extension);
            }

            return Canonicalize(path);
        }

        /// <summary>
        /// Try to make long crazy strings less long and crazy.
        /// </summary>
        public static string Canonicalize(string path)
        {
            // in .NET 5, so not on my old machine :-(
            // return Path.GetRelativePath(PERQdisk.BaseDir, path);

            try
            {
                // Special case: expand the Unix '~' to the users's home dir
                if (PERQdisk.HostIsUnix && path.Length > 0 && path[0] == '~')
                {
                    if (path == "~")
                    {
                        path = SettingsDir;
                    }
                    else if (path.StartsWith("~/", StringComparison.InvariantCulture))
                    {
                        path = Path.Combine(SettingsDir, path.Substring(2));
                    }
                    else
                    {
                        #region Ah-oo-gah!
                        /*
                            Ugh. This is nasty. Handling ~user requires that we
                            have to avoid expansions of the sort
                            
                                  ~user[/foo] => /home/path/user/user[/foo]
                                  
                             The quick and dirty solution makes these assumption
                             that the home directories on your machine use a common
                             root path, typically something like
                                  /home/<user>
                             or   /Users/<user>
                             
                             and NOT a Windows-ish
                                  "c:\\something\<user>\documents and settings"
                             kind of deal.

                            The "worst case" is that we mangle the path and
                            Open() fails and the user retypes the name to not
                            include a ~.  This could all be yanked out if/when
                            the Mono runtime could deal with ~ internally...
                         */
                        #endregion

                        // For now let's just blindly assume this works:
                        var usersDir = SettingsDir.Substring(0, SettingsDir.LastIndexOf('/'));
                        path = Path.Combine(usersDir, path.Substring(1));
                    }
                }

                // Let the runtime expand it
                var full = Path.GetFullPath(path);

                // See if we can trim it down some
                if (full == PERQdisk.BaseDir)
                {
                    return "";    // Base relative to base is, uh, right here
                }

                if (full.StartsWith(PERQdisk.BaseDir, PERQdisk.HostIsUnix ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
                {
                    return full.Substring(PERQdisk.BaseDir.Length + 1);
                }

                // Outside our working dir? for now, return the path unmodified
                return path;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Could not make a relative path for '{path}': {e.Message}");
                return path;
            }
        }

        /// <summary>
        /// Strip away any illegal characters from an output path.  This cleans
        /// up pathnames that POS allows but Windows (or Unix) balks at.
        /// </summary>
        /// <remarks>
        /// Rather than strip or map illegal characters to a fixed value, replace
        /// them with their hex code.  This makes them stand out and should help
        /// avoid creating naming collisions that would result in overwritten or
        /// skipped files (as in the case of boot>helpdir>?.help and *.help both
        /// being written as _.help or just .help).  There aren't many instances
        /// of this, usually.
        /// </remarks>
        public static string Sanitize(string path)
        {
            var clean = path;

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                clean = clean.Replace(c.ToString(), $"0x{(byte)c}");
            }

            return clean;
        }

        /// <summary>
        /// Return just the filename portion of a path.
        /// </summary>
        public static string Basename(string path)
        {
            return Path.GetFileName(path);
        }

        /// <summary>
        /// Create a directory path if it doesn't already exist.  In dryrun mode,
        /// always returns true, otherwise prints error and returns false.
        /// </summary>
        public static bool MakeDestPath(string dst, bool dryrun = false)
        {
            // Is it already there?
            if (Directory.Exists(dst)) return true;

            // Nope, try to create it (with intermediate dirs as needed)
            try
            {
                if (!dryrun)
                {
                    Directory.CreateDirectory(dst);
                }
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"** Could not create '{dst}': {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Turn a filename pattern into a proper Perl-style regex.  Accepts the
        /// wildcards '?' and '*' and quotes certain POS filename characters that
        /// have meaning to the regex engine.  Also useful to RT11 (and PNX eventually).
        /// </summary>
        public static string MakePattern(string pat)
        {
            // Empty?  Match all
            if (pat == "") pat = "*";

            // Quote common filename chars '.', '+' and '$'
            pat = pat.Replace(".", "\\.");
            pat = pat.Replace("+", "\\+");
            pat = pat.Replace("$", "\\$");

            // Expand wildcards
            pat = pat.Replace("*", ".*");
            pat = pat.Replace("?", ".?");

            // Anchor it!
            return $"^{pat}$";
        }

        public static bool IsPattern(string pat)
        {
            return !string.IsNullOrEmpty(pat) && (pat.Contains("?") || pat.Contains("*"));
        }
    }
}
