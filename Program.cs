using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.IO.Compression; // gzip decompression

// vvv to publish with dependencies - but it'll be like 30 megs
// dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true

/* 
    0v051 wip
    Improved syncopation between source VGM and EXTT VGM by carefully replacing existing values when possible
        This improves tracking around patch changes w/ ymfm core, maybe nuked as well
    Fixed trigger generation failing near loop points (sorry for not noticing this earlier)
    Fixed a bug that may have caused loop points to be set incorrectly
    Fixed the catch-all SoloVGM properties "SSG" and "FM"
    Fixed OPL3 being muted
    Fixed a bug that could cause incorrect alg/feedback in YM2608bank export
    Changed output filename (from %~n.vgm.vgm to just %~n.vgm). SoloVGM output changed as well

    0v05
    Note:
    The program now will remove and add commands to the vgm. This is the only way to make the triggers more reliable, but
    it's possible it may cause sync issues with some VGM players

    Major Features:
    Added YM2608ToneEditor .bank export (out_bank.cs). To enable: [EXTT.exe] bank 1 [file]
    Added .VGZ support (gzip compressed VGM)
    Added SoloVGM functionality, for muting channels with hard edits. To use: [EXTT.exe] SOLOVGM [list of channels to solo separated by spaces]
    FORCEOP returns, this time defaulting to 0 (auto), which will use the last DTML in the detected patch
        forceop "auto" should be more reliable with 'incomplete' patches, or MULT sweeps
    OPL3 Support (preliminary, 4-operator mode might be weird or broken)

    Minor Features:
    OPM DT2 and OPN Ch#3 Extended Mode is now noted in the Patch Report
        for dealing with DT2, for best results check the reported ratios and adjust MULT to best available denominator (with mult0 being 0.5)
            but you will have to do this manually for now. Still, the odd frequencies may not track well.
        for ch#3 mode, best results are with FORCEOP and then running extt multiple times for each voice
            Alternatively, you can use SoloVGM and mute the operators from the EXTT vgm that way

    Bugfixes:
    fixed bad chip detection with YM2413 or YM2612 VGMs with very tiny headers
    Fixed a bug in the timecode generation (ParseWaits was incorrectly using a signed int16)
    Added more stuff to ExamineVGMData (hopefully no more UNKNOWN COMMAND spam)

    todo
    better ch3 mode maybe? Triggerify check alg to see if it's appropriate to downscale mult? 
    concerned about desyncs... maybe don't always use last DTML. prioritize later OPs if they're close together somehow?

    add Sega PSG to SoloVGM

    Granular Detune via frequency
        This requires math formulas for converting BLOCK-FNUM to DT for OPN, and another for OPM which uses BLOCK-KEY-KEYFRACTION
            DT affects the phase directly. The relationship between pitch and detune is logarithmic, maybe. 

    Integrate MonofyVGM

    OPLL, OPL4, OPX support: OPLL is quite different from OPL. OPL4 uses a 4-byte VGM command instead of 3. I have no idea what OPX is

*/

// v041 - fixed a bug that caused patchkey commands to default to 0 if unspecified

/* v04 - major refactor
    Altwave removed, some DT algs removed
    Detune is set to 10, which is now AVERAGE OF ALL DETUNE VALUES
    MULTIPLIER is set AUTOMATICALLY (finally)
     ^ with these two, drag & drop tracking results should be much, much better!
    New chips supported: OPL (YM3526) & Y8950 (MSX-AUDIO, basically OPL + ADPCM-B)
        OPL3 still to come
    PatchKey system now supports 2-Operator FMs
        The syntax is different from 4-op, see -help
    Lost Patch Report now displays all the time and has some new features such as timecodes
        * input argument P P for clean patch report for copy/pasting into bat file
    Bugfixes
*/

// 03 changelog
// removed subtractmult. It barely worked anyway

/*
        LIMITATIONS
    "Mult sweeps" will break things
    OPL / OPL2 drum mode channels probably won't work and might break note detection
*/

namespace EXTT

{
    public partial class Program 
    {
        #region Constants & class declarations ----------
        static int VERSIONMAJOR = 0, VERSIONMINOR=5;
        static int VERSIONPATCH=1;
        public delegate void WriteDelegate(string msg, params object[] args); // shortcut commands
        public static readonly WriteDelegate tb = Console.WriteLine; 
        delegate string WriteDelegate2(byte msg, int tobase);
        private static readonly WriteDelegate2 cts = Convert.ToString;

        // string constants for use as dictionary keys. These are for the patchkey system, representing specific nibbles or bits. Full bytes are in DATA2.CS
        const string mult1 = "mult1", mult2 = "mult2", mult3 = "mult3", mult4 = "mult4"; 
        const string dt1 = "dt1", dt2 = "dt2", dt3 = "dt3", dt4 = "dt4"; 
        const string dt21="dt21", dt22="dt22", dt23="dt23", dt24="dt24";
        const string wave1 = "wave1", wave2 = "wave2", alg = "alg", vibrato1 = "vibrato1", vibrato2 = "vibrato2"; 
        const string desiredDTalg = "desiredDTalg", desiredMult = "desiredMult", desiredVibrato = "desiredVibrato"; 
        const string desiredForceOp = "desiredForceOp"; 
        public static readonly string[] patchkey_keys = new string[] {mult1, mult2, mult3, mult4, dt1, dt2, dt3, dt4, alg, desiredDTalg, desiredMult, 
                                                                    wave1, wave2, vibrato1, vibrato2, desiredVibrato, desiredForceOp};
        public static readonly string[] patchkey_keys_4op = new string[] {mult1, mult2, mult3, mult4, dt1, dt2, dt3, dt4, alg, desiredDTalg, desiredMult, desiredForceOp};
        public static readonly string[] patchkey_keys_2op = new string[] {mult1, mult2, alg, wave1, wave2, vibrato1, vibrato2, desiredVibrato};

        static bool Channel3ModeDetected=false;
        
        // Settings - per channel and global - are contained via class 'Arguments'
        static Arguments GlobalArguments = new Arguments(10, 99, "FMG"); // args: detunesetting, forcemult. These will be copied to unset values
        static Arguments FM0Args = new Arguments(99,99,"FM0"); static Arguments FM1Args = new Arguments(99,99,"FM1");
        static Arguments FM2Args= new Arguments(99,99,"FM2"); static Arguments FM3Args= new Arguments(99,99,"FM3");
        static Arguments FM4Args= new Arguments(99,99,"FM4"); static Arguments FM5Args= new Arguments(99,99,"FM5");
        static Arguments FM6Args= new Arguments(99,99,"FM6"); static Arguments FM7Args= new Arguments(99,99,"FM7");
        static Arguments FM8Args= new Arguments(99,99,"FM8"); static Arguments FM9Args= new Arguments(99,99,"FM9"); 
        static Arguments FM10Args= new Arguments(99,99,"FM10"); static Arguments FM11Args= new Arguments(99,99,"FM11"); 
        static Arguments FM12Args= new Arguments(99,99,"FM12"); static Arguments FM13Args= new Arguments(99,99,"FM13"); 
        static Arguments FM14Args= new Arguments(99,99,"FM14"); static Arguments FM15Args= new Arguments(99,99,"FM15"); 
        static Arguments FM16Args= new Arguments(99,99,"FM16"); static Arguments FM17Args= new Arguments(99,99,"FM17"); 
        static byte chiptype=0;
        static int operators=0;
        static string filename="";
        static byte[] data = new byte[]{0};
        static bool[] WaitFlags = new bool[]{false};
        static bool[] ByteFlags = new bool[]{false};
        static int[] timecodes = new int[]{0};
        static int startVGMdata, endVGMdata;
        static List<Dictionary<string,byte>> FMSystemList = new List<Dictionary<string,byte>>();
        static List<FMchannel> FMChannelList = new List<FMchannel>();
        // static ImmutableList<string> OPM_reg_OverwriteMe = new ImmutableList<string>(){};

        // ReplaceableCommands: v051 Syncopation Improvement
        // these will be 1. appended to start of file at these initial values
        //               2. possibly replaced by Triggerify to make room for new TL and DTML writes
        static Dictionary<string, byte> ReplaceableCommands_2op = new Dictionary<string,byte>(){
            {DTML, 0x00},   // XXXXYYYY AM enable / PM enable / EG type / KSR - MULT                                         
            {TL, 0x3F},                                             
            {AR_DR_OPL, 0xF0}, 
            {SL_RR, 0x00},     
            {WAVEFORM, 0x00}                                            
        };
        static Dictionary<string, byte> ReplaceableCommands_4op = new Dictionary<string,byte>(){
            {DTML, 0x07},
            {TL, 0x7F},
            {AR_KSR, 0b00011111},
            {DR_LFO_AM_ENABLE, 0x00},
            {SR_DT2, 0x00},
            {SL_RR, 0x00}
        };

