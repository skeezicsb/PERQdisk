//
//  Log.cs
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
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PERQemu
{
    /// <summary>
    /// Filter for the level of verbosity: higher the number, the less that
    /// we pester the user with.  Normal is the default; higher numbers weed
    /// out abnormal conditions, lower numbers increase the amount of detail.
    /// </summary>
    public enum Severity
    {
        All = 0,
        Verbose = 1,
        Debug = 2,
        Info = 3,
        Normal = 4,
        Warning = 5,
        Error = 6,
        Heresy = 7,
        None = 8
    }

    /// <summary>
    /// Bitmap to filter what categories of output to log.
    /// </summary>
    /// <remarks>
    /// Keep this compatible with PERQemu; someday I'll have to merge the
    /// projects or something.  Ugh.
    /// </remarks>
    [Flags]
    public enum Category
    {
        None = 0x0,
        UI = 0x100,
        POS = 0x1000,
        RT11 = 0x2000,
        MediaLoader = 0x10000,
        All = 0x7fffffff
    }

    /// <summary>
    /// Log messages to the console and/or a file, with filters for the level of
    /// verbosity and other nice features (to aid in debugging, mostly).
    /// </summary>
    /// <design>
    /// By default, formats and writes messages on the console (using colors for
    /// each category).  Can also write to a directory, specified as OutputDir
    /// (user settable); currently the filename pattern, number and size of the
    /// files are fixed and not exposed as Settings.  Note that enabling All
    /// categories in Verbose mode can fill 100MB of logs before VFY even starts
    /// up, so *selective* logging is key.  Bumped the default log size to 10MB.
    /// </design>
    /// <remarks>
    /// While indispensible in debugging, logging is _painfully_ slow, so most
    /// of the methods are compiled out completely in Release builds.
    /// </remarks>
    public static class Log
    {
        static Log()
        {
            _categories = Category.None;        // Nothing selected
            _consLevel = Severity.Normal;       // This is normal
            _lastOutput = string.Empty;
            _repeatCount = 0;

            SetColors();
        }


        public static Category Categories
        {
            get { return _categories; }
            set { _categories = value; }
        }

        public static Severity Level
        {
            get { return _consLevel; }
            set { _consLevel = value; }
        }


        /// <summary>
        /// A shortcut for logging debugging output.  Makes a slow Debug process
        /// that much slower but is compiled out entirely in Release builds.
        /// </summary>
        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Debug(Category c, string fmt, params object[] args)
        {
            WriteInternal(Severity.Debug, c, fmt, args);
        }

        /// <summary>
        /// A shortcut for logging extra detailed debugging output.  Like, stuff
        /// that's reeeally verbose.  Like Debug, adds a ton of overhead but is
        /// compiled out entirely in Release builds.
        /// </summary>
        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Detail(Category c, string fmt, params object[] args)
        {
            WriteInternal(Severity.Verbose, c, fmt, args);
        }

        /// <summary>
        /// Shortcut for displaying informational messages (verbose mode,
        /// not in performance-critical situations).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Info(Category c, string fmt, params object[] args)
        {
            WriteInternal(Severity.Info, c, fmt, args);
        }

        /// <summary>
        /// Shortcut for debug warnings that should stand out (but are non-fatal
        /// and can be ignored in Release builds).
        /// </summary>
        public static void Warn(Category c, string fmt, params object[] args)
        {
            WriteInternal(Severity.Warning, c, fmt, args);
        }

        /// <summary>
        /// Shortcut for displaying a serious error.
        /// </summary>
        /// <remarks>
        /// Gosh, it's a shame Console doesn't support <blink>attributes</blink>.
        /// </remarks>
        public static void Error(Category c, string fmt, params object[] args)
        {
            WriteInternal(Severity.Error, c, fmt, args);
        }

        /// <summary>
        /// A plain Write() is always displayed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(string fmt, params object[] args)
        {
            WriteInternal(Severity.None, Category.All, fmt, args);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(Category c, string fmt, params object[] args)
        {
            WriteInternal(Severity.Normal, c, fmt, args);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(Severity s, Category c, string fmt, params object[] args)
        {
            WriteInternal(s, c, fmt, args);
        }

        static void WriteInternal(Severity s, Category c, string fmt, params object[] args)
        {
            // Apply filters before we do the work to format the output
            if ((s >= _consLevel) && ((c & _categories) != 0))
            {
                var output = string.Format((c == Category.All ? "" : c.ToString() + ": ") + fmt, args);

                // Cut down on the noise: things like the processor looping to
                // check an I/O status byte spews a lot... summarize that (but
                // not endlessly to show progress)
                if (output == _lastOutput && _repeatCount < 256)
                {
                    _repeatCount++;
                    return;
                }

                // Was the last message repeated?
                if (_repeatCount > 0)
                {
                    if (_repeatCount == 1)
                        Console.WriteLine(_lastOutput);     // one repeat is ok :-)
                    else
                        Console.WriteLine($"[Last message repeated {_repeatCount} times]");
                }

                // Set the text color; in severe cases, override them to standout
                // Since the Mac Terminal blats the background color across the
                // entire output line, fudge the output to look a little better...
                switch (s)
                {
                    case Severity.Warning:
                        Console.BackgroundColor = ConsoleColor.Yellow;
                        Console.ForegroundColor = ConsoleColor.Black;

                        Console.Write(output);
                        Console.BackgroundColor = _defaultBackground;
                        Console.WriteLine();
                        break;

                    case Severity.Error:
                    case Severity.Heresy:
                        Console.BackgroundColor = ConsoleColor.Red;
                        Console.ForegroundColor = ConsoleColor.White;

                        Console.Write(output);
                        Console.BackgroundColor = _defaultBackground;
                        Console.WriteLine();
                        break;

                    default:
                        Console.ForegroundColor = _colors[c];
                        Console.WriteLine(output);
                        break;
                }

                Console.ResetColor();
                Thread.Sleep(0);   // Give it a rest why dontcha

                _lastOutput = output;
                _repeatCount = 0;
            }
        }


        /// <summary>
        /// Sets up a dictionary for mapping Categories to console colors,
        /// to aid in distinguishing output when debugging.
        /// </summary>
        static void SetColors()
        {
            _colors = new Dictionary<Category, ConsoleColor>();

            // This is a stripped-down version of Log.cs copied from PERQemu.

            // General messages
            _colors.Add(Category.All, ConsoleColor.White);
            _colors.Add(Category.None, ConsoleColor.Black);     // HeartOfGold theme
            _colors.Add(Category.UI, ConsoleColor.Gray);
            _colors.Add(Category.POS, ConsoleColor.Green);
            _colors.Add(Category.RT11, ConsoleColor.Cyan);
            _colors.Add(Category.MediaLoader, ConsoleColor.DarkYellow);
        }

        [Conditional("DEBUG")]
        public static void ShowColors()
        {
            foreach (Severity s in Enum.GetValues(typeof(Severity)))
            {
                foreach (Category c in Enum.GetValues(typeof(Category)))
                {
                    WriteInternal(s, c, "Test!");
                }
            }
        }

        static Severity _consLevel;
        static Category _categories;

        static Dictionary<Category, ConsoleColor> _colors;

        static ConsoleColor _defaultForeground = Console.ForegroundColor;
        static ConsoleColor _defaultBackground = Console.BackgroundColor;

        static string _lastOutput;
        static int _repeatCount;
    }
}
