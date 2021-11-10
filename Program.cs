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


// 04 - major refactor, ALTWAVE removed, some DT algs removed, wip

// 03 changelog

// removed subtractmult. It barely worked anyway


// TODO
// is this turning off the LFO on OPNA? ...Should it do that?
// make better bitwise functions
// it might be better to make a separate program for handing CH3 mode and always force last operator

/*
        LIMITATIONS
    "Mult sweeps" will break things
    OPL2 AM algorithm *might work*. Untested
    OPL2 drum mode channels probably won't work and might break note detection
*/

namespace EXTT

{
    public partial class Program {
        static int VERSIONMAJOR = 0, VERSIONMINOR=4;
        
        public delegate void WriteDelegate(string msg, params object[] args); // shortcut commands
        public static readonly WriteDelegate tb = Console.WriteLine; 
        delegate string WriteDelegate2(byte msg, int tobase);
        private static readonly WriteDelegate2 cts = Convert.ToString;
        
        // Settings - per channel and global - are contained via class 'Arguments'
        static Arguments GlobalArguments = new Arguments( 10, 4, 99, 0, "FMG"); // args: detunesetting, forceop, forcemult, altwaveform. These will be copied to unset values
        static Arguments FM0Args = new Arguments(99,99,99,99,"FM0"); static Arguments FM1Args = new Arguments(99,99,99,99,"FM1"); // 99 is unset yes this is goofy
        static Arguments FM2Args= new Arguments(99,99,99,99,"FM2"); static Arguments FM3Args= new Arguments(99,99,99,99,"FM3");
        static Arguments FM4Args= new Arguments(99,99,99,99,"FM4"); static Arguments FM5Args= new Arguments(99,99,99,99,"FM5");
        static Arguments FM6Args= new Arguments(99,99,99,99,"FM6"); static Arguments FM7Args= new Arguments(99,99,99,99,"FM7");
        static Arguments FM8Args= new Arguments(99,99,99,99,"FM8"); 
        static int chiptype=0; // 0 = auto // don't touch this. 

        static Dictionary<string, Arguments> GetChannel = new Dictionary<string, Arguments>(); // ex. key/pair: "FM0" FM0channel
        static FMchannel FM0 = new FMchannel(); static FMchannel FM1 = new FMchannel(); static FMchannel FM2 = new FMchannel();
        static FMchannel FM3 = new FMchannel(); static FMchannel FM4 = new FMchannel(); static FMchannel FM5 = new FMchannel(); 
        static FMchannel FM6 = new FMchannel(); static FMchannel FM7 = new FMchannel(); static FMchannel FM8 = new FMchannel();
        public static string LostPatchLog=""; // collects all lost patches logged by ReportLostPatches / ReturnLostPatches

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
Available options (4operator FM): DT(def 0), ForceMult(def disabled)
Available options (2operator OPL2): ForceMult(def disabled)
Advanced options: Patch [PatchKey] - 4-op FMs only, applies DT and ForceMult on a patch-by-patch basis
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
            // tb("exit now please");
            // Console.ReadKey();
            // Console.WriteLine("Main Initiated");
            debugstart(); // jump to a debug func for messing around
            if (args.Length > 0 && File.Exists(args[args.Length-1]) ) {
                // tb("arg0: {0}",args[args.Length-1]); Console.ReadKey();
                


                string filename = args[args.Length-1].ToString();             
                // tb("filename: {0}",filename); Console.ReadKey();

                byte[] data;
                data = File.ReadAllBytes(filename);
                if (data[0]!=0x56 && data[1]!=0x67 && data[2]!=0x6D) { // V G M 
                    tb("Error: Invalid File (VGM identifier not found)");      
                    System.Threading.Thread.Sleep(TimeSpan.FromSeconds(2));
                    Environment.Exit(1);
                }
                // if (data.Length < 0xff) {tb("Error: Invalid File (too few bytes): "+data.Length);Console.ReadKey();} // probably unnecessary now



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
                }   else if (Get32BitInt(data,0x4C) > 0){
                    chiptype=58; tb("Chip Detection: 0x4C clockrate: "+Get32BitInt(data,0x4C)+" YM2610 OPNB found"); 
                }  else if (Get32BitInt(data,0x50) > 0){
                    chiptype=510; tb("Chip Detection: 0x50 clockrate: "+Get32BitInt(data,0x50)+" YM3812 OPL2 found"); 
                }  //               System.Console.ReadKey();

