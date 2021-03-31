# VGM Oscilloscope External Trigger Tool
A .VGM hacking tool for FM synth oscilloscopes. For creating external trigger waveforms.
All this tool does is create edited .vgm files that play back exclusively in doot-mode. Perfect for using as External Triggers in oscilloscopes - such as new versions of SidWizPlus.
Supported chips: OPN OPNA OPNB OPN2, OPM, OPL2 

# Instructions
This is a command line program. Alternatively, you can drag & drop to produce a file at default settings. The command -h or opening the .exe with no arguments will bring up help info.

Usage: EXE [options] Infile.VGM"

Options may be set globally and/or per-channel, by preceding an option with a 'FM#' command (zero-bound)

Available options for 4-operator FM: DT(def 0), altwave(def true), ForceMult(def disabled)

Available options for 2-operator OPL2: altwave(def true), ForceMult(def disabled)

Advanced options: ForceOP(def 4) - 4-operator FMs only, doesn't work with altwave. Changing this value is not recommended. 

  SETTINGS FOR DETUNE (DT value, not for OPL2)
  
Because 4-operator FMs have up to 4 distinct detune values, we have to pick one for our external trigger. There's no one-size-fits-all solution here - manually editing triggers together in audacity may be necessary for the best results.

* 0 - No Detune (default)

0-7 - force a detune setting. 7-6-5-0-1-2-3 in order corresponds to -3 to +3 (4 is just 0)

* 8  - Use the DT of the lowest frequency operator bias OP#1 > OP#2 > OP#3 > OP#4
 
  9  - Use the DT of the lowest frequency operator bias OP#4 > OP#3 > OP#2 > OP#1
  
  10 - Use the DT of the lowest frequency operator bias OP#4 > OP#2 > OP#3 > OP#1
  
* 11 - Use the DT of the MOST detuned operator bias 1>2>3>4 (in all cases dt-3 and dt+3 count as matches)

  12 - Use the DT of the MOST detuned operator bias 4>3>2>1
  
  13 - Use the DT of the LEAST detuned operator bias 1>2>3>4
  
  14 - Use the DT of the LEAST detuned operator bias 4>3>2>1
  
  ...- any other value will be equivalent to 0 (default)
  
  21 - always use OP#1's associated DT
  
  22 - always use OP#2's associated DT
  
  23 - always use OP#3's associated DT
  
  24 - always use OP#4's associated DT
  
 * = recommended
  
  *Again none of this applicable for OPL2, because OPL2 has no operator detune settings.
  
  ALTERNATE WAVEFORM (altwave true)

Rather than produce a sine wave, produces a 50% FM wave. This kind of wave will NOT track better, but the cross at the 50% mark gives the scope something to grab onto if the frequency is too low for the scope framerate.
With altwave on, Low-frequency waveforms should skip around more predictably if the oscilloscope loses track.

  ADD MULT / FORCE MULT (forcemult/mult/addmult, off by default)

Some patches that utilize a chord-like structure need to have their implied roots defined by hand. Sorry. Look at the scope and see how many octaves down you need the trigger to be. Aim to cover the whole waveform.
Possible values: Positive values will force a multiplier, 0-15. Negative values subtract octaves, Try -1 or -2. 
# Example Usage
Options may be set globally and/or per-channel, by preceding an option with a 'FM#' command (zero-bound). Per-channel commands will always take precedence over global commands.

Example: invgm.VGM dt 0 altwave false         <- sets DT to 0 and altwave to false globally for all channels 

Example: invgm.VGM dt 0 altwave false fm0 dt 2 fm3 dt 11 <- does the above but sets dt to '0' for FM0, and '11' for FM3

Example: invgm.VGM dt 0 altwave false fm0 dt 2 fm3 dt 11 fm3 mult 1 <- + force fm3 to use multiplier 1

... or just drag & drop.


# Limitations
Some patches that have a chord-like structure (like a mult 4 + a mult 3, or a 5 into a 4 into a 3) will not track correctly....

... you'll have to hand adjust using ForceMult and edit triggers together. sorry. Usually -1 or -2 octaves will do it

... it's not impossible to track these with ext.triggers automatically, but it'll take some music theory math, maybe changing the note itself.

... for now, you may have to resort to using autocorrelate scope like Corrscope for some of these patches

In order to get the most consistent track, this program will use the lowest multiplier of all operators to do it's work...

...But if the lowest operator has a sharp decay it will lose track. This happens with some percussive patches and others.

OPL2 AM algorithm *might work*. Untested

OPL2 drum mode channels probably won't work and might break note detection

Most timing issues should be dealt with but please still check that output files are the correct length, and not shorter or longer.

# MISC

Special thanks to maxim-zhao, developer of SidWizPlus.
