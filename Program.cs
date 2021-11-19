using System;
using System.Collections.Generic;
using System.IO;
using System.Timers; // used for display
using System.Linq;

// using System.Reflection;
// using System.Text;
// using System.CommandLine; // not available for dotnet core
// using System.Data.Linq; // has a useful binary class but is only in dotnet framework 4.x

// vvv to publish with dependencies - but it'll be like 30 megs
// dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true


// v04 - major refactor, ALTWAVE removed, some DT algs removed, wip


/* v04 wip
ExamineVGMData (function that preemptively flags FM commands and Wait commands) is a relic from when this was strictly a find-replace program
but I'll still keep it because it makes other functions much simpler to understand

OPL2 patchkeys
	Patchkey refactor - working
		Show more in lost patch report, such as waveforms.
		Major thing you want to control is vibrato, in cases where alg is not connected
		
		******* Lost Patch Report *******
		Wave1 - Wave2 - Alg / Mult1 - Mult2 mMultDesired vVibrato?
		Sine-Sine-Connected-V1-V2 / 2-2m2v1
		
    todo
    test more
    update help
    monofy? OPM panning is part of the feedback register, not the same as OPNA
	? - could OPM DT2 be handled similarly?
	? OPL3?
	?	For OPL3 it would be a good time to refactor DATA.CS? idk	
Pin*Bot Mode
	if a song uses DT or ML sweeps, extt has to add values. No way around it.
	Split data into 10ms chunks then process through them
Granular Detune
	This requires math formulas for converting BLOCK-FNUM to DT for OPN, and another for OPM which uses BLOCK-KEY-KEYFRACTION
        DT affects the phase directly. The relationship between pitch and detune is logarithmic, maybe. 
*/


// 03 changelog
// removed subtractmult. It barely worked anyway

// TODO
// it might be better to make a separate program for handing CH3 mode and always force last operator

/*
        LIMITATIONS
    "Mult sweeps" will break things
    OPL / OPL2 drum mode channels probably won't work and might break note detection
*/

namespace EXTT

{
    public partial class Program {
        static int VERSIONMAJOR = 0, VERSIONMINOR=4;
        
        public delegate void WriteDelegate(string msg, params object[] args); // shortcut commands
        public static readonly WriteDelegate tb = Console.WriteLine; 
        delegate string WriteDelegate2(byte msg, int tobase);
        private static readonly WriteDelegate2 cts = Convert.ToString;
        // string constants for use as dictionary keys
        const string mult1 = "mult1";const string mult2 = "mult2";  const string mult3 = "mult3"; const string mult4 = "mult4"; 
        const string dt1 = "dt1";  const string dt2 = "dt2"; const string dt3 = "dt3"; const string dt4 = "dt4"; 
        const string wave1 = "wave1"; const string wave2 = "wave2"; const string alg = "alg"; const string vibrato1 = "vibrato1"; const string vibrato2 = "vibrato2"; 
        const string desiredDTalg = "desiredDTalg"; const string desiredMult = "desiredMult"; const string desiredVibrato = "desiredVibrato"; 
        public static readonly string[] patchkey_keys = new string[] {mult1, mult2, mult3, mult4, dt1, dt2, dt3, dt4, alg, desiredDTalg, desiredMult, 
                                                                    wave1, wave2, vibrato1, vibrato2, desiredVibrato};
        public static readonly string[] patchkey_keys_4op = new string[] {mult1, mult2, mult3, mult4, dt1, dt2, dt3, dt4, alg, desiredDTalg, desiredMult};
        public static readonly string[] patchkey_keys_2op = new string[] {mult1, mult2, alg, wave1, wave2, vibrato1, vibrato2, desiredVibrato};

        

        //? other const... misc patch data? KEYON1 KEYON2 KEYON3 DTML1 DTML2 DTML3 DTML4   etc etc etc?

        
        // Settings - per channel and global - are contained via class 'Arguments'
        static Arguments GlobalArguments = new Arguments(10, 4, 99, 0, "FMG"); // args: detunesetting, forceop, forcemult, altwaveform. These will be copied to unset values
        static Arguments FM0Args = new Arguments(99,99,99,99,"FM0"); static Arguments FM1Args = new Arguments(99,99,99,99,"FM1");
        static Arguments FM2Args= new Arguments(99,99,99,99,"FM2"); static Arguments FM3Args= new Arguments(99,99,99,99,"FM3");
        static Arguments FM4Args= new Arguments(99,99,99,99,"FM4"); static Arguments FM5Args= new Arguments(99,99,99,99,"FM5");
        static Arguments FM6Args= new Arguments(99,99,99,99,"FM6"); static Arguments FM7Args= new Arguments(99,99,99,99,"FM7");
        static Arguments FM8Args= new Arguments(99,99,99,99,"FM8"); 
        static int chiptype=0; // 0 = auto

        static Dictionary<string, Arguments> GetChannel = new Dictionary<string, Arguments>(); // ex. key/pair: "FM0" FM0channel
        static FMchannel FM0 = new FMchannel(); static FMchannel FM1 = new FMchannel(); static FMchannel FM2 = new FMchannel();
        static FMchannel FM3 = new FMchannel(); static FMchannel FM4 = new FMchannel(); static FMchannel FM5 = new FMchannel(); 
        static FMchannel FM6 = new FMchannel(); static FMchannel FM7 = new FMchannel(); static FMchannel FM8 = new FMchannel();
        public static string LostPatchLog=""; // collects all lost patches logged by ReportLostPatches / ReturnLostPatches

