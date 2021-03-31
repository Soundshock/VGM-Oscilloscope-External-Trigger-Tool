using System;
using System.Collections.Generic;
using System.IO;
using System.Timers; // used for display
using System.Linq;

// using System.Reflection;
// using System.Text;
// using System.CommandLine; // not available for dotnet core
// using System.Data.Linq; // has a useful binary class but is only in dotnet framework 4.x

// TODO
// clean up
// make better bitwise functions
// it might be better to make a separate program for handing CH3 mode and always force last operator
// loglevels rather than massive amounts of commented out debug code?

/*
        LIMITATIONS
    Some patches that have a chord-like structure (like a mult 4 + a mult 3, or a 5 into a 4 into a 3) will not track correctly....
    ... you'll have to hand adjust using ForceMult and edit triggers together. sorry. Usually -1 or -2 octaves will do it
    ... it's not impossible to track these with ext.triggers automatically, but it'll take some music theory math, maybe changing the note itself. beyond the scope of this project atm 
    ... may have to resort to using autocorrelate scope like Corrscope for some of these patches

    In order to get the most consistent track, this program will use the lowest multiplier of all operators to do it's work...
    ...But if the lowest operator has a sharp decay it will lose track. This happens with some percussive patches and others.
    todo maybe decay can be maintained for the primary operator? idk that's goofy

    OPL2 AM algorithm *might work*. Untested
    OPL2 drum mode channels probably won't work and might break note detection
    Most timing issues should be dealt with but please still check that output files are the correct length, and not shorter or longer.


*/

namespace EXTT

{
    
    public partial class Program {
        static int VERSIONMAJOR = 0, VERSIONMINOR=1;
        
        public delegate void WriteDelegate(string msg, params object[] args); // shortcut commands
        public static readonly WriteDelegate tb = Console.WriteLine; 
        delegate string WriteDelegate2(byte msg, int tobase);
        private static readonly WriteDelegate2 cts = Convert.ToString;

