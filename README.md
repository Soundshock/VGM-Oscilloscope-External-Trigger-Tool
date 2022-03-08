# VGM Oscilloscope External Trigger Tool
A .VGM hacking tool for FM synth oscilloscopes. For creating external trigger waveforms.
All this tool does is create edited .vgm files that play back exclusively in doot-mode. Perfect for using as External Triggers in oscilloscopes - such as SidWizPlus
Supported chips: OPM, OPN, OPNA, OPNB, OPN2, OPL, Y8950, OPL2 

[![SidWizPlus + EXTT Example](https://iili.io/5rqJHX.webp)](https://youtu.be/j8XTUCIRDMw)

# Instructions
This is a command line program. Alternatively, you can drag & drop to produce a file at default settings. The command -h or opening the .exe with no arguments will bring up help info.
Outputs a new VGM of trigger waveforms which will then have to be rendered externally

Usage: EXE [options] Infile.VGZ"

Available options for 4-operator DT(def 10), Mult(def unspecified/auto), FORCEOP (def unspecified/auto)

Available options for 2-operator OPL2: Mult(def unspecified/auto), VIBRATO(def unspecified/second Op's value)

Advanced options: Patch "PatchKey Commands" (see -help)

Special options: BANK (def 0)  ... YM2608ToneEditor .bank export, 4-op only. To Use: EXE Bank 1 InFile.VGZ
                 SOLOVGM [Arguments]  ... Mute channels. Example: EXE SSG0 InFile.VGZ would solo SSG0

Options may be set globally or per-channel, by preceding an option with a 'FM#' command (zero-bound)

PatchKey applies settings on a instrument-by-instrument basis, by providing some identifying info then the desired settings. This can be used to fine-tune the tracking of the output, particularly on very detuned FM instruments. This program will print a list of all the identified patches in the patchkey format.

If the argument ""P P"" is used, the patch report will use an even simpler syntax for quick copy-pasting into .bat files. See -help for more info.

... or just drag & drop.

# Limitations
Heavily modulated 4-operator FM patches may drift due to Detune being too coarse to track them perfectly

OPL drum mode channels probably won't work and might break note detection

# MISC

Special thanks to maxim-zhao, developer of SidWizPlus.
