
using System.Collections.Generic;
using System.Linq; // Lookup
using System.IO;
using System;

using data2=EXTT.Program;

//! OPM OPNx only!!!

namespace EXTT.SoloVGM

{


    public partial class Program {

        public delegate void WriteDelegate(string msg, params object[] args); // shortcut commands
        public static readonly WriteDelegate tb = Console.WriteLine; 



        public void SoloVGM(byte[] data, string[] args, byte chiptype, int startVGMdata, int endVGMdata, string filename, 
                            List<Dictionary<string,byte>> FMSystemList, List<data2.FMchannel2> FMChannelList) 
        {


            foreach (data2.FMchannel2 Ch in FMChannelList) { // initialize our channels
                Ch.Initialize(); // populates merged ops, reverse dictionaries
            }

            // tb("SoloVGM: Running");
            string str=""; // debug but also help display

            var MuteChannels = new List<string>(); // Populate list with everything possible, then remove arguments from it. 
            var ValidCommands = new List<string>();
            ValidCommands.Add("FM");
            for (int i = 0; i < FMChannelList.Count; i++) {
                MuteChannels.Add("FM"+i);
            }
            if (chiptype==0x55 || chiptype==0x56 || chiptype==0x58) {
                ValidCommands.Add("SSG");
                MuteChannels.Add("SSG0");MuteChannels.Add("SSG1");MuteChannels.Add("SSG2");
            }
            if (chiptype==0x52 || chiptype==0x55 || chiptype==0x56 || chiptype==0x58) {
                MuteChannels.Add("OP1");MuteChannels.Add("OP2");MuteChannels.Add("OP3");MuteChannels.Add("OP4");
            }
            if (chiptype==0x56 || chiptype==0x58) { // OPNA OPNB
                MuteChannels.Add("RSS"); MuteChannels.Add("ADPCMA");
            }
            if (chiptype==0x56 || chiptype==0x58 || chiptype == 0x5B) { // OPNA OPNB Y8950
                MuteChannels.Add("ADPCMB");
            }
            if (chiptype==0x52) {
                MuteChannels.Add("DAC"); ValidCommands.Add("PCM");
            }
                
            ValidCommands.AddRange(MuteChannels);
            if (args.Length < 3) {
                tb("SoloVGM: Error! Please insert some arguments!");
                foreach (string x in ValidCommands) {str+=x+" ";}
                tb("SoloVGM: USAGE: EXE solovgm [channel(s) to solo, separated by spaces] infile.vgz");
                tb("Valid arguments for detected FM chip: "+str); str="";
                Environment.Exit(1);
            }

            string suff="";
            // bool DisplayValidCommands=false;
            bool DisplayValidCommands=true; // DEBUG
            for (int i = 1; i < args.Length-1; i++) {args[i]=args[i].ToUpper() ;}
            for (int i = 1; i < args.Length-1; i++) {
                if (args[i] == "ADPCM") {
                    tb("SoloVGM: Ambiguous ADPCM, assuming ADPCMB"); 
                    args[i]="ADPCMB"; // let's assume it's the single ADPCM-B
                }
                if (args[i] == "PCM") { // OPN2 DAC synonym  --- note use .replace next time
                    args[i]="DAC";
                }
                if (args[i] == "RSS") { // ADPCMA synonym
                    if (MuteChannels.Contains("ADPCMA")) MuteChannels.Remove("ADPCMA");
                }
                if (args[i] == "SSG") {
                    if (MuteChannels.Contains("SSG0")) MuteChannels.Remove("SSG0");
                    if (MuteChannels.Contains("SSG1")) MuteChannels.Remove("SSG1");
                    if (MuteChannels.Contains("SSG2")) MuteChannels.Remove("SSG2");
                }
                if (args[i] == "FM") {
                    for (int ii = 0; ii < FMChannelList.Count; i++) {
                        if (MuteChannels.Contains("FM"+ii)) MuteChannels.Remove("FM"+ii);
                    }
                }
                
                if (MuteChannels.Contains(args[i])) {
                    MuteChannels.Remove(args[i]);
                    suff+=args[i];
                } else {
                    tb("SoloVGM: Invalid or Duplicate Command "+args[i]); DisplayValidCommands=true;
                }
            }



            if (DisplayValidCommands) {
                foreach (string s in ValidCommands) {str+=s+" ";}
                tb("SoloVGM: Valid commands for this chip="+str); str="";
            }
            tb("SoloVGM: Soloing these channels="+suff);
            if (suff.Length < 1) {
                tb("SoloVGM: Error! No valid commands specified!");
                Environment.Exit(1);
            }

            // need to make a list of 3-bytes. First byte chip (accomodate 2 banks)
            // second byte register
            // third byte value - mute value.. mute values differ based on type of voice
            var MuteCmds = new List<Tuple<byte,byte,byte>>();

            byte FMmuteValue = 0x7f;                    // * FM mute values
            if (FMChannelList[0].operators==2) {
                FMmuteValue=0x3f; // --xxxxxx
            }

            // * handle Ch3 Extended Mode
            if (chiptype == 0x52 || chiptype == 0x55 || chiptype == 0x56 || chiptype == 0x58) { // opn only
                if (!MuteChannels.Contains("OP1") || !MuteChannels.Contains("OP2") || !MuteChannels.Contains("OP3") || !MuteChannels.Contains("OP4")) {
                    if (MuteChannels.Contains("OP1")) MuteCmds.Add(Tuple.Create(FMChannelList[2].chip, FMChannelList[2].REF_LABEL_REG["TL1"], FMmuteValue));
                    if (MuteChannels.Contains("OP2")) MuteCmds.Add(Tuple.Create(FMChannelList[2].chip, FMChannelList[2].REF_LABEL_REG["TL2"], FMmuteValue));
                    if (MuteChannels.Contains("OP3")) MuteCmds.Add(Tuple.Create(FMChannelList[2].chip, FMChannelList[2].REF_LABEL_REG["TL3"], FMmuteValue));
                    if (MuteChannels.Contains("OP4")) MuteCmds.Add(Tuple.Create(FMChannelList[2].chip, FMChannelList[2].REF_LABEL_REG["TL4"], FMmuteValue));

                    if (MuteChannels.Contains("FM2")) { // So we don't mute all operators when we're trying to isolate Ch#3 extended mode voices
                        MuteChannels.Remove("FM2");
                    } 
                } else if (MuteChannels.Contains("FM2")) { // * so we aren't redundantly checking to mute both FM2 and FM2's operators
                    MuteChannels.Remove("OP1"); MuteChannels.Remove("OP2"); MuteChannels.Remove("OP3"); MuteChannels.Remove("OP4");
                } 
            }

            // * Handle general FM channels
            foreach (data2.FMchannel2 Ch in FMChannelList) {
                Ch.Initialize(); // populate merged ops, reverse dictionaries
                if (MuteChannels.Contains(Ch.name)) {
                    MuteCmds.Add(Tuple.Create(Ch.chip, Ch.REF_LABEL_REG["TL1"], FMmuteValue));
                    MuteCmds.Add(Tuple.Create(Ch.chip, Ch.REF_LABEL_REG["TL2"], FMmuteValue));
                    if (FMChannelList[0].operators==4) {
                        MuteCmds.Add(Tuple.Create(Ch.chip, Ch.REF_LABEL_REG["TL3"], FMmuteValue));
                        MuteCmds.Add(Tuple.Create(Ch.chip, Ch.REF_LABEL_REG["TL4"], FMmuteValue));
                    }
                }

            }

            // * Handle SSG (OPN series)
            if (MuteChannels.Contains("SSG0")) MuteCmds.Add(Tuple.Create(FMChannelList[0].chip, (byte)0x08, (byte)0x00));
            if (MuteChannels.Contains("SSG1")) MuteCmds.Add(Tuple.Create(FMChannelList[0].chip, (byte)0x09, (byte)0x00));
            if (MuteChannels.Contains("SSG2")) MuteCmds.Add(Tuple.Create(FMChannelList[0].chip, (byte)0x0A, (byte)0x00));


            // * Handle RSS & ADPCMA
            // todo not 100% sure about OPNB ADPCM-A
            if (MuteChannels.Contains("RSS")) MuteCmds.Add(Tuple.Create(FMChannelList[0].chip, (byte)0x11, (byte)0x00));
            if (MuteChannels.Contains("ADPCMA")) MuteCmds.Add(Tuple.Create(FMChannelList[0].chip, (byte)0x11, (byte)0x00));

            // * Handle ADPCMB (OPNA, OPNB, Y8950)
            if (MuteChannels.Contains("ADPCMB")) {
                if (chiptype==0x5C) {    // 5C 12 FF    Y8950:		DELTA-T: Volume: 0xFF = 100%
                    MuteCmds.Add(Tuple.Create(FMChannelList[0].chip, (byte)0x12, (byte)0x00));
                } else {
                    MuteCmds.Add(Tuple.Create(FMChannelList[4].chip, (byte)0x0B, (byte)0x00)); // todo Level control 57 0b 00 ...? not 100% sure
                }
            }

            // * Handle DAC (OPN2)
            // TODO not sure about this one
            // if (MuteChannels.Contains("DAC")) 




            foreach (string s in MuteChannels) { // debug delete me
                str+=s+" ";
            }
            tb("SoloVGM: Muting these channels="+str); str=""; 
            // tb("tuple cnt="+MuteCmds.Count);
            // tb("data cnt="+data.Length);
            // foreach (var tpl in MuteCmds) {
            //     tb(Convert.ToString(tpl.Item1,16)+" "+Convert.ToString(tpl.Item2,16)+" "+Convert.ToString(tpl.Item3,16) );
            // }
            // Console.ReadKey();

            bool[] WaitFlags = new bool[endVGMdata];
            bool[] ByteFlags = EXTT.Program.ExamineVGMData(data, FMChannelList[0].chip, startVGMdata, endVGMdata, ref WaitFlags, true);


            int c=0;
            for (int i = startVGMdata; i < endVGMdata; i++) {
                if (ByteFlags[i]) {
                    foreach (var tpl in MuteCmds) {
                        // tb("0x_"+Convert.ToString(i,16)+": "+Convert.ToString(data[i],16)+" = "+Convert.ToString(tpl.Item1,16));
                        if (data[i] == tpl.Item1) {
                            if (data[i+1] == tpl.Item2) {
                                data[i+2] = tpl.Item3; i+=2; 
                                c++; 
                                break;
                            }
                        } 
                    }
                }
            }

            tb("SoloVGM: Muted {0} Commands",c);

            string outfile=filename+"_Solovgm"+suff+".vgm";
            tb("SoloVGM: Writing "+outfile);

            if (File.Exists(outfile)) {
                File.Delete(outfile);
            }
            using (FileStream fs = File.Create(outfile)) {
                fs.Write(data, 0, data.Length);
            }              

            tb("SoloVGM: Complete!\n");
            Environment.Exit(0);













        }

    }


}