         public static int ProcessArgument(string arg1, string arg2, string arg3) { // returns number of indexes to skip
            if (GetChannel.TryGetValue(arg1, out Arguments? currentchannel) ) { // if dictionary key 'arg' exists, ref it to currentchannel
                tb("ProcessArgument: found FM arg, executing: "+arg1+" "+arg2+" "+arg3 );
                if (currentchannel.ParseValue(arg2,arg3) ) return 2;                 // 3 index arg (per channel) - ex. FM0 DT 7
            } else { 
                tb("ProcessArgument: found global arg, executing: "+arg1+" "+arg2 ); // 2 index global arg - ex altwaveform FALSE
                if (GlobalArguments.ParseValue(arg1,arg2) ) return 1;
            }
            return 0; // if Command.ParseValue returns false, it'll throw an error - Just continue to the next string and try again.
        }
        static void Main(string[] args) {
            tb("VGM External Trigger Tool Ver "+VERSIONMAJOR+"."+VERSIONMINOR+"\nA VGM hacking tool for creating external trigger waveforms for oscilloscopes\nUsage: EXE [options] Infile.VGM");
            if (args.Length < 1 || "-H"==args[0] || "-h"==args[0] || "h"==args[0] ) { 
                string helptext=@"Help (-h or no arguments)
Supported chips are these Yamaha FM synths: OPN OPNA OPNB OPN2, OPM, OPL2 
Available options (4operator FM): DT(def 0), altwave(def true), ForceMult(def 99, disabled)
Available options (2operator OPL2): altwave(def true), ForceMult(def 99, disabled)
Advanced options: ForceOP(def 4) - 4-operator FMs only, doesn't work with altwave. Changing this value is not recommended. 
                        - - - SETTINGS FOR DETUNE (DT value) - - - 
  * 0 - No Detune (default)
   0-7 - force a detune setting. 7-6-5-0-1-2-3 in order corresponds to -3 to +3 (4, if chosen, is the same as 0)
  * 8  - Use the DT of the lowest frequency operator bias 1>2>3>4 - If there are matches, use the DT of the earlier op (OP#1 > OP#2 > OP#3 > OP#4)
    9  - Use the DT of the lowest frequency operator bias 4>3>2>1 - If there are matches, use the DT of the later op (OP#4 > OP#3 > OP#2 > OP#1)
    10 - Use the DT of the lowest frequency operator bias 4>2>3>1 - *special* If match, favor likely carriers - op#4 > op#2 > op#3 > op#1
  * 11 - Use the DT of the MOST detuned operator bias 1>2>3>4 - If there are matches, use numerically *lowest* op (-3 and +3 match for these)
    12 - Use the DT of the MOST detuned operator bias 4>3>2>1 - If there are matches, use numerically *highest* op
    13 - Use the DT of the LEAST detuned operator bias 1>2>3>4 - If there are matches, use numerically *lowest*  op
    14 - Use the DT of the LEAST detuned operator bias 4>3>2>1 - If there are matches, use numerically *highest* op
    ...- any other value will be equivalent to 0 (default)
    21 - always use OP#1's associated DT
    22 - always use OP#2's associated DT
    23 - always use OP#3's associated DT
    24 - always use OP#4's associated DT
    * = recommended
    none of this applicable for OPL2, which has no operator detune settings.
                        - - -  ALTERNATE WAVEFORM (altwave true) - - - 
    Rather than produce a sine wave, produces a 50% FM wave. This kind of wave will NOT track better, but the cross 
    at the 50% mark gives the scope something to grab onto if the frequency is too low for the scope framerate.
    With altwave on, Low-frequency waveforms should skip around more predictably if the oscilloscope loses track.
                        - - - ADD MULT / FORCE MULT (forcemult/mult/addmult, off by default)
    Some patches that utilize a chord-like structure need to have their implied roots defined by hand. Sorry.
    Look at the scope and see how many octaves down you need the trigger to be. Aim to cover the whole waveform.
    Possible values: Positive values will force a multiplier, 0-15. Negative values subtract octaves, Try -1 or -2. 

Options may be set globally and/or per-channel, by preceding an option with a 'FM#' command (zero-bound)
Per-channel commands will always take precedence over global commands.
Example: invgm.VGM dt 0 altwave false         <- sets DT to 0 and altwave to false globally for all channels 
Example: invgm.VGM dt 0 altwave false fm0 dt 2 fm3 dt 11 <- does the above but sets dt to '0' for FM0, and '11' for FM3
Example: invgm.VGM dt 0 altwave false fm0 dt 2 fm3 dt 11 fm3 mult 1 <- + force fm3 to use multiplier 1
... or just drag & drop.";
                tb(helptext);
                Console.ReadKey();
                Environment.Exit(0); // 0 is good?
            }

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
            // tb(GlobalArguments.Report()+s); // debug

            // tb("length{0}",args.Length);
            // tb("arg0: {0}",args[0]);
            // Console.ReadKey();
            // Console.WriteLine("Main Initiated");
            debugstart(); // jump to a debug func for messing around
            if (args.Length > 0 && File.Exists(args[args.Length-1]) ) {
                // tb("arg0: {0}",args[args.Length-1]); Console.ReadKey();
                
                string filename = args[args.Length-1].ToString();             
                // tb("filename: {0}",filename); Console.ReadKey();
                ReadFile(filename);
            } else {
                tb("No file found @" +args[args.Length]); Console.ReadKey();
            }
        }



        // Settings - per channel and global - are contained via class 'Arguments'
        static Arguments GlobalArguments = new Arguments( 0, 4, 99, 1, "FMG"); // args: detunesetting, forceop, forcemult, altwaveform. These will be copied to unset values
        static Arguments FM0Args = new Arguments(99,99,99,99,"FM0"); static Arguments FM1Args = new Arguments(99,99,99,99,"FM1"); // 99 is unset yes this is goofy
        static Arguments FM2Args= new Arguments(99,99,99,99,"FM2"); static Arguments FM3Args= new Arguments(99,99,99,99,"FM3");
        static Arguments FM4Args= new Arguments(99,99,99,99,"FM4"); static Arguments FM5Args= new Arguments(99,99,99,99,"FM5");
        static Arguments FM6Args= new Arguments(99,99,99,99,"FM6"); static Arguments FM7Args= new Arguments(99,99,99,99,"FM7");
        static Arguments FM8Args= new Arguments(99,99,99,99,"FM8"); 

        static Dictionary<string, Arguments> GetChannel = new Dictionary<string, Arguments>(); // ex. key/pair: "FM0" FM0channel


        class Arguments { // contains global & per channel settings to be fed into main loop
            public int detunesetting, forceop, forcemult, altwaveform; // todo altwaveform bool conversion
            public string name;// {get; set;}

            // public void Arguments(int detunesetting){//}, int forceop, int forcemult, int altwaveform, string name) {
            public Arguments(int detunesetting, int forceop, int forcemult, int altwaveform, string name){
                this.detunesetting = detunesetting;
                this.forceop = forceop;
                this.forcemult = forcemult;
                this.altwaveform = altwaveform;
                this.name=name;
            }
            public bool ParseValue(string property, string value){
                if (value == "FALSE") value = "0";
                if (value == "TRUE") value = "1";
                if (value == "OFF") value = "99"; // forcemult 99 = off.. but don't do this
                int intval;
                if (Int32.TryParse(value, out intval) ) {
                    switch (property){
                        case "DT": this.detunesetting = intval; this.suffix+="DT"+detunesetting; break;
                        case "FORCEOP": this.forceop = intval; this.suffix+="op"+forceop; break;
                        case "MULT": this.forcemult = intval; this.suffix+="Mult"+forcemult; break;
                        case "FORCEMULT": this.forcemult = intval; this.suffix+="Mult"+forcemult; break;
                        case "ADDMULT": this.forcemult = intval; this.suffix+="Mult"+forcemult; break;
                        case "ALTWAVE": this.altwaveform = intval; this.suffix+="AltWave"; break;
                        case "ALTWAVEFORM": this.altwaveform = intval; this.suffix+="AltWave"; break;
                        default: tb("PARSEVALUE: property "+property +"not found"); return false;
                    }
                    // tb ("value good?");
                    return true; // hmm
                } else {
                    tb("PARSEVALUE ERROR: COULD NOT PARSE ARGUMENT: "+ value+"");
                    return false;
                }
            }
            public int addmult{ // todo move conditional here. should be 0 if forcemult is 0 or positive
                get {
                    return forcemult;
                }
            }
            public bool altwave{
                get {
                    if (this.altwaveform > 0) {
                        return true;
                    } else {
                        return false;
                    }
                }

            }
            public string AddGlobalValuesToFilename(){ //* only use for global values
                string s="";
                if (forceop > 0 && forceop < 4) s+= "Op"+forceop; // <5?
                if (forcemult < 16 && forcemult > -16) s+= "Mult"+forcemult;
                if (altwaveform > 0) s+= "AltWave"; 
                // if (forceop > 0 || forceop < 4) s+= "Op"+forceop; // <5?
                if (chiptype != 510){
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
            }

            public string Report(){ //* just debug
                return this.name+" settings:"
                +" DT"+this.detunesetting
                +" forceOP"+this.forceop
                +" forceMULT"+this.forcemult
                +" AltWave"+this.altwaveform+"\n";

            }
        }


        static int chiptype=0; // 0 = auto // don't touch this. 
        static void ReadFile(string filename) {

            byte[] data;
            data = File.ReadAllBytes(filename);
            if (data.Length < 0xff) {tb("WARNING! Invalid File (too few bytes): "+data.Length);Console.ReadKey();}


            // tb("CHIPTYPE:"+chiptype+"\nSupported chiptypes are 52(OPN2, untested) 54 (OPM, buggy) 55(OPN) 56(OPNA) 510(OPL2)");
            // tb("Supported chiptypes are 52(OPN2, untested) 54 (OPM, buggy) 55(OPN) 56(OPNA) 510(OPL2)");
            // if (chiptype == 0) tb("Chiptype is 0, automatic chip detection...");
            // Console.WriteLine(Get32BitInt(data,0x34)); // 0x34 is VGM data offset
            int startVGMdata = (Get32BitInt(data,0x34)+0x34); // tb("DEBUG: VGM data start point: 0x"+Convert.ToString(startVGMdata,16) );
            // * endVGMdata appears to be ending in the middle of the tag section... hope that's fine
            int endVGMdata = (Get32BitInt(data,0x04)+0x04); // tb("DEBUG: VGM data end point: 0x"+Convert.ToString(endVGMdata,16) );
            if (Get32BitInt(data,0x10) > 0) {
                tb("Chip Detection: 0x10 "+Get32BitInt(data,0x10)+" YM2413 clock rate found but chip not supported!");
            } else if (Get32BitInt(data,0x2C) > 0) {
                chiptype=52; tb("Chip Detection: 0x2C clockrate: "+Get32BitInt(data,0x2C)+" YM2612 OPN2 found"); 
            } else if (Get32BitInt(data,0x30) > 0) {
                chiptype=54; tb("Chip Detection: 0x30 clockrate: "+Get32BitInt(data,0x30)+" YM2151 OPM found"); 
            } else if (Get32BitInt(data,0x44) > 0){
                chiptype=55; tb("Chip Detection: 0x44 clockrate: "+Get32BitInt(data,0x44)+" YM2203 OPN found"); 
            }  else if (Get32BitInt(data,0x48) > 0){
                chiptype=56; tb("Chip Detection: 0x48 clockrate: "+Get32BitInt(data,0x48)+" YM2608 OPNA found"); 
            }   else if (Get32BitInt(data,0x4C) > 0){ // would be 58 maybe?
                chiptype=58; tb("Chip Detection: 0x4C clockrate: "+Get32BitInt(data,0x4C)+" YM2610 OPNB found"); 
            }  else if (Get32BitInt(data,0x50) > 0){
                chiptype=510; tb("Chip Detection: 0x50 clockrate: "+Get32BitInt(data,0x50)+" YM3812 OPL2 found"); 
            }  //               System.Console.ReadKey(); // pause



            //* PART 1/4: INITIAL COMMAND SETUP
            // setup: byte indexes, saved per channel
            // constructor includes this.chip for VGM chip number
            // (52/53 for OPN2, 54 for OPM, 55 for OPN, 56/57 for OPNA), 0x58/9 OPNB, 5A OPL2
            // TODO 0x51 YM2413 OPLL, 0x5B YM3526 OPLL, 0x5C Y8950 OPLL, 0x5E/F YMF262 OPL3
            FMchannel FM0,FM1,FM2,FM3,FM4,FM5,FM6,FM7,FM8;
            FM0 = new FMchannel(4,0x55); FM1 = new FMchannel(4,0x55); FM2 = new FMchannel(4,0x55); 
            FM3 = new FMchannel(4,0x55); FM4 = new FMchannel(4,0x55); FM5 = new FMchannel(4,0x55); 
            FM6 = new FMchannel(4,0x55); FM7 = new FMchannel(4,0x55); FM8 = new FMchannel(2,0x55);


            if (chiptype==55){          //* OPN
                FM0 = new FMchannel(4,0x55); FM1 = new FMchannel(4,0x55); FM2 = new FMchannel(4,0x55); 

                FM0.keyon = new byte[] {0x55, 0x28, 0xF0}; // keyon commands.
                FM1.keyon = new byte[] {0x55, 0x28, 0xF1}; //* These will be scanned as the starting point for patch searching
                FM2.keyon = new byte[] {0x55, 0x28, 0xF2};

            } else if (chiptype==56){   //* OPNA
                FM0 = new FMchannel(4,0x56);  FM1 = new FMchannel(4,0x56);  FM2 = new FMchannel(4,0x56);
                FM3 = new FMchannel(4,0x57);  FM4 = new FMchannel(4,0x57);  FM5 = new FMchannel(4,0x57);

                FM0.keyon = new byte[] {0x56, 0x28, 0xF0}; // keyon commands.
                FM1.keyon = new byte[] {0x56, 0x28, 0xF1}; //* These will be scanned as the starting point for patch searching
                FM2.keyon = new byte[] {0x56, 0x28, 0xF2};
                FM3.keyon = new byte[] {0x56, 0x28, 0xF4}; // not 57 btw
                FM4.keyon = new byte[] {0x56, 0x28, 0xF5};
                FM5.keyon = new byte[] {0x56, 0x28, 0xF6};
            } else if (chiptype==58){   //* OPNB (specifically YM2610B with 6 channels. this might break with 4-channel FMs)
                FM0 = new FMchannel(4,0x58);  FM1 = new FMchannel(4,0x58);  FM2 = new FMchannel(4,0x58);
                FM3 = new FMchannel(4,0x59);  FM4 = new FMchannel(4,0x59);  FM5 = new FMchannel(4,0x59);

                FM0.keyon = new byte[] {0x58, 0x28, 0xF0}; // keyon commands.
                FM1.keyon = new byte[] {0x58, 0x28, 0xF1}; //* These will be scanned as the starting point for patch searching
                FM2.keyon = new byte[] {0x58, 0x28, 0xF2};
                FM3.keyon = new byte[] {0x58, 0x28, 0xF4}; // not 57 btw
                FM4.keyon = new byte[] {0x58, 0x28, 0xF5};
                FM5.keyon = new byte[] {0x58, 0x28, 0xF6};
            } else if (chiptype==52){   //* OPN2
                FM0 = new FMchannel(4,0x52);  FM1 = new FMchannel(4,0x52);  FM2 = new FMchannel(4,0x52);
                FM3 = new FMchannel(4,0x53);  FM4 = new FMchannel(4,0x53);  FM5 = new FMchannel(4,0x53);

                FM0.keyon = new byte[] {0x52, 0x28, 0xF0}; // keyon commands.
                FM1.keyon = new byte[] {0x52, 0x28, 0xF1}; //* These will be scanned as the starting point for patch searching
                FM2.keyon = new byte[] {0x52, 0x28, 0xF2}; // copy pasted from OPNA. they probably work. 
                FM3.keyon = new byte[] {0x52, 0x28, 0xF4};
                FM4.keyon = new byte[] {0x52, 0x28, 0xF5};
                FM5.keyon = new byte[] {0x52, 0x28, 0xF6};
            }
            if (chiptype==52 || chiptype==55 || chiptype==56 || chiptype==58) { //* OPN2/OPN/OPNA/OPNB(?) ch#1 - ch#3
                FM0.name="FM0"; FM1.name="FM1"; FM2.name="FM2"; 

                FM0.op1_TL = 0x40; FM0.op2_TL = 0x48; FM0.op3_TL = 0x44; FM0.op4_TL = 0x4C;
                FM1.op1_TL = 0x41; FM1.op2_TL = 0x49; FM1.op3_TL = 0x45; FM1.op4_TL = 0x4D; 
                FM2.op1_TL = 0x42; FM2.op2_TL = 0x4A; FM2.op3_TL = 0x46; FM2.op4_TL = 0x4E;

                FM0.op1_DTML=0x30; FM0.op2_DTML=0x38; FM0.op3_DTML=0x34; FM0.op4_DTML=0x3C;
                FM1.op1_DTML=0x31; FM1.op2_DTML=0x39; FM1.op3_DTML=0x35; FM1.op4_DTML=0x3D;
                FM2.op1_DTML=0x32; FM2.op2_DTML=0x3A; FM2.op3_DTML=0x36; FM2.op4_DTML=0x3E;

                FM0.SetAR(0x50, 0x58, 0x54, 0x5C); FM1.SetAR(0x51, 0x59, 0x55, 0x5D); FM2.SetAR(0x52, 0x5A, 0x56, 0x5E);
                FM0.SetDR(0x60, 0x68, 0x64, 0x6C); FM1.SetDR(0x61, 0x69, 0x65, 0x6D); FM2.SetDR(0x62, 0x6A, 0x66, 0x6E);

                // Yamaha 4op synths use a 5-part envelope
                // Attack Rate - Decay Rate - Sustain Level - Sustain Rate - Release Rate
                // in OPN chips, SR / RR is the same byte (4bit-4bit) -- ???

                FM0.SR[0] = 0x70; FM0.SR[2] = 0x74; FM0.SR[1] = 0x78; FM0.SR[3] = 0x7C; 
                FM1.SR[0] = 0x71; FM1.SR[2] = 0x75; FM1.SR[1] = 0x79; FM1.SR[3] = 0x7D; 
                FM2.SR[0] = 0x72; FM2.SR[2] = 0x76; FM2.SR[1] = 0x7A; FM2.SR[3] = 0x7E; 


                FM0.RR[0] = 0x80; FM0.RR[2] = 0x84; FM0.RR[1] = 0x88; FM0.RR[3] = 0x8C; 
                FM1.RR[0] = 0x81; FM1.RR[2] = 0x85; FM1.RR[1] = 0x89; FM1.RR[3] = 0x8D; 
                FM2.RR[0] = 0x82; FM2.RR[2] = 0x86; FM2.RR[1] = 0x8A; FM2.RR[3] = 0x8E; 

                FM0.ALG = 0xB0; FM1.ALG = 0xB1; FM2.ALG = 0xB2;    // Alg shares a bit with feedback (feedback\ALG)

            }
            if (chiptype==52 || chiptype==56 || chiptype==58){  //* 6-ch OPNs, and also OPNB
                FM3.name="FM3"; FM4.name="FM4"; FM5.name="FM5"; // used for debugging
                FM3.op1_TL = 0x40; FM3.op2_TL = 0x48; FM3.op3_TL = 0x44; FM3.op4_TL = 0x4C; // chip=57 (opna) or 53 (opn2)
                FM4.op1_TL = 0x41; FM4.op2_TL = 0x49; FM4.op3_TL = 0x45; FM4.op4_TL = 0x4D; // chip=57 (opna) or 53 (opn2)
                FM5.op1_TL = 0x42; FM5.op2_TL = 0x4A; FM5.op3_TL = 0x46; FM5.op4_TL = 0x4E; // chip=57 (opna) or 53 (opn2)

                FM3.op1_DTML=0x30; FM3.op2_DTML=0x38; FM3.op3_DTML=0x34; FM3.op4_DTML=0x3C; // chip=57 (opna) or 53 (opn2)
                FM4.op1_DTML=0x31; FM4.op2_DTML=0x39; FM4.op3_DTML=0x35; FM4.op4_DTML=0x3D; // chip=57 (opna) or 53 (opn2)
                FM5.op1_DTML=0x32; FM5.op2_DTML=0x3A; FM5.op3_DTML=0x36; FM5.op4_DTML=0x3E; // chip=57 (opna) or 53 (opn2)

                //* decay rate shares a bit with AM. AM will be destroyed by this code but that's fine(?)
                FM3.SetAR(0x50, 0x58, 0x54, 0x5C); FM4.SetAR(0x51, 0x59, 0x55, 0x5D); FM5.SetAR(0x52, 0x5A, 0x56, 0x5E); // 53 / 57
                FM3.SetDR(0x60, 0x68, 0x64, 0x6C); FM4.SetDR(0x61, 0x69, 0x65, 0x6D); FM5.SetDR(0x62, 0x6A, 0x66, 0x6E);

                FM3.SR[0] = 0x70; FM3.SR[2] = 0x74; FM3.SR[1] = 0x78; FM3.SR[3] = 0x7C; 
                FM4.SR[0] = 0x71; FM4.SR[2] = 0x75; FM4.SR[1] = 0x79; FM4.SR[3] = 0x7D; 
                FM5.SR[0] = 0x72; FM5.SR[2] = 0x76; FM5.SR[1] = 0x7A; FM5.SR[3] = 0x7E; 

                FM3.RR[0] = 0x80; FM3.RR[2] = 0x84; FM3.RR[1] = 0x88; FM3.RR[3] = 0x8C; // 53 / 57
                FM4.RR[0] = 0x81; FM4.RR[2] = 0x85; FM4.RR[1] = 0x89; FM4.RR[3] = 0x8D; 
                FM5.RR[0] = 0x82; FM5.RR[2] = 0x86; FM5.RR[1] = 0x8A; FM5.RR[3] = 0x8E; 

                FM3.ALG = 0xB0; FM4.ALG = 0xB1; FM5.ALG = 0xB2;    // Alg shares a bit with feedback (feedback\ALG)


            }
            if (chiptype==54) { // 8-voice OPM YM2151
                FM0 = new FMchannel(4,0x54);  FM1 = new FMchannel(4,0x54);  FM2 = new FMchannel(4,0x54);
                FM3 = new FMchannel(4,0x54);  FM4 = new FMchannel(4,0x54);  FM5 = new FMchannel(4,0x54);
                FM6 = new FMchannel(4,0x54);  FM7 = new FMchannel(4,0x54);
                FM0.name="FM0";FM1.name="FM1";FM2.name="FM2";FM3.name="FM3";FM4.name="FM4";FM5.name="FM5";FM6.name="FM6";FM7.name="FM7";

                // todo looks like OPM has the ability to mute operators built into it's keyon commands? might cause problems
                FM0.keyon = new byte[] {0x54, 0x08, 0x78}; FM1.keyon = new byte[] {0x54, 0x08, 0x79}; // these values bit shift a lot
                FM2.keyon = new byte[] {0x54, 0x08, 0x7A}; FM3.keyon = new byte[] {0x54, 0x08, 0x7B}; // but we only need keyon so it's fine
                FM4.keyon = new byte[] {0x54, 0x08, 0x7C}; FM5.keyon = new byte[] {0x54, 0x08, 0x7D}; 
                FM6.keyon = new byte[] {0x54, 0x08, 0x7E}; FM7.keyon = new byte[] {0x54, 0x08, 0x7F};   	

                FM0.op1_TL = 0x60; FM0.op3_TL = 0x68; FM0.op2_TL = 0x70; FM0.op4_TL = 0x78;  // remember fully off TL is 7f. 127 is max value.
                FM1.op1_TL = 0x61; FM1.op3_TL = 0x69; FM1.op2_TL = 0x71; FM1.op4_TL = 0x79; 
                FM2.op1_TL = 0x62; FM2.op3_TL = 0x6A; FM2.op2_TL = 0x72; FM2.op4_TL = 0x7A;  
                FM3.op1_TL = 0x63; FM3.op3_TL = 0x6B; FM3.op2_TL = 0x73; FM3.op4_TL = 0x7B;  
                FM4.op1_TL = 0x64; FM4.op3_TL = 0x6C; FM4.op2_TL = 0x74; FM4.op4_TL = 0x7C;  
                FM5.op1_TL = 0x65; FM5.op3_TL = 0x6D; FM5.op2_TL = 0x75; FM5.op4_TL = 0x7D;  
                FM6.op1_TL = 0x66; FM6.op3_TL = 0x6E; FM6.op2_TL = 0x76; FM6.op4_TL = 0x7E;  
                FM7.op1_TL = 0x67; FM7.op3_TL = 0x6F; FM7.op2_TL = 0x77; FM7.op4_TL = 0x7F; 
                
                FM0.op1_DTML = 0x40; FM0.op3_DTML = 0x48; FM0.op2_DTML = 0x50; FM0.op4_DTML = 0x58; 
                FM1.op1_DTML = 0x41; FM1.op3_DTML = 0x49; FM1.op2_DTML = 0x51; FM1.op4_DTML = 0x59; 
                FM2.op1_DTML = 0x42; FM2.op3_DTML = 0x4A; FM2.op2_DTML = 0x52; FM2.op4_DTML = 0x5A; 
                FM3.op1_DTML = 0x43; FM3.op3_DTML = 0x4B; FM3.op2_DTML = 0x53; FM3.op4_DTML = 0x5B; 
                FM4.op1_DTML = 0x44; FM4.op3_DTML = 0x4C; FM4.op2_DTML = 0x54; FM4.op4_DTML = 0x5C; 
                FM5.op1_DTML = 0x45; FM5.op3_DTML = 0x4D; FM5.op2_DTML = 0x55; FM5.op4_DTML = 0x5D; 
                FM6.op1_DTML = 0x46; FM6.op3_DTML = 0x4E; FM6.op2_DTML = 0x56; FM6.op4_DTML = 0x5E; 
                FM7.op1_DTML = 0x47; FM7.op3_DTML = 0x4F; FM7.op2_DTML = 0x57; FM7.op4_DTML = 0x5F;

                FM0.AR[0] = 0x80; FM0.AR[1] = 0x88; FM0.AR[2] = 0x90; FM0.AR[3] = 0x98; // VGM2TXT says key scale / AR, but I think full byte is AR
                FM1.AR[0] = 0x81; FM1.AR[1] = 0x89; FM1.AR[2] = 0x91; FM1.AR[3] = 0x99;
                FM2.AR[0] = 0x82; FM2.AR[1] = 0x8A; FM2.AR[2] = 0x92; FM2.AR[3] = 0x9A; 
                FM3.AR[0] = 0x83; FM3.AR[1] = 0x8B; FM3.AR[2] = 0x93; FM3.AR[3] = 0x9B; 
                FM4.AR[0] = 0x84; FM4.AR[1] = 0x8C; FM4.AR[2] = 0x94; FM4.AR[3] = 0x9C; 
                FM5.AR[0] = 0x85; FM5.AR[1] = 0x8D; FM5.AR[2] = 0x95; FM5.AR[3] = 0x9D; 
                FM6.AR[0] = 0x86; FM6.AR[1] = 0x8E; FM6.AR[2] = 0x96; FM6.AR[3] = 0x9E; 
                FM7.AR[0] = 0x87; FM7.AR[1] = 0x8F; FM7.AR[2] = 0x97; FM7.AR[3] = 0x9F; 	                    

                FM0.DR[0] = 0xA0; FM0.DR[1] = 0xA8; FM0.DR[2] = 0xB0; FM0.DR[3] = 0xB8; // LFO amp  modul / DR 1. Not sure how these bits are split up.
                FM1.DR[0] = 0xA1; FM1.DR[1] = 0xA9; FM1.DR[2] = 0xB1; FM1.DR[3] = 0xB9; 
                FM2.DR[0] = 0xA2; FM2.DR[1] = 0xAA; FM2.DR[2] = 0xB2; FM2.DR[3] = 0xBA; 
                FM3.DR[0] = 0xA3; FM3.DR[1] = 0xAB; FM3.DR[2] = 0xB3; FM3.DR[3] = 0xBB; 
                FM4.DR[0] = 0xA4; FM4.DR[1] = 0xAC; FM4.DR[2] = 0xB4; FM4.DR[3] = 0xBC; 
                FM5.DR[0] = 0xA5; FM5.DR[1] = 0xAD; FM5.DR[2] = 0xB5; FM5.DR[3] = 0xBD; 
                FM6.DR[0] = 0xA6; FM6.DR[1] = 0xAE; FM6.DR[2] = 0xB6; FM6.DR[3] = 0xBE; 
                FM7.DR[0] = 0xA7; FM7.DR[1] = 0xAF; FM7.DR[2] = 0xB7; FM7.DR[3] = 0xBF; 	

                // SR opm first four bits are Detune 2

                FM0.SR[0]=0xC0; FM0.SR[2]=0xC8; FM0.SR[1]=0xD0; FM0.SR[3]=0xD8; // proper
                FM1.SR[0]=0xC1; FM1.SR[2]=0xC9; FM1.SR[1]=0xD1; FM1.SR[3]=0xD9; 
                FM2.SR[0]=0xC2; FM2.SR[2]=0xCA; FM2.SR[1]=0xD2; FM2.SR[3]=0xDA; 
                FM3.SR[0]=0xC3; FM3.SR[2]=0xCB; FM3.SR[1]=0xD3; FM3.SR[3]=0xDB; 
                FM4.SR[0]=0xC4; FM4.SR[2]=0xCC; FM4.SR[1]=0xD4; FM4.SR[3]=0xDC; 
                FM5.SR[0]=0xC5; FM5.SR[2]=0xCD; FM5.SR[1]=0xD5; FM5.SR[3]=0xDD; 
                FM6.SR[0]=0xC6; FM6.SR[2]=0xCE; FM6.SR[1]=0xD6; FM6.SR[3]=0xDE; 
                FM7.SR[0]=0xC7; FM7.SR[2]=0xCF; FM7.SR[1]=0xD7; FM7.SR[3]=0xDF; 


                FM0.RR[0] = 0xE0; FM0.RR[1] = 0xE8; FM0.RR[2] = 0xF0; FM0.RR[3] = 0xF8; // D1L / RR. Not sure how these bits are split up.
                FM1.RR[0] = 0xE1; FM1.RR[1] = 0xE9; FM1.RR[2] = 0xF1; FM1.RR[3] = 0xF9; // 
                FM2.RR[0] = 0xE2; FM2.RR[1] = 0xEA; FM2.RR[2] = 0xF2; FM2.RR[3] = 0xFA; 
                FM3.RR[0] = 0xE3; FM3.RR[1] = 0xEB; FM3.RR[2] = 0xF3; FM3.RR[3] = 0xFB; 
                FM4.RR[0] = 0xE4; FM4.RR[1] = 0xEC; FM4.RR[2] = 0xF4; FM4.RR[3] = 0xFC; 
                FM5.RR[0] = 0xE5; FM5.RR[1] = 0xED; FM5.RR[2] = 0xF5; FM5.RR[3] = 0xFD; 
                FM6.RR[0] = 0xE6; FM6.RR[1] = 0xEE; FM6.RR[2] = 0xF6; FM6.RR[3] = 0xFE; 
                FM7.RR[0] = 0xE7; FM7.RR[1] = 0xEF; FM7.RR[2] = 0xF7; FM7.RR[3] = 0xFF; 

                // DR 1 - LFO amplitude modulation / Decay Rate 1

                // ALG: first two bits Stereo (11, 10, 01) Next 3 bits FEEDBACK, Next 3 bits ALG. Feedback 0 / Alg 7 is 0xC7
                FM0.ALG=0x20; FM1.ALG=0x21; FM2.ALG=0x22; FM3.ALG=0x23; FM4.ALG=0x24; FM5.ALG=0x25; FM6.ALG=0x26; FM7.ALG=0x27; // ALG/FEEDBACK

            }
            if (chiptype==510) { // 5A - OPL2
                FM0 = new FMchannel(2,0x5A);  FM1 = new FMchannel(2,0x5A);  FM2 = new FMchannel(2,0x5A);
                FM3 = new FMchannel(2,0x5A);  FM4 = new FMchannel(2,0x5A);  FM5 = new FMchannel(2,0x5A);
                FM6 = new FMchannel(2,0x5A);  FM7 = new FMchannel(2,0x5A);  FM8 = new FMchannel(2,0x5A);
                FM0.name="FM0";FM1.name="FM1";FM2.name="FM2";FM3.name="FM3";FM4.name="FM4";FM5.name="FM5";FM6.name="FM6";FM7.name="FM7";FM8.name="FM8"; 

                // opl keyon works a bit differently. FM0 5A B0 xy    x  0|1=keyoff rest keyon?  >0x20 on, <0x20 off?
                FM0.keyon = new byte[] {0x5A, 0xB0}; FM1.keyon = new byte[] {0x5A, 0xB1}; FM2.keyon = new byte[] {0x5A, 0xB2}; 
                FM3.keyon = new byte[] {0x5A, 0xB3}; FM4.keyon = new byte[] {0x5A, 0xB4}; FM5.keyon = new byte[] {0x5A, 0xB5}; 
                FM6.keyon = new byte[] {0x5A, 0xB6}; FM7.keyon = new byte[] {0x5A, 0xB7}; FM8.keyon = new byte[] {0x5A, 0xB7}; 
                // tl  - first two bits are key scale, 0,1,2= 00, 01, 10. Rest is TL, a 6-bit value of 0-63 (3F = muted)
                FM0.op1_TL=0x40; FM0.op2_TL=0x43;
                FM1.op1_TL=0x41; FM1.op2_TL=0x44;
                FM2.op1_TL=0x42; FM2.op2_TL=0x45;
                FM3.op1_TL=0x48; FM3.op2_TL=0x4B;
                FM4.op1_TL=0x49; FM4.op2_TL=0x4C;
                FM5.op1_TL=0x4A; FM5.op2_TL=0x4D;
                FM6.op1_TL=0x50; FM6.op2_TL=0x53;
                FM7.op1_TL=0x51; FM7.op2_TL=0x54;
                FM8.op1_TL=0x52; FM8.op2_TL=0x55;
                // MULTIPLIER... aka AM|VIBRATO|KSR|EG / MULT - No Detune with OPL2
                FM0.op1_DTML=0x20; FM0.op2_DTML=0x23;
                FM1.op1_DTML=0x21; FM1.op2_DTML=0x24;
                FM2.op1_DTML=0x22; FM2.op2_DTML=0x25;
                FM3.op1_DTML=0x28; FM3.op2_DTML=0x2B;
                FM4.op1_DTML=0x29; FM4.op2_DTML=0x2C;
                FM5.op1_DTML=0x2A; FM5.op2_DTML=0x2D;
                FM6.op1_DTML=0x30; FM6.op2_DTML=0x33;
                FM7.op1_DTML=0x31; FM7.op2_DTML=0x34;
                FM8.op1_DTML=0x32; FM8.op2_DTML=0x35;

                // OPL2 waveform. this gets it's own byte, but only overwrite second 4-bit value to be safe. 0x00 for sine wave
                FM0.op1_waveform=0xE0; FM0.op2_waveform=0xE3;
                FM1.op1_waveform=0xE1; FM1.op2_waveform=0xE4;
                FM2.op1_waveform=0xE2; FM2.op2_waveform=0xE5;
                FM3.op1_waveform=0xE8; FM3.op2_waveform=0xEB;
                FM4.op1_waveform=0xE9; FM4.op2_waveform=0xEC;
                FM5.op1_waveform=0xEA; FM5.op2_waveform=0xED;
                FM6.op1_waveform=0xF0; FM6.op2_waveform=0xF3;
                FM7.op1_waveform=0xF1; FM7.op2_waveform=0xF4;
                FM8.op1_waveform=0xF2; FM8.op2_waveform=0xF5;

                // Algorithm / feedback (use 0x00)
                // MDplayer says second 4-bit, but the values are strange. 
                FM0.ALG = 0xC0; FM1.ALG = 0xC1; FM2.ALG = 0xC2;
                FM3.ALG = 0xC3; FM4.ALG = 0xC4; FM5.ALG = 0xC5;
                FM6.ALG = 0xC6; FM7.ALG = 0xC7; FM8.ALG = 0xC8;

                // FM0.SetDR(0x80, 0x83); // Sustain 'level', Release Rate (redundant)

                FM0.AR[0] = 0x60; FM0.AR[1] = 0x63; // AR / DR share the same byte in OPL2
                FM1.AR[0] = 0x61; FM1.AR[1] = 0x64; 
                FM2.AR[0] = 0x62; FM2.AR[1] = 0x65; 
                FM3.AR[0] = 0x68; FM3.AR[1] = 0x6B; 
                FM4.AR[0] = 0x69; FM4.AR[1] = 0x6C; 
                FM5.AR[0] = 0x6A; FM5.AR[1] = 0x6D; 
                FM6.AR[0] = 0x70; FM6.AR[1] = 0x73; 
                FM7.AR[0] = 0x71; FM7.AR[1] = 0x74; 
                FM8.AR[0] = 0x72; FM8.AR[1] = 0x75; 	


                FM0.RR[0] = 0x80; FM0.RR[1] = 0x83; // SR / RR
                FM1.RR[0] = 0x81; FM1.RR[1] = 0x84; 
                FM2.RR[0] = 0x82; FM2.RR[1] = 0x85; 
                FM3.RR[0] = 0x88; FM3.RR[1] = 0x8B; 
                FM4.RR[0] = 0x89; FM4.RR[1] = 0x8C; 
                FM5.RR[0] = 0x8A; FM5.RR[1] = 0x8D; 
                FM6.RR[0] = 0x90; FM6.RR[1] = 0x93; 
                FM7.RR[0] = 0x91; FM7.RR[1] = 0x94; 
                FM8.RR[0] = 0x92; FM8.RR[1] = 0x95; 	

            }


            //* PART 2/4: SCAN THROUGH DATA BYTE-BY-BYTE, FLAGGING FM COMMANDS THAT ARE SAFE TO EDIT

            bool[] byteflag = ExamineVGMData(data, FM0.chip, startVGMdata, endVGMdata);
            // // for (int b=0; b < byteflag.Length;b++) {byteflag[b]=true;} s+="_test_ctrl_";//* debug test, set all data to editable (THIS SHOULD BE COMMENTED OUT)

            // tb("0x"+cts(data[0x2ebc],16)+"... 0x2EBC:"+ byteflag[0x2ebc]);
            // tb(FM0.keyon[0]+" "+FM0.keyon[1]);
            // Console.ReadKey();

            //* PART 3/4 this does two things -
            //* pt1: Blanket edits across the board (for example removing all AR/DR/RS, channel feedback to 0, channel algorithms to 7)
            //* pt2: Find keyOn events and trace backwards to find patches, then edit them to our liking (mute operators, decide which detune value to use based on our settings, etc)

            // timer test
            ProgressTimer = new System.Timers.Timer(10); 
            ProgressTimer.AutoReset=true;
            ProgressTimer.Enabled=true;
            ProgressTimer.Elapsed += UpdateProgress;


            AutoTrigger(FM0, FM0Args, data, byteflag, startVGMdata, endVGMdata);
            AutoTrigger(FM1, FM1Args, data, byteflag, startVGMdata, endVGMdata);
            AutoTrigger(FM2, FM2Args, data, byteflag, startVGMdata, endVGMdata);
            if (chiptype==52 || chiptype==54 || chiptype==56 || chiptype==58 || chiptype==510){  // 6 voices - OPN2 / OPM / OPNA / OPL2    
                AutoTrigger(FM3, FM3Args, data, byteflag, startVGMdata, endVGMdata);
                AutoTrigger(FM4, FM4Args, data, byteflag, startVGMdata, endVGMdata);
                AutoTrigger(FM5, FM5Args, data, byteflag, startVGMdata, endVGMdata);
            }
            if (chiptype==54 || chiptype==510){     // 8 voices - OPM / OPL2
                AutoTrigger(FM6, FM6Args, data, byteflag, startVGMdata, endVGMdata);
                AutoTrigger(FM7, FM7Args, data, byteflag, startVGMdata, endVGMdata);
            }
            if (chiptype==510){                     // 9 voices - OPL2
                AutoTrigger(FM8, FM8Args, data, byteflag, startVGMdata, endVGMdata);
            }

            ProgressTimer.Stop(); // stop timer

            //* PART 4/4 - write new file
            string outfile = ""; // add suffixes to filename...
            Arguments[] args = new Arguments[] {FM0Args, FM1Args, FM2Args, FM3Args, FM4Args, FM5Args, FM6Args, FM7Args, FM8Args};
            foreach (Arguments FMx in args){
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
            tb("Completed");
            System.Console.ReadKey(); // pause
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

        public struct FMchannel 
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

            }
            return false;
        }



        //* pt 2/4 cont. - go through byte-by-byte, flag bytes that are safe to edit
        static bool[] ExamineVGMData(byte[] data, byte FMchip, int start, int end){ 
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
                    case 0x52:  //* If OPM+OPN2 it's probably the Bally/Williams/Midway DAC -> OPN2 DAC trick 
                        if (FMchip==0x54){
                            if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte Additional OPN2 command
                        } else { toif=true; break;} // send OPM to next conditional

                    //* skip wait commands, samples & OPN2 DAC commands
                    case 0x61: i+=2; break; // three-byte wait
                    case 0x62: break;
                    case 0x63: break;
                    case 0x66: i=end; tb("end reached @ 0x"+i); break; // end of sound data
                    case 0x67: // data block: 0x67 0x66 tt ss ss ss ss (data)
                        i+=2; i+=Get32BitInt(data,i+1); break;  // maybe?
                    case byte n when (n >= 0x70 && n <= 0x7F): break; // more waits. oh neat c# can do this
                    case byte n when (n >= 0x80 && n <= 0x8F): break; // OPN2 sample write & wait
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
            tb("ExamineVGMData Additional Chip Report vvvvv \n"+detectedchipcodes);
            tb("Good so far. Press any key to continue"); Console.ReadKey();
            return byteflag;
        }


        //! PT3/4 Cont.
        //! Pt A: Preliminary: Smash all DR/AR etc, change all ALG...
        //! find 3-byte KeyOn command -> search backwards to find other OPs (CLOSEST match or whatever's left in memory) 
        //! -> pick lowest mult ->  mute all other operators -> handle detune according to settings
        static void AutoTrigger(FMchannel FMx, Arguments FMargs, byte[] data, bool[] byteflag, int startVGMdata, int endVGMdata) {
            int detunesetting = FMargs.detunesetting;
            bool altwaveform = FMargs.altwave;
            int forceop = FMargs.forceop;
            int forcemult = 99;  // forcemult 99 disabled
            byte subtractmult = 0;
            if (FMargs.forcemult > 0){
                forcemult = FMargs.forcemult;
            } else if (FMargs.forcemult < 0){
                subtractmult = Convert.ToByte(Math.Abs(FMargs.forcemult) );
            }

            if (detunesetting > 24 || detunesetting < 0){ // if out of range set to default. Filename will still be wrong
                detunesetting=0;
            }
            if (detunesetting > 14 && detunesetting < 21){ // atm there is no DT choice alg between these values
                detunesetting=0;
            }
            
            byte altwavemodulation = 0x1F; //hardcode level of 2-op modulation in altwaveform mode...
            byte outvolume = 0x0A; //hardcode carrier out level - Altwave and not altwave too
            // byte carrierSR

            //* PT 1 - global changes
            bool flag4=false, flag3=false, flag2=false, flag1=false;    
            bool flag40=false, flag30=false, flag20=false, flag10=false; // flag operators as found their patch data
            if (FMx.operators==2){ // *smash feedback & algorithm (alg 4?)
                FindAndReplaceSecond4Bit(FMx.chip, FMx.ALG, 0x00, data, byteflag, startVGMdata, endVGMdata); // "connect" / feedback. -- ?
                // FindAndReplaceByte(FMx.chip, FMx.ALG, 0x00, data, byteflag, startVGMdata, endVGMdata); // OPL2 FM 1 / Feedback 0   -- either should be fine?
            } else if (FMx.chip==0x54) {  
                FindAndReplaceByte(FMx.chip, FMx.ALG, 0xC4, data, byteflag, startVGMdata, endVGMdata);    // On OPM the first 2 bits are for stereo. 00 will mute!
                // FindAndReplaceByte(FMx.chip, FMx.ALG, 0xC7, data, byteflag, startVGMdata, endVGMdata);    // On OPM the first 2 bits are for stereo. 00 will mute!
            } else {                                                                    
                FindAndReplaceByte(FMx.chip, FMx.ALG, 0x04, data, byteflag, startVGMdata, endVGMdata); // Feedback/alg to 0/4
                // FindAndReplaceByte(FMx.chip, FMx.ALG, 0x07, data, byteflag, startVGMdata, endVGMdata); // Feedback/alg to 0/7
            }

            foreach (byte b in FMx.AR){ //* smash Attack, Decay, Release
                    if (FMx.operators == 2) {
                        FindAndReplaceByte(FMx.chip, b, 0xF0, data, byteflag, startVGMdata, endVGMdata);   // OPL2: Attack F / Decay 0
                    } else {
                        FindAndReplaceByte(FMx.chip, b, 0x1F, data, byteflag, startVGMdata, endVGMdata);   // smash Attack to 1F
                    }
            }
            foreach (byte b in FMx.DR){
                    // FindAndReplaceByte(FMx.chip, b, 0x00, data, byteflag, startVGMdata, endVGMdata, 0x1f);   // smash Decay - if <= threshold 1f (F is max) not necessary really anymore since byte flags were added
                    FindAndReplaceByte(FMx.chip, b, 0x00, data, byteflag, startVGMdata, endVGMdata);   // smash Decay - if <= threshold 1f (F is max)
            }
            if (FMx.operators==4){ // SR, but not for OPL. OPL2 only has 4 envelope values? they're split like AR/DR  SR/RR
                if (FMx.chip == 54) { // OPM
                    foreach (byte b in FMx.SR){ // OPM SR might be a 2/6 split?
                        FindAndReplaceSecond6Bit(FMx.chip, b, 0x00, data, byteflag, startVGMdata, endVGMdata);
                    }
                } else { // OPNs
                    foreach (byte b in FMx.SR){ // full byte for OPNs
                        FindAndReplaceByte(FMx.chip, b, 0x00, data, byteflag, startVGMdata, endVGMdata);
                    }
                }
            }
            foreach (byte b in FMx.RR){
                    // FindAndReplaceByte(FMx.chip, b, 0x00, data, byteflag, startVGMdata, endVGMdata, 0x1f);   // smash Release
                    FindAndReplaceByte(FMx.chip, b, 0x00, data, byteflag, startVGMdata, endVGMdata);   // smash Release
            }

            if (FMx.operators==2) { //* smash TL and some other values
                    FindAndReplaceSecond4Bit(FMx.chip, FMx.op1_waveform, 0x00, data, byteflag, startVGMdata, endVGMdata); // waveform op1 to sine
                    FindAndReplaceSecond4Bit(FMx.chip, FMx.op2_waveform, 0x00, data, byteflag, startVGMdata, endVGMdata); // waveform op2 to sine
                    flag3=true;flag4=true;flag30=true;flag40=true; // OPL2: ignore operators #3 and #4
                    forceop=2;                                     // OPL2: always use OP#2 for carrier
                    if (altwaveform) {                                 //vvv vvv set these at top of function
                        // FindAndReplaceSecond6Bit(FMx.chip, FMx.op1_TL, altwavemodulation, data, byteflag, startVGMdata, endVGMdata); // it's fine to nuke key scaling
                        // FindAndReplaceSecond6Bit(FMx.chip, FMx.op2_TL, outvolume, data, byteflag, startVGMdata, endVGMdata); // Key Scaling(2bit)/TL(6bit).
                        FindAndReplaceByte(FMx.chip, FMx.op1_TL, altwavemodulation, data, byteflag, startVGMdata, endVGMdata); // maybe key scale affects no fnum, but amplitude?
                        FindAndReplaceByte(FMx.chip, FMx.op2_TL, outvolume, data, byteflag, startVGMdata, endVGMdata); // this smashes everything to KEYSCALE=0

                    } else { // if just SINE WAVE, mute modulator (3f = 64 = muted)
                        FindAndReplaceSecond6Bit(FMx.chip, FMx.op1_TL, 0x3F, data, byteflag, startVGMdata, endVGMdata); // Key Scaling(2bit)/TL(6bit).
                        FindAndReplaceSecond6Bit(FMx.chip, FMx.op2_TL, 0x00, data, byteflag, startVGMdata, endVGMdata); // Key Scaling(2bit)/TL(6bit).
                        // FindAndReplaceByte(FMx.chip, FMx.op1_TL, 0x3F, data, byteflag, startVGMdata, endVGMdata); // maybe key scale affects no fnum, but amplitude?
                        // FindAndReplaceByte(FMx.chip, FMx.op2_TL, 0x00, data, byteflag, startVGMdata, endVGMdata); // this smashes everything to KEYSCALE=0


                    }
                    // FindAndReplaceSecondBit(FMx.chip, FMx.op1_DTML, 0x00, data, byteflag, startVGMdata, endVGMdata); // having issues with first operator vibrato..
                    FindAndkillFirstTwoBits(FMx.chip, FMx.op1_DTML, 0b00000000, data, byteflag, startVGMdata, endVGMdata); // binary input does nothing set AM and Vibrato to 0
                    // bool debug = FindAndkillFirstTwoBits(FMx.chip, FMx.op1_DTML, 0b00000000, data, byteflag, startVGMdata, endVGMdata); // TODO binary input does nothing set AM and Vibrato to 0
                    // if (debug) {
                    //     tb("debug "+byteflag[0x17832]+", "+data[0x17832]+" "+data[0x17832+1]+" "+data[0x17832+2]);
                    //     Console.ReadKey();
                    // }

            } else if (FMx.operators==4) {
                forceop=4; // TODO make a workaround for ch3 mode (maybe use ALG 5?) but for now I want everything on 4 so dynamic TL changes won't break it.
                FindAndReplaceByte(FMx.chip, FMx.op1_TL, 0x7f, data, byteflag, startVGMdata, endVGMdata); // mute TL
                FindAndReplaceByte(FMx.chip, FMx.op2_TL, 0x7f, data, byteflag, startVGMdata, endVGMdata); // mute TL
                if (altwaveform) {
                    FindAndReplaceByte(FMx.chip, FMx.op3_TL, altwavemodulation, data, byteflag, startVGMdata, endVGMdata); // default mod freq 0x1d
                } else {
                    FindAndReplaceByte(FMx.chip, FMx.op3_TL, 0x7f, data, byteflag, startVGMdata, endVGMdata); // mute TL
                }
                FindAndReplaceByte(FMx.chip, FMx.op4_TL, outvolume, data, byteflag, startVGMdata, endVGMdata); // not too loud to avoid clipping OPM maybe?
            }


            //* pt2: find keydown, search backwards, pick lowest mult, mute everything else

            // string os="", s="", s2=""; // for debug
            int c = 0; // just keeps track of how many patches are edited
            int fails = 0; // keep track of failures to find patches to prevent infinite loop
            int i, ii; byte[] mults = new byte[FMx.operators]; 
            int[] mult_index = new int[FMx.operators];
            byte[] TLs = new byte[FMx.operators];
            int[] TL_index = new int[FMx.operators];

            int startfrom=startVGMdata; 
            int lastknownidx=0; // to speed things up. Put this on the index that finds op1 DT/ML
            int totalpatches=0; // just for display
            for (i=startVGMdata; i<data.Length-2; i++) { // make an initial count of the total keyon commands to show progress
                // if (data[i]==FMx.keyon[0] && data[i+1]==FMx.keyon[1] && data[i+2]==FMx.keyon[2]) {
                if (FMx.FoundKeyDownCommand(FMx.operators, i, data, byteflag) ) { // new function that supports OPL2. Returns True if keyon found at index.
                    totalpatches++; // just for debug/display
                }
            }
            // tb("debug:"+totalpatches); System.Console.ReadKey(); // pause - debug

            // tb(FMx.name+": Beginning KEYON search. Looking for keys- "+
            // Convert.ToString(FMx.keyon[0],16) + Convert.ToString(FMx.keyon[1],16) + Convert.ToString(FMx.keyon[2],16) );
            for (i=startfrom; i<data.Length-2; i++) { //* Go forward through data and find keydown commands

                // if (data[i]==FMx.keyon[0] && data[i+1]==FMx.keyon[1] && data[i+2]==FMx.keyon[2]) {
                if (FMx.FoundKeyDownCommand(FMx.operators, i , data, byteflag) ) { // new function that supports OPL2. Returns True if keyon found at index.
                    // tb(FMx.name+": ...KeyOn Found @ 0x"+Convert.ToString(i+2,16));
                    //* once keyon found, loop back a bit to find operator TL and DT/ML values
                    if (lastknownidx>(startVGMdata+16) ) {      //*if lastknownidx (uses OP#1 DT/ML) is found, start there 
                        startfrom=lastknownidx-16; // back 16 bytes just in case there's DT/ML or TL data right behind it. This will always pick the last values it finds, but this might speed things up.
                    } else { // 
                        startfrom=startVGMdata;
                        // os+=("\nSEARCHING FROM BEGINNING OF FILE...");
                    }
                    for (ii = startfrom; ii < i; ii++) {
                        startfrom=ii;
                        // tb("????? @ 0x"+Convert.ToString(ii,16));
                        // if (byteflag[ii] && byteflag[ii+1] && byteflag[ii+2]) { // old, now we only flag the first byte
                        if (byteflag[ii]) { // * only look for flagged bytes, flagged from ExamineVGMData
                            // tb("flagged @ 0x"+Convert.ToString(ii,16));
                            if (data[ii]==FMx.chip && data[ii+1]==FMx.op1_DTML) {       //* operator 1 DT/ML
                                mults[0] = data[ii+2]; mult_index[0]=ii+2;   // save mult value
                                flag1=true; lastknownidx=ii;
                                // tb(FMx.name+": ...DT/ML op1 ("+cts(FMx.chip,16)+cts(FMx.op1_DTML,16)+") 0x"+ cts(mults[0],16)+" found @ 0x" + Convert.ToString(ii+2,16) );

                            }
                            if (data[ii]==FMx.chip && data[ii+1]==FMx.op2_DTML) {       //* operator 2 DT/ML
                                // if (flag2 && ii > i) exitif; // discard forward looking patches? never implemented
                                mults[1] = data[ii+2]; mult_index[1]=ii+2;   // save mult values
                                flag2=true;
                                // tb(FMx.name+": ...DT/ML op2 ("+cts(FMx.chip,16)+cts(FMx.op2_DTML,16)+") 0x"+cts(mults[1],16)+" found @ 0x" + Convert.ToString(ii+2,16) );
                            }
                            if (FMx.operators == 4) {
                                if (data[ii]==FMx.chip && data[ii+1]==FMx.op3_DTML) {       //* operator 3 DT/ML (in 4-op chips this usually comes after op#1)
                                    mults[2] = data[ii+2]; mult_index[2]=ii+2;   // save mult values
                                    flag3=true;
                                    // tb(FMx.name+": ...DT/ML op3 ("+cts(FMx.chip,16)+cts(FMx.op3_DTML,16)+") 0x"+cts(mults[2],16)+" found @ 0x" + Convert.ToString(ii+2,16) );
                                }
                                if (data[ii]==FMx.chip && data[ii+1]==FMx.op4_DTML) {       //* operator 4 DT/ML
                                    mults[3] = data[ii+2]; mult_index[3]=ii+2;  // save mult values
                                    flag4=true;
                                    // tb(FMx.name+": ...DT/ML op4 ("+cts(FMx.chip,16)+cts(FMx.op4_DTML,16)+") 0x"+cts(mults[3],16)+" found @ 0x" + Convert.ToString(ii+2,16) );
                                }
                            }

                                // As TL is the only way to change volume, it is not fixed to the patch. <- this is old
                                // Whereas ML/DT values are unlikely to change without changing the whole patch with them
                            // if (data[ii]==FMx.chip && data[ii+1]==FMx.op1_TL) {       //* operator 1 TL
                            if (IsVolCommand(data[ii], data[ii+1], data[ii+2], FMx.chip, FMx.op1_TL, 1, ii) ) { // TODO byte flag stuff is unnecessary -???
                                TLs[0] = data[ii+2];  TL_index[0]=ii+2;   // save mult value
                                flag10=true;
                                // tb(FMx.name+": ...  TL  op1 ("+cts(FMx.chip,16)+cts(FMx.op1_TL,16)+") 0x"+cts(mults[0],16)+" found @ 0x" + Convert.ToString(ii+2,16) );
                            }
                            // if (data[ii]==FMx.chip && data[ii+1]==FMx.op2_TL) {       //* operator 2 TL
                            if (IsVolCommand(data[ii], data[ii+1], data[ii+2], FMx.chip, FMx.op2_TL, 2, ii) ) {
                                TLs[1] = data[ii+2];  TL_index[1]=ii+2;   // save mult value
                                flag20=true;
                                // tb(FMx.name+": ...  TL  op2 ("+cts(FMx.chip,16)+cts(FMx.op2_TL,16)+") 0x"+cts(mults[1],16)+" found @ 0x" + Convert.ToString(ii+2,16) );
                            }
                            // if (FMx.operators == 4) {// if (data[ii]==FMx.chip && data[ii+1]==FMx.op3_TL) {       //* operator 3 TL
                            if (IsVolCommand(data[ii], data[ii+1], data[ii+2], FMx.chip, FMx.op3_TL, 3, ii) ) {
                                TLs[2] = data[ii+2];  TL_index[2]=ii+2;   // save mult value
                                flag30=true;
                                // tb(FMx.name+": ...  TL  op3 ("+cts(FMx.chip,16)+cts(FMx.op3_TL,16)+") 0x"+cts(mults[2],16)+" found @ 0x" + Convert.ToString(ii+2,16) );
                            }
                            // if (data[ii]==FMx.chip && data[ii+1]==FMx.op4_TL) {       //* operator 4 TL
                            if (IsVolCommand(data[ii], data[ii+1], data[ii+2], FMx.chip, FMx.op4_TL, 4, ii) ) {
                                TLs[3] = data[ii+2];  TL_index[3]=ii+2;
                                flag40=true;
                                // tb(FMx.name+": ...  TL  op4 ("+cts(FMx.chip,16)+cts(FMx.op4_TL,16)+") 0x"+cts(mults[3],16)+" found @ 0x" + Convert.ToString(ii+2,16) );
                            }
                            if (FMx.operators == 2) { flag3=true; flag4=true; flag30=true; flag40=true;} // quick and dirty OPL support
                            // tb("ii lookback loop has looped #" + debug + " times" + flag1 + flag2 + flag3 + flag4);
                        }
                    }

                    // at this point we have looped back to our found KeyOn point
                    // If we we have complete patch data (flag1-2-3 etc), modify patches then continue the loop until i = end of file
                    // otherwise try searching from further back. This might infinite loop?
                    // if (i >= 0x2EBC) Console.ReadKey(); // debugging
                    if (flag4 && flag3 && flag2 && flag1
                        && flag40 && flag30 && flag20 && flag10) { 
                        c++;
                        // os+=      // print ending
                        // s=""; s2=FMx.name+": TOTAL LEVEL:"; 
                        // for (int x = 0; x < mults.Length; x++){ 
                        //     s+="op"+(x+1)+":0x"+cts(mults[x],16);
                        //     s2+="op"+(x+1)+":0x"+cts(TLs[x],16);
                        // }
                        // os+=("\n"+s+"\n"+s2);
                        
                        // tb(FMx.name+": DETUNE/MULT:"+"op1:0x"+cts(mults[0],16)+" op2:0x"+cts(mults[1],16)+" op3:0x"+cts(mults[2],16)+" op4:0x"+cts(mults[3],16) +
                        // " : TOTAL LEVEL:"+"op1:0x"+cts(TLs[0],16)+" op2:0x"+cts(TLs[1],16)+" op3:0x"+cts(TLs[2],16)+" op4:0x"+cts(TLs[3],16) );

                        //* handle TLs -------------------------
                        int mask = ReturnLowestMultIDX(mults, detunesetting); // * returns index, 0-3. If there are matches, always favors OP#4>2>3>1)  



                        if (!altwaveform){ // sine wave output
                            // determine which of the four operators has the lowest multiplier, then mute the others (we're at Alg 7).
                            // we will only use one operator (at least for now). determine what that is.
                            // os+=("\n"+FMx.name+": The lowest Mult determined to be OP#"+(mask+1)+" - using MULT 0x"+cts(data[mult_index[mask]],16)+"<- dis 4bit" );
                            //! atm - forceop should remain at 4
                            if (forceop>0 && forceop<5) {   //* overwrite mask - force use a specific operator for our sine TL (added this for CH3 modes)
                                data[mult_index[forceop-1]] = data[mult_index[mask]];    //* keep in mind zero-bounds. everything should be forceop-1
                                if (forcemult < 16 && forcemult > 0){
                                    data[mult_index[forceop-1]] = FourToEightCoder(First4Bit(data[mult_index[forceop-1]]),Convert.ToByte(forcemult));
                                }
                                mask = forceop-1;   // this will be our new OP to calc detune with
                                // os+=("\n"+FMx.name+": forceOP enabled: out operator changed to OP#"+forceop+" @ 0x"+mult_index[mask],16);
                            }
                            // s="";
                            for (int iii=0; iii < TLs.Length; iii++){  // mute all operators except our chosen mask'd one
                                if (iii != mask) { // 
                                    if (FMx.operators==2) { // * this does nothing!
                                        // s+="mute op#"+(iii+1)+": "+cts(data[TL_index[iii]-2],16)+" "+ cts(data[TL_index[iii]-1],16)+" "+ cts(data[TL_index[iii]],16)+")<-0bxxFFFF ";
                                        data[TL_index[iii]] = TwoToSixCoder(First2Bit(data[TL_index[iii]]), 0x3F); // last 6 bits -> 63
                                    } else {
                                        // s+="mute op#"+(iii+1)+": "+cts(data[TL_index[iii]-2],16)+" "+ cts(data[TL_index[iii]-1],16)+" "+ cts(data[TL_index[iii]],16)+")<-7f ";
                                        data[TL_index[iii]] = 0x7f;
                                    }
                                }
                            }
                            // os+=("\n"+FMx.name+": "+s);
                        } else {    //* try an alternate waveform. experimental. copy masked data into op 3/4? need to gather DT data first! -- handled below
                            // os+=("\n"+FMx.name+": The Smallest Mult is determined to be OP#"+(mask+1)+" - copying.. but not yet");
                        }
                        
                        //! handle DT -------------------------------
                        if (FMx.operators==2) {
                            // tb(FMx.name+": OPL2 has no detune to handle... continuing...");
                        } else if (detunesetting >= 8){ // take masked Operator set DT based on some algorithm (lowest, highest, 4231, 3142 etc)
                            int dtcopyidx = ReturnDesiredDTIDX(mults, detunesetting, FMx.operators); //* get the index of the DT we want
                            // int newdt = ReturnFirstByteAsInt(mults[dtcopyidx] ); // get the second byte as an int
                            int newdt = First4BitToInt(mults[dtcopyidx] ); // get the second byte as an int (better function)
                            // tb("mults 0x" + cts(mults[dtcopyidx],16)+" newDT="+newdt);
                            data[mult_index[mask]] = ReplaceFirstHalfByte(data[mult_index[mask]], newdt); // replaces first byte of DT/ML with 0 (or 4 it's the same)
                            // Console.WriteLine(FMx.name+": Our New Carrier Is: OP#"+(mask+1)+" @ 0x"+cts(mult_index[mask],16) +
                            //     " "  );
                            // Console.WriteLine(FMx.name+": detunesetting: "+cts(data[mult_index[mask]-2],16)+" "+cts(data[mult_index[mask]-1],16)+
                                // " "+"(should be 5x "+cts(DTML(FMx,mask+1),16)+")... "+cts(data[mult_index[mask]],16)+" -> 0x");

                        } else if (detunesetting<8) { // force DT to forcedtune value
                            // os+=("\n"+FMx.name+": detunesetting: forcing OP#"+(mask+1)+" @ 0x"+Convert.ToString(mult_index[mask],16) +
                            //     " to Detune "+detunesetting  );
                            // Console.WriteLine(FMx.name+": detunesetting: "+Convert.ToString(data[mult_index[mask]-2],16)+" "+Convert.ToString(data[mult_index[mask]-1],16)+
                                // " "+"(should be 5x "+Convert.ToString(DTML(FMx,mask+1),16)+")... "+Convert.ToString(data[mult_index[mask]],16)+" -> 0x");
                                                        
                            data[mult_index[mask]] = ReplaceFirstHalfByte(data[mult_index[mask]], detunesetting); // replaces first byte of DT/ML with 0 (or 4 it's the same)
                            // Console.Write(Convert.ToString(data[mult_index[iii]],16));
                            // Console.WriteLine();
                        } 
                        //! handle mult -------------------------------------
                        if (altwaveform){ //* now that we have TL and ML data settled in for mask idx, copy to op3 and then mute everything else
                            if (FMx.operators==2){ 
                                if (mask == 0){ //if modulator, copy mult to carrier. Leave carrier's existing first four bits ( OPL2 first four bits are AM, Vibrato, KSR, EG. )
                                    data[mult_index[1]] = FourToEightCoder(First4Bit(data[mult_index[1]]), Second4BitMinusMult(data[mult_index[mask]], subtractmult)   ); //
                                } else {   // if carrier, copy mult to modulator... zero out all the other guff(?)
                                    // data[mult_index[0]] = FourToEightCoder(0x00, Second4Bit(data[mult_index[mask]]) );
                                    // data[mult_index[0]] = FourToEightCoder(0x00, Convert.ToByte(Second4Bit(data[mult_index[mask]]) + addmult)  );
                                    data[mult_index[0]] = FourToEightCoder(0x00, Second4BitMinusMult(data[mult_index[mask]], subtractmult)  );
                                }
                                // data[mult_index[0]] = KillFirstTwoBits(data[mult_index[mask]]); data[mult_index[1]] = data[mult_index[mask]]; // keep Am/Vibrato 0 for modulator.
                                // data[mult_index[0]] = data[mult_index[mask]]; data[mult_index[1]] = data[mult_index[mask]];
                                // data[TL_index[2]] = 0x1D; data[TL_index[3]] = 0x00; // modulator -> carrier --- not set up for OPL2 hopefully above code handles it
                            } else {
                                // data[mult_index[3]] = data[mult_index[mask]]; data[mult_index[2]] = data[mult_index[mask]]; // old, pre- addmult
                                data[mult_index[3]] = FourToEightCoder(First4Bit(data[mult_index[mask]]), Second4BitMinusMult(data[mult_index[mask]], subtractmult) ); 
                                data[mult_index[2]] = FourToEightCoder(First4Bit(data[mult_index[mask]]), Second4BitMinusMult(data[mult_index[mask]], subtractmult) );
                                if (forcemult < 16 && forcemult > 0){
                                    data[mult_index[2]] = FourToEightCoder(First4Bit(data[mult_index[2]]),Convert.ToByte(forcemult));
                                    data[mult_index[3]] = FourToEightCoder(First4Bit(data[mult_index[3]]),Convert.ToByte(forcemult));
                                }
                                data[TL_index[2]] = altwavemodulation; data[TL_index[3]] = outvolume; // modulator -> carrier
                                data[TL_index[0]] = 0x7f; data[TL_index[1]] = 0x7f; // mute the rest
                            }

                        }

                        // if (FMx.operators==4 && FMx.chip!=0x54) { // skipping this for OPL2 for now
                        // // if (FMx.operators==4) { // skipping this for OPL2 for now
                        //     tb(FMx.name+": pumping op#"+(mask+1)+" @ 0x"+Convert.ToString(TL_index[mask],16)+
                        //             ": "+Convert.ToString(data[TL_index[mask]-2],16)+" "+Convert.ToString(data[TL_index[mask]-1],16)+""+
                        //             " "+Convert.ToString(data[TL_index[mask]],16)+" < Maxing TL to 0x00)");
                        //     data[TL_index[mask]]=0x00;
                        // }
                        // os+=s+"\n"+FMx.name+": *****PATCH # "+c+"/"+totalpatches+"*****";
                        showprogress= FMx.name+": *****PATCH "+c+"/"+totalpatches+" COMPLETED*****";
                        // Console.WriteLine(os); // log out
                        // os="";
                        // Console.WriteLine(FMx.name+"***************************************");
                        flag1=false;flag2=false;flag10=false;flag20=false;
                        if (FMx.operators==4){flag3=false;flag4=false;flag30=false;flag40=false;}
                        // break;
                        fails=0;

                    } else { //* if failed to find all our DTML and TL operator values... 
                        lastknownidx=0; fails++; i--; //* try again from start. i-- should immediately start ii loop again.
                        tb(FMx.name+": ! no patch changes found @ NoteOn 0x"+Convert.ToString(i,16)+
                        "\n...DTML op1-2-3-4?:"+flag1+flag2+flag3+flag4+"\n...  TL op1-2-3-4?:"+flag10+flag20+flag30+flag40+
                        "\nlooking further back... fails="+fails );
                        // System.Console.ReadKey(); // debug. It's not the end of the world if this trips
                        // startfrom=lastknownidx-16; i--; fails++; // try again from the same keydown
                        // tb("debug: current index "+ Convert.ToString(i,16));
                        if (fails > 1) { // prevents infinite loop
                            tb("patch search failed. if it's really early in the file this might be fine.\npress any key to continue"); 
                            fails=0; i++; flag4=false;flag3=false;flag2=false;flag1=false;
                            flag40=false;flag30=false;flag20=false;flag10=false; c++; //System.Console.ReadKey();
                            }
                    }
                }
            } //i
            // tb("op1:0x"+Convert.ToString(mults[0],16)+" op2:0x"+Convert.ToString(mults[1],16)+
            // " op3:0x"+Convert.ToString(mults[2],16)+" op4:0x"+Convert.ToString(mults[3],16) );
            // tb(FMx.name+" Complete!");
        }
        static int ReturnDesiredDTIDX(byte[] ba, int alg, int numberofops) {     //* this will return the index of an operator who we will clone. alg changes how this is determined
            int[] bDT = new int[numberofops];   
            int[] bmult = new int[numberofops];                   
            // string temp; char[] temp2;
            for (int i = 0; i < ba.Length; i++) {
                // temp = Convert.ToString(ba[i],16);      // convert to string
                // if (temp.Length <2) { temp="0"+temp; }  // add back leading 0 if necessary
                // temp2 = temp.ToCharArray();             // move to char array so it can be split 
                // temp = temp2[0]+"";                    // shave byte to just the first DT part
                // bDT[i] = Convert.ToInt32(Convert.ToByte(temp,16) );        // convert to int. sure we'll use int for this.
                bDT[i] = First4Bit(ba[i]);

                // temp = Convert.ToString(ba[i],16);      // convert to string
                // if (temp.Length <2) { temp="0"+temp; }  // add back leading 0 if necessary
                // temp2 = temp.ToCharArray();             // move to char array so it can be split 
                // temp = ""+temp2[1];                    // shave byte to just the second ML part
                // bmult[i] = Convert.ToInt32(Convert.ToByte(temp,16) );        // convert to int
                bmult[i] = Second4Bit(ba[i]);
                // Console.Write("input:"+Convert.ToString(bmult[i])+" -> "+Convert.ToString(sba[i],16)+" @"+i+".idx ... ");
            }
            // string s=""; for (int ii = 0; ii < numberofops; ii++) {s=s+bDT[ii]+bmult[ii]+" ";}
            // s+="ReturnDesiredDTIDX - DT/MULT in-values:"+s+" (DT is first 4-bit)";

            int outindex = Array.IndexOf(bmult, bmult.Min() ); // returns lowest index - probably 0 (case 8)
            // Console.WriteLine(bmult[0]+" "+bmult[1]+" "+bmult[2]+" "+bmult[3] + " indexof says: "+minindex);
            switch (alg) {  //! 7-6-5-0-1-2-3
                case 8: break; //Lowest mult, favor lowest operator
                case 9: outindex = Array.LastIndexOf(bmult, bmult[outindex]); break; // Lowest mult, favor highest operators
                case 10:                // Lowest mult, favor operators# 4 > 2 > 3 > 1
                    //                          0       1       2       3
                    if (numberofops < 4) break; // this alg doesn't apply to 2OP, leave before we error or something
                    int[] tmp = new int[] {bmult[3], bmult[1], bmult[2], bmult[0]};
                    int x = Array.IndexOf(tmp, tmp.Min() );
                    switch (x) {
                        case 3: outindex=0; break;
                        case 1: outindex=1; break;
                        case 2: outindex=2; break;
                        case 0: outindex=3; break;
                    }
                    break; 
                case 15: outindex = Array.IndexOf(bmult, bmult.Max() ); break;      // Highest mult, favor lowest operator#
                case 16: outindex = Array.LastIndexOf(bmult, bmult.Max() ); break;      // Lowest mult, favor Highest operator#
                case 21: return 0;  // force OP 0. these also exist in ReturnLowestMultIDX, so we will use both the operator and it's DT
                case 22: return 1;  // force OP 1
                case 23: return 2;  // force OP 2
                case 24: return 3;  // force OP 3
                default:        //* cases 11-12-13-14 are Most DT - this requires remapping detune values
                    if (numberofops < 4) break; //* OPL does not have any detune!
                    int[] tmpA = new int[] {bDT[0], bDT[1], bDT[2], bDT[3]}; //, favor highest detune    
                    // foreach (int val in tmpA) { // 7-6-5-0-1-2-3
                    for (int idx = 0; idx < tmpA.Length; idx++) { // 7-6-5-0-1-2-3
                            switch (tmpA[idx]) {  // remap detune values to something more logical for the code to chew on
                                case 7: tmpA[idx]=3; break;
                                case 6: tmpA[idx]=2; break;
                                case 5: tmpA[idx]=1; break;
                                case 0: tmpA[idx]=0; break;
                                case 1: tmpA[idx]=1; break;
                                case 2: tmpA[idx]=2; break;
                                case 3: tmpA[idx]=3; break;
                                case 4: tmpA[idx]=0; break; // DT 4 is equal to 0. rarely seen.
                                default: tmpA[idx]=3; break; // if >7... This shouldn't happen.
                            }
                    }
                    // tb("debug op#s: "+tmpA[0]+" "+tmpA[1]+" "+tmpA[2]+" "+tmpA[3]);
                    switch (alg) {  
                        case 11: outindex = Array.IndexOf(tmpA, tmpA.Max() ); break;    // Most Detune, favor lower operators
                        case 12: outindex = Array.LastIndexOf(tmpA, tmpA.Max() ); break; // Most Detune, favor higher operators
                        case 13: outindex = Array.IndexOf(tmpA, tmpA.Min() ); break;    // Least Detune, favor lower operators
                        case 14: outindex = Array.LastIndexOf(tmpA, tmpA.Min() ); break; // Least Detune, favor higher operators
                        // if default will still be = case 8.
                    }
                    break;
            }
            // Console.WriteLine("Final Output: INDEX="+outindex+" aka "+Convert.ToString(ba[outindex],16) );
            // tb(s+"\nReturnDesiredDTIDX alg="+alg+" result: copy OP#"+outindex+", "+bDT[outindex]+bmult[outindex]);
            // Console.ReadKey();
            return outindex;
        }

        static int ReturnLowestMultIDX(byte[] ba, int alg) { // returns index of second bit-shaved 4-bit byte (in this case probably MULT, as DT/MULT)
            byte[] sba = new byte[ba.Length];                              // if lowest matches, may change based on the alg (this may become irrelevant soon)
            // string temp; char[] temp2;
            for (int i = 0; i < ba.Length; i++) {
                // temp = Convert.ToString(ba[i],16);      // convert to string - this was proposterous way to get a 4-bit value. updated
                // if (temp.Length <2) { temp="0"+temp; }  // add back leading 0 if necessary
                // temp2 = temp.ToCharArray();             // move to char array so it can be split 
                // temp = "0"+temp2[1];                    // shave byte to just the second MULT part
                // sba[i] = Convert.ToByte(temp,16);        // convert back to byte for comparison purposes
                sba[i] = Second4Bit(ba[i]);          // do the above but not stupid
                // Console.Write("input:"+Convert.ToString(ba[i],16)+" -> "+Convert.ToString(sba[i],16)+" @"+i+".idx ... ");
            }

            // int minindex = Array.IndexOf(sba, sba.Min() ); // this works but it returns the lowest index if are matches, which is probably worse
            int minindex = Array.LastIndexOf(sba, sba.Min() ); // highest index - better for OPL2
            // Console.WriteLine(sba[0]+" "+sba[1]+" "+sba[2]+" "+sba[3] + " indexof says: "+minindex);
            // Console.WriteLine(sba[0]+" "+sba[1]+" "+ " indexof says: "+minindex);
            // minindex = Array.LastIndexOf(sba, sba[minindex]);   //* favor higher operators in case of match
            // TODO switch algs here
            // switch (alg) {   // don't do this!! mult must be minimum!!! unless you know what you are doing!!!
            //     case 21: return 0;  // force OP 0
            //     case 22: return 1;  // force OP 1
            //     case 23: return 2;  // force OP 2
            //     case 24: return 3;  // force OP 3
            // }
            if (ba.Length>2) {             //* weird test. idea here is to favor operator# 4 > 2 > 3 > 1
            //                          0       1       2       3
            byte[] sbf = new byte[] {sba[3], sba[1], sba[2], sba[0]};
            int x = Array.IndexOf(sbf, sbf.Min() );
            switch (x) {
                case 3: minindex=0;break;
                case 1: minindex=1;break;
                case 2: minindex=2;break;
                case 0: minindex=3;break;
            }
            }

            // 
            // Console.WriteLine("Final Output: INDEX="+minindex+" aka "+Convert.ToString(ba[minindex],16) );
            // Console.ReadKey();
            return minindex;
        }

        // static int ReturnFirstByteAsInt(byte ba) { // just return first 4-bit value as int.  nothing fancy.
        //     string temp; char[] temp2;  // remember it's DT|ML
        //     temp = Convert.ToString(ba,16);         // convert to hex string
        //     if (temp.Length <2) { temp="0"+temp; }  // add back leading 0 if necessary (is this necessary at all?)
        //     temp2 = temp.ToCharArray();             // move to char array so it can be split 
        //     temp = temp2[0]+"";                     // grab first part. Whats up with chars in c#?
        //     byte b = Convert.ToByte(temp,16);
        //     // Console.Write(Convert.ToString(b,16) );
        //     return Convert.ToInt32(b);        // return int
        //     // Console.Write("input:"+Convert.ToString(ba[i],16)+" -> "+Convert.ToString(sba[i],16)+" @"+i+".idx ... ");

        // }


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
        static byte Second4Bit(byte b){return (byte)(b & 0x0F); }
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


        static void debugstart() {
            int debug = 0;
            if (debug < 1) return;
            byte b1=0xFA, b2=0x75, b3=0xBB;

            // tb(GetBinary(b1)+" "+GetBinary(b2)+" "+ GetBinary(b3) );

            string s="";
            s+="default val:";
            s+="b1:0"+SecondBit(b1)+"000000 b2:0"+SecondBit(b2)+"000000 b3:0"+SecondBit(b3)+"000000\n";
            s+="Default ok :";
            s+="b1:"+GetBinary(b1)+" b2:"+GetBinary(b2)+" b3:"+GetBinary(b3)+" \n";
            s+="Set to One :";
            s+="b1:"+GetBinary(CodeSecondBit(b1,0x00) ) +" b2:"+GetBinary(CodeSecondBit(b2,0x00))+" b3:"+GetBinary(CodeSecondBit(b3,0x00))+" \n";
            s+="Set to Zero:";
            s+="b1:"+GetBinary(CodeSecondBit(b1,0xC0) ) +" b2:"+GetBinary(CodeSecondBit(b2,0xC0))+" b3:"+GetBinary(CodeSecondBit(b3,0xC0))+" \n";
            tb(s+"");



            if (debug==1) System.Console.ReadKey();
            // byte b1=0x96;
            // byte b2=0xFF;
            // int i=255;
            // byte pt1 = First2Bit(b1);
            // byte pt2 = Second6Bit(b1);
            // tb("0x"+cts(b1,16)+" pt1:"+cts(First2Bit(b1),16)+" pt2:"+cts(Second6Bit(b1),16) );
            // tb("0b"+GetBinary(b1)+"\n0b"+GetBinary(First2Bit(b1))+"\n0b"+GetBinary(Second6Bit(b1)) );
            // tb("0b"+GetBinary(TwoToSixCoder(pt1,pt2)));
            // System.Console.ReadKey();

            // byte b1=0x96;
            // // byte b2=0xFF;
            // // int i=255;
            // byte pt1 = First2Bit(b1);
            // byte pt2 = Second6Bit(b1);
            // tb("0x"+cts(b1,16)+" pt1:"+cts(First2Bit(b1),16)+" pt2:"+cts(Second6Bit(b1),16) );
            // tb("0b"+GetBinary(b1)+"\n0b"+GetBinary(First2Bit(b1))+"\n0b"+GetBinary(Second6Bit(b1)) );
            // tb("0b"+GetBinary(TwoToSixCoder(pt1,pt2)));
            // System.Console.ReadKey();
            // byte b = 0x0A;
            // byte c = 0xAA;
            // byte b1,b2;
            // tb(cts(b1=First4bit(b),16) );
            // tb(cts(b2=Second4bit(c),16) );
            // tb(cts(FourToEightCoder(b1,b2),16) );

            // return;

            // byte b = 0x9A;              // bitwise operation - get 2nd 6-bit value (erase last six bits) 
            // byte c = (byte)(b << 2); 
            // c = (byte)(c >> 2);
            // tb(cts(c,16) );

            // // byte d = 0x9A;              // bitwise operation - get 1st 2-bit value (erase first two bits)
            // byte e = (byte)(b >> 6);
            // // e = (byte)(e << 6);
            // tb(cts(e,16));

            // // for TL, these have to be recombined

            // // byte x = (byte)(c&e);
            // tb(cts((byte)(e|c),16) );   // Binary OR Operator copies a bit if it exists in either operand.


            // tb(""+tmp);
            // byte b = 0x5A;
            // byte half1 = (byte)(b & 0x0F);
            // byte half2 = (byte)(b >> 4);
            // tb(half1 + " " + half2);
            // tb(R1BAI(0x12)+" "+)
            // string s=""; byte[] ba = new byte[] {0x00, 0xFF, 0xA1, 0x56, 0xF0, 0x5A};
            // for (int i = 0; i < ba.Length; i++) {
            //     s=s+"0x"+Convert.ToString(ba[i],16)+"="+First4bitToInt(ba[i])+Second4bitToInt(ba[i])+" ";
            //     s=s+"0x"+Convert.ToString(ba[i],16)+"="+First4bit(ba[i])+Second4bit(ba[i])+" ";
            // }
            // tb(s);
            // System.Console.ReadKey();
        }

        // todo vvv this is really old
        static byte ReplaceFirstHalfByte(byte xa, int dt){  // for setting DT. in:byte, int DT value (0-7)
            string s = dt.ToString();   // ...convert detune value to string here       
            string temp;        // TODO make a better function for this. test fourToEightCoder
            //* reconstitute half-byte
            temp = Convert.ToString(xa,16);   // convert byte to string                    
            if (temp.Length < 2) { temp = "0"+temp; } // if byte starts with a 0, a 0 will need to be added back
            char[] temp2 = temp.ToCharArray();      // split byte in half using char                                    
            temp = s + temp2[1];                    // DT + Freq Multiplier                     
            byte b = Convert.ToByte(temp, 16);      // recombine into 8-bit value

            // tb("ReplaceFirstHalfByte: converted 0x" + Convert.ToString(xa,16) +
            // " -> 0x"+Convert.ToString(b,16)+" ");
            return b;                           //* <- Actual modification is done here
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
            int i; int c=0; //string s="";// TODO c is not used for anything
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