        static Dictionary<string, Arguments> GetChannel = new Dictionary<string, Arguments>(); // ex. key/pair: "FM0" FM0channel. For argument parser
        public static string LostPatchLog=""; // collects all lost patches logged by ReportLostPatches / ReturnLostPatches
        #endregion
         public static int ProcessArgument(string arg1, string arg2, string arg3) { // returns number of indexes to skip
            if (GetChannel.TryGetValue(arg1, out Arguments? currentchannel) ) { // if dictionary key 'arg' exists, ref it to currentchannel
                // tb("ProcessArgument: found FM arg, executing: "+arg1+" "+arg2+" "+arg3 ); // ex. FM0, DT, 0
                if (currentchannel.ParseArgument(arg2,arg3) ) return 2;                 // 3 index arg (per channel) - ex. FM0 DT 7
            } else { 
                // tb("ProcessArgument: found global arg, executing: "+arg1+" "+arg2 ); // 2 index global arg - ex altwaveform FALSE
                if (GlobalArguments.ParseArgument(arg1,arg2) ) return 1;
            }
            return 0; // if Command.ParseValue returns false, it'll throw an error - Just continue to the next string and try again.
        }
        static void Main(string[] args) 
        {
            debugstart(); // jump to a debug func for messing around
            #region PART 0/5 Check Valid VGM Input / Display Help ------------
            tb("VGM External Trigger Tool Ver "+VERSIONMAJOR+"."+VERSIONMINOR+VERSIONPATCH+
            "\nA VGM hacking tool for creating external trigger waveforms for oscilloscopes\nUsage: EXE [options] Infile.VGZ");
            if (args.Length < 1 || "-H"==args[0] || "-h"==args[0] || "h"==args[0] || "/?"==args[0] || "-help"==args[0] ) { 
                string helptext=@"Help (-h or no arguments)
Supported chips are these Yamaha FM synths: OPM, OPN, OPNA, OPNB, OPN2, OPL, Y8950, OPL2 
Available options (4operator FM): DT(def 10), FORCEOP (def 0, auto) Mult(def 99, auto)
Available options (2operator OPL2): Mult(unspecified / automatic, again this is not recommended)
Special options: BANK (def 0)  ... YM2608ToneEditor .bank export, 4-op only. To Use: EXE Bank 1 InFile.VGZ
                 SOLOVGM [Arguments]  ... Mute channels. Example: EXE SSG0 InFile.VGZ would solo SSG0
Advanced options: Patch ""PatchKey Commands"" 

Press any key to continue...";
                tb(helptext);
                Console.ReadKey();                
                helptext=@"
            *************** PatchKey Description ***************
applies settings on a instrument-by-instrument basis, by providing some identifying info 
then the desired settings. This can be used to fine-tune the tracking of the output, 
particularly on very detuned FM instruments
This program will print a list of all the identified patches in the patchkey format. 
If the argument ""P P"" is used, the patch report will use an even simpler syntax for 
quicker copy-pasting into .bat files

Press any key to continue...";
                tb(helptext);
                Console.ReadKey();                
                helptext=@"
            *************** PatchKey Syntax ***************
4-Operator FM Synth PatchKey Syntax: 
""patch""           patch info                           commands
v      v                                v v                                   v 
PATCH ""M1-M2-M3-M4 / DT1-DT2-DT3-DT4 ALG DT(desired) MULT(desired, optional) FORCEOP (desired, optional)""

2-Operator FM Synth PatchKey Syntax:
PATCH ""WAVEFORM1-WAVEFORM1-ALG-VIBRATO1-VIBRATO2 / MULT1-MULT2 VIBRATO(desired) MULT(desired, optional)""
Examples:
4-OP Example: patch ""12-15-1-3 / 3-4-3-2 alg4 dt3"" 
    if a 4-op patch has mult values of 12-15-1-3 and dt values of 3-4-3-2, use detune 3
4-OP Example: p ""12-15-1-3 / 3-4-3-2 a4 e3""
    a simplified version of the example above
2-OP Example: patch ""0-0-0-1-0 / 1-2 v1""
    a patch with wave1=0, wave2=0, ALG 0 (not connected), vib1=1 vib=0 is set to vib=1

*NOTE: Patch Key MUST be in quotes if there are blank spaces!*
Press any key to continue...";
                tb(helptext);
                Console.ReadKey();                
                helptext=@"
                        - - - SETTINGS FOR DETUNE (DT value) - - - 
  * 0 - No Detune (good starting point for PatchKey system)
   0-7 - force a detune setting. 7-6-5-0-1-2-3 in order corresponds to -3 to +3 (4 is the same as 0)
  * 8  - Use the DT of the lowest frequency operator bias 1>2>3>4 - If there are matches, use the DT of the 
         earlier op (OP#1 > OP#2 > OP#3 > OP#4)
    9  - Use the DT of the lowest frequency operator bias 4>3>2>1 - If there are matches, use the DT of the 
         later op (OP#4 > OP#3 > OP#2 > OP#1)
  * 10 - Use an average of all Detune values (*** NEW DEFAULT ***)
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
    none of this applicable for 2-Operator FM synths, which have no operator detune settings.
        - - - ADD MULT / FORCE MULT (forcemult/mult/addmult, unspecified by default) - - - 
    If unspecified, Multiplier is set automatically (highest common denominator of all multipliers) 
    This provides the best possible tracking. However, it may be useful to set higher multplier if the 
    waveform's length is very long, such as with some percussion patches with inharmonic multipliers. 
    Possible values: 0-15

Options may be set globally and/or per-channel, by preceding an option with a 'FM#' command (zero-bound)
Per-channel commands will always take precedence over global commands.
To prevent confusion, it's recommended to only use PatchKey globally.
Example: extt dt 0 FILE.vgz                    <- sets DT to 0 globally for all channels 
Example: extt dt 0 fm0 dt 2 FILE.vgz           <- does the above but sets dt to '0' for FM0
Example: extt dt 0 fm0 dt 2 fm3 dt 11 FILE.vgz <- additionally, set channel fm3 to detune 11
... or just drag & drop.";
                tb(helptext);
                Environment.Exit(0);
            }
            if (!File.Exists(args[args.Length-1]) ) {
                 tb($"Error: No such file exists {args[args.Length-1]}"); Console.ReadKey(); 
                 Environment.Exit(1);
            }

            data = new byte[]{0};
            filename = args[args.Length-1].ToString();       

            if (filename.Substring(filename.Length-3).ToUpper() == "VGZ") { // * decompress VGZ
                using FileStream compressedFileStream = File.Open(filename, FileMode.Open);
                using var decompressor = new GZipStream(compressedFileStream, CompressionMode.Decompress);
                using (MemoryStream ms = new MemoryStream()) {
                    decompressor.CopyTo(ms);
                    data = ms.ToArray();
                }

            } else {
                data = File.ReadAllBytes(filename);
            } 

            var InitLengthDBG=data.Length; // debug

            #endregion
            #region PART 1/5 Detect FM Chip, Setup Initial Data --------------
            ParseVGMHeader();
            FMSystemList = new List<Dictionary<string,byte>>();
            FMChannelList = new List<FMchannel>();
            SetupData2(chiptype, out FMSystemList, out FMChannelList); 
            // tb("syslist & channellist lengths: "+FMSystemList.Count+" "+FMChannelList.Count);
            operators=FMChannelList[0].operators;
                        //* SCAN THROUGH DATA BYTE-BY-BYTE, FLAGGING FM COMMANDS THAT ARE SAFE TO EDIT
            //      this flags command bytes and wait bytes to make things a bit easier. It may also return timecodes if I add that
            WaitFlags = new bool[endVGMdata];
            ByteFlags = ExamineVGMData(false);
            if (chiptype == 0x52 || chiptype == 0x55 || chiptype == 0x58 || chiptype == 0x56) {
                FMChannelList[2].Add(TIMER_LOAD_SAVE, FMSystemList[0][TIMER_LOAD_SAVE]); // have FM2 track ch#3 mode (second bit of this system reg)
            }
            foreach (FMchannel ch in FMChannelList) {
                ch.Initialize(); // merge operator label_reg dicts into channel (ex. ch.op2.dtml becomes ch.dtml2), initialize reverse dictionaries
            }
            
            if (args[0].ToUpper()=="SOLO" || args[0].ToUpper()=="SOLOVGM" || args[0].ToUpper()=="MONO"){
                if (args[0].ToUpper()=="MONO") {tb("EXTT: Error! No such argument: MONO. Did you mean SOLO / SOLOVGM?"); Environment.Exit(1);};
                var solovgm = new EXTT.SoloVGM.Program();
                solovgm.SoloVGM(data, ByteFlags, args, chiptype, startVGMdata, endVGMdata, filename, FMSystemList, FMChannelList);
            }


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


            #endregion
            #region PART 4/5 To Main Loop, External Triggerify ------------------
            // PT 4 - Main Loop, External Triggerify

            //* pt1: Blanket edits across the board (for example removing all AR/DR/RS, channel feedback to 0, channel algorithms to 7)
            //// pt2: Find keyOn events and trace backwards to find patches, then edit them to our liking (mute operators, decide which detune value to use based on our settings, etc)
            //* Pt2 0.4: Parse through data, saving register indexes values as we go, if Detune / Mult changes are found search forward (in milliseconds) to find patches

            timecodes = new int[endVGMdata];
            timecodes=CreateTimeCode(ref timecodes, in data, in WaitFlags, in startVGMdata, in endVGMdata); // count samples, int value for every byte shows current 


            List<Arguments> ChannelArgumentList = new List<Arguments>() {FM0Args, FM1Args, FM2Args, FM3Args, FM4Args, FM5Args, FM6Args, FM7Args, FM8Args,
                                                      FM9Args, FM10Args, FM11Args, FM12Args, FM13Args, FM14Args, FM15Args, FM16Args, FM17Args};


            for (int i = 0; i < FMChannelList.Count; i++) {
                // if (i == 17) { // debug
                // AutoTrigger(FMChannelList[i], ChannelArgumentList[i], data, in byteflag, in WaitFlags, startVGMdata, endVGMdata, in timecodes);
                AutoTrigger(FMChannelList[i], ChannelArgumentList[i]);
                // }
            }

            // todo maybe kill LFO am sens on system level? anything else on system level?

            tb("EXTT complete. Creating timecode & preparing patch report...");

            foreach (Arguments a in ChannelArgumentList) {
                if (a.LostPatches.Count() > 0) {
                    // foreach (Dictionary<string,int> lp in a.LostPatches){ // moved to Triggerify
                    //     lp["TIMECODE"] = timecodes[Convert.ToInt32(lp["IDX"])];
                    // }
                    LostPatchLog+=a.ReturnLostPatches(operators)+"\n";
                }
                // FM0Args.LostPatches[0];
            }

            // ProgressTimer.Stop(); // stop timer
            tb("\n"+LostPatchLog);

            if (operators==2) {
                string wv="0="+ReturnWaveTypeString(0)+" 1="+ReturnWaveTypeString(1)+" 2="+ReturnWaveTypeString(2)+" 3="+ReturnWaveTypeString(3);
                tb("OPL Patch Format: wave1-wave2-alg-vib1-vib2 / mult1-mult2 _ commands");
                tb("waveforms (OPL2 only): {0}",wv );
                tb("alg: aka 'Operator connection algorithm', 1=connected 0=disconnected (AM mode)");
                tb("vibrato: Auto always uses the carrier vibrato. If alg=0, best results depends on the patch");
                tb("\n");
            } else {
                tb("OPM/OPN Patch Format: mult1-mult2-mult3-mult4 / dt1-dt2-dt3-dt4 _alg & commands");
                tb("alg (algorithm): Narrows patch identification (do not change)");
                tb("DT (Detune): Auto will use the average all detune levels");
                tb(" DT Tip: when trigger is DT7, waveform will appear to swim left. DT3 swims right (left-to-right: 7-6-5-4/0-1-2-3)");
                tb("MULT (multiplier): Auto will choose the highest common denominator of all mults (this is best)");
                tb("\n");
                // if (Channel3ModeDetected) tb("CH#3 Extended Mode Detected\n");
            }

            #endregion
            #region PART 5/5 Name & Save Output VGM --------------------------


            //* 422 test: append data update header
            // 0x04 EoF offset 
            // 0x1c Loop offset - will have to rerun timecodes for this one
            int AppendCnt = AppenderCount(Appender);

            int newEoF = BitConverter.ToInt32(data,0x04)+AppendCnt;
            // tb("old EOF="+BitConverter.ToInt32(data,0x04)+" new EoF="+newEoF+" appender cnt = "+AppendCnt); // debug
            byte[] newEoFA = BitConverter.GetBytes(newEoF);
            data[0x04] = newEoFA[0]; data[0x05] = newEoFA[1]; data[0x06] = newEoFA[2]; data[0x07] = newEoFA[3];

            if (BitConverter.ToInt32(data,0x14) > 0) { // only update this if GD3 offset is present
                int newGD3EoF = BitConverter.ToInt32(data,0x14)+AppendCnt;
                // tb("old newGD3EoF="+BitConverter.ToInt32(data,0x14)+" new newGD3EoF="+newGD3EoF+" appender cnt = "+AppendCnt); // debug
                byte[] newGD3EoFa = BitConverter.GetBytes(newGD3EoF);
                data[0x14] = newGD3EoFa[0]; data[0x15] = newGD3EoFa[1]; data[0x16] = newGD3EoFa[2]; data[0x17] = newGD3EoFa[3];
            }

            if (BitConverter.ToInt32(data,0x1c) > 0) { //* handle loop, if present. As loop duration should remain the same, we shouldn't have to split 3-byte wait commands, we can do this last
                int LoopPointIDX_1, samples_to_loop, samples_from_loop;
                // should be 1015800
                LoopPointIDX_1 = ReadLoops(out samples_to_loop, out samples_from_loop, (BitConverter.ToInt32(data,0x1c)+28), data, WaitFlags, startVGMdata, endVGMdata);
        
                // tb("specified loop duration="+BitConverter.ToInt32(data,0x20)+" "+SamplesToMinutes(BitConverter.ToInt32(data,0x20))+
                // " actual loop duration="+samples_from_loop+" "+SamplesToMinutes(samples_from_loop) ); // debug
                // tb("Samples start->Loop:"+samples_to_loop+" "+SamplesToMinutes(samples_to_loop)+
                // " Samples loop->end:"+samples_from_loop+" "+SamplesToMinutes(samples_from_loop));

                // tb("old data cnt="+data.Count()+" end+?"+(endVGMdata+AppendCnt));
                // data = AppendData(data, Appender); // ! ------- ------- ------- ------- ------- ------- if loop
                data = AppendData(in data, Appender); // ! ------- ------- ------- ------- ------- ------- if loop
                // tb("new data cnt="+data.Count()+" end+?"+(endVGMdata+AppendCnt));

                WaitFlags = new bool[data.Count()];
                ExamineVGMData(true);
                // timecodes=CreateTimeCode(timecodes, data, WaitFlags, startVGMdata, endVGMdata + AppendCnt); // count samples, int value for every byte shows current 
                int LoopPointIDX_2 = FindLoopPoint(samples_to_loop, in data, in WaitFlags, startVGMdata, (endVGMdata+AppendCnt));

                // // remake timecodes
                byte[] newlooppoint = BitConverter.GetBytes(LoopPointIDX_2 - 28);
                // tb("old LP="+BitConverter.ToInt32(data,0x1c)+" new LP="+LoopPointIDX_2); // debug
                // tb("loop point data @ 0X_"+Convert.ToString(LoopPointIDX_2)+": "+Convert.ToString(data[LoopPointIDX_2],16)+" "
                //     +Convert.ToString(data[LoopPointIDX_2+1],16)+" "+Convert.ToString(data[LoopPointIDX_2+2],16)+" ");
                data[0x1C] = newlooppoint[0]; data[0x1D] = newlooppoint[1]; data[0x1E] = newlooppoint[2]; data[0x1F] = newlooppoint[3];

                // * loop samples # should not change
                // byte[] newoopsamples = BitConverter.GetBytes(LoopSamples);
                // tb("old loop duration="+BitConverter.ToInt32(data,0x20)+" "+SamplesToMinutes(BitConverter.ToInt32(data,0x20))+" new loop duration="+LoopSamples+" "+SamplesToMinutes(LoopSamples) ); // debug
                // data[0x20] = newoopsamples[0]; data[0x21] = newoopsamples[1]; data[0x22] = newoopsamples[2]; data[0x23] = newoopsamples[3];
            } else {
                data = AppendData(data, Appender); // ! ------- ------- ------- ------- ------- ------- if no loop
            }

            // PT 5 - Save output VGM
            if (GlobalArguments.bankexport == 1 && FMChannelList[0].operators==2) {
                tb("Bank Export: Error! OPL not supported (YM2608ToneEditor .bank format)");
            }
            if (GlobalArguments.bankexport == 1 && FMChannelList[0].operators==4) {
                var patches_tmp = new List<Dictionary<string,int>>();
                foreach (Arguments FMargs in ChannelArgumentList) {
                    // FMargs.index;
                    // FMargs.LostPatches
                    foreach (var dict in FMargs.LostPatches) {
                        
                        dict.Add("name",Int32.Parse(FMargs.name.Substring(2)) );
                        // foreach ()
                        patches_tmp.Add(dict);
                        // tb(kv.Key+" = 0x_"+Convert.ToString(kv.Value,16));
                    }

                }
                #region Decimate Duplicate Patches (BANK EXPORT) unused (commented out)
                // decimate duplicate patches
                // patches_tmp.OrderBy(k => k["TIMECODE"]); // ? order by timecode 

                // var patches = new List<Dictionary<string,int>>(){patches_tmp[0]};
                // var patches = new List<Dictionary<string,int>>();

                // tb("bankexport: unique patches = "+patches_tmp.Count); //Console.ReadKey();
                // bool uniquepatch=false; // * decimate duplicate patches across channels. This works but it makes the output more confusing
                // string[] k = new String[]{"DTML1", "DTML2", "DTML3","DTML4",FEEDBACK_ALG,"TL1"};
                // foreach (Dictionary<string,int> p1 in patches_tmp) {
                //     uniquepatch=false;
                //     foreach (Dictionary<string,int> p2 in patches_tmp) {
                //         if (p1["IDX"] != p2["IDX"]) { // so we do don't end up comparing patches to themselves
                //             foreach (var kv1 in p1) {
                //                 if (k.Contains(kv1.Key)) {
                //                     if (p2.TryGetValue(kv1.Key, out int key2val) ) {
                //                         // str+=kv1.Key+": "+Convert.ToString(kv1.Value,16)+" vs "+Convert.ToString(key2val,16)+" ";
                //                         if (kv1.Value != key2val) {
                //                             // str+="\n";
                //                             uniquepatch=true; break;
                //                         }
                //                     }   
                //                 }
                //             }
                //             break;

                //         }
                //     }
                //     if (uniquepatch) {patches.Add(p1); uniquepatch=false;}
                // }
                // tb("bankexport: unique patches after decimation = "+patches.Count); //Console.ReadKey();
                #endregion

                var bankout = new io_bank.Program.BankOut(chiptype, patches_tmp, filename); // undecimated output
                // var bankout = new io_bank.Program.BankOut(chiptype, patches, filename);
                // var bankout = new io_bank.Program

            }


            string outfile = ""; // add suffixes to filename...

            foreach (Arguments FMx in ChannelArgumentList){
                outfile+=FMx.AddToFileName();
            }

            outfile=filename[0..^4]+"_extt"+GlobalArguments.AddGlobalValuesToFilename()+outfile+".vgm";
            tb("Writing "+outfile);

            if (File.Exists(outfile)) {
                File.Delete(outfile);
            }

            using (FileStream fs = File.Create(outfile)) {
                fs.Write(data, 0, data.Length);
            }                
            tb("EXTT v{0}.{1}{2} Complete",VERSIONMAJOR,VERSIONMINOR,VERSIONPATCH);
            // tb($"Old filesize={InitLengthDBG} new={data.Length}");
            Environment.Exit(0);
            #endregion
        }

        public class Arguments { // contains global & per channel settings to be fed into main loop. Patch data is then fed back in, for patch report and for bank export
            public int detunesetting, forcemult; // altwaveform;
            public int forceop = 0; public int bankexport = 0; // v42
            public string name;
            public bool CleanPatchReport=false; // simplify patch report for copy/paste if arg P P
            public List<Dictionary<string,int>> PatchKeys2 = new List<Dictionary<string,int>>();
            public bool LookForPatchKeys=false;

            public List<Dictionary<string,int>> LostPatches = new List<Dictionary<string,int>>();

            public List<int> LostPatchCnt = new List<int>();

            public Arguments(int detunesetting, int forcemult, string name){
                this.detunesetting = detunesetting;
                this.forcemult = forcemult;
                this.name=name;
            }

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
                            case "FORCEOP": this.forceop = intval; if (intval>0) this.suffix+="op"+forceop; break; 
                            case "OP": this.forceop = intval; if (intval>0) this.suffix+="op"+forceop; break; 
                            case "BANK": GlobalArguments.bankexport=intval; break; 
                            case "BANKOUT": GlobalArguments.bankexport=intval; break; 
                            case "BANKEXPORT": GlobalArguments.bankexport=intval; break; 
                            case "WRITEBANK": GlobalArguments.bankexport=intval; break; 
                            // case "ADDMULT": this.forcemult = intval; this.suffix+="Mult"+forcemult; break; // nonfunctional
                            default: tb("PARSEVALUE: property "+property +" not found"); return false;
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
                        mult1 - mult2 - mult3 - mult4 / dt1 - dt2 - dt3 - dt4 _ e1m1    forceop4 - forceop
                        4op example syntax: FM0 Parse 15-3-0-0/3-3-3-3_e9_m1
                        4op example syntax: FM0 Parse 15-3-0-0/3-3-3-3_dt9_mult1
                        
                        OPL / OPL2 patchkey input syntax
                    	Wave1 - Wave2 - Alg / Mult1 - Mult2 _ desired vibrato, desired mult
		                Sine-Sine-Connected1-V1-V2 / 2-2m2v1
                        Sine-Sine-connect0-vib0-vib1 / 2-2 v1 m1

                        OPL2 waves (last two bits) (int -> string via ReturnWaveTypeString)
                        0 Sine      1 Half Sine (Half-wave rectified)
                        2 ABS Sine (Full-wave rectified)     3 Quarter Sine
                    */
                    if (value=="P") {CleanPatchReport=true; return false;} // if P P
                    string s=""; //* debug

                    // value = value.ToUpper(); // redundant when implimented
                    // handle synonyms
                    value = value.Replace("OUTOP", "F"); // * note these need to be in a specific order so they don't mangle each other
                    value = value.Replace("FORCEOP", "F");
                    value = value.Replace("OP", "F"); 
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

                    // s+="ParseValues2: input="+value+" "; //* debug
                    // tb(value);Console.ReadKey();

                    string[] StringSeparators, Segments;
                    if (operators==2) { // remove letters from required section. EG: "0-0-a0-v0-v1 / 2-3v1m1" -> 0-0-0-0-1 / 2-3_v1m1
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
                    string[] values1 = Segments[0].Split(StringSeparators, StringSplitOptions.RemoveEmptyEntries); // 4op: ML
                    string[] values2 = Segments[1].Split(StringSeparators, StringSplitOptions.RemoveEmptyEntries); // 4op: DT

                    // PrintStringArray(values1); // debug
                    // PrintStringArray(values2);
                    // tb(commands);'

                    var parser_out = new Dictionary<string,int>();
                    if (operators == 4) {  // handle identifiers (required values)
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
                            case "F": parser_out[desiredForceOp] = numerics[i+1]; break;
                        }
                    }

                    PatchKeys2.Add(parser_out); // copy parsed patchkey to PatchKeys list

                    s+="Output(dictionary form)= "+PrintPatch(parser_out, operators); //* debug
                    // foreach (string k in parser_out.Keys) {
                    //     s+=k+"="+parser_out[k]+" ";
                    // }
                    // tb(s); //* debug text
                    this.LookForPatchKeys = true;
                    return true; // return true if succesful and dictionary as ref, much like int32.TryParse
                }
            }

            public bool CompareTwoPatchKeys(Dictionary<string,int> Key1, Dictionary<string,int> Key2, String[] keyarray) {
                // if (Key1.SequenceEqual(Key2)) return true; // old, this needs to be more robust if we want to add additional info, such as full patch data or timecodes
                // check for minimum Count? 
                // tb(Key1.Count+" "+Key2.Count); Console.ReadKey();
                foreach (KeyValuePair<string,int> kv in Key1) {
                    if (keyarray.Contains(kv.Key)) { // patchkey_keys: only compare relevant patchkeys like mult, dt, desiredDTalg, etc - but not things like TL or timecode or whatever
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
                if (operators==2) {
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
                        if (patch.ContainsKey(desiredForceOp)) { OutputData[desiredForceOp] = patch[desiredForceOp]; } // v05
                        if (operators==2 && patch.ContainsKey(desiredVibrato)) { OutputData[desiredVibrato] = Convert.ToByte(patch[desiredVibrato]); }
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
                    if (CompareTwoPatchKeys(existingpatch, inputpatch, patchkey_keys)) {
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

            // public string DecodeRegister(int reg, FMchannel ch) {
            //     // in: reg(byte) 
            //     // out: reg(string) (?) which then goes through decimation based on DTML/MULT/etc...
            //     // ultimate out: io:FMpatch                
            //     if ()

            // }

            public string ReturnLostPatches(int operators) {
                if (LostPatches.Count == 0){return "";}
                string h=""; string s=""; string p=""; // header, string, patch
                // s+=this.name+" ("+LostPatches.Count+" / "+LostPatchCnt.Count+")"+": ---- Lost patch report ---- ";
                h+=this.name+" ("+LostPatches.Count+" / "+LostPatchCnt.Count+")"+": --------  patch report ----- ";
                if (operators==4) h+= "dt setting:"+this.detunesetting+" ("+DetuneDescription(this.detunesetting)+")";
                if (this.forcemult < 99) h+=" mult:"+this.forcemult;
                h+="\n";

                // 3-3-4-4 / 3-3-7-7 ALG4
                // SINE-SINE-ALG1-VIBRAT0-VIBRATO1 / 1-2
                for (int i = 0; i < LostPatches.Count; i++) {
                    if (!CleanPatchReport) s+=SamplesToMinutes(LostPatches[i]["TIMECODE"])+" (Count:"+ LostPatchCnt[i]+") ";
                    p+=PrintPatch(LostPatches[i],operators);
                    if (operators==4) {
                        int outDT = LostPatches[i]["OUTDT"];
                        if (CleanPatchReport) {
                            p+="e"+outDT;
                        } else {
                            p+=" DT"+outDT;
                        }
                    } 
                    int OutMult = LostPatches[i]["OUTMULT"];
                    if (CleanPatchReport) {
                        p+="m"+OutMult;
                    } else {
                        p+=" mult"+OutMult;
                    }
                    if (operators==2) {
                        int OutVibrato = LostPatches[i]["OUTVIBRATO"];
                        if (CleanPatchReport) {
                            p+="v"+OutVibrato;
                        } else {
                            p+=" Vibrato"+OutVibrato;
                        }
                    }
                    if (LostPatches[i].ContainsKey("OUTOP")) {
                        p+=" ForceOP"+LostPatches[i]["OUTOP"];
                    }
                    if (LostPatches[i].ContainsKey("P") && !CleanPatchReport ) p+=" (patchkey)";
                    // s+="idx "+Convert.ToString(Convert.ToInt32(LostPatches[i]["IDX"]),16);
                    if (CleanPatchReport) {
                        s+="p \""+p+"\" ^"; p=""; 
                    } else {
                        s+=p; p="";
                        if (chiptype==0x54) { /// XX-YYYYY DT2 / SR. (DT2 cut from OPN to make Chowning cry)    
                            // string[] k = new string[]{SR_DT2+1,SR_DT2+1,SR_DT2+3,SR_DT2+4}; // * show DT2 info in patch report
                            bool dt2present=false;
                            p=" ! DT2=";
                            string ratios="";
                            // foreach (string lbl in k) {
                                // PrintDictionary(LostPatches[i]); // debug
                            for (int ii = 1; ii < 5; ii++) {
                                byte dt2=Convert.ToByte(LostPatches[i]["dt2"+ii]); // can't use SR_DT2 for this as it'll only be there if bankexport is enabled
                                if (dt2 > 0x00) dt2present=true;
                                p+=dt2;
                                ratios+=String.Format("{0:0.00}",DT2ratio(dt2,Convert.ToByte(LostPatches[i]["mult"+ii])) )+" ";
                            }
                            if (dt2present) {
                                s+=p+" ratios = "+ratios; 

                            }
                            p="";
                        }
                        if (Channel3ModeDetected) {
                            if (LostPatches[i].ContainsKey("ch3mode")) {
                                if (LostPatches[i]["ch3mode"] == 1) {
                                    s+=" ! CH#3 Extended Mode";
                                }
                            }
                        }


                    }
                    s+="\n";
                }

                return h+s;
            }

            public string AddGlobalValuesToFilename(){ //* only use for global values
                string s="";
                if (forcemult < 16 && forcemult > -16) s+= "Mult"+forcemult;
                // if (altwaveform > 0) s+= "AltWave"; 
                if (this.LookForPatchKeys) s+= "MultiPatch";
                if (forceop > 0 && forceop < 5) s+= "Op"+forceop;
                if (operators==4){
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
                // if (this.altwaveform > 98) this.altwaveform = GlobalArguments.altwaveform;
                if (!this.LookForPatchKeys && GlobalArguments.LookForPatchKeys){
                    // this.PatchKeys = GlobalArguments.PatchKeys.Cast<FMpatchkey>().ToList();
                    this.PatchKeys2 = GlobalArguments.PatchKeys2.Cast<Dictionary<string,int>>().ToList();
                    this.LookForPatchKeys=true;
                    // tb("MatchAgainstGlobalValues: Casting FMpatchkey list (Count="+this.PatchKeys2.Count()+") to "+this.name);
                }
                if (!this.CleanPatchReport && GlobalArguments.CleanPatchReport) this.CleanPatchReport = true;
            }

            public string Report(){ //* just debug
                return this.name+" settings:"
                +" DT"+this.detunesetting
                +" forceMULT\n"+this.forcemult;
                // +" forceOP"+this.forceop
                // +" AltWave"+this.altwaveform+"\n";

            }
        }

        public class FMregisters {  // for the main loop, value holder for our simulated FM chip. Holds values, and their last known IDX in data
            // This class consists of two Dictionaries related to Values
            // REG_IDX and REG_VAL    --  and a few methods derived from these: LABEL_IDX LABEL_VAL
            // and a FM *channel* reference called FMref which has all known registers in string label - byte register form (and reverse)
            // finally, one more list of registers called registers_we_can_overwrite, we will overwrite data at our triggerify

            /* a lot of data going on here, an example
            ex: 0x000102: 57 48 1F  CHANNEL: FM3 (implicit, this class encompasses 1 channel's registers (channel and all operators), no global commands)
                                    LABEL: TL2 (string - "TL2" = 0x48) (operator 2's TL)
                                    REG: 48 (byte)
                                    VALUE:1F  (byte)
                                    IDX = 0x102 (int32)
                                    operators = 4 (int32) implicit from SetupData->FMchannel. This is an OPNA example.
                                    chip = 57 (int32) implicit from SetupData->FMchannel. OPNA/B/2 have two banks of registers
                                    // KEYON: [0x56, 0x28, 0xF0] - implicit from SetupData->FMchannel. Always 0x56 on OPNA. May end up unused, we'll see.

                OUTPUT of this object: Triggerify Method.
                1. Hard edits to a few registers, such as OPL vibrato registers (OPL)
                2. Soft additions to DTML AND TL values, to be applied after all channels have been looped
                2. Output reference data values to FMarguments.AddLostPatch (<string label, byte value>)
                3. Output *special* data values to FMarguments.AddLostPatch (<string label, byte value>): 
                    "P" (patchkey'ed flag) 
                    "dt1" "dt2" "dt3" "dt4" "mult1" "mult2" "mult3" "mult4" "alg" "wave1" "wave2" "vibrato1" "vibrato2" 
                    "dt21" "dt22" "dt23" "dt24" "ch3mode"
                    "OUTDT" "OUTMULT" "OUTVIBRATO" "IDX" "TIMECODE" 
            */
            public readonly FMchannel FMref; 
            public Dictionary<byte, int> REG_IDX = new Dictionary<byte, int>(); // DATA - BUFFER
            public Dictionary<byte, byte> REG_VAL = new Dictionary<byte, byte>(); // DATA - BUFFER

            public int lastidx=0; // for print
            List<byte> registers_we_can_overwrite;
            
            public FMregisters(FMchannel fMchannel) {

                FMref = fMchannel;
                registers_we_can_overwrite = new List<byte>();

                var dictionary_initcmds=ReplaceableCommands_2op;
                if (operators==4) dictionary_initcmds=ReplaceableCommands_4op; 

                foreach (var kv in dictionary_initcmds) {
                    for (int i = 1; i < operators+1; i++) {
                        registers_we_can_overwrite.Add(FMref.REF_LABEL_REG[kv.Key+i]); // list of registers we can overwrite with our replacement commands in Triggerify
                    }
                }


                foreach(KeyValuePair<string, byte> LG in FMref.REF_LABEL_REG) { // outputs LG.Key, LG.Value (register)
                    this.REG_VAL.Add(LG.Value, 0x00); // Register, init value 0x00
                    this.REG_IDX.Add(LG.Value, 0); // Register, init idx value 0
                }

                // initialize DTML idxes? I believe this is safe to do, as Triggerify doesn't make hard edits so much anymore
                for (int i = 1; i < operators+1; i++) {
                    this.REG_IDX[FMref.REF_LABEL_REG[DTML+i]] = startVGMdata;
                }



                // PrintDictionary(REG_VAL);

            }
            public int LABEL_IDX(string label) { // returns IDX, via REG_IDX & LABEL_REG dictionaries
                return REG_IDX[FMref.REF_LABEL_REG[label]]; 
            }
            public byte LABEL_VAL(string label) {
                return REG_VAL[FMref.REF_LABEL_REG[label]];
            }
            // static int lastidx_DTML1, lastidx_DTML2, lastidx_DTML3, lastidx_DTML4; // unused
            static int muteA=1, muteB=2, muteC=3; // ? local var?
            public int outop=0;
            // public int currentTLoutOP = 0;
            public string Triggerify(Arguments FMargs, int currentIDX, Dictionary<byte,int> DTMLop_idx) {
                string str=$"Triggerify {FMref.name}:";
                byte LastDTMLop = DTMLop_idx.Aggregate((l, r) => l.Value < r.Value ? l : r).Key;
                // byte FirstDTMLop = DTMLop_idx.Aggregate((l, r) => l.Value > r.Value ? l : r).Key; // I don't really see any reason to use this over last idx
                // // //* Triggerify Part 1 / 4: if any DT (or DT idx) is empty then log & skip. Should only occur with early garbage data...      old, this should only be called if DTML is found!
                // // str=FMref.name+": !WARNING!: 0x"+Convert.ToString(lastidx,16)+": MISSING ";
                // bool warn=false;
                // // string[] fullpatch = new string[]{"DTML","TL"};
                // string[] fullpatch = new string[]{"DTML"};

                // foreach (string s in fullpatch) {   // look for DTML1 DTML2 DTML3 DTML4
                //     for (int i = 1; i < operators+1; i++) {
                //         if (LABEL_IDX(s+i) == 0) {
                //             str+=s+i+" idx=0(!) "; warn=true;
                //         // } else {
                //         //     str+=s+i+"="+LABEL_IDX(s+i)+" ";
                //         }
                //     }
                //     // tb(FMref.name+"chip :"+Convert.ToString(FMref.chip,16)+" :"+str); Console.ReadKey();
                // }

                // str+=" ... lag+"+(currentIDX - lastidx)+" bytes";
                // // if (warn) {tb(str); System.Console.ReadKey(); return str;}
                // if (warn) {tb(str); return str;}
                // // tb(str);
                // str="";


                // * Triggerify Part 2 / 4: Compare our identified patch with our patch algos or PatchKeys (both come from FMx.Arguments)
                // *     Set our DT, ML, and Vibrato(OPL) accordingly
                var datavalues = new Dictionary<string,int>();
                if (operators==2) { // OPL2 note - AM|PM|KSR|EG / MULT - No Detune with OPL2. Looks like 4 bit 0-F to me, but I've heard it skips some values (OPLL maybe?)
                    if (chiptype==0x5A || chiptype==0x5E) {
                        datavalues.Add(wave1,Second4BitToInt(LABEL_VAL("WAVEFORM1") ) ); // last 2 bits OPL2, last 3 bits OPL3. Nonexistent OPL1
                        datavalues.Add(wave2,Second4BitToInt(LABEL_VAL("WAVEFORM2") ) );
                    } else {
                        datavalues.Add(wave1, 0); // wave register is very unlikely to be in data, but patchkey syntax will remain the same
                        datavalues.Add(wave2, 0);
                    }
                    datavalues.Add(alg, Convert.ToInt32(LastBit(LABEL_VAL(FEEDBACK_ALG) ) ) );
                    datavalues.Add(vibrato1,Convert.ToInt32(SecondBit(LABEL_VAL("DTML1") ) ) );
                    datavalues.Add(vibrato2,Convert.ToInt32(SecondBit(LABEL_VAL("DTML2") ) ) );
                    datavalues.Add(mult1, Second4BitToInt(LABEL_VAL("DTML1"))); 
                    datavalues.Add(mult2, Second4BitToInt(LABEL_VAL("DTML2"))); 

                    // tb(FMref.name+"@ 0x"+lastidx+" Connect/Vibrato=alg"+LastBit(LABEL_VAL(alg))+" vib"+ Convert.ToString(SecondBit(LABEL_VAL("DTML1")),16)+"-"+Convert.ToString(SecondBit(LABEL_VAL("DTML2")),16));

                } else if (operators == 4){
                    datavalues.Add(dt1,First4BitToInt(LABEL_VAL("DTML1"))); datavalues.Add(mult1,Second4BitToInt(LABEL_VAL("DTML1")));
                    datavalues.Add(dt2,First4BitToInt(LABEL_VAL("DTML2"))); datavalues.Add(mult2,Second4BitToInt(LABEL_VAL("DTML2")));
                    datavalues.Add(dt3,First4BitToInt(LABEL_VAL("DTML3"))); datavalues.Add(mult3,Second4BitToInt(LABEL_VAL("DTML3")));
                    datavalues.Add(dt4,First4BitToInt(LABEL_VAL("DTML4"))); datavalues.Add(mult4,Second4BitToInt(LABEL_VAL("DTML4")));
                    //              xx - L/R (OPM only)
                    //        B0-B3 --xxx--- Feedback level for operator 1 (0-7)
                    //              -----xxx Operator connection algorithm (0-7)
                    datavalues.Add(alg, Convert.ToInt32(Last3Bit(LABEL_VAL(FEEDBACK_ALG) ) ) );
                }
                int OutDT=99; int OutMult=99; int OutDTalg=99; int OutVibrato=99;//int OutCarrier=null;
                outop=FMargs.forceop;

                if (FMargs.LookForPatchKeys) { 
                    var keyvalues = new Dictionary<string,int>();
                    if (FMargs.DataMatchesPatchKeys(datavalues, ref keyvalues)) { // this will handle all loop stuff
                        int tmpvalue=99;
                        if (keyvalues.TryGetValue(desiredDTalg, out tmpvalue)) OutDTalg=tmpvalue; //* bugfix 041 - TryGetValue will 'out' 0 if no match. This worked when I used 'int?'...
                        if (keyvalues.TryGetValue(desiredMult, out tmpvalue)) OutMult=tmpvalue;
                        if (keyvalues.TryGetValue(desiredVibrato, out tmpvalue)) OutVibrato=tmpvalue;
                        if (keyvalues.TryGetValue(desiredForceOp, out tmpvalue)) {
                            outop=tmpvalue;
                            if (outop > FMref.operators) outop=0;
                            datavalues["OUTOP"]=outop; // forceop should be niche, as a command only use if necessary
                            // if (outop > 0) tb(FMref.name+": outop {0} at idx 0x_{1}",outop,Convert.ToString(lastidx,16));
                        } 
                        datavalues["P"]=1; // for ReturnLostPatches: mark this patch as succesfully patchkey'ed
                    }
                } 

                // str += current_values[4]+"-"+current_values[5]+"-"+current_values[6]+"-"+current_values[7]+" / "; // debug, will break with OPL2, remove me
                // str += current_values[0]+"-"+current_values[1]; // opl2 debug
                // for (int i = 0; i < current_values.Length; i++) {
                //     str += current_values[i]+"-";
                // }

                byte outDTML=0x00;// = data[LABEL_IDX("DTML"+outop)+2];
                // * Triggerify Part 3 / 4: Make Hard Edits to DTML, edits to TL are added to "appender" to be added after all the channels are done
                // // careful with these LABEL_VAL calls, they're copied from the initial data and may be outdated (race condition) 
                if (operators == 4) {  //* handle DT
                    if (outop==0) {  // if ForceOp=0, use last DTML found (extra reliable for mult sweeps or incomplete patch data)
                        outop=LastDTMLop; // implicit conversion to int
                    }
                    if (outop == 1) {muteA=2; muteB=3; muteC=4;}
                    if (outop == 2) {muteA=1; muteB=3; muteC=4;}
                    if (outop == 3) {muteA=2; muteB=1; muteC=4;}
                    if (outop == 4) {muteA=2; muteB=3; muteC=1;}

                    if (OutDTalg == 99) { OutDTalg=FMargs.detunesetting; } 
                    OutDT = ReturnDesiredDT(datavalues, OutDTalg); //* <--- 'big function' for all DT algorithms

                    // byte debug = data[LABEL_IDX("DTML4")+2];
                    // data[LABEL_IDX("DTML4")+2] = FourToEightCoder(Convert.ToByte(OutDT) , Second4Bit(LABEL_VAL("DTML4")) );             //* WRITE DT (4-op only) old
                    // data[LABEL_IDX("DTML"+outop)+2] = FourToEightCoder(Convert.ToByte(OutDT) , Second4Bit(LABEL_VAL("DTML"+outop)) );             //* WRITE DT (4-op only) 
                    outDTML = FourToEightCoder(Convert.ToByte(OutDT), Second4Bit(LABEL_VAL("DTML"+outop)) );                //* SETUP DT FOR WRITE DT (APPEND METHOD v42)

                    datavalues["OUTDT"] = OutDT; 

                    // tb(FMref.name+": DT 0x"+Convert.ToString(LABEL_IDX("DTML4")+0,16)+
                    // " "+Convert.ToString(debug,16)+" -> "+Convert.ToString(data[LABEL_IDX("DTML4")+2],16)+" val "+Convert.ToString(First4Bit(LABEL_VAL("DTML4")),16) );
                    // Console.ReadKey();
                } else if (operators==2) {
                    outop=2; // always use carrier with OPL (?)
                    if (outop == 1) muteA=2;
                    if (outop == 2) muteA=1;
                    outDTML = data[LABEL_IDX("DTML"+outop)+2];                //* SETUP DT FOR WRITE DT (APPEND METHOD v42)
                }

                if (OutMult == 99) { // if mult is defined in patch key use it, otherwise automatically compensate    -- Handle Mult
                    if (operators==2) {
                        OutMult=HighestCommonFactorINT(new int[] {Second4BitToInt(LABEL_VAL("DTML1")), Second4BitToInt(LABEL_VAL("DTML2") )} ); // no need for careful we're not making hard edits yet
                    } else {
                        OutMult=HighestCommonFactorINT(new int[] {Second4BitToInt(LABEL_VAL("DTML1")),Second4BitToInt(LABEL_VAL("DTML2")),Second4BitToInt(LABEL_VAL("DTML3")),Second4BitToInt(LABEL_VAL("DTML4") )} );
                    }
                }
                datavalues["OUTMULT"] = OutMult;

                // * Setup output DT/ML based on arguments for our channel, also vibrato for OPL
                if (operators == 2) {
                    if (OutVibrato != 99) {
                        outDTML = CodeSecondBit(outDTML, Convert.ToByte(OutVibrato));             //* WRITE CARRIER VIBRATO (OPL)
                        datavalues["OUTVIBRATO"] = OutVibrato;
                    } else {
                        datavalues["OUTVIBRATO"] = datavalues[vibrato2];
                    }
                    // data[LABEL_IDX("DTML2")+2] = FourToEightCoder(First4Bit(data[LABEL_IDX("DTML2")+2]), Convert.ToByte(OutMult) );                //* WRITE MULT
                    outDTML = FourToEightCoder(First4Bit(outDTML), Convert.ToByte(OutMult) );                //* SETUP MULT FOR WRITE DTML (APPEND METHOD v42)
                    outDTML = (byte)(outDTML << 1); outDTML = (byte)(outDTML >> 1); // * kill AM LFO flag while we're here. OPL: XXXXYYYY AM enable / PM enable / EG type / KSR - MULT 
                    // data[LABEL_IDX("TL2")+2] = 12; // set volume OPL2 - first two bits are key scale LEVEL, 0,1,2= 00, 01, 10. Rest is TL, a 6-bit value of 0-63 (3F = muted)
                    // Appender.Add(DTMLop_idx[LastDTMLop], new byte[]{FMref.chip, FMref.REF_LABEL_REG["TL"+outop], 0x00, 
                    // Append(DTMLop_idx[LastDTMLop], new byte[]{FMref.chip, FMref.REF_LABEL_REG["TL"+outop], 0x00, // old
                    //                                      FMref.chip, FMref.REF_LABEL_REG["TL"+muteA], 0x3F,
                    //                                      FMref.chip, FMref.REF_LABEL_REG["DTML"+outop], outDTML});
                    // Appender.Add(LastDTMLidx, new byte[]{FMref.chip, FMref.REF_LABEL_REG["TL"+muteA], 0x3F}); //* can't have multiple keys
                } else {   // 4-op
                    // data[LABEL_IDX("DTML"+outop)+2] = FourToEightCoder(First4Bit(data[LABEL_IDX("DTML"+outop)+2]), Convert.ToByte(OutMult) );                //* WRITE MULT
                    outDTML = FourToEightCoder(First4Bit(outDTML), Convert.ToByte(OutMult) );                //* SETUP MULT FOR WRITE DTML (APPEND METHOD v42)
                    // Appender.Add(LastDTMLidx, new byte[]{FMref.chip, FMref.REF_LABEL_REG["TL"+outop], 0x00, //* WRITE TL (LATER, AFTER AutoTriggers HAVE ALL BEEN RUN) old see below
                    //                                      FMref.chip, FMref.REF_LABEL_REG["TL"+muteA], 0x7F,
                    //                                      FMref.chip, FMref.REF_LABEL_REG["TL"+muteB], 0x7F,
                    //                                      FMref.chip, FMref.REF_LABEL_REG["TL"+muteC], 0x7F,
                    //                                      FMref.chip, FMref.REF_LABEL_REG["DTML"+outop], outDTML}); 

                }

                // * okay what we're going to try to do to improve sync in emulators that use a queue for commands..
                // the problem: By removing values nearest the keyon, our extt output has a quicker response, leading to the initial note being offset to the right
                // the solution: search around LastDTMLidx (if we hit a keyon, we've gone too far) for existing DTML or TL values, then replacing those writes with our new ones. 
                // Essentially: Here we are now *replacing* commands where we can. Using Pokey and then Appender. 
                // then later in main loop we are *modifying* our TL values to filler envelope writes, rather than removing them
                // it won't always be 1:1 but hopefully it'll be a big improvement!

                // bool addlegacy=false; // fallthrough condition if we don't find any values to replace old
                // if (FoundKeyOn(DTMLop_idx[LastDTMLop], FMref, out int keyonpos, 244)) { // search forward for keyon. ~6ms tolerance. If it doesn't find a keyon, it'll return the start position again
                bool test = FindKeyOn(DTMLop_idx[LastDTMLop], FMref, out int keyonpos, 244);  // search forward for keyon. ~6ms tolerance. If it doesn't find a keyon, it'll return the start position again

                // tb(test.ToString());

                // tb($"outop = {outop}"); Console.ReadKey();

                // registers_we_can_overwrite.ForEach(x => tb($"{FMref.REF_REG_LABEL[x]} 0x_{Convert.ToString(x,16)}"));

                List<int> replaceable_indxs;

                // * registers_we_can_overwrite (FMregisters constructor, based on ReplaceableCommands_*op) Basically, it's a bunch of envelope writes + DTML and TL values. All are per-Operator registers.
                if (ReturnDataIdxsToOverwrite(keyonpos, FMref, registers_we_can_overwrite, out replaceable_indxs, 5, 300)  // returns false if can't find *any* indexes. That shouldn't happen
                    || ReturnDataIdxsToOverwrite(DTMLop_idx[LastDTMLop], FMref, registers_we_can_overwrite, out replaceable_indxs, 5, 244) ) // as a backup, check backwards from keyon. This should always find at least 1
                { 
                    List<byte[]> cmdstoindex;
                    if (operators == 2) {
                        cmdstoindex = new List<byte[]>(){
                            new byte[]{FMref.chip, FMref.REF_LABEL_REG["DTML"+outop], outDTML}, // XXXXYYYY AM enable / PM enable / EG type / KSR - MULT  ...  first idx will be closest to the keyon
                            new byte[]{FMref.chip, FMref.REF_LABEL_REG["TL"+outop], 0x00},
                            new byte[]{FMref.chip, FMref.REF_LABEL_REG["TL"+muteA], 0x7F}
                        };

                    } else { // 4op
                        cmdstoindex = new List<byte[]>(){
                            new byte[]{FMref.chip, FMref.REF_LABEL_REG["DTML"+outop], outDTML}, // this will be closest to the keyon
                            new byte[]{FMref.chip, FMref.REF_LABEL_REG["TL"+outop], 0x00},
                            new byte[]{FMref.chip, FMref.REF_LABEL_REG["TL"+muteA], 0x7F},
                            new byte[]{FMref.chip, FMref.REF_LABEL_REG["TL"+muteB], 0x7F},
                            new byte[]{FMref.chip, FMref.REF_LABEL_REG["TL"+muteC], 0x7F}
                        };
                    }

                    for (int i = 0; i < Math.Min(replaceable_indxs.Count,cmdstoindex.Count); i++) { // whichever's smaller, if cmds aren't all handled we'll handle it in the conditional below this one
                        // tb($"Triggerify {FMref.name}: @0x{Convert.ToString(replaceable_indxs[i],16)} -> {ReturnArrayHex(FMref, cmdstoindex[i])}"); Console.ReadKey();
                        // Appender.Add(replaceable_indxs[i], cmdstoindex[i]);
                        Append(replaceable_indxs[i], cmdstoindex[i]); // c++;
                        PokeyDataAtIDX(replaceable_indxs[i]);
                    }

                    if (replaceable_indxs.Count < cmdstoindex.Count) { // if we only found between 1 and 4 replaceable values, add the rest to the earliest index
                        // tb($"Triggerify {FMref.name}: Warning! Mismatch between values we've found ({replaceable_indxs.Count}) and values we need to replace ({cmdstoindex.Count}) @0x{Convert.ToString(currentIDX,16)}."); Console.ReadKey();
                        for (int i = replaceable_indxs.Count; i < cmdstoindex.Count; i++) {
                            // Appender.Add(replaceable_indxs[replaceable_indxs.Count-1], cmdstoindex[i]);
                            // Appender.Add(DTMLop_idx[LastDTMLop], cmdstoindex[i]);
                            Append(DTMLop_idx[LastDTMLop], cmdstoindex[i]); // c++;
                        }
                        PokeyDataAtIDX(DTMLop_idx[LastDTMLop]);
                    }

                    // if (FMref.channel == 0) // debug
                    // tb($"Triggerify {FMref.name}: complete, appended {c} values @ {SamplesToMinutes(timecodes[currentIDX])}"); // Console.ReadKey();

                } else { // ? this should never occur, right?... It should always at least have the dtml value we started with
                    // tb($"... Triggerify {FMref.name}: ReturnDataIdxsToOverwrite returned false! idx={currentIDX} press any key to continue"); Console.ReadKey();
                    tb($"Triggerify {FMref.name}: Warning! No patch data found @ ~{SamplesToMinutes(timecodes[currentIDX])}, stacking values at last found DTML instead (v0.50 legacy behavior)");
                    // Appender.Add(DTMLop_idx[LastDTMLop], new byte[]{FMref.chip, FMref.REF_LABEL_REG["TL"+outop], 0x00, //* WRITE TL (LATER, AFTER AutoTriggers HAVE ALL BEEN RUN)

                    if (operators==2) {
                        Append(DTMLop_idx[LastDTMLop], new byte[]{FMref.chip, FMref.REF_LABEL_REG["TL"+outop], 0x00, 
                                                            FMref.chip, FMref.REF_LABEL_REG["TL"+muteA], 0x3F,
                                                            FMref.chip, FMref.REF_LABEL_REG["DTML"+outop], outDTML});
                    } else {
                        Append(DTMLop_idx[LastDTMLop], new byte[]{FMref.chip, FMref.REF_LABEL_REG["TL"+outop], 0x00, //* WRITE TL (LATER, AFTER AutoTriggers HAVE ALL BEEN RUN)
                                                            FMref.chip, FMref.REF_LABEL_REG["TL"+muteA], 0x7F,
                                                            FMref.chip, FMref.REF_LABEL_REG["TL"+muteB], 0x7F,
                                                            FMref.chip, FMref.REF_LABEL_REG["TL"+muteC], 0x7F,
                                                            FMref.chip, FMref.REF_LABEL_REG["DTML"+outop], outDTML}); 
                    }
                    PokeyDataAtIDX(DTMLop_idx[LastDTMLop]);
                    // Console.ReadKey(); // debug.

                }



                // * additional information to patchkey system
                if (Channel3ModeDetected && FMref.name=="FM2") { // track CH3 extended mode (CSM/Multi-frequency mode)
                    datavalues["ch3mode"] = SecondBit(LABEL_VAL(TIMER_LOAD_SAVE) ); // main adds this system command TIMER_LOAD_SAVE to Ch2 (CH#3)
                }
                if (chiptype==0x54) {   // capture current DT2 for each operator, Lost Patch Report will notify of this
                    datavalues["dt21"] = (byte)(LABEL_VAL(SR_DT2+1) >> 6); // first two bits
                    datavalues["dt22"] = (byte)(LABEL_VAL(SR_DT2+2) >> 6);
                    datavalues["dt23"] = (byte)(LABEL_VAL(SR_DT2+3) >> 6);
                    datavalues["dt24"] = (byte)(LABEL_VAL(SR_DT2+4) >> 6);
                }

                datavalues["IDX"] = lastidx; // * IDX and TIMECODE are the only integers in this collection, the rest are bytes..
                datavalues["TIMECODE"] = timecodes[lastidx]; // * ... can this be worked around in an elegant way?
                
                // str+=FMref.name+" Triggerify: Current Pos 0x"+Convert.ToString(currentIDX,16) + " " +SamplesToMinutes(timecodes[currentIDX]) + "\n";
                // str+="DTML1 0x"+Convert.ToString(LABEL_IDX("DTML1"),16)+" = "+Convert.ToString(LABEL_VAL("DTML1"),16) + "\n";
                // str+="DTML2 0x"+Convert.ToString(LABEL_IDX("DTML2"),16)+" = "+Convert.ToString(LABEL_VAL("DTML2"),16) + "\n";
                // str+="DTML3 0x"+Convert.ToString(LABEL_IDX("DTML3"),16)+" = "+Convert.ToString(LABEL_VAL("DTML3"),16) + "\n";
                // str+="DTML4 0x"+Convert.ToString(LABEL_IDX("DTML4"),16)+" = "+Convert.ToString(LABEL_VAL("DTML4"),16) + "\n";
                // tb(str);
                // Console.ReadKey(); 

                // str=FMref.name+": 0x"+Convert.ToString(lastidx,16)+" ... "+str+"--> mult"+Convert.ToByte(OutMult);
                // if (this.operators==4) str+=" DTout:"+OutDT;
                // tb(str);
                // tb(Convert.ToString(data[LABEL_IDX("DTML4")+2],16) + " should be = "+ Convert.ToString(LABEL_VAL("DTML4"),16) );
                // Console.ReadKey();

                
                // * Triggerify Part 4 / 4: Output our current found patch to AddLostPatch. AddLostPatch may dispose of it, if it's found to be a dupe
                // *    dupes are determined using a limited set of values, mostly just the values of DTML
                if (GlobalArguments.bankexport == 1 && operators==4) {
                    foreach (var reg_lbl in FMref.REF_REG_LABEL) {
                        // datavalues[Convert.ToString(reg_val.Key)] = REG_VAL[reg_val.Key]; // "120" = 0x78  ???

                        //* convert REG-VALUE to LABEL-VALUE, using the data2.cs FMchannel data as our reference
                        // FMref.REF_LABEL_REG and REG_LABEL are derived from FMref.ch, we can use FMin.ch.Op1-op4 to get the operator values

                        // if (REG_LABEL.ContainsKey(reg_val.Key)) {      
 
                        if (FMref.REF_REG_LABEL.ContainsKey(reg_lbl.Key)) {    // check acquired data vs reference data     
                            // tb("bankexport: Ch cmd found output="+datavalues[FMref.REF_FMref.REF_LABEL_REG]+", 0x_"+ Convert.ToString(REG_VAL[reg_lbl.Key],16));
                            datavalues[reg_lbl.Value] = REG_VAL[reg_lbl.Key];   // * this will pump ~40+ values through datavalues that otherwise wouldn't be there
                        }

                    }
                    
                }   
                FMargs.AddLostPatch(datavalues);
                return str; // debug text
            }

        }

        delegate byte ModifyByte(byte b);
        delegate void Modify3Bytes(ref byte b1, ref byte b2, ref byte b3);

        //! Main Loop
        //! find DTML values, look 10ms ahead for full patch values, then apply detune and mult 
        //! After that runs: Smash all DR/AR etc, change mute all operators except the last. Do this last so we get a better snapshot of the patches in use
        static void AutoTrigger(FMchannel FMin, Arguments FMargs) {
            //! main loop!
            // * a brief explanation of what this does
            // Parse through the vgm data, collecting it as we go (via FMregister object REG_IDX/REG_VAL dictionaries) 
            // If we find a DT/ML register, search ~13 ms ahead to anticipate a full patch
            // Then launch FMregister.Triggerify: which makes modifications to the patch according to our FMarguments object
            //      these hard edits are parked into 'Appender' and applied later in the Main() section, so we can safely resize our data buffer (v42+)
            //      the full patch data is sent off via FMarguments.AddLostPatch. This is used for the patch report, and v42+, the .bank export 

            // After Triggerify pass is done, many values are edited (attack rate, algorithm etc). 
            //      These are all per-channel values so they shouldn't taint our data down the line
            // all TL values are removed (without resizing) by turning them into POKEY writes. Appender will provide all our new TL writes

            FMregisters fMregisters = new FMregisters(FMin);
            // v05a - increased threshold slightly to account for 1/100 logs, such as NP21 .s98 -> vgm conversions
            int LagThreshold = 570; // after a DT value is found, look ahead this many samples before Triggerify (441 = 10ms)
            bool BeginDelay=false; // start lag
            int Lag = 0; // in samples, via parsewaits


            var DTMLop_idx = new Dictionary<byte,int>(){};

            for (int i = startVGMdata; i < endVGMdata; i++) {
                if (ByteFlags[i] && data[i]==FMin.chip) // data is structured in 3 bytes: [chip][reg][value]   [chip]=byteflag true
                { 
                    if (fMregisters.REG_VAL.ContainsKey(data[i+1]) ) { //* REG_VAL will begin with all necessary keys for patchkey
                        fMregisters.REG_IDX[data[i+1]] = i; 
                        fMregisters.REG_VAL[data[i+1]] = data[i+2];
                        // tb("0x"+Convert.ToString(i,16) + " added reg "+Convert.ToString(data[+1],16));
                    } else { // * add other registers. This is only necessary for .bank output
                        fMregisters.REG_VAL.Add(data[i+1], data[i+2]);
                        fMregisters.REG_IDX.Add(data[i+1], i);          
                    }
                    // tb("looking for "+Convert.ToString(fMregisters.FMref.REF_LABEL_REG["DTML1"],16) +" "+Convert.ToString(fMregisters.FMref.REF_LABEL_REG["DTML2"],16) +" "
                    // +Convert.ToString(fMregisters.FMref.REF_LABEL_REG["DTML3"],16) +" "+Convert.ToString(fMregisters.FMref.REF_LABEL_REG["DTML4"],16) +" ");
                    if (data[i+1] == fMregisters.FMref.REF_LABEL_REG["DTML1"]) { //* FMref via data2.cs
                        fMregisters.lastidx = i;
                        Lag = 0; BeginDelay=true; // tb("0x"+Convert.ToString(i,16) + " dt1");
                        // LastDTMLidx = i; LastDTMLnmbr = 1;
                        DTMLop_idx[1] = i;
                    } else if (data[i+1] == fMregisters.FMref.REF_LABEL_REG["DTML2"]) {
                        fMregisters.lastidx = i;
                        Lag = 0; BeginDelay=true; // tb("0x"+Convert.ToString(i,16) + " dt2");
                        // LastDTMLidx = i; LastDTMLnmbr = 2;
                        DTMLop_idx[2] = i;
                    } else if (operators == 4) {
                        if (data[i+1] == fMregisters.FMref.REF_LABEL_REG["DTML3"]) {
                            fMregisters.lastidx = i;
                            Lag = 0; BeginDelay=true; // tb("0x"+Convert.ToString(i,16) + " dt3");
                            // LastDTMLidx = i; LastDTMLnmbr = 3;
                            DTMLop_idx[3] = i;
                        } else if (data[i+1] == fMregisters.FMref.REF_LABEL_REG["DTML4"]) {
                            fMregisters.lastidx = i;
                            Lag = 0; BeginDelay=true; // tb("0x"+Convert.ToString(i,16) + " dt4");
                            // LastDTMLidx = i; LastDTMLnmbr = 4;
                            DTMLop_idx[4] = i;
                        }
                    }
                    //  tb($"{FMin.name}");
                }

                if (BeginDelay) {
                    if (WaitFlags[i])
                        Lag += ParseWaits(data,ref i,WaitFlags);
                    if (Lag >= LagThreshold) { // bug with tight loops. Will this cause problems with non-looped?
                        if (DTMLop_idx.Count > 0)
                            fMregisters.Triggerify(FMargs,i,DTMLop_idx); // hard edit happens here

                        Lag=0; BeginDelay=false;
                        DTMLop_idx = new Dictionary<byte,int>();
                    }
                }
            }
            if (BeginDelay) // v0.51 bugfix: Issue with tightly looped VGMs. If we hit end of data but have a patch ready to trigger, finish it up
                if (DTMLop_idx.Count > 0){
                    // tb($"finishing up {FMin.name}"); Console.ReadKey();
                    fMregisters.Triggerify(FMargs,endVGMdata-1,DTMLop_idx);
                }


            //* Pt.B: Global changes (smashing feedback, decay, muting operators, etc)

            // Because our Triggerify may have replaced important envelope writes, we need to check they exist and if they don't, append them to startVGMdata

            var values_to_init = new List<byte>();
            // var values_to_init = new Dictionary<string,byte>();
            var dictionary_initcmds=ReplaceableCommands_2op; // * add replaceable commands to start of file
            if (operators==4) dictionary_initcmds=ReplaceableCommands_4op; 

            // var EnvlpReg_EXTTval_fnd = new Dictionary<byte,(byte newvalue,bool found)>(); // byte register (byte value to replace, bool found before first keyon)
            var EnvlpReg_Val = new Dictionary<byte,byte>(); // byte register, byte replacevalue - no bool necessary, instead just remove the entry
            foreach (var kv in dictionary_initcmds) {
                for (int i = 1; i < operators+1; i++) {
                    // EnvlpReg_EXTTval_fnd[FMin.REF_LABEL_REG[kv.Key+i]] = (kv.Value, false);
                    EnvlpReg_Val[FMin.REF_LABEL_REG[kv.Key+i]] = kv.Value;
                }
            }

            // string str=$"{FMin.name}: Env handle:" ; // debug
            for (int i = startVGMdata; i < endVGMdata; i++) { // Go through until the first keyon to look if an envelope register is already present. If so, flag it. Otherwise, append @ startVGMdata
                if (ByteFlags[i]) {
                    if (IsKeyOn(FMin, data[i], data[i+1], data[i+2])) break; // 4op note - IsKeyOn will consider anything above 0 to be keyon. So ch.3 mode should work fine
                    if (data[i] == FMin.chip) {
                        // foreach (var tpl in EnvlpReg_EXTTval_fnd) {// byte-(byte newvalue, bool found)
                        foreach (var tpl in EnvlpReg_Val) {// byte-(byte newvalue, bool found)
                            // if (EnvlpReg_EXTTval_fnd.ContainsKey(data[i+1])) {
                            if (EnvlpReg_Val.ContainsKey(data[i+1])) {
                                // var tmptpl = EnvlpReg_EXTTval_fnd[data[i+1]];
                                // EnvlpReg_EXTTval_fnd[data[i+1]] = (tmptpl.newvalue, true);
                                // EnvlpReg_EXTTval_fnd.Remove(data[i+1]);

                                EnvlpReg_Val.Remove(data[i+1]);
                                // str+=$"0x_{Convert.ToString(i,16)}={FMin.REF_REG_LABEL[data[i+1]]} "; // debug
                                break;
                            }
                        }
                    }
                }
            }
            // tb(str); // debug
            // tb($"kv count = {EnvlpReg_EXTTval_fnd.Count}");
            // tb($"kv count = {EnvlpReg_Val.Count}");

            // foreach (var kvp in EnvlpReg_EXTTval_fnd) {
            if (EnvlpReg_Val.Count > 0) {
                foreach (var kvp in EnvlpReg_Val) {
                    // if (!kvp.Value.found) {
                        // values_to_init.Add(FMin.chip); values_to_init.Add(kvp.Key); values_to_init.Add(kvp.Value.newvalue);
                        values_to_init.Add(FMin.chip); values_to_init.Add(kvp.Key); values_to_init.Add(kvp.Value);
                        tb($"Triggerify Syncopation note - {FMin.name}: \"{FMin.REF_REG_LABEL[kvp.Key]}\" cmd not found before first keyon, appending to start. @0x_{Convert.ToString(startVGMdata,16)}, +{cts(FMin.chip,16)} {cts(kvp.Key,16)} {cts(kvp.Value,16)}");
                    // }
                }
                Append(startVGMdata, values_to_init.ToArray()); // append initial data to actual start
            }

            Modify3Bytes PokeyMe2 = delegate(ref byte slot, ref byte reg, ref byte val) {
                slot=0xBB; reg=0x00; val=0x00;
            };
            Modify3Bytes ReplaceWithSL_RR = delegate(ref byte slot, ref byte reg, ref byte val) { // should be a safe junk reg write for all FM chips
                if (slot==0xBB) return; 
                slot=FMin.chip; reg=FMin.REF_LABEL_REG[SL_RR+"1"]; val=0x00; 
            };
            Modify3Bytes KillSecondNibble = delegate(ref byte slot, ref byte reg, ref byte val) {
                if (val==0) return;
                val = (byte)(val >> 4);
                val = (byte)(val << 4);
            };

            var Col_Reg_3bytes = new Dictionary<byte,Modify3Bytes>();

            for (int i = 1; i < FMin.operators+1; i++) { 
                // Col_Reg_3bytes[FMin.REF_LABEL_REG["TL"+i]] = PokeyMe2;
                // Col_Reg_3bytes[FMin.REF_LABEL_REG["DTML"+i]] = PokeyMe2; // for OPL, Triggerify should handle the first nibble.  XXXXYYYY AM enable / PM enable / EG type / KSR - MULT

                Col_Reg_3bytes[FMin.REF_LABEL_REG["TL"+i]] = ReplaceWithSL_RR; // ymfm workaround attempt, instead of deleting commands, put some redundant value in there
                Col_Reg_3bytes[FMin.REF_LABEL_REG["DTML"+i]] = ReplaceWithSL_RR; // 

            }
            if (FMin.operators==2) Col_Reg_3bytes[FMin.REF_LABEL_REG[FEEDBACK_ALG]] = KillSecondNibble; // OPL - XXXXYYYZ - CHD/CHC/CHB/CHA output (OPL3 only) / Feedback / ALG

            // * Operator Wide changes vv
            var tmp = new Dictionary<string, byte>();
            if (FMin.operators == 2) { // OPL           // * operator wide commands first (2-4 of these)
                tmp[AR_DR_OPL] = 0xF0;
                tmp[SL_RR] = 0x00;
                tmp[WAVEFORM] = 0x00; // -----XXX OPL3, ------XX OPL2
            } else if (FMin.operators == 4) { // OPM / OPN
                tmp[AR_KSR] = 0b00011111; // XX-YYYYY KSR-AR OPM/OPN ONLY        aka 1f
                tmp[DR_LFO_AM_ENABLE] = 0x00; // X--YYYYY LFO AM Enable / Decay Rate
                tmp[SR_DT2] = 0x00; // XX-YYYYY DT2 / SR
                tmp[SL_RR] = 0x00; // XX-YYYYY DT2 / SR
                if (FMin.chip != 0x54) tmp[SSGEG_ENABLE_ENVELOPE] = 0x00; // ----XYYY SSG-EG enable / SSG-EG envelope (0-7) OPN series only
                // opswap[WAVEFORM] = 0x00; // XXX---- OPZ...   ym2414 only 
            }

            var byteswap = new Dictionary<byte, byte>();
            foreach (var lg in tmp) {      // Add one of the above for each operator ex. SL_RR becomes SL_RR1 SL_RR2 SL_RR3 SL_RR4
                for (int i = 1; i < FMin.operators+1; i++) {
                    byteswap[FMin.REF_LABEL_REG[lg.Key+i]] = lg.Value;
                }
            }

            if (FMin.chip==0x54) { // then we can safely add in channel-wide commands
                byteswap[FMin.REF_LABEL_REG[FEEDBACK_ALG]] = 0xC7;    // On OPM the first 2 bits are for stereo, so setting first nibble to 0 will mute!
            } else if (operators == 4) {
                byteswap[FMin.REF_LABEL_REG[FEEDBACK_ALG]] = 0x07;    // OPN
            }
            // TODO mono output? should this be an argument? OPM stereo is in FEEDBACK_ALG, OPNx is in LFO_CHANNEL_SENSITIVITY, OPL3 FEEDBACK_ALG

            // PrintDictionary(byteswap); Console.ReadKey();

            // int c=0;
            for (int i = startVGMdata; i < endVGMdata; i++) { // * combined all smasher writes into one loop (well, one per channel anyway)
                if (ByteFlags[i] && data[i]==FMin.chip) {
                    foreach (var rv in byteswap) {  // * simple full byte swaps
                        if (data[i+1] == rv.Key) {
                            data[i+2] = rv.Value; // c++;
                        }
                    }
                    foreach (var reg_3bytefunc in Col_Reg_3bytes) { // register, delegate to run (over all 3 bytes, though only PokeyMe2 rewrites all of them)
                        if (data[i+1] == reg_3bytefunc.Key) { // * more complicated writes (kill just first bit, kill second nible, pokeyme etc)
                            // tb("{0} {1} {2} <- old",Convert.ToString(data[i],16),Convert.ToString(data[i+1],16),Convert.ToString(data[i+2],16));
                            // tb("{0} {1} {2} <- old",Convert.ToString(data[i],16),FMin.REF_REG_LABEL[data[i+1]],Convert.ToString(data[i+2],16));
                            reg_3bytefunc.Value(ref data[i],ref data[i+1],ref data[i+2]); 
                            // tb("{0} {1} {2} <- new",Convert.ToString(data[i],16),Convert.ToString(data[i+1],16),Convert.ToString(data[i+2],16)); Console.ReadKey();
                        }
                    }
                }
            }

            // tb(showprogress);
        }

        // public static string showprogress="", lastprogress=""; //* timer is nonfunctional atm
        // public static Timer? ProgressTimer; // why is this non nullable
        // public static void UpdateProgress(Object source, System.Timers.ElapsedEventArgs e){
        //     // Console.WriteLine("Raised: {0}", e.SignalTime);
        //     if (showprogress != lastprogress){ // quick and dirty
        //         tb(showprogress);
        //     }
        //     lastprogress = showprogress;
        // }

        #region VGM-format-related helper functions
        static void PokeyDataAtIDX(int idx) {
            data[idx] = 0xBB; data[idx+1] = 0x00; data[idx+2] = 0x00;
        }
        static Dictionary<int, byte[]> Appender = new Dictionary<int, byte[]>(); // v42 DTML and TL is appended to the data later

        static void Append(int idx, in byte[] a) {
            if (!Appender.ContainsKey(idx)) {
                Appender[idx]=a;
            } else {
                var b = Appender[idx]; // temp array of existing contents
                var newarray = new byte[(a.Length+b.Length)];
                a.CopyTo(newarray,0); b.CopyTo(newarray,a.Length);
                var oldcnt=a.Length; // * debug
                Appender[idx]=newarray;

                // * debug
                string str = ""; foreach (var v in newarray) str+=$"{cts(v,16)} ";
                // tb($"Appender: 0x_{Convert.ToString(idx,16)} combined arrays. new array ={str} (cnt={oldcnt}->{newarray.Length})");
            }

        }

        // looking backwards through data, find 5 TL or DTML values and return their indexes
        // input: a list of registers to look for
        static bool ReturnDataIdxsToOverwrite(int startpnt, in FMchannel FM, in List<byte> regs, out List<int> indxs, int IndexToFind, int tolerance) {
            indxs = new List<int>();
            int t=0;
            // tb($"startpnt = {startpnt} startvgmdata={startVGMdata}");
            // int IndexToFind = regs.Count;
            for (int i = startpnt; i > startVGMdata; i--) {
                if (t > tolerance) {  // hit tolerance, unsuccesful
                    if (indxs.Count==0) {
                        indxs = new List<int>(){0};
                        tb($"ReturnDataIdxsToOverwrite {FM.name} @ 0x_{Convert.ToString(startpnt,16)}: Backwards search yielded no results!");
                        return false;
                    } else {          // hit tolerance, partial success

                        // tb($"ReturnDataIdxsToOverwrite: Partial success! Found {indxs.Count}/5! = {ReturnList(indxs)}");
                        return true;
                    }
                    
                }
                t+=ParseWaitsLegacy(data, i, WaitFlags); // parsewaitslegacy doesn't increment indxes so we can use it backwards

                // tb($"{ByteFlags[i]} && {Convert.ToString(data[i],16)} == {Convert.ToString(FM.chip,16)} ??");
                // tb($"{cts(data[i],16)} {cts(data[i+1],16)} {cts(data[i+2],16)}");
                // tb($"{regs.Contains(data[i+1])}");
                // Console.ReadKey();

                if (ByteFlags[i] && data[i] == FM.chip) {
                    if (regs.Contains(data[i+1])) {
                        indxs.Add(i);
                        IndexToFind--; // regs.Remove(data[i+1]);
                    }
                }
                // if (regs.Count==0) return true;
                if (IndexToFind==0) {
                    // tb($"ReturnDataIdxsToOverwrite: Total success! Found {indxs.Count}/5! = {ReturnList(indxs)}");
                    return true;
                }
            }

            if (indxs.Count>0) {
                // tb($"ReturnDataIdxsToOverwrite {FM.name} @ 0x_{Convert.ToString(startpnt,16)}: Partial success! Found {indxs.Count}/5! = {ReturnList(indxs)}"); 
                return true;  
            }
            return false; // unsuccesful, found startVGMdata

        }

        static bool FindKeyOn(int startpoint, in FMchannel FM, out int idx, int tolerance) {
            int t=0;
            for (int i = startpoint; i < endVGMdata; i++) {
                if (t >= tolerance) {
                    idx = startpoint;
                    return false;
                }
                t+=ParseWaits(data, ref i, WaitFlags);
                if (ByteFlags[i] && IsKeyOn(FM, data[i], data[i+1], data[i+2])) {
                    idx = i;
                    return true;
                }
            }
            idx = 0; return false;
        }

        static bool IsKeyOn(in FMchannel FM, byte b1, byte b2, byte b3) {
            if (operators == 4) {
                if (b1 == chiptype && b2 == FMSystemList[0][KEYON_OFF]) { 
                    int tmp = (int)Last3Bit(b3); // channel identifier
                    int channel = FM.channel;
                    if (chiptype != 0x54 && channel > 2) channel++; // OPN channel indexes are screwy: 0-1-2 | 4-5-6
                    // tb($"{cts(b1,16)} {cts(b2,16)} {cts(b3,16)}");
                    // tb($"{channel} -- {tmp}"); Console.ReadKey();
                    if (channel == tmp) {
                        tmp = (byte) b3 >> 4;
                        return (tmp > 0) ? true : false;
                    }
                } 
            } else {
                if (b1 == FM.chip && b2 == FM.REF_LABEL_REG[FNUM_MSB_KEYON_OPL]) { // OPL - --XYYYZZ KeyOn / Block / 2-bit FNUM MSB
                    var tmp = (byte) b3 << 2;
                    tmp = (byte) tmp >> 7;
                    return (tmp > 0) ? true : false;
                }
            }
            return false;

        }


        static void ParseVGMHeader() {
            if (data[0]!=0x56 && data[1]!=0x67 && data[2]!=0x6D) { // V G M 
                tb("Error: Invalid File \""+filename+"\" (VGM identifier not found)"); 
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(2));
                Environment.Exit(1);
            }

            startVGMdata = (Get32BitInt(data,0x34)+0x34); // tb("DEBUG: VGM data start point: 0x"+Convert.ToString(startVGMdata,16) );
            endVGMdata = (Get32BitInt(data,0x04)+0x04); // tb("DEBUG: VGM data end point: 0x"+Convert.ToString(endVGMdata,16) );

            if (endVGMdata > data.Length) {
                tb($"Warning! VGM data end point (0x_{Convert.ToString(endVGMdata,16)}) > file size! setting endVGMdata to 0x{Convert.ToString(data.Length-1,16)}"); 
                endVGMdata = data.Length-1; System.Threading.Thread.Sleep(1000);
            }

            if (Get32BitInt(data,0x10) > 0) {
                tb("Chip Detection: 0x10 "+Get32BitInt(data,0x10)+" YM2413 clock rate found but chip not supported!");
            } else if (Get32BitInt(data,0x30) > 0 && startVGMdata > 0x30) { // * v050 check header length using startvgm data - old VGMs have very tiny headers
                chiptype=0x54; tb("Chip Detection: 0x30 clockrate: "+Get32BitInt(data,0x30)+" YM2151 OPM found");
            } else if (Get32BitInt(data,0x44) > 0 && startVGMdata > 0x44){
                chiptype=0x55; tb("Chip Detection: 0x44 clockrate: "+Get32BitInt(data,0x44)+" YM2203 OPN found"); 
            } else if (Get32BitInt(data,0x48) > 0 && startVGMdata > 0x48){
                chiptype=0x56; tb("Chip Detection: 0x48 clockrate: "+Get32BitInt(data,0x48)+" YM2608 OPNA found"); 
            } else if (Get32BitInt(data,0x4C) > 0 && startVGMdata > 0x4c){
                chiptype=0x58; tb("Chip Detection: 0x4C clockrate: "+Get32BitInt(data,0x4C)+" YM2610 OPNB found"); 
            } else if (Get32BitInt(data,0x50) > 0 && startVGMdata > 0x50){
                chiptype=0x5A; tb("Chip Detection: 0x50 clockrate: "+Get32BitInt(data,0x50)+" YM3812 OPL2 found"); 
            } else if (Get32BitInt(data,0x54) > 0 && startVGMdata > 0x54){
                chiptype=0x5b; tb("Chip Detection: 0x54 clockrate: "+Get32BitInt(data,0x54)+" YM3526 OPL found"); 
            } else if (Get32BitInt(data,0x58) > 0 && startVGMdata > 0x58){
                chiptype=0x5c; tb("Chip Detection: 0x58 clockrate: "+Get32BitInt(data,0x58)+" MSX-AUDIO Y8950 (OPL) found"); 
            } else if (Get32BitInt(data,0x5C) > 0 && startVGMdata > 0x5c){
                chiptype=0x5E; tb("Chip Detection: 0x5C clockrate: "+Get32BitInt(data,0x5C)+" YMF262 OPL3 found"); 
            } else if (Get32BitInt(data,0x60) > 0 && startVGMdata > 0x60){
                tb("Chip Detection: 0x60 "+Get32BitInt(data,0x60)+" YMF278 OPL4 found, but not supported!");
                // chiptype=0x60; tb("Chip Detection: 0x60 clockrate: "+Get32BitInt(data,0x60)+" YMF278 OPL4 found"); // 
            } else if (Get32BitInt(data,0x64) > 0 && startVGMdata > 0x64){
                tb("Chip Detection: 0x64 "+Get32BitInt(data,0x64)+" YMF271 OPX found, but not supported!");
                // chiptype=0x64; tb("Chip Detection: 0x60 clockrate: "+Get32BitInt(data,0x64)+" YMF271 OPX found"); // 
            } else if (Get32BitInt(data,0x2C) > 0 && startVGMdata > 0x2c) {
                chiptype=0x52; tb("Chip Detection: 0x2C clockrate: "+Get32BitInt(data,0x2C)+" YM2612 OPN2 or YM3438 OPN2C found"); // check OPN2 last, as OPN2 DAC tends to be repurposed 
            }  
        }

        static public bool[] ExamineVGMData(bool quiet) { // updated v42: DAC stream, complete (maybe) data skip
            string detectedchipcodes="";
            bool[] byteflag = new bool[endVGMdata];
            bool toif = false; int c=0;
            // tb("datalength="+data.Count());
            for (int i = 0; i < endVGMdata;i++) {byteflag[i]=false;} // initialize all flags to false

            int[] chips = new int[256]; //* log first location of chip code
            for (int i = 0; i < chips.Length; i++) {chips[i]=0;};
            // tb("start = 0x"+Convert.ToString(start,16));
            for (int i = startVGMdata; i < endVGMdata; i++){
                switch (data[i]){
                    //* skip (and log) additional chip cmnds
                    case byte n when (n >= 0x30 && n <= 0x3F): if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=1; break; // dual chip two-bytes
                    case 0x4F: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=1; break; // two-byte GameGear command (these show up on Genesis)
                    case 0x50: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=1; break; // two-byte SN76496 command (such as Genesis/MD PSG)
                    case 0xA0: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte AY8910 command (such as x1 turbo)
                    case 0xB0: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte RF5C68 command
                    case 0xB1: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte RF5C164 command
                    case 0xB5: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte MultiPCM command
                    case 0xB6: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte uPD7759 command
                    case 0xB7: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte OKIM6258 command
                    case 0xB8: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte OKIM6295 command
                    case 0xB9: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte HuC6280 command
                    case 0xBA: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte K053260 command
                    case 0xBB: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte POKEY command
                    case 0xBC: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte WonderSwan command
                    case 0xBD: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte SAA1099 command
                    case 0xBE: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte ES5506 command
                    case 0xBF: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte GA20 command
                    case byte n when (n >= 0x40 && n <= 0x4E): if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // dual chip three-bytes
                    case byte n when (n >= 0xA1 && n <= 0xAF): if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // dual chip three-bytes cnt 
                    case 0xC0: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=3; break; // Four-byte Sega PCM command
                    case byte n when (n >= 0xC1 && n <= 0xD6): if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=3; break; // Other Four-byte cmds
                    case byte n when (n >= 0xC9 && n <= 0xCF): if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=3; break; // dual chip Four-bytes
                    case byte n when (n >= 0xD7 && n <= 0xDF): if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=3; break; // dual chip Four-bytes cnt.
                    case 0xE1: if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=4; break; // Five-byte C352 command
                    case byte n when (n >= 0xE2 && n <= 0xFF): if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=4; break; // dual chip five-bytes
                    case 0x52:  //* If OPM+OPN2 it's probably the Bally/Williams/Midway DAC -> OPN2 DAC trick or similar
                        if (chiptype != 0x52) { 
                            if (chips[data[i]] == 0 ) {chips[data[i]] = i; }; i+=2; break; // three-byte Additional OPN2 command
                        } else { toif=true; break;} // send OPM to next conditional

                    //* skip wait commands, samples & OPN2 DAC commands
                    case 0x61: WaitFlags[i]=true; i+=2; break; // three-byte wait
                    case 0x62: WaitFlags[i]=true; break;
                    case 0x63: WaitFlags[i]=true; break;
                    // case 0x66: i=end; tb("end reached @ 0x"+i); break; // end of sound data
                    case 0x66: i=endVGMdata; if (!quiet) tb("ExamineVGMdata: 0x66 end byte reached @ 0x"+Convert.ToString(i,16) ); break; // end of sound data
                    // case 0x66: i=end; break; // end of sound data
                    case 0x67: // data block: 0x67 0x66 tt ss ss ss ss (data)
                        int tmp=Get32BitInt(data,i+3);
                        // tb("ExamineVGMdata: 0x"+Convert.ToString(i,16)+": data block size="+Convert.ToString(tmp,16)+" skipping to 0x"+Convert.ToString(i+tmp,16));
                        i+=Get32BitInt(data,i+3); i+=6;
                        break;
                    case byte n when (n >= 0x70 && n <= 0x7F): WaitFlags[i]=true; break; // more waits. oh neat c# can do this
                    case byte n when (n >= 0x80 && n <= 0x8F): WaitFlags[i]=true; break; // OPN2 sample write & wait
                    case byte n when (n >= 0x90 && n <= 0x91): i+=4; break; // DAC Stream Control Write       0x90 ss tt pp cc
                    case 0x92: i+=5; break; // DAC Stream Control Start Stream: 0x93 ss aa aa aa aa mm ll ll ll ll
                    case 0x93: i+=10; break; // DAC Stream Control Start Stream: 0x93 ss aa aa aa aa mm ll ll ll ll
                    case 0x94: i++; break; // DAC Stream Control Stop Stream: 0x94 ss
                    case 0x95: i+=4; break; // DAC Stream Control Start Stream (fast call):  0x95 ss bb bb ff
                    case 0xE0: i+=4; break; // OPN2 PCM pointer, followed by 32-bit value 
                    // case byte chiptype: break; // not possible to do this type of comparison in switch?
                    default: toif=true;break; //* all chiptype commands should go through to the next conditional
                }
                if (toif) { //* continuation of the switch above  
                    if (IsFMRegister(data[i], chiptype)) { // * for OPNA / OPNB / OPN2 which have two possible registers depend on channel
                        if (chiptype == 0x52 || chiptype == 0x55 || chiptype == 0x56 || chiptype == 0x58) { // if OPN, detect existence of ch#3 mode
                            if (data[i] == chiptype && data[i+1]==0x27 && SecondBit(data[i+2])==1) { // 56 27 xx - timer command, second bit enables Ch#3 Extended Mode
                                Channel3ModeDetected = true;
                            }
                        }
                        byteflag[i]=true; // byteflag[i+1]=true;byteflag[i+2]=true; //* mark only the first byte so we don't trip over the same data. Was having a problem with lines like 54-54-xx..
                        // tb("xvgm: "+Convert.ToString(data[i],16)); Console.ReadKey();
                        i+=2; // all FM chip commands are 3-byte values
                        c++; // count up all our commands
                    } else {
                        if (!quiet) tb("ExamineVGMData: UNKNOWN COMMAND @0x"+(Convert.ToString(i,16))+": 0x"+Convert.ToString(data[i],16));
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
            if (!quiet) {
                tb("ExamineVGMData: scanned "+ (endVGMdata-startVGMdata)+" bytes, found "+c+" FM commands. Total bytes / command-related-bytes: "+ String.Format("{0:P2}.", Decimal.Divide((c*3),(endVGMdata-startVGMdata)) ));
                if (detectedchipcodes != "") tb("ExamineVGMData: vvvvv Additional Chip Report vvvvv \n"+detectedchipcodes);
            }
            return byteflag;
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
                case 0x5E: if (b==0x5E || b==0x5F) return true; break;   // OPL3 (YMF262)

            }
            return false;
        }

        static int ParseWaits(in byte[] data, ref int idx, in bool[] WaitFlags){ // * will iterate index by 2 if 3-byte wait is found
            if (WaitFlags[idx]){ // 'wait' commands should be flagged ahead of time by this bool array
                switch (data[idx]){
                    case 0x61: idx+=2; return BitConverter.ToUInt16(data, idx-1);   // three-byte wait // should be data+1 but incremented index first so
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

        // * vv this parsewaits method is not accurate enough for loop points due to how it handles 3-byte waits. However, it's still useful for searching backwards        
        static int ParseWaitsLegacy(in byte[] data, int idx, in bool[] WaitFlags){ // just return delay of current byte, in samples
            if (WaitFlags[idx]){ // 'wait' commands should be flagged ahead of time by this bool array
                switch (data[idx]){
                    case 0x61: return BitConverter.ToUInt16(data, idx+1);   // three-byte wait  bugfix 2022-02-22. needs to be UINT not int...
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


        static int ReturnDesiredDT(Dictionary<string,int> values, int DesiredDTalg) {   // todo DT is a 4-bit value, first bit is a sign. This func works, but is ridiculous
            var LUT = new Dictionary<int, int>(); //DT lut
            LUT[0] = 0; LUT[1] = 1; LUT[2] = 2; LUT[3] = 3; 
            LUT[4] = 0; LUT[5] = -1; LUT[6] = -2; LUT[7] = -3;
             // Wrap-around DTs... maybe. used in Williams drivers for some reason
            LUT[8] = 0;  LUT[9] = 1; LUT[10] = 2; LUT[11] = 3; 
            LUT[12] = 0; LUT[13] = -1; LUT[14] = -2; LUT[15] = -3;

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
                    // outDT = ((LUT[DTs[0]]+LUT[DTs[1]]+LUT[DTs[2]]+LUT[DTs[3]]) / 4 ); // this is doing a just awful job
                    double tmp = LUT[DTs[0]]+LUT[DTs[1]]+LUT[DTs[2]]+LUT[DTs[3]];
                    tmp /= 4;
                    outDT = Convert.ToInt32(Math.Round(tmp, MidpointRounding.AwayFromZero));
                    // tb(LUT[DTs[0]]+" "+LUT[DTs[1]]+" "+LUT[DTs[2]]+" "+LUT[DTs[3]]+" -> "+tmp+ "->"+Math.Round(tmp, MidpointRounding.AwayFromZero));
                    // Console.ReadKey();
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
        // in - blank int array sized to the VGM
        // out - every array index filled with a integer timecode (in samples). 
        //     - Header and EOF will be filled with 0s (not really irrelevant)
        // static int looppointIDX = 0;
        // static int LoopSamples = 0;
        static int ReadLoops(out int samples_to_loop, out int samples_from_loop, int looppoint_header, in byte[] data, in bool[] WaitFlags, int startVGMdata, int endVGMdata) {    // out LooppointIDX
            int samples=0;

            // looking for: 
            //      looppointIDX: index of loop point based on data offsets
            //      LoopSamples: Duration of loop. From loop point index to endVGMdata (0x66, tag section, or straight up EoF)
            // int looppoint_header = BitConverter.ToInt32(data, 0x1C) - 28; // example
            samples_to_loop=0;  // from start to loop point
            samples_from_loop=0; // from loop point to end
            int lp_idx=0;
            for (int i = startVGMdata; i < endVGMdata; i++){
                if (i >= looppoint_header) {
                    samples_to_loop=samples;
                    lp_idx=i;
                    break;
                }
                samples+=ParseWaits(data, ref i, WaitFlags);
            }
            // int tmp=0;
            for (int i = looppoint_header; i < endVGMdata; i++) { // From loop point index to endVGMdata (0x66, tag section, or straight up EoF)
                samples_from_loop+=ParseWaits(data, ref i, WaitFlags);
            }
            // samples+=samples_from_loop; // debug?
            // LoopSamples = samples_from_loop;
            return lp_idx;
        }
        static int FindLoopPoint(int samples_to_loop, in byte[] data, in bool[] WaitFlags, int startVGMdata, int endVGMdata) {    // out LooppointIDX
            int samples=0;
            int lp_idx=0;
            for (int i = startVGMdata; i < endVGMdata; i++){
                if (samples >= samples_to_loop) {
                    lp_idx=i;
                    break;
                }
                samples+=ParseWaits(data, ref i, WaitFlags); // will iterate index by 2 if 3-byte wait is found
            }
            tb("FindLoopPoint:"+lp_idx);
            return lp_idx;
        }

        static int[] CreateTimeCode(ref int[] timecodes, in byte[] data, in bool[] WaitFlags, in int startVGMdata, in int endVGMdata){ // waitflags should be true if first byte is a wait command
            
            for (int i = 0; i < startVGMdata; i++) {timecodes[i]=0;}                // write 0 to header section
            for (int i = endVGMdata; i < timecodes.Length; i++) {timecodes[i]=0;}   // write 0 to EOF (tags and such live here)

            int samples=0;
            // int last=0; // * debug
            for (int i = startVGMdata; i < endVGMdata; i++){
                samples+=ParseWaits(data, ref i, WaitFlags);
                if (i < endVGMdata)
                    timecodes[i]=samples;
            }
            return timecodes;
        }

        static int SamplesToMS(int samples) {
            return Convert.ToInt32(Math.Round(samples / 44.1));
        }

        static byte[] AppendData(in byte[] data, Dictionary<int, byte[]> append) { // assumes 3 bytes in dict

            List<byte> datalist = new List<byte>(data); //? can this be improved? having to convert from array to list to array
            foreach (var cmd in append.OrderByDescending(x => x.Key)) {
                // datalist.Insert(cmd.Key, cmd.Value);
                datalist.InsertRange(cmd.Key, cmd.Value);

            }
            return datalist.ToArray<byte>();    // Maybe always have data in list form?
        }

        static int AppenderCount(Dictionary<int, byte[]> appender) {
            int c=0;
            foreach (var kv in appender) {
                c+=kv.Value.Count();
            }
            return c;
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


        #endregion


        #region Bitwise function jungle
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
        static byte LastBit(int b){
            b = (byte)(b << 7); // erase first 7 bits
            b = (byte)(b >> 7); // move to first
            return Convert.ToByte(b);  
        }
        static byte ReplaceFirstHalfByte(byte xa, byte dt){  // for setting DT. in:byte, int DT value (0-7)
            return FourToEightCoder(dt, Second4Bit(xa)); // DT|ML
        }

        #endregion


        #region String-based helper functions (for exceptions, display, debug etc)
        

        static string ReturnArrayHex(byte[] b) {
            string str=""; 
            foreach (var x in b) str+=" 0x"+Convert.ToString(x,16);
            return str;
        }
        static string ReturnArrayHex(FMchannel FM, byte[] b) {
            string str=""; 
            foreach (var x in b) {
                str+=$" 0x{Convert.ToString(x,16)} ";
                if (FM.REF_REG_LABEL.ContainsKey(x) ) str+=FM.REF_REG_LABEL[x];
            }
            return str;
        }

        public static void PrintStringArray(in string[] strA) {
            string s="";
            for (int i = 0; i < strA.Length; i++) {
                s+=i+":"+strA[i]+" ";
            }
            tb("PSA: "+s+ " (L="+strA.Length+")");
        }
        public static void PrintStringArray(in int[] strA) {
            string s="";
            for (int i = 0; i < strA.Length; i++) {
                s+=i+":"+strA[i]+" ";
            }
            tb("PSA: "+s+ " (L="+strA.Length+")");
        }

        static void PrintDictionary(Dictionary<string,int> d) {
            foreach (var kv in d) {
                tb(kv.Key+" "+kv.Value);
            }
        }
        static void PrintDictionary(Dictionary<byte,byte> d) {
            foreach (var kv in d) {
                tb(kv.Key+" "+kv.Value);
            }
        }
        static void PrintDictionary(Dictionary<string,byte> d) {
            foreach (var kv in d) {
                tb(kv.Key+" "+cts(kv.Value,16));
            }
        }

        static string ReturnList(List<int> l) {
            var str="";
            foreach (var b in l) {
                // if (b==null) {
                //     str+="NL ";
                // } else {
                    // str+=String.Format("{00}",Convert.ToString((byte)b,16))+" ";
                    str+="0x"+String.Format("{000000}",Convert.ToString(b,16))+" ";
                    // str+=$"0x{Convert.ToString(b,16)}";
                // }
            }
            return str;
        }

        static void PatchKey_Error(string arg) {
            tb("Invalid patchkey "+arg+"  continuing...");
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(3));
        }

        static string PrintPatch(Dictionary<string,int> patch, int operators) { // can take DATA or PATCHKEY
            string s="";
            if (operators==4) {
                // 4op: mults / detune   commands - alg, desiredDTalg, desiredMult
                s+=patch[mult1]+"-"+patch[mult2]+"-"+patch[mult3]+"-"+patch[mult4]+" / "+patch[dt1]+"-"+patch[dt2]+"-"+patch[dt3]+"-"+patch[dt4];
                if (patch.ContainsKey(alg)) s+=" alg"+patch[alg]+" ";
                if (patch.ContainsKey(desiredDTalg)) s+=" DT"+patch[desiredDTalg]+" ";
                if (patch.ContainsKey(desiredMult)) s+=" MULT"+patch[desiredMult]+" ";
                if (patch.ContainsKey(desiredForceOp)) s+=" FORCEOP"+patch[desiredForceOp]+" ";
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
            if (operators==4) {
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


        static string ReturnWaveTypeString(int w) { // OPL2 OPL3
            switch(w) {
                case 0: return "sine";
                case 1: return "halfsine";
                case 2: return "ABSsine";
                case 3: return "quartersine";
                case 4: return "altsine"; // OPL3
                case 5: return "ABSaltsine"; // OPL3
                case 6: return "square"; // OPL3
                case 7: return "saw"; // aka 'derived square'. OPL3
                case 99: return "ReturnWaveTypeString: Err (null input)";
                default: return w+"?";
            }
        }

        static string DetuneDescription(int a) {
            switch(a) {
                case 8: return "lowest mult, early";
                case 9: return "lowest mult, late";
                case 10: return "avg";
                case 11: return "most dt, early";
                case 12: return "most dt, late";
                case 13: return "least dt, early";
                case 14: return "least dt, late";
                case int n when (n < 8): return "force "+a; // 0-7 forceop
                case 21: return "Op#1's dt";
                case 22: return "Op#2's dt";
                case 23: return "Op#3's dt";
                case 24: return "Op#4's dt";
            }
            return "invalid";
        }

        static string SamplesToMinutes(int samples) { // input 44.1khz samples (VGM format) MM:SS:MS
            double ms = Convert.ToDouble(samples / 44.1);
            return TimeSpan.FromSeconds(ms/1000).ToString(@"m\mss\.ff\s");
        }




        #endregion


        static void debugstart() {
            // bool debug = true;
            bool debug = false;
            if (!debug) return;


            // var testlist = new List<int>();
            var testDict = new Dictionary<byte,int>() {
                {1, 300},
                {2, 400},
                {3, 500},
                {4, 44}
            };

            // return the lowest VALUES' associated KEY

             var min = testDict.Aggregate((l, r) => l.Value < r.Value ? l : r).Key;
             var max = testDict.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;

            tb($"min = {min} ={testDict[min]} max= {max} ={testDict[max]}");

            testDict = new Dictionary<byte,int>();


            tb("debugstart: End of Code");Console.ReadKey();

        }
        


    // todo investigating frequency modified detune
	// compute the keycode: block_freq is:
	//
	//     BBBFFFFFFFFFFF
	//     ^^^^???
	//
	// the 5-bit keycode uses the top 4 bits plus a magic formula
	// for the final bit

    static int ProcessBlockFreq(byte High, byte Low) {
        // int value=High;
        // string tmp = "0x"+Convert.ToString(High,16)+Convert.ToString(Low,16);
        // tb(tmp);
        // return Convert.ToInt32(tmp);
        // int h=High;
        // tb(""+h);
        // h*=256;
        // return h+Low;

        return (int)(High*256)+Low;

    }

	// int keycode = bitfield(block_freq, 10, 4) << 1;

	// lowest bit is determined by a mix of next lower FNUM bits
	// according to this equation from the YM2608 manual:
	//
	//   (F11 & (F10 | F9 | F8)) | (!F11 & F10 & F9 & F8)
	//
	// for speed, we just look it up in a 16-bit constant
	// keycode |= bitfield(0xfe80, bitfield(block_freq, 7, 4));
// 
	// detune adjustment
	// cache.detune = detune_adjustment(op_detune(opoffs), keycode);
// 
    // todo vv via YMFM source vv
    //-------------------------------------------------
    //  detune_adjustment - given a 5-bit key code
    //  value and a 3-bit detune parameter, return a
    //  6-bit signed phase displacement; this table
    //  has been verified against Nuked's equations,
    //  but the equations are rather complicated, so
    //  we'll keep the simplicity of the table
    //-------------------------------------------------

    static int detune_adjustment(int detune, int keycode)
    {
        int[,] s_detune_adjustment = new int[32, 4]
        {
            { 0,  0,  1,  2 },  { 0,  0,  1,  2 },  { 0,  0,  1,  2 },  { 0,  0,  1,  2 },
            { 0,  1,  2,  2 },  { 0,  1,  2,  3 },  { 0,  1,  2,  3 },  { 0,  1,  2,  3 },
            { 0,  1,  2,  4 },  { 0,  1,  3,  4 },  { 0,  1,  3,  4 },  { 0,  1,  3,  5 },
            { 0,  2,  4,  5 },  { 0,  2,  4,  6 },  { 0,  2,  4,  6 },  { 0,  2,  5,  7 },
            { 0,  2,  5,  8 },  { 0,  3,  6,  8 },  { 0,  3,  6,  9 },  { 0,  3,  7, 10 },
            { 0,  4,  8, 11 },  { 0,  4,  8, 12 },  { 0,  4,  9, 13 },  { 0,  5, 10, 14 },
            { 0,  5, 11, 16 },  { 0,  6, 12, 17 },  { 0,  6, 13, 19 },  { 0,  7, 14, 20 },
            { 0,  8, 16, 22 },  { 0,  8, 16, 22 },  { 0,  8, 16, 22 },  { 0,  8, 16, 22 }
        };
        int result = s_detune_adjustment[keycode, detune & 3];
        // return bitfield(detune, 2) ? -result : result;
        return result;
    }







    }



    

}




