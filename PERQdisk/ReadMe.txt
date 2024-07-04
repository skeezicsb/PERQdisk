
PERQdisk Readme
===============


1.0  Introduction
-----------------

PERQdisk is a standalone utility to navigate and extract files from archived
PERQ media.  It uses the same style of command-line interpreter as PERQemu and
can read any format supported by the PERQmedia library, with the ability to
transpose between them.  Please see the file UserGuide.txt for information about
installing and running PERQdisk.

This utility is still in active development; for more information about PERQemu,
the PERQmedia storage format, and other PERQ information in general, please see
the Github repository at:

    https://github.com/skeezicsb/PERQemu/

This new version of PERQdisk can read POS filesystems from hard disk or floppy
images.  It can also read and write RT-11 data floppies, and can interpret most
POS floppy command files to help automate retrieval of sets of files.

PERQdisk cannot currently read or interpret PNX disks in any form, as details
of those formats are unknown.


1.1  Original Release Notes (v0.0-v0.1)
---------------------------------------
J. Dersch 8/19/2006

PERQdisk is yet another quick-'n-dirty PERQ utility I whipped up to serve a 
personal need.  Maybe it will serve a need for you as well.  The code is pretty
simple and not too ugly (there are a few places where things should be better 
factored) and should be easy to change for a variety of POS-filesystem related 
tasks.

As it currently stands it's hardcoded for reading a POS filesystem dumped off 
of a Shugart SA4002 14" hard drive (202 cyls, 8 tracks, 30 sectors) using my 
"dumpdisk" utility.

It reads the drive, partition, and directory/file structures from the image and
allows traversing the partition/directory trees, prodding/dumping/copying files
off the "virtual" drive to a "real" disk.  It runs in "read-only" mode (you
can't modify the virtual disk.)

It provides a very limited pseudo-shell that allows you to do a few simple 
actions on the virtual disk's filesystems.  All of this code was written based
on the POS documentation -- copies are available on Bitsavers.

Currently everything seems to work OK, there are some fuzzy spots in the code 
(mostly around determining the size of/reading the blocks for a Directory file.)
I tried to comment things, but if you have questions feel free to shoot me 
e-mail at jdersch@gmail.com.


1.2  Release Notes (v0.2)
-------------------------
S. Boondoggle 3/24/2022

This is a hacked up version of PERQdisk that uses the new PERQmedia library
for loading and saving disk images.  It's a client that serves as proof-of-
concept for the new library, prior to incorporating it into PERQemu.  The
challenge now is to figure out how best to package all this up so that many
projects can share the underlying library -- but also allow the library (and
this client) to re-use code from the emulator too!  It's likely this entire
project will get slurped up into PERQemu so that it can all be built as one
giant wad of code.  Yeah baby.

A few bugs fixed/enhancements:
    PERQdisk can now properly read POS filesystem floppies!

    PERQmedia means that any supported disk or floppy device can be
    read, not just Shugart images.  (So far there aren't any other
    real disk images archived, but we can synthesize/format new ones.)

Plans:
    Break out the primitive PERQdisk CLI from Program.cs and use the
    PERQemu CommandAttributes and CommandProcessor to do all the nice
    command completion, history, etc.

    Bring all the features from the Perl version (perqdisk.pl) like
    wildcards, online help, easier naviagation, etc.


1.3  Release Notes (v0.9)
-------------------------
S. Boondoggle 8/3/2023

This version aims to make PERQdisk a "first class citizen" alongside PERQemu
as a tool for exploring and manipulating PERQ disk images.  It incorporates
many of the features of "perqdisk.pl", an unreleased Perl program that offered
a large number of features and improvements over the original C# PERQdisk.

It also adds the RT-11 floppy capabilities of "perqfloppy.pl", another Perl
tool that can read AND write floppies (in the "raw" or "pfd" formats).  Using
the PERQmedia library, this version of PERQdisk can handle all of the available
media formats and transpose images among them.

It currently does not posess the traditional command parsing style of the Perl
implementations that mimic the POS syntax, so switches are not available or are
given only as boolean parameters.  This is crude but doesn't impact the core
functionality (navigating the filesystems and viewing/extracting files).  Many
CLI improvements to come in subsequent updates.  Additional help, documentation
and maybe even a nice user's guide to be provided too?


1.4  Release Notes (v0.9.6)
---------------------------
S. Boondoggle 7/3/2024

Inching toward a 1.0 release, the 0.9.5/0.9.6 updates add a few new features
and bug fixes.  Most significantly, as PERQemu can now use the 8" Micropolis
and 5.25" MFM disks, PERQdisk has been extended to read them.  All official
releases of POS (through G.7) were limited to the first ~32MB of any drive,
while Accent and PNX could "see" and use all of the space on the largest disks
that 3RCC/ICL shipped and supported.  This is simply a development/early pre-
release snapshot to allow interrogation of new PERQ-2 disk images.  See the
PERQemu repository for the latest updates on emulator development.


2.0  About the Source Code
--------------------------
S. Boondoggle 11/20/2022

This Giant Wad of Code bears almost no resemblance to Josh Dersch's original
2006 version of PERQdisk.  He is blameless in this; anything cringeworthy here
that offends your programming aesthetic or melts your professional C# coding
brain is entirely my fault.  This is pretty much a slapped-together port of my
Perl tools with a C#-ish flavor, kind of.  I can *hear* the purists tense up at
reading that. :-)  Yeah, I know it ain't pretty, but it works.

Given the basic assumption that this utility won't find much use beyond my own
needs for building and extracting floppy sets or curating hundreds of archived
disk images, I haven't thought much about making the UI more accessible.  The
idea of making the Perl tools mimic the POS syntax was for my own amusement,
plus it enhanced the ability to use existing POS floppy command files (which
saved a TON of typing).  But I'm releasing it in case somebody out there finds
this useful or at least mildly entertaining.  It does contain quite a bit of
interesting and highly esoteric PERQ filesystem and RT-11 floppy volume hackery
for those who are drawn to such intensely nerdy subjects.

If you find bugs, have questions or want to send feedback, cash, or lucrative
development offers I'd be delighted to hear from you.


Revision History:

v0.9.6 - 7/3/2024 : fixes to work with 8" and 5.25" disk images > 32MB
v0.9.5 - 8/3/2023 : updates to parser, small bug fixes, ls command
v0.9 - 11/27/2022 : rewrite to incorporate tons of new functionality
v0.2 - 1/18/2022 : updated to use PERQmedia for all file loading!
v0.1 - 12/20/2009 : updated for new hard disk image format
v0.0 - 8/19/2006 : original release
