//
//  Radix50.cs
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
using System.Text;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;

using PERQemu;

namespace PERQdisk.RT11
{
    /// <summary>
    /// Converts RT11 filenames between Radix-50 format and regular strings.
    /// </summary>
    public static class Radix50
    {
        static Radix50()
        {
            _encoder = new Hashtable(40);
            _decoder = new char[40];

            InitEncoder();
        }

        /// <summary>
        /// Convert a series of RAD50 words to characters.
        /// </summary>
        public static string Rad50ToString(ushort[] encoded)
        {
            var sb = new StringBuilder();

            foreach (var word in encoded)
            {
                int i = word / 1600;
                int j = word - i * 1600;

                sb.Append(_decoder[i]);
                sb.Append(_decoder[j / 40]);
                sb.Append(_decoder[j % 40]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Format a string (filename) as a series of RAD50-encoded 16-bit
        /// unsigned integers (three characters per word).
        /// </summary>
        public static ushort[] StringToRad50(string name)
        {
            var offset = 1600;
            ushort word = 0;
            var words = new List<ushort>();

            // Find all valid characters in the string, and drop the illegal
            // ones.  Convert to upper case.
            foreach (var c in name.ToUpper())
            {
                if (_encoder.ContainsKey(c))
                {
                    // Store the mapped char, offset
                    word += (ushort)((int)_encoder[c] * offset);

                    // Now bump the offset
                    if (offset == 1600)
                    {
                        offset = 40;
                    }
                    else if (offset == 40)
                    {
                        offset = 1;
                    }
                    else
                    {
                        // Last one, push and reset
                        words.Add(word);
                        offset = 1600;
                        word = 0;
                    }
                }
                else
                {
                    Log.Detail(Category.RT11, "Skipping illegal char '{0}'", c);
                }
            }

            // Catch remnants in the last word (if input not a multiple of 3 chars)
            if (word > 0)
            {
                words.Add(word);
            }

            return words.ToArray();
        }

        /// <summary>
        /// Munge any filename into a valid floppy name.  Returns a string of
        /// only Radix-50 characters in "6.3" format.  Squeezes out spaces and
        /// throws out any invalid characters.
        /// </summary>
        public static string Format6dot3(string name)
        {
            // Lazy way to screen out the bad stuff!
            var newName = TrimAllWhitespace(Rad50ToString(StringToRad50(name)));

            // Catch other bogus stuff...
            while (newName.StartsWith(".", StringComparison.Ordinal))
            {
                newName = newName.Substring(1);
            }

            if (newName.Length > 0)
            {
                string file = "";
                string ext = "";

                // This is... insane.
                if (Split6dot3(newName, ref file, ref ext))
                    newName = Join6dot3(file, ext);
            }

            Log.Detail(Category.RT11, "Formatted '{0}' => '{1}'", name, newName);
            return newName;
        }

        /// <summary>
        /// Break apart a filename into its basename and extension, padded to
        /// 6 and 3 characters.  Returns false if the name is empty or invalid
        /// (contains too many dots, which I think POS balks at?)
        /// </summary>
        public static bool Split6dot3(string name, ref string file, ref string ext)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                Log.Debug(Category.RT11, "Could not split empty name!");
                return false;
            }

            var parts = name.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

            // Too few parts - empty or seriously deranged filename
            if (parts.Length < 1)
            {
                Log.Debug(Category.RT11, "Malformed filename '{0}'", name);
                return false;
            }

            // Too many parts!?
            if (parts.Length > 2)
            {
                // Squash 'em (dots removed) and assume last part is the extension
                file = TrimAllWhitespace(string.Join("", parts, 0, parts.Length - 1));
            }
            else
            {
                // Otherwise it's a well-formed file (with or without extension)
                file = (parts[0]);
            }

            // Pad to exactly six chars
            file = (file + "     ").Substring(0, 6);

            // If we have an extension, pad it to three chars
            if (parts.Length > 1)
            {
                ext = (parts[parts.Length - 1] + "   ").Substring(0, 3);
            }
            else
            {
                ext = "   ";
            }

            Log.Detail(Category.RT11, "Split '{0}' => '{1}' '{2}'", name, file, ext);
            return true;
        }

        /// <summary>
        /// Put the two filename parts together and squeeze spaces.
        /// </summary>
        public static string Join6dot3(string name, string ext)
        {
            // Add the extension and implicit "." only if not empty
            if (!string.IsNullOrWhiteSpace(ext)) name += "." + ext;

            return TrimAllWhitespace(name);
        }


        public static string TrimAllWhitespace(this string str)
        {
            return string.Join("", str.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
        }


        static void InitEncoder()
        {
            // @Rad50 = (' ', 'A'..'Z', '$', '.', "\377", '0'..'9');   # Rad50 decodes
            // %Rad05 = (map { $_ => $i++ } @Rad50 );                  # Rad05 encodes

            // Perl does it in two lines.  Gotta be a cleaner way to do this in C#?

            _encoder.Add(' ', 0);
            _decoder[0] = ' ';

            var c = 'A';
            for (var i = 1; c <= 'Z'; i++, c++)
            {
                _encoder.Add(c, i);
                _decoder[i] = c;
            }

            _encoder.Add('$', 27);
            _decoder[27] = '$';

            _encoder.Add('.', 28);
            _decoder[28] = '.';

            _encoder.Add((char)0xff, 29);   // RT-11 says undefined, but some
            _decoder[29] = (char)0xff;      // encode this as '%' or '*'...

            c = '0';
            for (var i = 30; c <= '9'; i++, c++)
            {
                _encoder.Add(c, i);
                _decoder[i] = c;
            }
        }


        [Conditional("DEBUG")]
        public static void DumpEncoder()
        {
            /*
                Console.WriteLine("Decoder table:");
                for (var i = 0; i < _decoder.Length; i++)
                {
                    Console.Write($"{i} => '{_decoder[i]}'  ");
                }
                Console.WriteLine();

                Console.WriteLine("Encoder table:");
                for (var i = 0; i < 256; i++)
                {
                    if (_encoder.ContainsKey((char)i))
                        Console.Write($"'{(char)i}' => {_encoder[(char)i]}  ");
                }
                Console.WriteLine();
            */

            Console.WriteLine("\nChecking Radix50 encoder:\n");

            var testStrings = new List<EncoderTest>()
            {
                new EncoderTest("FILE  TXT", "FILE  TXT", "FILETX"),
                new EncoderTest("file  .txt", "FILE  .TXT", "FILE.TXT"),
                new EncoderTest("this is a random long string", "THIS IS A RANDOM LONG STRING", "THISIS"),
                new EncoderTest("Too..many.dots...", "TOO..MANY.DOTS...", "TOOMAN.DOT"),
                new EncoderTest("perq.files.seg", "PERQ.FILES.SEG", "PERQFI.SEG"),
                new EncoderTest("123456789", "123456789", "123456"),
                new EncoderTest("a.", "A.", "A"),
                new EncoderTest("bad^&ch.ar*)s_$", "BADCH.ARS$", "BADCH.ARS"),
                new EncoderTest(".illegal", ".ILLEGAL", "ILLEGA"),
                new EncoderTest("rad50.pas", "RAD50.PAS", "RAD50.PAS"),
                new EncoderTest("IO_Unit.seg", "IOUNIT.SEG", "IOUNIT.SEG")
            };

            var failed = 0;

            foreach (var s in testStrings)
            {
                var munged = Format6dot3(s.Original);
                if (munged != s.Expected6dot3)
                {
                    failed++;
                    Console.WriteLine($"Format 6.3 test '{s.Original}' failed:");
                    Console.WriteLine($"Expected '{s.Expected6dot3}', got '{munged}'\n");
                }

                var result = Rad50ToString(StringToRad50(s.Original)).TrimEnd();
                if (result != s.ExpectedRad50)
                {
                    failed++;
                    Console.WriteLine($"RAD50 Codec test '{s.Original}' failed:");
                    Console.WriteLine($"Expected '{s.ExpectedRad50}', got '{result}'\n");
                }
            }

            Console.WriteLine($"Tested {testStrings.Count} strings with {failed} failures.");
        }

        internal struct EncoderTest
        {
            internal EncoderTest(string orig, string rad50, string fn6dot3)
            {
                Original = orig;
                ExpectedRad50 = rad50;
                Expected6dot3 = fn6dot3;
            }

            public string Original;
            public string ExpectedRad50;
            public string Expected6dot3;
        }

        static Hashtable _encoder;
        static char[] _decoder;
    }
}