                SetupData(chiptype);  //* PART 1/4: INITIAL COMMAND SETUP (Data.cs)

                //* PART 2/4: SCAN THROUGH DATA BYTE-BY-BYTE, FLAGGING FM COMMANDS THAT ARE SAFE TO EDIT
                bool[] WaitFlags = new bool[endVGMdata];
                bool[] byteflag = ExamineVGMData(data, FM0.chip, startVGMdata, endVGMdata, ref WaitFlags);

                // tb("0x"+cts(data[0x2ebc],16)+"... 0x2EBC:"+ byteflag[0x2ebc]);
                // tb(FM0.keyon[0]+" "+FM0.keyon[1]);
                // Console.ReadKey();

                //* PART 3/4  - main loop -
                //* pt1: Blanket edits across the board (for example removing all AR/DR/RS, channel feedback to 0, channel algorithms to 7)
                //* pt2: Find keyOn events and trace backwards to find patches, then edit them to our liking (mute operators, decide which detune value to use based on our settings, etc)

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

                ProgressTimer.Stop(); // stop timer
                tb("\n"+LostPatchLog);

                //* PART 4/4 - write new file
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



            } else {
                tb("No file found @" +args[args.Length]); Console.ReadKey();
            }
        }



        public class Arguments { // contains global & per channel settings to be fed into main loop
            public int detunesetting, forceop, forcemult, altwaveform; // todo altwaveform bool conversion
            public string name;// {get; set;}

            public List<FMpatchkey> PatchKeys = new List<FMpatchkey>();
            public bool LookForPatchKeys=false;

            public List<int[]> LostPatches = new List<int[]>();
            public List<int> LostPatchCnt = new List<int>();

            public void AddLostPatch(int[] input) { // log unfound patches
                bool newpatch=true;
                int idx=0;
                foreach (int[] existingpatch in LostPatches){
                    if (existingpatch.SequenceEqual(input) ){
                        // LostPatchCnt[LostPatches.IndexOf(existingpatch)]+=1; // unsure of this
                        LostPatchCnt[idx]+=1; // hm
                        newpatch=false;
                        break;
                    }
                    idx++;
                }
                if (newpatch){
                    LostPatches.Add(input);
                    LostPatchCnt.Add(1);
                }
            }

            public void ReportLostPatches(){
                if (LostPatches.Count == 0){return;}
                tb(this.name+": ---- Lost patch report ("+LostPatches.Count+" / "+LostPatchCnt.Count+")---- " );
                // tb(" mult    /  dt     ");
                // tb("14-1-0-0 / 15-3-0-0");

                string s="";
                for (int i = 0; i < LostPatches.Count; i++){
                    s+="\""+LostPatches[i][0]+"-"+LostPatches[i][1]+"-"+LostPatches[i][2]+"-"+LostPatches[i][3]+" / "+
                    LostPatches[i][4]+"-"+LostPatches[i][5]+"-"+LostPatches[i][6]+"-"+LostPatches[i][7] + "\" (Count:"+ LostPatchCnt[i]+") applied DT "+this.detunesetting+", mult AUTO\n";
                }
                tb(s);
            }
            public string ReturnLostPatches(){
                if (LostPatches.Count == 0){return "";}
                string s="";
                s+=this.name+" ("+LostPatches.Count+" / "+LostPatchCnt.Count+")"+": ---- Lost patch report ---- "+"dt setting:"+this.detunesetting+" mult:auto"+"\n" ;
                for (int i = 0; i < LostPatches.Count; i++){
                    int? DTalg = this.detunesetting;
                    int DT = ReturnDesiredDT(new int[] {LostPatches[i][0],LostPatches[i][1],LostPatches[i][2],LostPatches[i][3],
                            LostPatches[i][4],LostPatches[i][5],LostPatches[i][6],LostPatches[i][7]}, DTalg);
                    int mult;
                    // tb("forcemult="+this.forcemult);
                    if (this.forcemult < 99) { mult=this.forcemult; } else {
                        mult=HighestCommonFactorINT(new int[] {LostPatches[i][0],LostPatches[i][1],LostPatches[i][2],LostPatches[i][3]} );
                    }
                    // s+="\""+LostPatches[i][0]+"-"+LostPatches[i][1]+"-"+LostPatches[i][2]+"-"+LostPatches[i][3]+" / "+
                    // LostPatches[i][4]+"-"+LostPatches[i][5]+"-"+LostPatches[i][6]+"-"+LostPatches[i][7] + "\" (Count:"+ LostPatchCnt[i]+") applied DT "+DTalg+", mult "+mult+"\n";
                    s+="\""+LostPatches[i][4]+"-"+LostPatches[i][5]+"-"+LostPatches[i][6]+"-"+LostPatches[i][7]+" / "+
                    LostPatches[i][0]+"-"+LostPatches[i][1]+"-"+LostPatches[i][2]+"-"+LostPatches[i][3] + "\" (Count:"+ LostPatchCnt[i]+") out dt:"+DT+" mult:"+mult+"\n";
                }
                return s;
            }



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
                bool ParsePatchKeys=false;
                switch (property){
                    case "P":ParsePatchKeys=true; break;    // go to next conditional to parse patch data
                    case "PATCH":ParsePatchKeys=true; break;
                    // case "NOPATCH":ParsePatchKeys=true; break; // * maybe rig this up to disable patch detection for certain channels?
                    // case "PATCH":ParsePatchKeys=true
                }

                if (ParsePatchKeys){ // paerser goes here

                    // example syntax: FM0 Parse 15-3-0-0/3-3-3-3_e9_m1
                    // example syntax: FM0 Parse 15-3-0-0/3-3-3-3_dt9_mult1
                    
                    // break down the string into it's broad parts -
                    // 0: patch mults  1: patch DT  2: desired DT algorithm  3: desired mult(if exists)

                    string[] StringSeparators = new string[] {"/","_","e","dt","m","mult","DT","E","M","MULT"};
                    string[] Segments = value.Split(StringSeparators, StringSplitOptions.RemoveEmptyEntries);

                    StringSeparators = new string[] {"-"};
                    // string tmp=argmults[0];
                    string[] valuesMULT = Segments[0].Split(StringSeparators, StringSplitOptions.RemoveEmptyEntries);
                    string[] valuesDT = Segments[1].Split(StringSeparators, StringSplitOptions.RemoveEmptyEntries);
                    string[] valueDESIRED_DT = Segments[2].Split(StringSeparators, StringSplitOptions.RemoveEmptyEntries);

                    //* Note - this does NOT work with negative values (because - is used as a separator...)
                    string[] valueDESIRED_MULT = new string[]{"99"}; // 99 = disabled
                    if (Segments.Length > 3){
                        valueDESIRED_MULT = Segments[3].Split(StringSeparators, StringSplitOptions.RemoveEmptyEntries);
                    }

                    PatchKeys.Add(new FMpatchkey(valuesMULT[0],valuesMULT[1],valuesMULT[2],valuesMULT[3],
                                            valuesDT[0],valuesDT[1],valuesDT[2],valuesDT[3],
                                            valueDESIRED_DT[0], valueDESIRED_MULT[0]));
                    tb(this.name+" ParseValue: Added Patch Key INPUT:"+value+"  ->   OUTPUT:"+PatchKeys[PatchKeys.Count-1].DebugPrint() );
                    this.LookForPatchKeys=true;
                    // Console.ReadKey(); // debug
                    return true;
                }


                int intval;
                if (Int32.TryParse(value, out intval) ) {
                    switch (property){
                        case "DT": this.detunesetting = intval; this.suffix+="DT"+detunesetting; break;
                        case "FORCEOP": this.forceop = intval; this.suffix+="op"+forceop; break;
                        case "MULT": this.forcemult = intval; this.suffix+="Mult"+forcemult; break;
                        case "FORCEMULT": this.forcemult = intval; this.suffix+="Mult"+forcemult; break;
                        case "ADDMULT": this.forcemult = intval; this.suffix+="Mult"+forcemult; break;

                        default: tb("PARSEVALUE: property "+property +"not found"); return false;
                    }
                    // tb ("value good?");
                    return true;
                } else {
                    tb("PARSEVALUE ERROR: COULD NOT PARSE ARGUMENT: "+ value+" "+ParsePatchKeys);
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
                if (this.LookForPatchKeys) s+= "MultiPatch";
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
                if (!this.LookForPatchKeys && GlobalArguments.LookForPatchKeys){
                    this.PatchKeys = GlobalArguments.PatchKeys.Cast<FMpatchkey>().ToList();
                    this.LookForPatchKeys=true;
                    tb("MatchAgainstGlobalValues: Casting FMpatchkey list (Count="+this.PatchKeys.Count()+") to "+this.name);
                }
            }

            public string Report(){ //* just debug
                return this.name+" settings:"
                +" DT"+this.detunesetting
                +" forceOP"+this.forceop
                +" forceMULT"+this.forcemult
                +" AltWave"+this.altwaveform+"\n";

            }
        }

        public static void PrintStringArray(string[] strA){
            string s="";
            for (int i = 0; i < strA.Length; i++){
                s+=i+":"+strA[i]+" ";
            }
            tb("PSA: "+s+ " (L="+strA.Length+")");
        }
        public static void PrintStringArray(int[] strA){
            string s="";
            for (int i = 0; i < strA.Length; i++){
                s+=i+":"+strA[i]+" ";
            }
            tb("PSA: "+s+ " (L="+strA.Length+")");
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

            public bool IsKeyDown(byte[] data, int idx){ // to be used in main loop (simpler version)
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
        static bool[] ExamineVGMData(byte[] data, byte FMchip, int start, int end, ref bool[] WaitFlags) { // v04 added waitflags
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
                        if (FMchip==0x54){ //! bug: this code will never be reached because YM2612 is earlier in the header 
                            if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte Additional OPN2 command
                        } else { toif=true; break;} // send OPM to next conditional

                    //* skip wait commands, samples & OPN2 DAC commands
                    case 0x61: WaitFlags[i]=true; i+=2; break; // three-byte wait
                    case 0x62: WaitFlags[i]=true; break;
                    case 0x63: WaitFlags[i]=true; break;
                    case 0x66: i=end; tb("end reached @ 0x"+i); break; // end of sound data
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
                        case 0x52: detectedchipcodes+="OPN2 repurposed for DAC ("+Convert.ToString(i,16)+") @ 0x"+Convert.ToString(chips[i],16)+"\n"; break; // TODO will never reach
                    }
                }
            }
            // double pcnt = (Convert.ToUInt64(c*3))/(Convert.ToUInt64(end-start));
            // decimal pcnt = Decimal.Divide((c*3),(end-start));//(Convert.ToUInt64(c*3))/(Convert.ToUInt64(end-start));
            tb("ExamineVGMData: scanned "+ (end-start)+" bytes, found "+c+" FM commands. Total bytes / command-related-bytes: %"+ Decimal.Divide((c*3),(end-start))*100 );
            tb("ExamineVGMData Additional Chip Report vvvvv \n"+detectedchipcodes);
            // tb("Good so far. Press any key to continue"); Console.ReadKey();
            tb("continuing...");
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(1));
            return byteflag;
        }


        public struct FMpatchkey{ // ex. 15-3-0-0 / 3-3-3-3
            public byte mult1, mult2, mult3, mult4, dt1, dt2, dt3, dt4; // todo make private?
            public int desiredDTalg;
            public int desiredmult;

            public FMpatchkey(string m1, string m2, string m3, string m4, string d1, string d2, string d3, string d4, string alg, string mult){
                PrintStringArray(new string[]{m1,m2,m3,m4,d1,d2,d3,d4,alg,mult}); // debug
                mult1=Convert.ToByte(m1);
				mult2=Convert.ToByte(m2);
				mult3=Convert.ToByte(m3);
				mult4=Convert.ToByte(m4);
                dt1=Convert.ToByte(d1); 
				dt2=Convert.ToByte(d2);
				dt3=Convert.ToByte(d3);
				dt4=Convert.ToByte(d4);
				desiredDTalg = Convert.ToInt32(alg);
                if (Convert.ToByte(mult) < 16) {
                    this.desiredmult=Convert.ToInt32(mult);
                } else {
                    this.desiredmult=Convert.ToByte(99); // disabled
                }
            }

            public bool MatchesValues(byte[] a){ // byte input - ML then DT
                byte[] tmp = new byte[]{mult1, mult2, mult3, mult4, dt1, dt2, dt3, dt4};
                // tb("MatchesValue: "+a.Length+" = "+tmp.Length);
                return a.SequenceEqual(tmp);
            }
            public bool MatchesValues2(int[] a, int operators){ // byte input - ML then DT
                int[] tmp;
                if (operators==2) {
                    tmp = new int[]{mult1, mult2, mult3, mult4};
                } else {
                    tmp = new int[]{mult1, mult2, mult3, mult4, dt1, dt2, dt3, dt4};
                }
                // tb("MatchesValue: "+a.Length+" = "+tmp.Length);
                return a.SequenceEqual(tmp);
            }
            public bool MatchesValues(byte m1, byte m2, byte m3, byte m4, byte d1, byte d2, byte d3, byte d4){ // byte input - ML then DT
                byte[] input = new byte[]{m1, m2, m3, m4, d1, d2, d3, d4};
                byte[] key = new byte[]{mult1, mult2, mult3, mult4, dt1, dt2, dt3, dt4};
                // tb("MatchesValue: "+a.Length+" = "+tmp.Length);
                return input.SequenceEqual(key);
            }
            

            public string DebugPrint(){
                string s=""; if (desiredmult < 16) {s="mult"+desiredmult;}
                return(mult1+"-"+mult2+"-"+mult3+"-"+mult4+"/"+dt1+"-"+dt2+"-"+dt3+"-"+dt4+"_e"+desiredDTalg+s);
            }

            // FMpatchkey(int m1, int m2, int m3, int m4, int d1, int d2, int d3, int d4){
            //     mult1=Convert.ToByte(m1); mult2=Convert.ToByte(m2); mult3=Convert.ToByte(m3); mult4=Convert.ToByte(m4);
            //     dt1=Convert.ToByte(d1);  dt2=Convert.ToByte(d2); dt3=Convert.ToByte(d3); dt4=Convert.ToByte(d4);
            // }

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

            public readonly byte[] keyon;
            private FMchannel FMref;

            private Dictionary<string, byte> LABEL_REG = new Dictionary<string, byte>(); 
            private Dictionary<byte, string> REG_LABEL = new Dictionary<byte, string>(); 
            // private readonly Dictionary<string, int> LABEL_IDX = new Dictionary<string, int>();  // use REG_IDX function instead
            private Dictionary<byte, int> REG_IDX = new Dictionary<byte, int>(); 

            private Dictionary<byte, byte> REG_VAL = new Dictionary<byte, byte>(); 
            // public Dictionary<string, FMregister> RG = new Dictionary<string, FMregister>(); 
            
            int lastidx_debug=0; // for print
            public FMregisters(FMchannel fMchannel) {
                this.operators = fMchannel.operators;
                this.chip = fMchannel.chip;
                // DTML1=0xff; DTML2=0xff; DTML3=0xff; DTML4=0xff;
                FMref = fMchannel; //! please work
                 // TODO this'd be a whole lot smoother if the data was in dictionaries too
                this.LABEL_REG.Add("DTML1",fMchannel.op1_DTML); this.LABEL_REG.Add("DTML2",fMchannel.op2_DTML);
                if (fMchannel.operators > 2) { this.LABEL_REG.Add("DTML3",fMchannel.op3_DTML); this.LABEL_REG.Add("DTML4",fMchannel.op4_DTML); }
                // this.LABEL_IDX.Add("DTML_IDX1",0); this.LABEL_IDX.Add("DTML_IDX2",0); 
                // if (fMchannel.operators > 2) { this.LABEL_IDX.Add("DTML_IDX3",0); this.LABEL_IDX.Add("DTML_IDX4",0); }

                this.LABEL_REG.Add("TL1",fMchannel.op1_TL); this.LABEL_REG.Add("TL2",fMchannel.op2_TL); 
                if (fMchannel.operators > 2) { this.LABEL_REG.Add("TL3",fMchannel.op3_TL); this.LABEL_REG.Add("TL4",fMchannel.op4_TL); }
                // this.LABEL_IDX.Add("TL1",0); this.LABEL_IDX.Add("TL2",0); 
                // if (fMchannel.operators > 2) { this.LABEL_IDX.Add("TL3",0); this.LABEL_IDX.Add("TL4",0); }

                REG_LABEL = LABEL_REG.ToDictionary(x => x.Value, x => x.Key); // reverse keys/vals to create REG_LABEL out of LABEL_REG

                foreach(KeyValuePair<string, byte> LG in LABEL_REG) { // outputs LG.Key, LG.Value (register)
                    this.REG_VAL.Add(LG.Value, 0x00); // Register, init value 0x00
                    this.REG_IDX.Add(LG.Value, 0); // Register, init idx value 0
                }

                // tb(FMref.name+": dictionary counts: LABEL_REG:"+LABEL_REG.Count+" REG_LABEL:"+REG_LABEL.Count+" REG_VAL:"+REG_VAL.Count+" REG_IDX:"+REG_IDX.Count);
                // tb("test: "+LABEL_REG["TL1"]);
                // tb("test2: "+REG_LABEL[0x48]+ " c:"+REG_LABEL.Count());

                // System.Console.ReadKey();

                // this.LABEL_IDX.Add("KEYON_IDX",0);


                // if (ops<4) {keyon = new byte[3];} else {keyon = new byte[2];}   // OPL keyon is 5 half-bytes
                keyon = new byte[3] {fMchannel.keyon[0],fMchannel.keyon[1],fMchannel.keyon[2] };   // OPL keyon is 5 half-bytes, using two bytes then >20 for on <20 for off
                // ALG = 0x00;
                // name = "FM?";
                // LFO = new byte[2]; // one per OPL operator... -- shared with multiplier, disregard

                /*   label, chipcode, register, value, index
                    Label -> Register -> Value
                */

            }
            public int ParseAndSetValue(byte[] data, int idx) {
                if (data[idx]==chip){
                    if (REG_VAL.ContainsKey(data[idx+1]) ) { 
                        REG_VAL[data[idx+1]] = data[idx+2];
                        REG_IDX[data[idx+1]] = idx;
                        lastidx_debug=idx;
                        return 1;
                    }
                }
                if (FMref.IsKeyDown(data, idx) ) { // not in first conditional because on OPN2/A/B, chip is always first bank for keyon purposes
                    keyon_idx=idx; //? necessary? 
                    return 2; // this does nothing btw
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
                //* if any DT (or DT idx) is empty then log & skip. Should only occur with early garbage data...
                // str=FMref.name+": !WARNING!: 0x"+Convert.ToString(lastidx_debug,16)+": MISSING ";
                // bool warn=false;
                foreach(KeyValuePair<byte, int> RG in REG_IDX) { // outputs LG.Key, LG.Value (register)
                    // this.REG_VAL.Add(LG.Value, 0x00); // Register, init value 0x00
                    if (RG.Value==0) {
                        // str+=REG_LABEL[RG.Key]+" "; warn=true;
                        return "";
                    }
                }
                // str+=" ... lag+"+(currentIDX - lastidx_debug)+" bytes";
                // // if (warn) {tb(str); System.Console.ReadKey(); return str;}
                // if (warn) {tb(str); return str;}
                // str="";

                // str=
                // " 1:"+Convert.ToString(Second4Bit(LABEL_REG["DTML1"]),16)+" 2:"+Convert.ToString(Second4Bit(LABEL_REG["DTML2"]),16)+
                // " 3:"+Convert.ToString(Second4Bit(LABEL_VAL("DTML3")),16)+" 4:"+Convert.ToString(Second4Bit(LABEL_REG["DTML4"]),16);
                // System.Console.ReadKey();


                int[] current_values;
                if (operators==2) { // OPL2 note - AM|VIBRATO|KSR|EG / MULT - No Detune with OPL2. Looks like 4 bit 0-F to me, but I've heard it skips some values
                    current_values=new int[]{Second4Bit(LABEL_VAL("DTML1")), Second4Bit(LABEL_VAL("DTML2")), Second4Bit(LABEL_VAL("DTML3")), Second4Bit(LABEL_VAL("DTML4"))};
                } else {
                    current_values=new int[]{First4Bit(LABEL_VAL("DTML1")), First4Bit(LABEL_VAL("DTML2")), First4Bit(LABEL_VAL("DTML3")), First4Bit(LABEL_VAL("DTML4")),
                                              Second4Bit(LABEL_VAL("DTML1")), Second4Bit(LABEL_VAL("DTML2")), Second4Bit(LABEL_VAL("DTML3")), Second4Bit(LABEL_VAL("DTML4"))};
                }
                int? DesiredDT=null; int? DesiredMult=null; int? DesiredDTalg=null; //int? DesiredCarrier=null; // let's try Alg4 if desired carrier is 4 or 2. but handle this globally?

                bool foundpatch=false;
                if (FMargs.LookForPatchKeys) {
                    foreach (FMpatchkey patch in FMargs.PatchKeys) {
                        if (patch.MatchesValues2(current_values, operators) ) {
                            DesiredDTalg = patch.desiredDTalg; 
                            if (patch.desiredmult < 16) { 
                                DesiredMult = patch.desiredmult;
                            }
                            // countpatchkeysfound+=1;
                            foundpatch = true;
                            break;
                        } 
                    }
                }

                str += current_values[4]+"-"+current_values[5]+"-"+current_values[6]+"-"+current_values[7]+" / "; // debug, will break with OPL2, remove me
                for (int i = 0; i < 4; i++) {
                    str += current_values[i]+"-";
                }

                if (operators == 4) {  //* handle DT
                    if (DesiredDTalg == null) { DesiredDTalg=FMargs.detunesetting; } 
                    DesiredDT = ReturnDesiredDT(current_values, DesiredDTalg); //* <--- 'big function' for all DT algorithms
                    data[LABEL_IDX("DTML4")+2] = FourToEightCoder(Convert.ToByte(DesiredDT) , Second4Bit(LABEL_VAL("DTML4")) ); //* WRITE DT
                }
                if (DesiredMult == null) { // if mult is defined in patch key use it, otherwise automatically compensate    -- Handle Mult
                    if (this.operators==2) {
                        DesiredMult=HighestCommonFactorINT(new int[] {Second4BitToInt(LABEL_VAL("DTML1")), Second4BitToInt(LABEL_VAL("DTML2") )} );
                    } else {
                        DesiredMult=HighestCommonFactorINT(new int[] {Second4BitToInt(LABEL_VAL("DTML1")),Second4BitToInt(LABEL_VAL("DTML2")),Second4BitToInt(LABEL_VAL("DTML3")),Second4BitToInt(LABEL_VAL("DTML4") )} );
                    } 
                }
                if (!foundpatch){
                    FMargs.AddLostPatch(current_values); // log unfound patches
                }
                data[LABEL_IDX("DTML4")+2] = FourToEightCoder(LABEL_VAL("DTML4"), Convert.ToByte(DesiredMult) );                //* WRITE MULT
                
                data[LABEL_IDX("TL4")+2] = 0x80; // set volume. This should be set globally really. 0x80 for debug
                str=FMref.name+": 0x"+Convert.ToString(lastidx_debug,16)+" ... "+str+"--> mult"+Convert.ToByte(DesiredMult)+" DTout:"+DesiredDT;
                return str;
            }

        }



        //! PT3/4 Cont. (v04)
        //! Pt A: Preliminary: Smash all DR/AR etc, change mute all operators except the last
        //! find DTML values, look 10ms ahead for full patch values, then apply detune and mult 
        //! -> pick lowest mult & handle detune according to settings
        static void AutoTrigger(FMchannel FMin, Arguments FMargs, byte[] data, bool[] ByteFlags, bool[] WaitFlags, int StartVGMdata, int EndVGMdata) {
            
            FMregisters fMregisters = new FMregisters(FMin);
            byte outvolume = 0x0A; //hardcode carrier out level

            //* PT 1 - global changes
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
            if (FMin.operators==4){ // SR, but not for OPL. OPL2 only has 4 envelope values? they're split like AR/DR  SR/RR
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

                FindAndkillFirstTwoBits(FMin.chip, FMin.op1_DTML, 0b00000000, data, ByteFlags, StartVGMdata, EndVGMdata); // binary input does nothing set AM and Vibrato to 0 (?)

            } else if (FMin.operators==4) {
                FindAndReplaceByte(FMin.chip, FMin.op1_TL, 0x7f, data, ByteFlags, StartVGMdata, EndVGMdata); // mute TL
                FindAndReplaceByte(FMin.chip, FMin.op2_TL, 0x7f, data, ByteFlags, StartVGMdata, EndVGMdata); // mute TL
                FindAndReplaceByte(FMin.chip, FMin.op3_TL, 0x7f, data, ByteFlags, StartVGMdata, EndVGMdata); // mute TL
                FindAndReplaceByte(FMin.chip, FMin.op4_TL, outvolume, data, ByteFlags, StartVGMdata, EndVGMdata); // unmute carrier 4
            }

            //! main loop
            // do parsewaits bool array (timecodes shouldn't be necessary)
            // when ParseAndSetValue returns an operator DTML, 
            //     start tracking delay samples
            //      second conditional counts delays ahead, after threshold (~8ms?) THEN do triggerfy. this will even things out.
            ////      triggerify should use whatever the biggest index is for carrier? Unless it's OPL2, then always use 2 (just return 1 only on carrier 2?)
            //      ^^ nah this wont work for ML sweeps because TL values won't update with DTML, so old sections of music will get muted. unimplemented for now.

            int LagThreshold = 441; // after DT value found, look ahead this many samples before Triggerify (441 = 10ms)
            bool BeginDelay=false; // start delay bool, Compensates for driver+hardware delay before values take effect
            int Lag = 0; // in samples, via parsewaits

            for (int i = StartVGMdata; i < EndVGMdata; i++) {
                if (ByteFlags[i] && fMregisters.ParseAndSetValue(data, i) == 1) { // return 1 for DT, return 2 for KEYON
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

            tb(showprogress);
            if (FMargs.LookForPatchKeys){
                LostPatchLog+=FMargs.ReturnLostPatches()+"\n";
            }

        }


        static int ParseWaits(byte[] data, int idx, bool[] WaitFlags){ // just return delay of current byte, in samples
            if (WaitFlags[idx]){ // 'wait' commands should be flagged ahead of time by this bool array
                // tb("found wait @ 0x"+idx);
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

        
        static int ReturnDesiredDT(int[] DTML, int? DesiredDTalg) {
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
            int[] DTs = new int[] {DTML[0], DTML[1], DTML[2],DTML[3]};
            int[] MLs = new int[] {DTML[4], DTML[5], DTML[6],DTML[7]};
            
            int idx, outDT=0;
            switch (DesiredDTalg) {
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


        static int ReturnDesiredDTIDX(byte[] ba, int alg, int numberofops) {     //* this will return the index of an operator who we will clone. alg changes how this is determined
            int[] bDT = new int[numberofops];   
            int[] bmult = new int[numberofops];                   
            // string temp; char[] temp2;
            for (int i = 0; i < ba.Length; i++) {
                bDT[i] = First4Bit(ba[i]);
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
                                case 4: tmpA[idx]=0; break; // DT 4 is equal to 0
                                default: tmpA[idx]=3; break; // if >7... This shouldn't happen. - actually it can happen, values wrap around
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
        // static int First4Bit_Int(byte b){return (int)(b >> 4); }
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
        // static byte HighestCommonFactor(byte[] input, int ops) {
        //     for (int i = 0; i < ops; i++) {

        //     }
        // }

        static byte HighestCommonFactor(int[] numbers)
        {
            return Convert.ToByte(numbers.Aggregate(GCD));
            // return Convert.ToByte(numbers.Aggregate(GCD));
        }
        static int HighestCommonFactorINT(int[] numbers)
        {   
            // PrintStringArray(numbers);
            // tb(" -> "+numbers.Aggregate(GCD));

            // int[] test = new int[] {4,6,12,12};
            // tb("test1 "+test.Aggregate(GCD));
            // test = new int[] {1,6,12,12};
            // tb("test2 "+test.Aggregate(GCD));
            // if (test.Min()==0) {tb("bing oh no!");}
            // test = new int[] {0,6,12,12};
            // tb("test3 "+test.Aggregate(GCD));
            // tb("test 420 = "+GCD(4,22));
            // System.Console.ReadKey();

            if (numbers.Min() == 0) {return 0;} // math doesn't work with 0, but if there's a 0, just return 0
            return numbers.Aggregate(GCD);
            // return Convert.ToByte(numbers.Aggregate(GCD));
        }

        static int GCD(int a, int b)
        {
            return b == 0 ? a : GCD(b, a % b);
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