         public static int ProcessArgument(string arg1, string arg2, string arg3) { // returns number of indexes to skip
            if (GetChannel.TryGetValue(arg1, out Arguments? currentchannel) ) { // if dictionary key 'arg' exists, ref it to currentchannel
                tb("ProcessArgument: found FM arg, executing: "+arg1+" "+arg2+" "+arg3 ); // ex. FM0, DT, 0
                if (currentchannel.ParseArgument(arg2,arg3) ) return 2;                 // 3 index arg (per channel) - ex. FM0 DT 7
            } else { 
                tb("ProcessArgument: found global arg, executing: "+arg1+" "+arg2 ); // 2 index global arg - ex altwaveform FALSE
                if (GlobalArguments.ParseArgument(arg1,arg2) ) return 1;
            }
            return 0; // if Command.ParseValue returns false, it'll throw an error - Just continue to the next string and try again.
        }
        static void Main(string[] args) {
            debugstart(); // jump to a debug func for messing around
            #region PART 0/5 Check Valid VGM Input / Display Help ------------
            tb("VGM External Trigger Tool Ver "+VERSIONMAJOR+"."+VERSIONMINOR+"\nA VGM hacking tool for creating external trigger waveforms for oscilloscopes\nUsage: EXE [options] Infile.VGM");
            if (args.Length < 1 || "-H"==args[0] || "-h"==args[0] || "h"==args[0] || "/?"==args[0] || "-help"==args[0] ) { 
                string helptext=@"Help (-h or no arguments)
Supported chips are these Yamaha FM synths: OPM, OPN, OPNA, OPNB, OPN2, OPL, OPL2 
Available options (4operator FM): DT(def 0), ForceMult(def disabled)
Available options (2operator OPL2): ForceMult(def disabled)
Advanced options: Patch [PatchKey] - applies settings on a patch-by-patch basis
                  [PatchKey] syntax: (OP#1 mult)-(OP#2 mult)-(OP#3 mult)-(OP#4 mult) / (OP#1 DT)-(OP#2 DT)-(OP#3 DT)-(OP#4 DT)DT(desired DT)mult(desired Mult-optional)
                  Operator Multiplier values dilineated by '-', '/' separator, Operator Detune values dilineated by '-', DT(or e), 
                  Desired Detune or Detune algorithm, mult(or m) desired mult level (optional, mult will otherwise be chosen automatically) 
                  *Patch Key MUST be in quotes if there are blank spaces!*
                        Example: patch '12-15-1-3 / 3-4-3-2dt3' - this would use detune level '3' for this harpsichord patch
                        Example: patch '4-11-4-15 / 3-4-7-7dt5m1' - An inharmonic church bell patch is set to DT 5 and Mult 1 
                        Example: p '4-11-4-15/3-4-7-7e5m1' - Alternate syntax version of the above example

                    If this setting is in use, enables the 'Lost Patch Report' which will log all patch keys that aren't already specified
                    so using p '0-0-0-0/0-0-0-0e0' can give you an initial readout of all the patch keys in a VGM!
                    *At this time it is recommended to ONLY use Patch on a Global basis to prevent confusion*
                        

                        - - - SETTINGS FOR DETUNE (DT value) - - - 
  * 0 - No Detune
   0-7 - force a detune setting. 7-6-5-0-1-2-3 in order corresponds to -3 to +3 (4, if chosen, is the same as 0)
  * 8  - Use the DT of the lowest frequency operator bias 1>2>3>4 - If there are matches, use the DT of the earlier op (OP#1 > OP#2 > OP#3 > OP#4)
    9  - Use the DT of the lowest frequency operator bias 4>3>2>1 - If there are matches, use the DT of the later op (OP#4 > OP#3 > OP#2 > OP#1)
  * 10 - Use an average of all Detune values (*** NEW DEFAULT ***)
  * 11 - Use the DT of the MOST detuned operator bias 1>2>3>4 - If there are matches, use numerically *lowest* op (-3 and +3 match for these)
    12 - Use the DT of the MOST detuned operator bias 4>3>2>1 - If there are matches, use numerically *highest* op
    13 - Use the DT of the LEAST detuned operator bias 1>2>3>4 - If there are matches, use numerically *lowest*  op
    14 - Use the DT of the LEAST detuned operator bias 4>3>2>1 - If there are matches, use numerically *highest* op
    ...- any other value will be equivalent to 0
    21 - always use OP#1's associated DT
    22 - always use OP#2's associated DT
    23 - always use OP#3's associated DT
    24 - always use OP#4's associated DT
    * = recommended
    none of this applicable for OPL2, which has no operator detune settings.
                        - - - ADD MULT / FORCE MULT (forcemult/mult/addmult, auto by default)
    Multiplier is set automatically now (highest common denominator of all multipliers) but this can be useful to set
    higher if the patch length is very long, such as with some percussion patches with inharmonic multipliers
    Possible values: 0-15

Options may be set globally and/or per-channel, by preceding an option with a 'FM#' command (zero-bound)
Per-channel commands will always take precedence over global commands.
Example: invgm.VGM dt 0                               <- sets DT to 0 globally for all channels 
Example: invgm.VGM dt 0 fm0 dt 2 fm3 dt 11            <- does the above but sets dt to '0' for FM0, and '11' for FM3
Example: invgm.VGM dt 0 fm0 dt 2 fm3 dt 11 fm3 mult 1 <- + force fm3 to use multiplier 1
... or just drag & drop.";
                tb(helptext);
                Environment.Exit(0);
            }
            if (!File.Exists(args[args.Length-1]) ) {
                 tb("No file found @" +args[args.Length]); Console.ReadKey(); 
                 Environment.Exit(1);
            }
            byte[] data;
            string filename = args[args.Length-1].ToString();             
            data = File.ReadAllBytes(filename);
            if (data[0]!=0x56 && data[1]!=0x67 && data[2]!=0x6D) { // V G M 
                tb("Error: Invalid File \""+filename+"\" (VGM identifier not found) if VGZ, please extract first");      
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(2));
                Environment.Exit(1);
            }
            #endregion
            #region PART 1/5 Detect FM Chip, Setup Initial Data --------------
            int startVGMdata = (Get32BitInt(data,0x34)+0x34); // tb("DEBUG: VGM data start point: 0x"+Convert.ToString(startVGMdata,16) );
            int endVGMdata = (Get32BitInt(data,0x04)+0x04); // tb("DEBUG: VGM data end point: 0x"+Convert.ToString(endVGMdata,16) );
            if (Get32BitInt(data,0x10) > 0) {
                tb("Chip Detection: 0x10 "+Get32BitInt(data,0x10)+" YM2413 clock rate found but chip not supported!");
            } else if (Get32BitInt(data,0x30) > 0) {
                chiptype=54; tb("Chip Detection: 0x30 clockrate: "+Get32BitInt(data,0x30)+" YM2151 OPM found");
            } else if (Get32BitInt(data,0x44) > 0){
                chiptype=55; tb("Chip Detection: 0x44 clockrate: "+Get32BitInt(data,0x44)+" YM2203 OPN found"); 
            }  else if (Get32BitInt(data,0x48) > 0){
                chiptype=56; tb("Chip Detection: 0x48 clockrate: "+Get32BitInt(data,0x48)+" YM2608 OPNA found"); 
            }   else if (Get32BitInt(data,0x4C) > 0){
                chiptype=58; tb("Chip Detection: 0x4C clockrate: "+Get32BitInt(data,0x4C)+" YM2610 OPNB found"); 
            }  else if (Get32BitInt(data,0x50) > 0){
                chiptype=510; tb("Chip Detection: 0x50 clockrate: "+Get32BitInt(data,0x50)+" YM3812 OPL2 found"); 
            } else if (Get32BitInt(data,0x54) > 0){
                chiptype=510; tb("Chip Detection: 0x54 clockrate: "+Get32BitInt(data,0x54)+" YM3526 OPL found"); 
            } else if (Get32BitInt(data,0x58) > 0){
                chiptype=510; tb("Chip Detection: 0x58 clockrate: "+Get32BitInt(data,0x58)+" MSX-AUDIO Y8950 (OPL) found"); 
            } else if (Get32BitInt(data,0x2C) > 0) {
                chiptype=52; tb("Chip Detection: 0x2C clockrate: "+Get32BitInt(data,0x2C)+" YM2612 OPN2 found"); // check OPN2 last, as OPN2 DAC tends to be repurposed 
            }  
            
            SetupData(chiptype);  //* INITIAL COMMAND SETUP (Data.cs)
            #endregion
            #region PART 2/5 Parse Arguments ---------------------------------

            Arguments[] initchannelcommands = new Arguments[] {FM0Args, FM1Args, FM2Args, FM3Args, FM4Args, FM5Args, FM6Args, FM7Args, FM8Args};
            for (int i = 0; i < initchannelcommands.Length; i++) {GetChannel.Add(("FM"+i), initchannelcommands[i]); } //* initialize the dictionary

            for (int i = 0; i < args.Length - 2; i++){ // parse arguments
                i+=ProcessArgument(args[i].ToUpper(), args[i+1].ToUpper(), args[i+2].ToUpper() ); // process args one at a time (convert to upper case)
            }
            string s=""; // debug
            for (int i = 0; i < initchannelcommands.Length; i++){ // after argument data is fed in, match unset values with global values
                initchannelcommands[i].MatchAgainstGlobalValues(); 
                s+=initchannelcommands[i].Report();
            }  

            #endregion
            #region PART 3/5 Examine VGM Data, flagging commands for edit ----
            //* SCAN THROUGH DATA BYTE-BY-BYTE, FLAGGING FM COMMANDS THAT ARE SAFE TO EDIT
            //      this flags command bytes and wait bytes to make things a bit easier. It may also return timecodes if I add that
            bool[] WaitFlags = new bool[endVGMdata];
            bool[] byteflag = ExamineVGMData(data, FM0.chip, startVGMdata, endVGMdata, ref WaitFlags);

            #endregion
            #region PART 4/5 Main Loop, External Triggerify ------------------
            // PT 4 - Main Loop, External Triggerify

            //* pt1: Blanket edits across the board (for example removing all AR/DR/RS, channel feedback to 0, channel algorithms to 7)
            //// pt2: Find keyOn events and trace backwards to find patches, then edit them to our liking (mute operators, decide which detune value to use based on our settings, etc)
            //* Pt2 0.4: Parse through data, saving register indexes values as we go, if Detune / Mult changes are found search forward (in milliseconds) to find patches

            // Updates on a timer to speed things up
            ProgressTimer = new System.Timers.Timer(20); 
            ProgressTimer.AutoReset=true;
            ProgressTimer.Enabled=true;
            ProgressTimer.Elapsed += UpdateProgress;


            AutoTrigger(FM0, FM0Args, data, byteflag, WaitFlags, startVGMdata, endVGMdata);
            AutoTrigger(FM1, FM1Args, data, byteflag, WaitFlags, startVGMdata, endVGMdata);
            AutoTrigger(FM2, FM2Args, data, byteflag, WaitFlags, startVGMdata, endVGMdata);
            if (chiptype==52 || chiptype==54 || chiptype==56 || chiptype==58 || chiptype==510){  // 6 voices - OPN2 / OPM / OPNA / OPL2    
                AutoTrigger(FM3, FM3Args, data, byteflag, WaitFlags, startVGMdata, endVGMdata);
                AutoTrigger(FM4, FM4Args, data, byteflag, WaitFlags, startVGMdata, endVGMdata);
                AutoTrigger(FM5, FM5Args, data, byteflag, WaitFlags, startVGMdata, endVGMdata);
            }
            if (chiptype==54 || chiptype==510){     // 8 voices - OPM / OPL2
                AutoTrigger(FM6, FM6Args, data, byteflag, WaitFlags, startVGMdata, endVGMdata);
                AutoTrigger(FM7, FM7Args, data, byteflag, WaitFlags, startVGMdata, endVGMdata);
            }
            if (chiptype==510){                     // 9 voices - OPL2
                AutoTrigger(FM8, FM8Args, data, byteflag, WaitFlags, startVGMdata, endVGMdata);
            }

            tb("EXTT complete. Creating timecode & preparing patch report...");
            List<Arguments> arglist = new List<Arguments>() {FM0Args, FM1Args, FM2Args, FM3Args, FM4Args, FM5Args, FM6Args, FM7Args, FM8Args};
            int[] timecodes = new int[endVGMdata];
            timecodes=CreateTimeCode(timecodes, data, WaitFlags, startVGMdata, endVGMdata); // count samples, int value for every byte shows current 

            foreach (Arguments a in arglist) {
                if (a.LostPatches.Count() > 0) {
                    foreach (Dictionary<string,int> lp in a.LostPatches){
                        lp["TIMECODE"] = timecodes[Convert.ToInt32(lp["IDX"])];
                    }
                    LostPatchLog+=a.ReturnLostPatches(FM0.operators)+"\n";
                }
                // FM0Args.LostPatches[0];
            }

            ProgressTimer.Stop(); // stop timer
            tb("\n"+LostPatchLog);

            if (FM0.operators==2) {
                tb("OPL Patch Format: wave1-wave2-alg-vib1-vib2 / mult1-mult2 _ commands");
                tb("waveforms (OPL2 only): 0="+ReturnWaveTypeString(0)+" 1="+ReturnWaveTypeString(1)+" 2="+ReturnWaveTypeString(2)+" 3="+ReturnWaveTypeString(3) );
                tb("alg: aka 'Operator connection algorithm', 1=connected 0=disconnected (AM mode)");
                tb("vibrato: Auto always uses the carrier vibrato. If alg=0, best results depends on the patch");
                tb("\n");
            } else {
                tb("OPM/OPN Patch Format: mult1-mult2-mult3-mult4 / dt1-dt2-dt3-dt4 _alg & commands");
                tb("alg (algorithm): Narrows patch identification (do not change)");
                tb("DT (Detune): Auto will use the average all detune levels. Better tracking requires setting DT by hand, per patch (see -help).");
                tb(" tip for setting DT: when trigger is DT7, waveform will appear to swim left. DT3 swims right (left-to-right: 7-6-5-4/0-1-2-3)");
                tb("MULT (multiplier): Auto will choose the highest common denominator of all mults (this is best)");
                tb("\n");
            }

            #endregion
            #region PART 5/5 Name & Save Output VGM --------------------------
            // PT 5 - Save output VGM
            string outfile = ""; // add suffixes to filename...
            Arguments[] FMargs = new Arguments[] {FM0Args, FM1Args, FM2Args, FM3Args, FM4Args, FM5Args, FM6Args, FM7Args, FM8Args};
            foreach (Arguments FMx in FMargs){
                outfile+=FMx.AddToFileName();
            }

            outfile=filename+".extt"+GlobalArguments.AddGlobalValuesToFilename()+outfile+".vgm";
            tb("Writing "+outfile);

            if (File.Exists(outfile)) {
                File.Delete(outfile);
            }
            using (FileStream fs = File.Create(outfile)) {
                fs.Write(data, 0, data.Length);
            }                
            tb("Complete");
            Environment.Exit(0);
            // System.Console.ReadKey(); // pause
            #endregion
        }


        public class Arguments { // contains global & per channel settings to be fed into main loop
            public int detunesetting, forceop, forcemult, altwaveform;
            public string name;
            public List<Dictionary<string,int>> PatchKeys2 = new List<Dictionary<string,int>>();
            public bool LookForPatchKeys=false;

            public List<Dictionary<string,int>> LostPatches = new List<Dictionary<string,int>>();

            public List<int> LostPatchCnt = new List<int>();

            public bool ParseArgument(string property, string value) { // returns true if succesful
                if (value == "FALSE") value = "0";
                if (value == "TRUE") value = "1";
                if (value == "OFF") value = "99"; // forcemult 99 = off.. but don't do this
                bool ParsePatchKeys=false;
                switch (property){
                    case "P":ParsePatchKeys=true; break;    // go to next conditional to parse patch data
                    case "PATCH":ParsePatchKeys=true; break;
                    // case "NOPATCH":ParsePatchKeys=true; break; // * maybe rig this up to disable patch detection for certain channels? nah
                    // case "PATCH":ParsePatchKeys=true
                }

                if (!ParsePatchKeys) { 
                    // parser_out = new Dictionary<string,int>(); // if no patchkeys are set, return false
                    int intval;
                    if (Int32.TryParse(value, out intval) ) {
                        switch (property){
                            case "DT": this.detunesetting = intval; this.suffix+="DT"+detunesetting; break;
                            case "MULT": this.forcemult = intval; this.suffix+="Mult"+forcemult; break;
                            case "FORCEMULT": this.forcemult = intval; this.suffix+="Mult"+forcemult; break;
                            // case "FORCEOP": this.forceop = intval; this.suffix+="op"+forceop; break; // nonfunctional
                            // case "ADDMULT": this.forcemult = intval; this.suffix+="Mult"+forcemult; break; // nonfunctional
                            default: tb("PARSEVALUE: property "+property +"not found"); return false;
                        }
                        // tb ("value good?");
                        return true;
                    } else {
                        tb("PARSEVALUE ERROR: COULD NOT PARSE ARGUMENT: "+ value+" in argument:"+property+" "+value);
                        return false;
                    }

                } else {
                    /*
                        OPM / OPN patchkey input syntax                          vv desired dt alg, mult
                        mult1 - mult2 - mult3 - mult4 / dt1 - dt2 - dt3 - dt4 _ e1m1
                        4op example syntax: FM0 Parse 15-3-0-0/3-3-3-3_e9_m1
                        4op example syntax: FM0 Parse 15-3-0-0/3-3-3-3_dt9_mult1
                        
                        OPL / OPL2 patchkey input syntax
                    	Wave1 - Wave2 - Alg / Mult1 - Mult2 _ desired vibrato, desired mult
		                Sine-Sine-Connected1-V1-V2 / 2-2m2v1
                        Sine-Sine-connect0-vib0-vib1 / 2-2 v1 m1

                        OPL2 waves (last two bits) (int -> string via ReturnWaveTypeString)
                        0 Sine      1 Half Sine (Half-wave rectified)
                        2 ABS Sine (Full-wave rectified)     3 Quarter Sine

                        todo - OPM DT2?
                    */
                    string s=""; //* debug

                    // value = value.ToUpper(); // redundant when implimented
                    // handle synonyms
                    value = value.Replace("DT", "E"); 
                    value = value.Replace("MULT", "M");
                    value = value.Replace("VIBRATO", "V");
                    value = value.Replace("VIB", "V");
                    value = value.Replace("LFO", "V");
                    value = value.Replace("ALG", "A");
                    value = value.Replace("CONNECTED", "A"); // OPL2
                    value = value.Replace("CONNECT", "A");
                    value = value.Replace("C", "A");
                    value = value.Replace("SINE", "0");
                    value = value.Replace("HALFSINE", "1");
                    value = value.Replace("ABSSINE", "2");
                    value = value.Replace("RECTIFIEDSINE", "2");
                    value = value.Replace("QUARTERSINE", "3");
                    value = value.Replace("PULSESINE", "3");
                    s+="ParseValues2: input="+value+" "; //* debug

                    string[] StringSeparators, Segments;
                    if (FM0.operators==2) { // remove letters from required section. EG: "0-0-a0-v0-v1 / 2-3v1m1" -> 0-0-0-0-1 / 2-3_v1m1
                        StringSeparators = new string[] {"/"};
                        Segments = value.Split(StringSeparators, StringSplitOptions.RemoveEmptyEntries);
                        if (Segments.Length < 2) {PatchKey_Error(value + " (Segments.Length < 2)"); return false;} // if patch incomplete, throw error and exit
                        Segments[0] = Segments[0].Replace("V","");
                        Segments[0] = Segments[0].Replace("A","");
                        value = Segments[0]+"/"+Segments[1];
                        // tb(value);
                    }
                    // Console.ReadKey();

                    StringSeparators = new string[] {"/","_","DT","E","M","V","A"};
                    Segments = value.Split(StringSeparators, StringSplitOptions.RemoveEmptyEntries);
                    //  1         2       3 4    <segments
                    //  2-2-3-3 / 3-3-7-7e3m1    < src string
                    //    ^identifiers^   ^commands and also maybe alg identifier
                    // I say we keep 1 and 2, recombine the rest and parse through them character-by-character

                    string commands=value.Split(Segments[1],StringSplitOptions.RemoveEmptyEntries)[1];
                    if (Segments.Length < 2) {PatchKey_Error(value + " (Segments.Length < 2)"); return false;} // if patch incomplete, throw error and exit

                    StringSeparators = new string[] {"-"};
                    string[] values1 = Segments[0].Split(StringSeparators, StringSplitOptions.RemoveEmptyEntries);
                    string[] values2 = Segments[1].Split(StringSeparators, StringSplitOptions.RemoveEmptyEntries);

                    // PrintStringArray(values1); // debug
                    // PrintStringArray(values2);
                    // tb(commands);'

                    var parser_out = new Dictionary<string,int>();
                    if (FM0.operators == 4) {  // handle identifiers (required values)
                        if (values1.Length != 4) {PatchKey_Error(value + " (values1.Length != 4)"); return false;}
                        if (values2.Length != 4) {PatchKey_Error(value + " (values2.Length != 4)"); return false;}
                        parser_out[mult1] = Int32.Parse(values1[0]);
                        parser_out[mult2] = Int32.Parse(values1[1]);
                        parser_out[mult3] = Int32.Parse(values1[2]);
                        parser_out[mult4] = Int32.Parse(values1[3]);
                        parser_out[dt1] = Int32.Parse(values2[0]);
                        parser_out[dt2] = Int32.Parse(values2[1]);
                        parser_out[dt3] = Int32.Parse(values2[2]);
                        parser_out[dt4] = Int32.Parse(values2[3]);
                    } else {             // handle identifiers (required values) OPL
                        if (values1.Length != 5) {PatchKey_Error(value + " (values1.Length != 5)"); return false;}
                        if (values2.Length != 2) {PatchKey_Error(value + " (values2.Length != 2)"); return false;}
                        parser_out[wave1] = Int32.Parse(values1[0]); // sine-sine-1-v1-v2 / 2-3m2v1
                        parser_out[wave2] = Int32.Parse(values1[1]);
                        parser_out[alg] = Int32.Parse(values1[2]);
                        parser_out[vibrato1] = Int32.Parse(values1[3]);
                        parser_out[vibrato2] = Int32.Parse(values1[4]);
                        parser_out[mult1] = Int32.Parse(values2[0]);
                        parser_out[mult2] = Int32.Parse(values2[1]);
                    }

                    // set numeric values of 'commands' section to int array 'numerics'
                    // numeric values can be double digit which makes this block of code a headache
                    char[] cmds = commands.ToCharArray(); 
                    bool[] isnumber = new bool[cmds.Length];
                    int[] numerics = new int[cmds.Length];
                    for (int i = 1; i < cmds.Length; i++) {
                        if (Int32.TryParse(cmds[i].ToString(), out int x)) { // flag numbers
                            isnumber[i] = true;
                        }
                        if (isnumber[i] && isnumber[i-1]) {                 // handle double digit value
                            numerics[i-1] = Int32.Parse(cmds[i-1].ToString() + cmds[i].ToString() );
                        } else if (isnumber[i]) {
                            numerics[i] = Int32.Parse(cmds[i].ToString() ); // handle single digit value
                        }
                    }
                    // PrintStringArray(numerics);
                    for (int i = 0; i < cmds.Length; i++) { // finally, parse through commands and set values
                        switch(cmds[i].ToString()) {
                            case "A": parser_out[alg] = numerics[i+1]; break;
                            case "E": parser_out[desiredDTalg] = numerics[i+1]; break;
                            case "M": parser_out[desiredMult] = numerics[i+1]; break;
                            case "V": parser_out[desiredVibrato] = numerics[i+1]; break;
                        }
                    }

                    PatchKeys2.Add(parser_out); // copy parsed patchkey to PatchKeys list

                    s+="Output(dictionary form)= "+PrintPatch(parser_out, FM0.operators); //* debug
                    // foreach (string k in parser_out.Keys) {
                    //     s+=k+"="+parser_out[k]+" ";
                    // }
                    tb(s); //* debug text
                    this.LookForPatchKeys = true;
                    return true; // return true if succesful and dictionary as ref, much like int32.TryParse
                }
            }

            public bool CompareTwoPatchKeys(Dictionary<string,int> Key1, Dictionary<string,int> Key2) {
                // if (Key1.SequenceEqual(Key2)) return true; // old, this needs to be more robust if we want to add additional info, such as full patch data or timecodes
                // check for minimum Count? 
                foreach (KeyValuePair<string,int> kv in Key1) {
                    if (patchkey_keys.Contains(kv.Key)) { // patchkey_keys: only compare relevant patchkeys like mult, dt, desiredDTalg, etc - but not things like TL or timecode or whatever
                        if (Key2.TryGetValue(kv.Key, out int Key2Value) ) { // if Key2 has this key,
                            if (kv.Value != Key2Value) { return false;}     //  compare value. If key absent or value doesn't match, return false (no match)
                        } else { return false;}
                    }
                }
                return true;
            }

            public bool DataMatchesPatchKeys(Dictionary<string,int> InputData, ref Dictionary<string,int> OutputData) { // If match, returns OutputData & True (like TryParse)
            OutputData = new Dictionary<string,int>(); // this should come in from argument list
            string[] requiredkeys, optionalkeys;
            requiredkeys = new string[] {mult1, mult2, mult3, mult4, dt1, dt2, dt3, dt4};
            optionalkeys = new string[] {alg};
            if (FM0.operators==2) {
                requiredkeys = new string[] {wave1, wave2, mult1, mult2};
                optionalkeys = new string[] {alg, vibrato1, vibrato2};
            }

            string[] v = requiredkeys.Concat(optionalkeys).ToArray();   // check to make sure data is complete
            foreach (string k in v) {
                if (!InputData.ContainsKey(k)) {
                    tb("PatchKey: Warning! INPUT DATA incomplete @ 0x"+"something"+", missing "+k);
                    return false;
                }
            }

            foreach (Dictionary<string,int> patch in PatchKeys2) {
                bool match=true;
                foreach (string k in requiredkeys) {    // first, check data against required keys
                    if (!match) {break;}
                    if (InputData[k] != patch[k]) { 
                        match = false; break;
                    }
                }
                foreach (string ko in optionalkeys) { // next, check optional keys
                    if (!match) {break;}
                    if (patch.ContainsKey(ko)) {
                        if (InputData[ko] != patch[ko]) { 
                            match = false; break;
                        }
                    }
                } 
                if (match) {
                    if (patch.ContainsKey(desiredDTalg)) { OutputData[desiredDTalg] = Convert.ToByte(patch[desiredDTalg]); }
                    if (patch.ContainsKey(desiredMult)) { OutputData[desiredMult] = Convert.ToByte(patch[desiredMult]); }
                    if (FM0.operators==2 && patch.ContainsKey(desiredVibrato)) { OutputData[desiredVibrato] = Convert.ToByte(patch[desiredVibrato]); }
                    return true;
                } else {
                    // AddLostPatch(SanitizePatchKey(InputData) ); // strips out keys like DTML1 or KeyOn or whatever for easier compare
                    // AddLostPatch(InputData); // above is unnecessary in implementation
                    //* changing this to ALWAYS add lost patch
                }
            }
            // string s="";
            // tb(InputData.Count()+" <- InputData count. DATA = "+PrintPatch(InputData,FM0.operators));
            // s="";

            // foreach (string k in OutputData.Keys) {
            //     s+=k+": "+OutputData[k]+" ";
            // }
            // tb(OutputData.Count()+" <- output count. data/key match results in "+s);
            return false;
            }

            public void AddLostPatch(Dictionary<string,int> inputpatch) { // now all patches from data
                bool newpatch=true; int idx=0;
                foreach (Dictionary<string,int> existingpatch in LostPatches) {
                    if (CompareTwoPatchKeys(existingpatch, inputpatch)) {
                        LostPatchCnt[idx]++; // how many occurences of this patch in data ++
                        newpatch=false;
                        break;
                    }
                    idx++;
                }
                if (newpatch) {
                    LostPatches.Add(inputpatch);
                    LostPatchCnt.Add(1); // how many occurences of this patch in data
                }
            }

            public string ReturnLostPatches(int operators) {
                if (LostPatches.Count == 0){return "";}
                string s="";
                // s+=this.name+" ("+LostPatches.Count+" / "+LostPatchCnt.Count+")"+": ---- Lost patch report ---- ";
                s+=this.name+" ("+LostPatches.Count+" / "+LostPatchCnt.Count+")"+": --------  patch report ----- ";
                if (FM0.operators==4) s+= "dt setting:"+this.detunesetting;
                if (this.forcemult < 99) s+=" mult:"+this.forcemult;
                s+="\n";

                // 3-3-4-4 / 3-3-7-7 ALG4
                // SINE-SINE-ALG1-VIBRAT0-VIBRATO1 / 1-2
                for (int i = 0; i < LostPatches.Count; i++) {
                    s+=" "+SamplesToMinutes(LostPatches[i]["TIMECODE"])+" ";
                    s+="(Count:"+ LostPatchCnt[i]+") "+PrintPatch(LostPatches[i],FM0.operators);
                    if (FM0.operators==4) {
                        int outDT = LostPatches[i]["OUTDT"];
                        s+=" DT"+outDT;
                    } 
                    int OutMult = LostPatches[i]["OUTMULT"];
                    s+=" mult"+OutMult;
                    if (FM0.operators==2) {
                        int OutVibrato = LostPatches[i]["OUTVIBRATO"];
                        s+=" Vibrato"+OutVibrato;
                    }
                    if (LostPatches[i].ContainsKey("P") ) s+=" (patchkey)";
                    // s+="idx "+Convert.ToString(Convert.ToInt32(LostPatches[i]["IDX"]),16);
                    s+="\n";
                }
                return s;
            }


            public Arguments(int detunesetting, int forceop, int forcemult, int altwaveform, string name){
                this.detunesetting = detunesetting;
                this.forceop = forceop; // delete me?
                this.forcemult = forcemult;
                this.altwaveform = altwaveform; // delete me
                this.name=name;
            }


            public string AddGlobalValuesToFilename(){ //* only use for global values
                string s="";
                if (forceop > 0 && forceop < 4) s+= "Op"+forceop; // <5?
                if (forcemult < 16 && forcemult > -16) s+= "Mult"+forcemult;
                if (altwaveform > 0) s+= "AltWave"; 
                if (this.LookForPatchKeys) s+= "MultiPatch";
                // if (forceop > 0 || forceop < 4) s+= "Op"+forceop; // <5?
                if (FM0.operators==4){
                    return "DT"+this.detunesetting+s;
                } else {
                    return s; // no detune for OPL2
                }
            }
            string suffix="";
            public string AddToFileName(){ // to be added to file name
                if (this.suffix == ""){
                    return "";
                } else {
                    return "_"+this.name+this.suffix;
                }
            }
            public void MatchAgainstGlobalValues(){ // if values are not specified by arguments, set them to global values
                if (this.detunesetting > 98) this.detunesetting = GlobalArguments.detunesetting;
                if (this.forceop > 98) this.forceop = GlobalArguments.forceop;
                if (this.forcemult > 98) this.forcemult = GlobalArguments.forcemult;
                if (this.altwaveform > 98) this.altwaveform = GlobalArguments.altwaveform;
                if (!this.LookForPatchKeys && GlobalArguments.LookForPatchKeys){
                    // this.PatchKeys = GlobalArguments.PatchKeys.Cast<FMpatchkey>().ToList();
                    this.PatchKeys2 = GlobalArguments.PatchKeys2.Cast<Dictionary<string,int>>().ToList();
                    this.LookForPatchKeys=true;
                    tb("MatchAgainstGlobalValues: Casting FMpatchkey list (Count="+this.PatchKeys2.Count()+") to "+this.name);
                }
            }

            public string Report(){ //* just debug
                return this.name+" settings:"
                +" DT"+this.detunesetting
                +" forceMULT\n"+this.forcemult;
                // +" forceOP"+this.forceop
                // +" AltWave"+this.altwaveform+"\n";

            }
        }

        public static string showprogress="", lastprogress="";
        public static Timer? ProgressTimer; // why is this non nullable
        public static void UpdateProgress(Object source, System.Timers.ElapsedEventArgs e){
            // Console.WriteLine("Raised: {0}", e.SignalTime);
            if (showprogress != lastprogress){ // quick and dirty
                tb(showprogress);
            }
            lastprogress = showprogress;
        }

        public struct FMchannel // Channel specific registers, at least the relevant ones. set up in Data.cs
        {   // This started with less variables... should this be an object?
            public string name; // debug
            public byte chip; // VGM chip code: 52/53 for opn2, 54 OPM, 55 OPN, 56/57 for OPNA, 510 OPL2
            public byte op1_TL, op2_TL, op3_TL, op4_TL;
            public byte op1_DTML, op2_DTML, op3_DTML, op4_DTML;
            public byte op1_waveform, op2_waveform;
            public byte[] AR,DR,SR,RR; // no need to split these just lump them in an array
            public byte[] keyon;
            public byte ALG;
            public int operators;
            // public byte[] LFO;
            public FMchannel(int ops, byte chipcode) {
                operators = ops;
                this.chip = chipcode;
                op1_TL=0x00; op2_TL=0x00; op3_TL=0x00; op4_TL=0x00;
                op1_DTML=0x00; op2_DTML=0x00; op3_DTML=0x00; op4_DTML=0x00;
                op1_waveform=0x00; op2_waveform=0x00;
                AR = new byte[ops];
                DR = new byte[ops];
                SR = new byte[ops];
                RR = new byte[ops];
                // if (ops<4) {keyon = new byte[3];} else {keyon = new byte[2];}   // OPL keyon is 5 half-bytes
                keyon = new byte[3] {0,0,0};   // OPL keyon is 5 half-bytes, using two bytes then >20 for on <20 for off
                ALG = 0x00;
                name = "FM?";
                // LFO = new byte[2]; // one per OPL operator... -- shared with multiplier, disregard
            }
  
            public void SetAR(byte AR0, byte AR1, byte AR2, byte AR3){ this.AR[0]=AR0;this.AR[1]=AR1;this.AR[2]=AR2;this.AR[3]=AR3; }
            public void SetAR(byte AR0, byte AR1) { this.AR[0]=AR0;this.AR[1]=AR1; } // 2 op overloads

            public void SetDR(byte DR0, byte DR1, byte DR2, byte DR3){ this.DR[0]=DR0;this.DR[1]=DR1;this.DR[2]=DR2;this.DR[3]=DR3; }
            public void SetDR(byte DR0, byte DR1){ this.DR[0]=DR0;this.DR[1]=DR1; }
            public void SetRR(byte RR0, byte RR1, byte RR2, byte RR3){ this.RR[0]=RR0;this.RR[1]=RR1;this.RR[2]=RR2;this.RR[3]=RR3; }
            public void SetRR(byte RR0, byte RR1){ this.RR[0]=RR0;this.RR[1]=RR1; }

            public bool FoundKeyDownCommand(int operators, int idx, byte[] data, bool[] byteflag){ // to be used in main loop
                // string s=""; tb(cts(key0,16)+" "+cts(key2,16)+" "+cts(key1,16)); Console.ReadKey();
                if (!byteflag[idx]) return false;

                if (operators==2) { // 2-operator OPL2
                        // tb(s+"... FoundKeyDownCommand found operators==2 @ 0x"+Convert.ToString(idx));
                    // if (data[idx]==key0 && data[idx+1]==key1 && data[idx+2]>0x20) { // * is >0x20 reliable?
                    if (data[idx]==this.keyon[0] && data[idx+1]==this.keyon[1] && data[idx+2]>0x20) { // * is >0x20 reliable?
                        // tb("... FoundKeyDownCommand found operators==2 @ 0x"+Convert.ToString(idx));
                        return true;

                    } //else {tb(s);}
                } else { // 4-operators
                    if (data[idx]==this.keyon[0] && data[idx+1]==this.keyon[1] && data[idx+2]==this.keyon[2]) {
                    // if (data[idx]==key0 && data[idx+1]==key1 && data[idx+2]==key2){
                        return true;
                    }
                }
                return false;
            }

            public bool IsKeyDown(byte[] data, int idx){ // to be used in main loop (simpler version) v2
                if (operators==2) { // 2-operator OPL2
                    if (data[idx]==this.keyon[0] && data[idx+1]==this.keyon[1] && data[idx+2]>0x20) { // * is >0x20 reliable?
                        return true;
                    } //else {tb(s);}
                } else { // 4-operators
                    if (data[idx]==this.keyon[0] && data[idx+1]==this.keyon[1] && data[idx+2]==this.keyon[2]) {
                        return true;
                    }
                }
                return false;
            }
         // end FMchannel struct
        }

        public static bool IsVolCommand(byte d1, byte d2, byte d3, byte chip, byte cmd, int op, int idx) { // idx just for debug
            // if (!byteflag) {     // redundant - handled in main loop now 
            //     tb("IsVolCommand hit mask @ 0x"+Convert.ToString(idx,16); 
            //     return false;
            // }
            if (chip==0x5A && op > 2) return false;  // OPL2: skip > 2-operator
            if (d1==chip && d2==cmd) {          // if we have a potential match...
                if (chip==0x5A) return true;    // don't bother bit-shifting for OPL2's 2/6 bit key/TL...
                if (d3 > 0x7f) {               // 7f is the min volume. Anything higher is probably erroneous!
                    tb("IfIsCommand: skipping erroneous match @ 0x"+Convert.ToString(idx,16)+" "+cts(d1,16)+cts(d2,16)+cts(d3,16) );
                    Console.ReadKey(); // debug. but this should not trip anymore!!
                    return false; // OPM: This is catching 54 61 B9 - FM1.op1_TL is 54 61 XX, but 61 B9 XX is a wait command.
                } else {
                    return true;
                }
            } else {return false;}
        }

        static bool IsFMRegister(byte b, byte FMchip){ // ExamineVGMData helper
            switch (FMchip){
                case 0x52: if (b==0x52 || b==0x53) return true; break; // OPN2
                case 0x54: if (b==FMchip) return true; break;   // OPM
                case 0x55: if (b==FMchip) return true; break;   // OPN
                case 0x56: if (b==0x56 || b==0x57) return true; break; // OPNA
                case 0x58: if (b==0x58 || b==0x59) return true; break; // OPNB
                case 0x5A: if (b==FMchip) return true; break;   // OPL2
                case 0x5B: if (b==FMchip) return true; break;   // OPL (YM3526) 
                case 0x5C: if (b==FMchip) return true; break;   // OPL MSX-AUDIO (Y8950) 
                // case 0x5E: if (b==FMchip) return true; break;   // todo OPL3 

            }
            return false;
        }


        //* pt 2/4 cont. - go through byte-by-byte, flag bytes that are safe to edit (v04 also wait commands)
        static bool[] ExamineVGMData(byte[] data, byte FMchip, int start, int end, ref bool[] WaitFlags) {
            string detectedchipcodes="";
            bool[] byteflag = new bool[end];
            bool toif = false; int c=0;
            for (int i = 0; i < end;i++) {byteflag[i]=false;} // initialize all flags to false

            int[] chips = new int[256]; //* log first location of chip code
            for (int i = 0; i < chips.Length; i++) {chips[i]=0;};

            for (int i = start; i < end; i++){
                // if (i==0x2EBC) {tb("0x2EBC entering loop :"+data[i]);Console.ReadKey();}
                switch (data[i]){
                    //* skip (and log) additional chips
                    case 0x4F: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=1; break; // two-byte GameGear command (these show up on Genesis)
                    case 0x50: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=1; break; // two-byte SN76496 command (such as Genesis/MD PSG)
                    case 0xA0: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte AY8910 command (such as x1 turbo)
                    case 0xB0: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte RF5C68 command
                    case 0xB1: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte RF5C164 command
                    case 0xB5: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte MultiPCM command
                    case 0xB6: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte uPD7759 command
                    case 0xB7: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte OKIM6258 command
                    case 0xB8: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte OKIM6295 command
                    // case 0xC0: i+=3; if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; break; // four-byte Sega PCM command
                    case 0x52:  //* If OPM+OPN2 it's probably the Bally/Williams/Midway DAC -> OPN2 DAC trick or similar
                        if (FMchip==0x54){ 
                            if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte Additional OPN2 command
                        } else { toif=true; break;} // send OPM to next conditional

                    //* skip wait commands, samples & OPN2 DAC commands
                    case 0x61: WaitFlags[i]=true; i+=2; break; // three-byte wait
                    case 0x62: WaitFlags[i]=true; break;
                    case 0x63: WaitFlags[i]=true; break;
                    // case 0x66: i=end; tb("end reached @ 0x"+i); break; // end of sound data
                    case 0x66: i=end; break; // end of sound data
                    case 0x67: // data block: 0x67 0x66 tt ss ss ss ss (data)
                        i+=2; i+=Get32BitInt(data,i+1); break;  // maybe?
                    case byte n when (n >= 0x70 && n <= 0x7F): WaitFlags[i]=true; break; // more waits. oh neat c# can do this
                    case byte n when (n >= 0x80 && n <= 0x8F): WaitFlags[i]=true; break; // OPN2 sample write & wait
                    case 0xE0: i+=4; break; // OPN2 PCM pointer, followed by 32-bit value 
                    // case byte FMchip: break; // not possible to do this type of comparison in switch?
                    default: toif=true;break; //* all FMchip commands should go through to the next conditional
                }
                if (toif) { //* continuation of the switch above  
                    // if (data[i] == FMchip) { 
                    if (IsFMRegister(data[i], FMchip)) { // * for OPNA / OPNB / OPN2 which have two possible registers depend on channel
                        byteflag[i]=true; // byteflag[i+1]=true;byteflag[i+2]=true; //* mark only the first byte so we don't trip over the same data. Was having a problem with lines like 54-54-xx..
                        i+=2; // all FM chip commands are 3-byte values
                        c++; // count up all our commands
                    } else {
                        tb("ExamineVGMData: UNKNOWN COMMAND @0x"+(Convert.ToString(i,16))+": 0x"+Convert.ToString(data[i],16));
                        // Console.ReadKey();
                    }
                    toif=false;
                }
            }
            
            for (int i = 0; i < chips.Length; i++) {
                if (chips[i] > 0) {
                    switch (i){
                        case 0x4F: detectedchipcodes+="SN76496-GameGear ("+Convert.ToString(i,16)+") @ 0x"+Convert.ToString(chips[i],16)+"\n"; break;
                        case 0x50: detectedchipcodes+="SN76496 ("+Convert.ToString(i,16)+") @ 0x"+Convert.ToString(chips[i],16)+"\n"; break;
                        case 0xA0: detectedchipcodes+="AY8910 ("+Convert.ToString(i,16)+") @ 0x"+Convert.ToString(chips[i],16)+"\n"; break;
                        case 0xB0: detectedchipcodes+="RF5C68 ("+Convert.ToString(i,16)+") @ 0x"+Convert.ToString(chips[i],16)+"\n"; break;
                        case 0xB1: detectedchipcodes+="RF5C164 ("+Convert.ToString(i,16)+") @ 0x"+Convert.ToString(chips[i],16)+"\n"; break;
                        case 0xB5: detectedchipcodes+="MultiPCM ("+Convert.ToString(i,16)+") @ 0x"+Convert.ToString(chips[i],16)+"\n"; break;
                        case 0xB6: detectedchipcodes+="uPD7759 ("+Convert.ToString(i,16)+") @ 0x"+Convert.ToString(chips[i],16)+"\n"; break;
                        case 0xB7: detectedchipcodes+="OKIM6258 ("+Convert.ToString(i,16)+") @ 0x"+Convert.ToString(chips[i],16)+"\n"; break;
                        case 0xB8: detectedchipcodes+="OKIM6295 ("+Convert.ToString(i,16)+") @ 0x"+Convert.ToString(chips[i],16)+"\n"; break;
                        case 0x52: detectedchipcodes+="OPN2 repurposed for DAC ("+Convert.ToString(i,16)+") @ 0x"+Convert.ToString(chips[i],16)+"\n"; break;
                    }
                }
            }
            // double pcnt = (Convert.ToUInt64(c*3))/(Convert.ToUInt64(end-start));
            // decimal pcnt = Decimal.Divide((c*3),(end-start));//(Convert.ToUInt64(c*3))/(Convert.ToUInt64(end-start));
            tb("ExamineVGMData: scanned "+ (end-start)+" bytes, found "+c+" FM commands. Total bytes / command-related-bytes: %"+ Decimal.Divide((c*3),(end-start))*100 );
            tb("ExamineVGMData: Additional Chip Report vvvvv \n"+detectedchipcodes);
            // tb("Good so far. Press any key to continue"); Console.ReadKey();
            // tb("continuing...");
            // System.Threading.Thread.Sleep(TimeSpan.FromSeconds(1));
            return byteflag;
        }



        public class FMregisters {  // for the main loop, emulates what a FM channel's (relevant) commands will be at any specific point
            public int operators, chip, keyon_idx;
            /* a lot of data going on here, an example
            ex: 0x000102: 57 48 1F  CHANNEL: FM3 (implicit, this class encompasses 1 channel's specific register, no global commands)
                                    LABEL: TL1 (string - op1_TL = 0x40)
                                    REG: 48 (byte)
                                    VALUE:1F  (byte)
                                    IDX = 0x102 (int32)
                                    operators = 4 (int32) implicit from SetupData->FMchannel. This is an OPNA example.
                                    chip = 57 (int32) implicit from SetupData->FMchannel. OPNA/B/2 have two banks of registers - keyons are always in first bank
                                    KEYON: [0x56, 0x28, 0xF0] - implicit from SetupData->FMchannel. Always 0x56 on OPNA. May end up unused, we'll see.
                 more data may be added to get a fuller snapshot of the FM patches used
            */

            // public readonly byte[] keyon;
            private FMchannel FMref;
            private Dictionary<string, byte> LABEL_REG = new Dictionary<string, byte>(); 
            private Dictionary<byte, string> REG_LABEL = new Dictionary<byte, string>(); 
            private Dictionary<byte, int> REG_IDX = new Dictionary<byte, int>(); 
            private Dictionary<byte, byte> REG_VAL = new Dictionary<byte, byte>(); 
            // public Dictionary<string, FMregister> RG = new Dictionary<string, FMregister>(); 
            
            int lastidx=0; // for print
            public FMregisters(FMchannel fMchannel) {
                this.operators = fMchannel.operators;
                this.chip = fMchannel.chip;
                // DTML1=0xff; DTML2=0xff; DTML3=0xff; DTML4=0xff;
                FMref = fMchannel;
                 // TODO this'd be a whole lot smoother if the data was in dictionaries too
                this.LABEL_REG.Add("DTML1",fMchannel.op1_DTML); this.LABEL_REG.Add("DTML2",fMchannel.op2_DTML);
                if (fMchannel.operators > 2) { this.LABEL_REG.Add("DTML3",fMchannel.op3_DTML); this.LABEL_REG.Add("DTML4",fMchannel.op4_DTML); }

                this.LABEL_REG.Add("TL1",fMchannel.op1_TL); this.LABEL_REG.Add("TL2",fMchannel.op2_TL); 
                if (fMchannel.operators > 2) { this.LABEL_REG.Add("TL3",fMchannel.op3_TL); this.LABEL_REG.Add("TL4",fMchannel.op4_TL); }

                //? I'm disabling the keyons for now. It's incidental to the patch loading process.
                // this.LABEL_REG.Add("KEYON1", fMchannel.keyon[0]); // always first bank with OPNs (eg 0x56 OPNA)
                // this.LABEL_REG.Add("KEYON2", fMchannel.keyon[1]); // OPL keyon is channel specific, OPN is always 0x28, OPM is always 0x08.
                // if (fMchannel.operators==4) {this.LABEL_REG.Add("KEYON3", fMchannel.keyon[2]); } // not for OPL

                this.LABEL_REG.Add(alg,fMchannel.ALG);

                if (operators==2) {
                    this.LABEL_REG.Add("WAVEFORM1", fMchannel.op1_waveform); 
                    this.LABEL_REG.Add("WAVEFORM2", fMchannel.op2_waveform);
                }

                REG_LABEL = LABEL_REG.ToDictionary(x => x.Value, x => x.Key); // reverse keys/vals to create REG_LABEL out of LABEL_REG
                foreach(KeyValuePair<string, byte> LG in LABEL_REG) { // outputs LG.Key, LG.Value (register)
                    this.REG_VAL.Add(LG.Value, 0x00); // Register, init value 0x00
                    this.REG_IDX.Add(LG.Value, 0); // Register, init idx value 0
                }



            }

            public int ParseAndSetValue(byte[] data, int idx) {
                if (data[idx]==chip) { 
                    if (REG_VAL.ContainsKey(data[idx+1]) ) { 
                        REG_VAL[data[idx+1]] = data[idx+2];
                        REG_IDX[data[idx+1]] = idx;
                        if (data[idx+1] == LABEL_REG["DTML1"] || data[idx+1] == LABEL_REG["DTML2"]) {
                            lastidx=idx; return 1;
                        } else if (operators == 4) {
                            if (data[idx+1] == LABEL_REG["DTML3"] || data[idx+1] == LABEL_REG["DTML4"]) {
                                lastidx=idx; return 1;
                            }
                        }
                        //* I'm disabling the keyons for now. It's incidental to the patch loading process.
                        // if (data[idx]==LABEL_REG["KEYON1"]) { // ex 56. this is just chip / bank 1 of opna/b/etc
                        //     if (data[idx+1] == LABEL_REG["KEYON2"]) {
                        //         if (FM0.operators==2) {
                        //             REG_IDX[data[idx]] = idx; // KEYON1 idx
                        //             REG_IDX[data[idx+1]] = idx; // KEYON2 idx
                        //         } else if (data[idx+2] == LABEL_REG["KEYON3"]) {
                        //             REG_IDX[data[idx]] = idx; // KEYON1 idx
                        //             REG_IDX[data[idx+1]] = idx; // KEYON2 idx
                        //             REG_IDX[data[idx+2]] = idx; // KEYON3 idx (OPM / OPN) needs better implimentation, channel detection key on vs off etc
                        //         }
                        //     }
                        //     return 2; // does nothing atm
                        // }
                    }
                }
                return 0;
            }
            public int LABEL_IDX(string label) { // returns IDX, via REG_IDX & LABEL_REG dictionaries
                return REG_IDX[LABEL_REG[label]]; 
            }
            public byte LABEL_VAL(string label) {
                return REG_VAL[LABEL_REG[label]];
            }
            public string Triggerify(byte[] data, Arguments FMargs, int currentIDX) {
                string str="";
                // tb("hello?");
                //* if any DT (or DT idx) is empty then log & skip. Should only occur with early garbage data...
                // str=FMref.name+": !WARNING!: 0x"+Convert.ToString(lastidx,16)+": MISSING ";
                bool warn=false;
                // tb(REG_LABEL.Count()+" = count REG_LABEL");
                // tb(REG_VAL.Count()+" = count REG_VAL");
                // tb(REG_IDX.Count()+" = count REG_IDX");
                foreach(KeyValuePair<byte, int> RG in REG_IDX) { // outputs LG.Key, LG.Value (register)
                    // this.REG_VAL.Add(LG.Value, 0x00); // Register, init value 0x00
                    if (RG.Value==0) {
                        str+=REG_LABEL[RG.Key]+"=0 "; warn=true;
                        // return "";
                    } else {
                        str+=REG_LABEL[RG.Key]+"="+REG_VAL[RG.Key]+" ";
                    }
                }
                str+=" ... lag+"+(currentIDX - lastidx)+" bytes";
                // if (warn) {tb(str); System.Console.ReadKey(); return str;}
                if (warn) {tb(str); return str;}
                str="";


                // int[] current_values; 
                var datavalues = new Dictionary<string,int>();
                if (operators==2) { // OPL2 note - AM|PM|KSR|EG / MULT - No Detune with OPL2. Looks like 4 bit 0-F to me, but I've heard it skips some values (OPLL maybe?)
                    datavalues.Add(wave1,Second4BitToInt(LABEL_VAL("WAVEFORM1") ) ); // last 2 bits OPL2, last 3 bits OPL3. Nonexistent OPL1
                    datavalues.Add(wave2,Second4BitToInt(LABEL_VAL("WAVEFORM2") ) );
                    datavalues.Add(alg, Convert.ToInt32(LastBit(LABEL_VAL(alg) ) ) );
                    datavalues.Add(vibrato1,Convert.ToInt32(SecondBit(LABEL_VAL("DTML1") ) ) );
                    datavalues.Add(vibrato2,Convert.ToInt32(SecondBit(LABEL_VAL("DTML2") ) ) );
                    datavalues.Add(mult1, Second4BitToInt(LABEL_VAL("DTML1"))); 
                    datavalues.Add(mult2, Second4BitToInt(LABEL_VAL("DTML2"))); 

                    // tb(FMref.name+"@ 0x"+lastidx+" Connect/Vibrato=alg"+LastBit(LABEL_VAL(alg))+" vib"+ Convert.ToString(SecondBit(LABEL_VAL("DTML1")),16)+"-"+Convert.ToString(SecondBit(LABEL_VAL("DTML2")),16));

                } else {
                    datavalues.Add(dt1,First4BitToInt(LABEL_VAL("DTML1"))); datavalues.Add(mult1,Second4BitToInt(LABEL_VAL("DTML1")));
                    datavalues.Add(dt2,First4BitToInt(LABEL_VAL("DTML2"))); datavalues.Add(mult2,Second4BitToInt(LABEL_VAL("DTML2")));
                    datavalues.Add(dt3,First4BitToInt(LABEL_VAL("DTML3"))); datavalues.Add(mult3,Second4BitToInt(LABEL_VAL("DTML3")));
                    datavalues.Add(dt4,First4BitToInt(LABEL_VAL("DTML4"))); datavalues.Add(mult4,Second4BitToInt(LABEL_VAL("DTML4")));
                    //              xx - L/R (OPM only)
                    //        B0-B3 --xxx--- Feedback level for operator 1 (0-7)
                    //              -----xxx Operator connection algorithm (0-7)
                    datavalues.Add(alg, Convert.ToInt32(Last3Bit(LABEL_VAL(alg) ) ) );
                }
                int OutDT=99; int OutMult=99; int OutDTalg=99; int OutVibrato=99;//int OutCarrier=null;

                if (FMargs.LookForPatchKeys) { 
                    var keyvalues = new Dictionary<string,int>();
                    if (FMargs.DataMatchesPatchKeys(datavalues, ref keyvalues)) { // this will handle all loop stuff
                        keyvalues.TryGetValue(desiredDTalg, out OutDTalg);
                        keyvalues.TryGetValue(desiredMult, out OutMult);
                        keyvalues.TryGetValue(desiredVibrato, out OutVibrato);
                        datavalues["P"]=1; // for ReturnLostPatches: mark this patch as succesfully patchkey'ed
                        // tb("dtalg, mult= "+OutDTalg+" "+OutMult);
                    }
                } 

                // str += current_values[4]+"-"+current_values[5]+"-"+current_values[6]+"-"+current_values[7]+" / "; // debug, will break with OPL2, remove me
                // str += current_values[0]+"-"+current_values[1]; // opl2 debug
                // for (int i = 0; i < current_values.Length; i++) {
                //     str += current_values[i]+"-";
                // }

                if (this.operators == 4) {  //* handle DT
                    if (OutDTalg == 99) { OutDTalg=FMargs.detunesetting; } 
                    OutDT = ReturnDesiredDT(datavalues, OutDTalg); //* <--- 'big function' for all DT algorithms
                    data[LABEL_IDX("DTML4")+2] = FourToEightCoder(Convert.ToByte(OutDT) , Second4Bit(LABEL_VAL("DTML4")) );             //* WRITE DT (4-op only)
                    datavalues["OUTDT"] = OutDT; 
                }
                if (OutMult == 99) { // if mult is defined in patch key use it, otherwise automatically compensate    -- Handle Mult
                    if (this.operators==2) {
                        OutMult=HighestCommonFactorINT(new int[] {Second4BitToInt(LABEL_VAL("DTML1")), Second4BitToInt(LABEL_VAL("DTML2") )} );
                    } else {
                        OutMult=HighestCommonFactorINT(new int[] {Second4BitToInt(LABEL_VAL("DTML1")),Second4BitToInt(LABEL_VAL("DTML2")),Second4BitToInt(LABEL_VAL("DTML3")),Second4BitToInt(LABEL_VAL("DTML4") )} );
                    } 
                }
                datavalues["OUTMULT"] = OutMult;
                // if (!foundpatch){
                //     FMargs.AddLostPatch(current_values); // log unfound patches
                // }
                if (this.operators == 2) {
                    if (OutVibrato != 99) {
                        data[LABEL_IDX("DTML2")+2] = CodeSecondBit(data[LABEL_IDX("DTML2")+2], Convert.ToByte(OutVibrato));             //* WRITE CARRIER VIBRATO (OPL)
                        datavalues["OUTVIBRATO"] = OutVibrato;
                    } else {
                        datavalues["OUTVIBRATO"] = datavalues[vibrato2];
                    }
                    data[LABEL_IDX("DTML2")+2] = FourToEightCoder(First4Bit(LABEL_VAL("DTML2") ), Convert.ToByte(OutMult) );                //* WRITE MULT
                    data[LABEL_IDX("TL2")+2] = 12; // set volume OPL2 - first two bits are key scale, 0,1,2= 00, 01, 10. Rest is TL, a 6-bit value of 0-63 (3F = muted)
                } else {
                    data[LABEL_IDX("DTML4")+2] = FourToEightCoder(First4Bit(LABEL_VAL("DTML4") ), Convert.ToByte(OutMult) );                //* WRITE MULT
                    data[LABEL_IDX("TL4")+2] = 0x80; // set volume. This should be set globally really. 0x80 for debug
                }
                
                // str=FMref.name+": 0x"+Convert.ToString(lastidx,16)+" ... "+str+"--> mult"+Convert.ToByte(OutMult);
                // if (this.operators==4) str+=" DTout:"+OutDT;
                // tb(str);
                datavalues["IDX"] = lastidx; //* for calculating timecodes later
                FMargs.AddLostPatch(datavalues); // lost patch report is cool let's use it all the time
                return str;
            }

        }



        //! Main Loop
        //! find DTML values, look 10ms ahead for full patch values, then apply detune and mult 
        //! Pt B: Smash all DR/AR etc, change mute all operators except the last. Do this after so we get a better snapshot of the patches in use
        static void AutoTrigger(FMchannel FMin, Arguments FMargs, byte[] data, bool[] ByteFlags, bool[] WaitFlags, int StartVGMdata, int EndVGMdata) {
            
            FMregisters fMregisters = new FMregisters(FMin);

            //! main loop
            // do parsewaits bool array (timecodes shouldn't be necessary)
            // when ParseAndSetValue returns an operator DTML, 
            //     start tracking delay samples
            //      second conditional counts delays ahead, after threshold (~8ms?) THEN do triggerfy. this will even things out.
            ////      triggerify should use whatever the biggest index is for carrier? Unless it's OPL2, then always use 2 (just return 1 only on carrier 2?)
            //      ^^ nah this wont work for ML sweeps because TL values won't update with DT/ML, so old sections of music will get muted. unimplemented for now.
            //? when ParseAndSetValue returns an f-num command, do the same but modify f-num for more granular detune? (OPN / OPM - unimplimented) 
            //?     DT is applied directly to phase, so a formula is needed to match it accurately with f-num (unimplimented)

            int LagThreshold = 441; // after a DT value is found, look ahead this many samples before Triggerify (441 = 10ms)
            bool BeginDelay=false; // start delay bool, Compensates for driver+hardware delay before values take effect
            int Lag = 0; // in samples, via parsewaits

            for (int i = StartVGMdata; i < EndVGMdata; i++) {
                if (ByteFlags[i] && fMregisters.ParseAndSetValue(data, i) == 1) { // return 1 for DT, return 2 for KEYON?
                    if (!BeginDelay) {Lag = 0; BeginDelay=true; }
                }
                if (BeginDelay && WaitFlags[i]) {
                    Lag += ParseWaits(data,i,WaitFlags);
                    if (Lag >= LagThreshold) {
                        showprogress = fMregisters.Triggerify(data,FMargs,i); // hard edit happens here
                        Lag=0; BeginDelay=false;
                    }
                }
            }


            //* Pt.B: Global changes (smashing feedback, decay, muting operators, etc). Do this last so we get a more accurate snapshot of data
            byte outvolume = 0x0A; //hardcode carrier out level

            if (FMin.operators==2){ // *smash feedback & algorithm (alg 4?)
                FindAndReplaceSecond4Bit(FMin.chip, FMin.ALG, 0x00, data, ByteFlags, StartVGMdata, EndVGMdata); // "connect" / feedback. -- ?
                // FindAndReplaceByte(FMin.chip, FMin.ALG, 0x00, data, ByteFlags, StartVGMdata, EndVGMdata); // OPL2 FM 1 / Feedback 0   -- either should be fine?
            } else if (FMin.chip==0x54) {  
                FindAndReplaceByte(FMin.chip, FMin.ALG, 0xC4, data, ByteFlags, StartVGMdata, EndVGMdata);    // On OPM the first 2 bits are for stereo. 00 will mute!
                // FindAndReplaceByte(FMin.chip, FMin.ALG, 0xC7, data, ByteFlags, StartVGMdata, EndVGMdata);    // On OPM the first 2 bits are for stereo. 00 will mute!
            } else {                                                                    
                FindAndReplaceByte(FMin.chip, FMin.ALG, 0x04, data, ByteFlags, StartVGMdata, EndVGMdata); // Feedback/alg to 0/4
                // FindAndReplaceByte(FMin.chip, FMin.ALG, 0x07, data, ByteFlags, StartVGMdata, EndVGMdata); // Feedback/alg to 0/7
            }

            foreach (byte b in FMin.AR){ //* smash Attack, Decay, Release
                if (FMin.operators == 2) {
                    FindAndReplaceByte(FMin.chip, b, 0xF0, data, ByteFlags, StartVGMdata, EndVGMdata);   // OPL2: Attack F / Decay 0
                } else {
                    FindAndReplaceByte(FMin.chip, b, 0x1F, data, ByteFlags, StartVGMdata, EndVGMdata);   // smash Attack to 1F
                }
            }
            foreach (byte b in FMin.DR){
                    FindAndReplaceByte(FMin.chip, b, 0x00, data, ByteFlags, StartVGMdata, EndVGMdata);   // smash Decay - if <= threshold 1f (F is max)
            }
            if (FMin.operators==4){ // SR, but not for OPL
                if (FMin.chip == 54) { // OPM
                    foreach (byte b in FMin.SR){ // OPM SR might be a 2/6 split?
                        FindAndReplaceSecond6Bit(FMin.chip, b, 0x00, data, ByteFlags, StartVGMdata, EndVGMdata);
                    }
                } else { // OPNs
                    foreach (byte b in FMin.SR){ // full byte for OPNs
                        FindAndReplaceByte(FMin.chip, b, 0x00, data, ByteFlags, StartVGMdata, EndVGMdata);
                    }
                }
            }
            foreach (byte b in FMin.RR){
                    // FindAndReplaceByte(FMin.chip, b, 0x00, data, ByteFlags, StartVGMdata, EndVGMdata, 0x1f);   // smash Release
                    FindAndReplaceByte(FMin.chip, b, 0x00, data, ByteFlags, StartVGMdata, EndVGMdata);   // smash Release
            }

            if (FMin.operators==2) { //* smash TL and some other values
                FindAndReplaceSecond4Bit(FMin.chip, FMin.op1_waveform, 0x00, data, ByteFlags, StartVGMdata, EndVGMdata); // waveform op1 to sine
                FindAndReplaceSecond4Bit(FMin.chip, FMin.op2_waveform, 0x00, data, ByteFlags, StartVGMdata, EndVGMdata); // waveform op2 to sine
                // mute modulator (3f = 64 = muted)
                FindAndReplaceSecond6Bit(FMin.chip, FMin.op1_TL, 0x3F, data, ByteFlags, StartVGMdata, EndVGMdata); // Key Scaling(2bit)/TL(6bit).
                FindAndReplaceSecond6Bit(FMin.chip, FMin.op2_TL, 0x00, data, ByteFlags, StartVGMdata, EndVGMdata); // Key Scaling(2bit)/TL(6bit).

                // FindAndkillFirstTwoBits(FMin.chip, FMin.op1_DTML, 0b00000000, data, ByteFlags, StartVGMdata, EndVGMdata); // binary input does nothing set AM and Vibrato to 0 (?)

            } else if (FMin.operators==4) {
                FindAndReplaceByte(FMin.chip, FMin.op1_TL, 0x7f, data, ByteFlags, StartVGMdata, EndVGMdata); // mute TL
                FindAndReplaceByte(FMin.chip, FMin.op2_TL, 0x7f, data, ByteFlags, StartVGMdata, EndVGMdata); // mute TL
                FindAndReplaceByte(FMin.chip, FMin.op3_TL, 0x7f, data, ByteFlags, StartVGMdata, EndVGMdata); // mute TL
                FindAndReplaceByte(FMin.chip, FMin.op4_TL, outvolume, data, ByteFlags, StartVGMdata, EndVGMdata); // unmute carrier 4
            }



            // tb(showprogress);
            // if (FMargs.LookForPatchKeys){
            // LostPatchLog+=FMargs.ReturnLostPatches(FM0.operators)+"\n";
            // }

        }


        static int ParseWaits(byte[] data, int idx, bool[] WaitFlags){ // just return delay of current byte, in samples
            if (WaitFlags[idx]){ // 'wait' commands should be flagged ahead of time by this bool array
                switch (data[idx]){
                    case 0x61: return BitConverter.ToInt16(data, idx+1);   // three-byte wait
                    case 0x62: return 735; // wait 735 samples (60th of a second)
                    case 0x63: return 882; // wait 882 samples (50th of a second)
                    case byte n when (n >= 0x70 && n <= 0x7F): // 1-byte waits
                        return Second4BitToInt(data[idx])+1; // 1-16
                    case byte n when (n >= 0x80 && n <= 0x8F):  // OPN2 sample write & wait
                        return Second4BitToInt(data[idx]); // 0-15
                }
            }
            return 0;
        }
        static int SamplesToMS(int samples) {
            return Convert.ToInt32(Math.Round(samples / 44.1));
        }

        
        static int ReturnDesiredDT(Dictionary<string,int> values, int DesiredDTalg) {
            var LUT = new Dictionary<int, int>(); //DT lut
            LUT[0] = 0; LUT[1] = 1; LUT[2] = 2; LUT[3] = 3; 
            LUT[4] = 0; LUT[5] = -1; LUT[6] = -2; LUT[7] = -3;
             // Wrap-around DTs... maybe. used in Williams drivers for some reason
            LUT[8] = 0;  LUT[9] = 1; LUT[10] = 2; LUT[11] = 3; 
            LUT[12] = 0; LUT[13] = -1; LUT[14] = -2; LUT[15] = -3;
            // var LUTreverse = LUT.ToDictionary(x => x.Value, x => x.Key); // for returning an encoded value // ah this won't work because of duplicate keys
            var LUTreverse = new Dictionary<int,int>();
            LUTreverse[-3]=7; LUTreverse[-2]=6; LUTreverse[-1]=5; LUTreverse[0]=0;  LUTreverse[1]=1;  LUTreverse[2]=2;  LUTreverse[3]=3;  

            // DTML is 8 byte values - DTs first, then MLs
            // values = values.Cast<int>.ToInt32();
            int[] DTs = new int[] {Convert.ToInt32(values[dt1]), Convert.ToInt32(values[dt2]), Convert.ToInt32(values[dt3]), Convert.ToInt32(values[dt4])};
            int[] MLs = new int[] {Convert.ToInt32(values[mult1]), Convert.ToInt32(values[mult2]), Convert.ToInt32(values[mult3]), Convert.ToInt32(values[mult4])};
            // int[] DTs = new int[] {DTML[0], DTML[1], DTML[2],DTML[3]};
            // int[] MLs = new int[] {DTML[4], DTML[5], DTML[6],DTML[7]};
            
            int idx, outDT=0;
            switch (DesiredDTalg) {
                case int n when (n < 8): outDT = Convert.ToInt32(DesiredDTalg); break; // 0-7 return DesiredDTalg
                case 8: // lowest mult, early
                    idx = Array.IndexOf(MLs, MLs.Min() );
                    outDT = DTs[idx]; break;
                case 9: // lowest mult, late
                    idx = Array.IndexOf(MLs, MLs.Min() );
                    outDT = DTs[Array.LastIndexOf(MLs,MLs[idx])]; break;
                case 10: // was "Lowest mult, favor operators# 4 > 2 > 3 > 1", now just average the DTs
                    outDT = ((LUT[DTs[0]]+LUT[DTs[1]]+LUT[DTs[2]]+LUT[DTs[3]]) / 4 ); 
                    outDT = LUTreverse[outDT];
                    break;
                // case 15: // Highest mult, early
                // case 16: // Highest mult, late   these are pretty useless!
                case 21: outDT=DTs[0]; break; // Op#1
                case 22: outDT=DTs[1]; break; // Op#2
                case 23: outDT=DTs[2]; break; // Op#3
                case 24: outDT=DTs[3]; break; // Op#4
                default: // 11-14 Most/Least DT 
                    int[] DT_decoded = new int[] {LUT[DTs[0]]+LUT[DTs[1]]+LUT[DTs[2]]+LUT[DTs[3]]};
                    switch(DesiredDTalg) {
                        case 11: outDT = DTs[Array.IndexOf(DT_decoded, DT_decoded.Max() )]; break;    // Most Detune, early
                        case 12: outDT = DTs[Array.LastIndexOf(DT_decoded, DT_decoded.Max() )]; break; // Most Detune, late
                        case 13: outDT = DTs[Array.IndexOf(DT_decoded, DT_decoded.Min() )]; break;    // Least Detune, early
                        case 14: outDT = DTs[Array.LastIndexOf(DT_decoded, DT_decoded.Min() )]; break; // Least Detune, late
                    }
                    break;
            }
            // string str=DTML[4]+"-"+DTML[5]+"-"+DTML[6]+"-"+DTML[7]+" / "+DTML[0]+"-"+DTML[1]+"-"+DTML[2]+"-"+DTML[3];
            // str="ReturnDesiredDT: "+str+" DT"+DesiredDTalg+" = "+outDT;
            // if (str != debugDesiredDT) {
            //     tb(str);
            // }
            // debugDesiredDT = str;
            return outDT;
        }
        // static string debugDesiredDT="";



        static byte Second4BitMinusMult(byte mult, byte subtractme){ // handle addmult (forcemult <0)
            // byte mult2 = Convert.ToInt16();
            if (subtractme == 0x00) return Second4Bit(mult);
            mult = Second4Bit(mult);
            // int mult = amult;
            // short subtractme = asubtractme;
            if (mult == subtractme) { // doing subtract and ending up with 0 raises error so..
                return 0x00;
            } else if (mult > subtractme){      // return subtracted value
                // string s="";
                // s="in:"+mult2;
                // mult2-=subtractme; 
                // s+=" out:"+mult2;
                // tb("in:"+mult+"-"+subtractme+ " out:"+(byte)(mult - subtractme) );
                // if (mult == 0x02) {tb("!!!!!in:"+mult+"-"+subtractme+ " out:"+(byte)(mult - subtractme) ); }
                // if (mult2 != 0x00) {tb("in:"+Convert.ToString(mult2)+" out:"+Convert.ToString(Convert.ToByte(mult2-subtractme)) );}
                // return Convert.ToByte(mult2-subtractme);
                return (byte)(mult - subtractme);
            } else {
                return Convert.ToByte(mult); // if mult value + add is less than 0, just return second 4 bit
            }
        }

        static byte KillFirstTwoBits(byte b) { return (byte)(b & 0b00111111);}
        static byte First4Bit(byte b){return (byte)(b >> 4); }
        // static int First4Bit_Int(byte b){return (int)(b >> 4); }
        static byte Second4Bit(byte b){return (byte)(b & 0x0F); }
        static byte First5Bit(byte b){return (byte)(b >> 3); }
        static byte Last3Bit(byte b){return (byte)(b & 0x07); }
        static byte FiveToEightCoder(byte value1, byte value2) { //? untested
            byte returnValue = 0;
            returnValue += value1;              //Write value1;
            returnValue *= 32;              //move it 5 bit left
            returnValue += value2;              //write value2
            return returnValue;
        }
        static int First4BitToInt(byte b){return Convert.ToInt32((byte)(b >> 4) ); } // could overload but probably better to just remember
        static int Second4BitToInt(byte b){return Convert.ToInt32((byte)(b & 0x0F) ); }
        static byte FourToEightCoder(byte value1, byte value2){             //Check if both values are below 16
            byte returnValue = 0;                           //If not, throw an argument exception
            returnValue += value1;              //Write value1;
            returnValue *= 16;              //move it 4 bit left
            returnValue += value2;              //write value2
            return returnValue;
        }
        static byte First2Bit(byte b){return (byte)(b >> 6); }
        static byte Second6Bit(byte b){return (byte)(b & 0x3F);}
        static byte TwoToSixCoder(byte value1, byte value2){  
            byte returnValue = 0;   
            returnValue += value1;              //Write value1;
            returnValue *= 64;              //move it 6 bit left
            returnValue += value2;              //write value2
            return returnValue;
        }
        static string GetBinary(byte b){ return Convert.ToString(b,2).PadLeft(8, '0');} // for debug
        static int Get32BitInt(byte[] d, byte i){ return BitConverter.ToInt32(d,i);}
        static int Get32BitInt(byte[] d, int i){ return BitConverter.ToInt32(d,i);}

        static byte SecondBit(byte b){
            b = (byte)(b << 1); // erase first bit
            b = (byte)(b >> 7); // move to first // THIS MIGHT FLOOD THE BYTE WITH ONES...
            return b;  
        }
        static byte CodeSecondBit(byte byt, byte b2){
            if (b2>=0x01){
                return (byte) (byt | 0b01000000); // 0x40 = 0b01000000 

            } else {
                return (byte) (byt & ~0b01000000); // ~ inverts byte mask
            }
        }
        static byte LastBit(byte b){
            b = (byte)(b << 7); // erase first 7 bits
            b = (byte)(b >> 7); // move to first
            return b;  
        }

        public static void PrintStringArray(string[] strA) {
            string s="";
            for (int i = 0; i < strA.Length; i++) {
                s+=i+":"+strA[i]+" ";
            }
            tb("PSA: "+s+ " (L="+strA.Length+")");
        }
        public static void PrintStringArray(int[] strA) {
            string s="";
            for (int i = 0; i < strA.Length; i++) {
                s+=i+":"+strA[i]+" ";
            }
            tb("PSA: "+s+ " (L="+strA.Length+")");
        }




        static byte ReplaceFirstHalfByte(byte xa, byte dt){  // for setting DT. in:byte, int DT value (0-7)
            return FourToEightCoder(dt, Second4Bit(xa)); // DT|ML
        }


        
        // (FMx.chip, FMx.op1_DTML, 0x00, data, byteflag, startVGMdata, endVGMdata
        static void FindAndReplaceSecondBit(byte xa, byte xb, byte insertbit, byte[] data, bool[] byteflag, int startVGMdata, int endVGMdata){
            // string s=""; // debug / printing
            for (int i = startVGMdata; i < endVGMdata; i++){
                if (byteflag[i] && data[i]==xa && data[i+1]==xb) {
                    // s+="FindAndReplaceSecondBit: "+GetBinary(data[i+2])+" ->\n";
                    data[i+2] = CodeSecondBit(data[i+2], insertbit);
                    
                    // s+="FindAndReplaceSecondBit: "+GetBinary(data[i+2])+"\n";
                }
            }
            // tb(s); Console.ReadKey();
        }
        static void FindAndkillFirstTwoBits(byte xa, byte xb, byte byt, byte[] data, bool[] byteflag, int startVGMdata, int endVGMdata){
            //string s="FindAndReplaceFirstTwoBits:0b"+GetBinary(byt)+"\n"; // debug / printing
            for (int i = startVGMdata; i < endVGMdata; i++){
                    // if (i == 0x17832){
                    //     tb("debug "+byteflag[i]+", "+data[i]+" "+data[i+1]+" "+data[i+2]);//Console.ReadKey();
                    // }
                if (byteflag[i] && data[i]==xa && data[i+1]==xb) {
                    // s+="FindAndReplaceFirstTwoBits: "+GetBinary(data[i+2])+" ->\n";
                    byt = (byte) (data[i+2] & 0b00111111);  // this might be broken at the moment 
                    data[i+2] = byt; // mask out everything but first two, then add

                    // if (i == 0x17832){
                    //     tb("debug "+byteflag[i]+", "+data[i]+" "+data[i+1]+" "+data[i+2]);Console.ReadKey();
                    //     return true;
                    // }


                    // data[i+2] = CodeSecondBit(data[i+2], insertbit);
                    // if (b2>=0x01){
                    //     data[i+2] = (byte) (byt | data[i+2]); // 0x40 = 0b01000000 
                    // } else {
                    //     data[i+2] = (byte) (byt & ~data[i+2]); // ~ inverts byte mask
                    // }
                    // s+="FindAndReplaceFirstTwoBits: "+GetBinary(data[i+2])+"\n";
                    //  tb(s); Console.ReadKey();
                }
            }
            // tb(s);// Console.ReadKey();
            // return false;
        }

        static void FindAndReplaceSecond4Bit(byte xa, byte xb, byte xc, byte[] data, bool[] byteflag, int startVGMdata, int endVGMdata){  // 
            int i; //string s=""; int c=0;
            for (i=startVGMdata; i<endVGMdata; i++){
                if (byteflag[i]){   // if start of 3-byte command
                    if (data[i]==xa && data[i+1]==xb){
                        // c+=1; // just count
                        // s+=("FindAndReplaceSecond4Bit: matched 0x" + Convert.ToString(i,16)+" ("+Convert.ToString(data[i],16)+"|"+
                        // Convert.ToString(data[i+1],16)+"|"+ Convert.ToString(data[i+2],16)+") -> " );

                        data[i+2] = FourToEightCoder(First4Bit(data[i+2]),xc);

                        // s+=(Convert.ToString(data[i+2],16)+"\n" );
                        // i+=2; // move on to prevent potential false matches with replaced data...
                    }
                }
            }
            // tb(s+"\nMuting*"+Convert.ToString(xa,16)+"|"+Convert.ToString(xb,16)+" completed after "+ c + "matches!");
            // c=0; // should be unnecessary
                        // System.Console.ReadKey(); // debug step-through
            return;   // will just return last value

        }
        static void FindAndReplaceSecond6Bit(byte xa, byte xb, byte xc, byte[] data, bool[] byteflag, int startVGMdata, int endVGMdata){  // PLEASE only feed in 6 bit values for xb
            int i; int c=0; //string s="";
            for (i=startVGMdata; i < endVGMdata; i++) {
                if (byteflag[i]){   // if start of 3-byte command
                    if (data[i]==xa && data[i+1]==xb){
                        c+=1; // just count
                        // s+="FindAndReplaceSecond4Bit: matched 0x" + Convert.ToString(i,16)+" ("+Convert.ToString(data[i],16)+"|"+
                        // Convert.ToString(data[i+1],16)+"|"+ Convert.ToString(data[i+2],16)+") -> " ;

                        data[i+2] = TwoToSixCoder(First2Bit(data[i+2]),xc);

                        // s+=(Convert.ToString(data[i+2],16)+"\n" );
                        // i+=2; // move on to prevent potential false matches with replaced data...
                    }
                }
            }
            // tb(s+"Muting*"+Convert.ToString(xa,16)+"|"+Convert.ToString(xb,16)+" completed after "+ c + "matches!");
            c=0; // should be unnecessary
                        // System.Console.ReadKey(); // debug step-through
            return;   // will just return last value

        }

        static void FindAndReplaceByte(byte xa, byte xb, byte xc, byte[] data, bool[] byteflag, int startVGMdata, int endVGMdata){ 
            int i; int c=0; //string s="";// c is not used for anything
            for (i=startVGMdata; i < endVGMdata; i++) {
                if (byteflag[i]){   // if start of 3-byte command
                    if (data[i]==xa && data[i+1]==xb){
                        c+=1;
                        // s+="ReplaceByte: matched 0x" + Convert.ToString(i,16)+" ("+Convert.ToString(data[i],16)+"|"+
                        // Convert.ToString(data[i+1],16)+"|"+ Convert.ToString(data[i+2],16)+") -> " ;
                        data[i+2] = xc;
                        // s+=Convert.ToString(data[i+2],16)+"\n";
                        // i+=2; // move on to prevent potential false matches with replaced data...
                    }
                }
            }
            // tb(s+"Muting*"+Convert.ToString(xa,16)+"|"+Convert.ToString(xb,16)+" completed after "+ c + "matches!");
            c=0; // should be unnecessary
            return;   // will just return last value

        }
        static void FindAndReplaceByte(byte xa, byte xb, byte xc, byte[] data, bool[] byteflag, int startVGMdata, int endVGMdata, byte upperthreshold){  // 
            int i; //int c=0; string s="";
            for (i=startVGMdata; i < endVGMdata; i++) {
                if (data[i]==xa && data[i+1]==xb){
                    if (byteflag[i]){   // if start of 3-byte command
                        //c+=1;
                        if (data[i+2] >= upperthreshold) {
                            tb("FindAndReplaceByte @"+cts(xa,16)+" "+cts(xb,16)+": upperthreshold reached :"+Convert.ToString(data[i+2],16)+">"+cts(upperthreshold,16)+
                            "***************** 0x"+ Convert.ToString(i,16));
                        } else {
                            // s+="ReplaceByte: matched 0x" + Convert.ToString(i,16)+" ("+Convert.ToString(data[i],16)+"|"+
                            // Convert.ToString(data[i+1],16)+"|"+ Convert.ToString(data[i+2],16)+") -> " ;
                            data[i+2] = xc;
                            // s+=Convert.ToString(data[i+2],16)+"\n";

                            // i+=2; // move on to prevent potential false matches with replaced data...
                        }
                    }
                }
            }
            // tb("Muting*"+Convert.ToString(xa,16)+"|"+Convert.ToString(xb,16)+" completed after "+ c + "matches!");
            //c=0; // should be unnecessary
            return;   // will just return last value

        }        


        static byte HighestCommonFactor(int[] numbers)
        {
            return Convert.ToByte(numbers.Aggregate(GCD));
            // return Convert.ToByte(numbers.Aggregate(GCD));
        }
        static int HighestCommonFactorINT(int[] numbers)
        {   
            if (numbers.Min() == 0) {return 0;} // math doesn't work with 0, but if there's a 0, just return 0
            return numbers.Aggregate(GCD);
        }

        static int GCD(int a, int b)
        {
            return b == 0 ? a : GCD(b, a % b);
        }

        static void debugstart() {
            int debug = 0;
            if (debug < 1) return;


        }
        static void PatchKey_Error(string arg) {
            tb("Invalid patchkey "+arg+"  continuing...");
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(3));
        }

        static string PrintPatch(Dictionary<string,int> patch, int operators) { // can take DATA or PATCHKEY
            string s="";
            // if (FM0.operators==4)  // todo REMOVE OPERATORS ARG
            if (operators==4) {
                // 4op: mults / detune   commands - alg, desiredDTalg, desiredMult
                s+=patch[mult1]+"-"+patch[mult2]+"-"+patch[mult3]+"-"+patch[mult4]+" / "+patch[dt1]+"-"+patch[dt2]+"-"+patch[dt3]+"-"+patch[dt4];
                if (patch.ContainsKey(alg)) s+=" alg"+patch[alg]+" ";
                if (patch.ContainsKey(desiredDTalg)) s+=" DT"+patch[desiredDTalg]+" ";
                if (patch.ContainsKey(desiredMult)) s+=" MULT"+patch[desiredMult]+" ";
            } else {
                // 2op: wave1 - wave2 - alg - vibrato1 - vibrato2 / mult1 - mult2    commands - desiredMult, desiredVibrato
                // s+=ReturnWaveTypeString(patch[wave1])+"-"+ReturnWaveTypeString(patch[wave2])+"-alg"+patch[alg]+"-vib"+patch[vibrato1]+"-vib"+patch[vibrato2]+" / "+patch[mult1]+"- "+patch[mult2]+" ";
                s+=patch[wave1]+"-"+patch[wave2]+"-a"+patch[alg]+"-v"+patch[vibrato1]+"-v"+patch[vibrato2]+" / "+patch[mult1]+"-"+patch[mult2];
                if (patch.ContainsKey(desiredMult)) s+=" MULT"+patch[desiredMult]+" ";
                if (patch.ContainsKey(desiredVibrato)) s+=" VIB"+patch[desiredVibrato]+" ";
            }

            return s;
        }

        static Dictionary<string,int> SanitizePatchKey(Dictionary<string,int> input) { // strip out irrelevant data for data -> patchkey process. Not used.
            string[] desiredkeys;
            if (FM0.operators==4) {
                desiredkeys = new string[] {mult1, mult2, mult3, mult4, dt1, dt2, dt3, dt4, alg};
            } else {
                desiredkeys = new string[] {wave1, wave2, alg, vibrato1, vibrato2, mult1, mult2};
            }
            foreach (string k in input.Keys) {
                if (!desiredkeys.Contains(k)) { // examples of data guff would be like KEYON1, TL1, DTM1, etc
                    input.Remove(k);
                }
            }
            return input;
        }


        static string ReturnWaveTypeString(int w) { // OPL2
            switch(w) {
                case 0: return "sine";
                case 1: return "halfsine";
                case 2: return "ABSsine";
                case 3: return "quartersine";
                case 4: return "altsine"; // OPL3
                case 5: return "ABSaltsine"; // OPL3
                case 6: return "square"; // OPL3
                case 7: return "saw"; // OPL3
                case 99: return "ReturnWaveTypeString: Err (null input)";
                default: return w+"?";
            }
        }

        // in - blank int array sized to the VGM
        // out - every array index filled with a integer timecode (in samples). 
        //     - Header and EOF will be filled with 0s (not really irrelevant)
        static int[] CreateTimeCode(int[] timecodes, byte[] data, bool[] WaitFlags, int startVGMdata, int endVGMdata){ // waitflags should be true if first byte is a wait command
            
            for (int i = 0; i < startVGMdata; i++) {timecodes[i]=0;}                // write 0 to header section
            for (int i = endVGMdata; i < timecodes.Length; i++) {timecodes[i]=0;}   // write 0 to EOF (tags and such live here)

            int samples=0;
            for (int i = startVGMdata; i < endVGMdata; i++){
                samples+=ParseWaits(data, i, WaitFlags);
                timecodes[i]=samples;
                if (samples > 0){
                    // tb("CreateTimeCode: 0x"+i+": "+samples + " ms:"+ SamplesToMS(samples));
                }
            }
            return timecodes;
        }

        static string SamplesToMinutes(int samples) { // input 44.1khz samples (VGM format)
            double ms = Convert.ToDouble(samples / 44.1);

            return TimeSpan.FromSeconds(ms/1000).ToString(@"m\mss\.ff\s");
            // .ToString(@"hh\:mm\:ss\:fff"); 

            // int m=0; double s=0; string pad="";
            // while (t >= 60000) {
            //     t-=60000;
            //     m+=1;
            // }
            // s = (t / 1000);
            // s = Math.Round(s * 100) / 100;
            // if (s < 10) pad="0"; // pad seconds -> 0m00s
            // // return m+"m"+pad+s+"s";
        }






















    }



    

}














//         static void PrintHex(byte[] b) { // hex view?
//             int length; // file length
//             length = new System.IO.FileInfo(filename).Length;

//             Console.WriteLine("...............................................");
//             Console.WriteLine("00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F");
//             int i;
//             int c=0; // count to 16
//             // int r=0; // row
//             string s = "";
//             // Console.WriteLine(Convert.ToString(b[0],16));
//             for (i = 0; i < length; i++) {

//                 // s+=r; // left row
//                 // r+=1;    
//                 c+=1;  
//                 s+=Convert.ToString(b[i],16);
//                 s+=" ";

//                 if (c == 16) {
//                     Console.WriteLine(s);
//                     c = 0; s = "";
//                 }
//             }
//             Console.WriteLine(s); // leftovers
//             return;
//         }

//     }
// }











