# VGM Oscilloscope External Trigger Tool
A .VGM hacking tool for FM synth oscilloscopes. For creating external trigger waveforms.
All this tool does is create edited .vgm files that play back exclusively in doot-mode. Perfect for using as External Triggers in oscilloscopes - such as SidWizPlus
Supported chips: OPM, OPN, OPNA, OPNB, OPN2, OPL, Y8950, OPL2 

# Instructions
This is a command line program. Alternatively, you can drag & drop to produce a file at default settings. The command -h or opening the .exe with no arguments will bring up help info.

Usage: EXE [options] Infile.VGM"

Options may be set globally and/or per-channel, by preceding an option with a 'FM#' command (zero-bound)

Available options for 4-operator DT(def 10), Mult(def unspecified/auto)

Available options for 2-operator OPL2: Mult(def unspecified/auto)

Advanced options: Patch "PatchKey Commands" (see -help)

PatchKey applies settings on a instrument-by-instrument basis, by providing some identifying info then the desired settings. This can be used to fine-tune the tracking of the output, particularly on very detuned FM instruments. This program will print a list of all the identified patches in the patchkey format.

If the argument ""P P"" is used, the patch report will use an even simpler syntax for quick copy-pasting into .bat files. See -help for more info.

  SETTINGS FOR DETUNE (DT value, not for OPL)
  
Because 4-operator FMs have up to 4 distinct detune values, we have to pick one for our external trigger, which is done using the following algorithm settings:

* 0 - No Detune (good starting point for PatchKey system)

  0-7 - force a detune setting. 7-6-5-0-1-2-3 in order corresponds to -3 to +3 (4 is the same as 0)
  
* 8  - Use the DT of the lowest frequency operator bias 1>2>3>4 - If there are matches, use the DT of the earlier op (OP#1 > OP#2 > OP#3 > OP#4)

  9  - Use the DT of the lowest frequency operator bias 4>3>2>1 - If there are matches, use the DT of the later op (OP#4 > OP#3 > OP#2 > OP#1)

* 10 - Use an average of all Detune values <- NEW DEFAULT

* 11 - Use the DT of the MOST detuned operator bias 1>2>3>4 - If there are matches, use earliest op

  12 - Use the DT of the MOST detuned operator bias 4>3>2>1 - If there are matches, use later op

  13 - Use the DT of the LEAST detuned operator bias 1>2>3>4 - If there are matches, use earliest op

  14 - Use the DT of the LEAST detuned operator bias 4>3>2>1 - If there are matches, use later op

  ...- any other value will be equivalent to 0

  21 - always use OP#1's associated DT

  22 - always use OP#2's associated DT

  23 - always use OP#3's associated DT

  24 - always use OP#4's associated DT

  * = recommended
  
... or just drag & drop.


# Limitations
Heavily modulated 4-operator FM patches may drift due to Detune being too coarse to track them perfectly

OPN CH#3 Extended mode is not yet supported

OPL drum mode channels probably won't work and might break note detection

# MISC

Special thanks to maxim-zhao, developer of SidWizPlus.
