
using System.Collections.Generic;
using System.Linq; // Lookup
using System.IO;
using System;

using data2=EXTT.Program;
using program=EXTT;

//! OPM OPNx only!!!

namespace EXTT.io_bank

{

    public partial class Program {

        public delegate void WriteDelegate(string msg, params object[] args); // shortcut commands
        public static readonly WriteDelegate tb = Console.WriteLine; 


        public class patch {
            byte chip;
            public int idx, timecode;
            public Dictionary<string,byte> p;

            public patch(byte chip, Dictionary<string,int> data) {
                this.chip = chip;
                // this.idx = idx; this.timecode = timecode;
                this.idx = data["IDX"]; this.timecode = data["TIMECODE"];
                this.p = new Dictionary<string,byte>();
                foreach (var kv in data) {
                    if (kv.Key != "IDX" && kv.Key != "TIMECODE") {
                        this.p[kv.Key]=Convert.ToByte(kv.Value);
                    }
                }
                // add stuff to patch data if it's not there (OPN, or just absent)
                string[] cmds = new string[]{data2.OPM_LFO_FREQUENCY, data2.OPM_LFO_AM_PM_DEPTH, 
                data2.OPM_LFO_WAVEFORM, data2.OPNA_LFO_ENABLE, data2.LFO_CHANNEL_SENSITIVITY, data2.FEEDBACK_ALG};
                foreach (string s in cmds) {
                    if (!p.ContainsKey(s)) {
                        p[s]=0;
                    }
                }
                // same as above, but per-Operator Commands
                string[] OPcmds = new string[]{data2.DR_LFO_AM_ENABLE, data2.SSGEG_ENABLE_ENVELOPE, 
                data2.LFO_CHANNEL_SENSITIVITY, data2.AR_KSR, data2.DR_LFO_AM_ENABLE, data2.SR_DT2, data2.SL_RR, data2.DTML};
                foreach (string s in OPcmds) {
                    if (!p.ContainsKey(s+1)) {
                        // p.Add(s,0x00);
                        p[s+1]=0; p[s+2]=0; p[s+3]=0; p[s+4]=0;
                    }
                }


                // this.p = data;
            }

            string SamplesToMinutes(int samples) { // input 44.1khz samples (VGM format)
                double ms = Convert.ToDouble(samples / 44.1);
                return TimeSpan.FromSeconds(ms/1000).ToString(@"m\mss\.ff\s");
            }

            public string name { get{
                string str = "FM"+p["name"]+"@"+SamplesToMinutes(timecode);

                bool dt2present=false; string dt2str="";
                for (int i = 0; i < 4; i++) {
                    int tmp=DT2(i+1);
                    if (tmp > 0) dt2present=true;
                    dt2str+=tmp;
                }
                if (dt2present) str+=" DT2="+dt2str;

                return str;
            }}

            public byte alg { get{
                    return (byte)(p[data2.FEEDBACK_ALG] & 0x0F); // ----XXXX
            } }
            public byte feedback { get{
                    return (byte)(p[data2.FEEDBACK_ALG] >> 4); // XXXX----
            } }

            public byte DT1(int op) {
                // return (byte)(p[data2.DTML+op] & 0b01110000); 
                return (byte)(p[data2.DTML+op] >> 4); // -XXXyyyy
            }
            public byte MULT(int op) {
                return (byte)(p[data2.DTML+op] & 0x0F); // -xxxYYY
            }
            public byte TL(int op) {
                return (byte)(p[data2.TL+op] & 0b01111111); // last 7 bits
            }
            public byte KSR(int op) {
                return (byte)(p[data2.AR_KSR+op] >> 6); // first two bits
            }
            public byte AR(int op) {
                return (byte)(p[data2.AR_KSR+op] & 0b00011111); // last five bits
            }
            public byte LFO_AM_Enable(int op) {
                return (byte)(p[data2.DR_LFO_AM_ENABLE+op] >> 6); // first bit
            }
            public byte DR(int op) {
                return (byte)(p[data2.DR_LFO_AM_ENABLE+op] & 0b00011111); // last five bits
            }
            public byte DT2(int op) {   // * OPM only, not supported by bank
                return (byte)(p[data2.SR_DT2+op]  >> 6); // first two bits
            }
            public byte SR(int op) {
                return (byte)(p[data2.SR_DT2+op] & 0b00011111); // last five bits
            }
            public byte SL(int op) {   
                return (byte)(p[data2.SL_RR+op]  >> 4); // first four bits
            }
            public byte RR(int op) {   
                return (byte)(p[data2.SL_RR+op] & 0x0F); // last four bits
            }
            public byte SSGEG(int op) {
                return (byte)(p[data2.SSGEG_ENABLE_ENVELOPE+op] & 0x0F); //? five is enable flag, integrated?
            }
            public byte LFO_RATE { get {
                if (chip == 0x54) {
                    return p[data2.OPM_LFO_FREQUENCY]; // * full 8-bit freq on OPM: remember to handle this
                } else if (chip == 0x55) {
                    return 0; // OPN has no LFO
                } else { // OPNA etc
                    // return (byte)(p[data2.OPNA_LFO_ENABLE] & 0b00000111); // just LFO rate
                    return p[data2.OPNA_LFO_ENABLE]; // I think this is correct, enable flag is supposed to be there
                }
            } }

            public byte LFO_PM_SENS { get { 
                if (chip==0x54) {
                    return (byte)(p[data2.LFO_CHANNEL_SENSITIVITY] >> 4);  //-XXX----
                } else {
                    return (byte)(p[data2.LFO_CHANNEL_SENSITIVITY] & 0b00000111); // -----XXX
                }
            }}
            public byte LFO_AM_SENS { get { 
                if (chip==0x54) {
                    return (byte)(p[data2.LFO_CHANNEL_SENSITIVITY] & 0b00000011);  //------XX
                } else {
                    byte b = p[data2.LFO_CHANNEL_SENSITIVITY]; // --XX----
                    b = (byte)(b << 2); // erase first two bits (pan LR)
                    return (byte )(b >> 6); // move to first position
                }
            }}


        }
        public class BankOut {
            byte chip;
            // List<Dictionary<string,int>> list_patches;
            List<patch> list_patches = new List<patch>();
            public BankOut(byte chip, List<Dictionary<string,int>> patches, string filename) { // ! 4 op only!
                this.chip=chip;
                // this.list_patches = patches;
                foreach (var patch in patches) {
                    list_patches.Add(new patch(chip, patch)); // patch obj will convert patch to <string,byte>
                }
                WriteBank(list_patches, filename);
            }

            // public byte outname

            // public byte alg() {return label_val[data2.TESTREGISTER]; }
            // public byte feedback() {return label_val[data2.TESTREGISTER]; }
        }


        public static void WriteBank(List<patch> FMpatches, string filename)
        {

            //** output YM2608editor .bank format
            //** The following are not supported by .bank: KLS DT2 ... some LFO things?

            // string outfilename=args[0]+".bank";
            string outfilename=filename+".bank";
            if (File.Exists(outfilename)) {
                File.Delete(outfilename);
            }
            using (FileStream NewFile = new FileStream(outfilename, FileMode.CreateNew))
            {
                // Bind the BinaryReader instance to the fs stream
                using (BinaryWriter bw = new BinaryWriter(NewFile))
                {
                    bw.Write(System.Text.Encoding.ASCII.GetBytes("BKV3"));   // ident
                    bw.Write(Convert.ToUInt16(FMpatches.Count() )); // number of patches, 2 bytes
                    // Each Patch
                    foreach (patch patch in FMpatches) {
                        string OutputName = patch.name;
                        // tb(OutputName+" ... ");
                        bw.Write(Convert.ToByte(OutputName.Length)); // starts w/ 8 bit name length
                        bw.Write(System.Text.Encoding.ASCII.GetBytes(OutputName)); // then name
                        bw.Write(patch.alg); // data, in this order
                        bw.Write(patch.feedback);
                        // per operator stuff
                        // foreach (FMoperator op in patch.OpsList) {
                        for (int i = 1; i < 5; i++) {
                            bw.Write(patch.AR(i));
                            bw.Write(patch.DR(i));
                            bw.Write(patch.SR(i));
                            bw.Write(patch.RR(i));
                            bw.Write(patch.SL(i));
                            bw.Write(patch.TL(i));
                            bw.Write(patch.KSR(i));
                            bw.Write(patch.MULT(i));
                            bw.Write(patch.DT1(i));
                            bw.Write(patch.LFO_AM_Enable(i));
                            bw.Write(patch.SSGEG(i)); // Not in VOPM
                        }
                        // ... FREQ_LFO, PMS_LFO, AMS_LFO

                            // OPM LFO frequency is 8 bit. The manual says this is a range of 53Mhz to 0.008 Hz
                            // via YMFM
                                // treat the rate as a 4.4 floating-point step value with implied
                                // leading 1; this matches exactly the frequencies in the application
                                // manual, though it might not be implemented exactly this way on chip
                            // OPNA LFO frequencies are 0-7
                            // via the manual the settings are
                            // 3.98, 5.56, 6.02, 6.37, 6.88, 9.63, 48.1, 72.2 (all hz)
                            
                            // todo on OPNA this byte also includes the LFO enable flag, which is bit 5

                            // convert LFO freq the cheap quick way             44100
                            // double frq = pSlope(patch.LFO_RATE, 0, 0.008, 255, 530000);
                            double frq = pSlope(patch.LFO_RATE, 0, 0.0008, 255, 55); // not sure this might be more correct
                            // tb("test "+frq);
                            frq = pSlope(frq, 3.98, 0, 72.2, 7);
                            if (frq < 0) frq = 0;
                            if (frq > 7) frq = 7;
                            // byte finalfreq = Convert.ToByte(Math.Round(frq, MidpointRounding.AwayFromZero));
                            // tb("in freq "+patch.LFO_RATE +" psloped:"+Math.Round(frq,3)+" OPNA output:"+finalfreq);
                            // bw.Write(finalfreq);
                            bw.Write(Convert.ToByte(Math.Round(frq, MidpointRounding.AwayFromZero)) );

                            bw.Write(Convert.ToByte(patch.LFO_PM_SENS) );
                            bw.Write(Convert.ToByte(patch.LFO_AM_SENS) );
                        // 
                        // patch.ReportDT2(); // if DT2 <> 0, display a warning    old
                            // patch.Report();

                    }
                }
            }
            
            tb("WriteBank: processed "+FMpatches.Count+" patches (YM2608ToneEditor .bank format)");
            tb("WriteBank: output: "+outfilename);
            // Console.ReadKey();



            // first, remove comments
            // then, split into patches based on /r@:
            // parse through each patch, assign to object

            //LFO: LFRQ AMD PMD WF NFRQ
            //@:[Num] [Name]
            //CH: PAN FL CON AMS PMS SLOT NE
            //[OPname]: AR D1R D2R  RR D1L  TL  KS MUL DT1 DT2 AMS-EN





            // tb("end of code");

        }





        static double pSlope(double x, double x1, double y1, double x2, double y2) {
            double m; double b;
            m = (y2 - y1) / (x2 - x1);
            b = y2 - m * x2;
            return m * x + b;
        }

        static int ParseStrNF(string s) { // used by VOPM interpreter
            string input=s;
            int output;
            // string output="";
            s.Trim();
            if (Int32.TryParse(s, out output)) {
                // tb("ParseStrNF: IN:"+s+" OUT:"+output);
            } else {
                output = 0;
                tb("ParseStrNF: IN:\""+s+"\" PARSE FAILED !!!");
            }
            return output;
        }
 



        // public class FMoperator { // value slave for FMpatch class
        //     public int dt1, dt2, mult, TL, KeyScale, AR, LFO_AM_Enable, DR, SR, SL, RR, SSGEG_Enable, SSGEG_Envelope;
        //     public int KLS; //* Key Level Value (7-bit value). Standard feature in all yamaha keyboards, but a rare feature in gamer drivers...
        //     public int FixedFreq, Waveform; // TX81ZE / DX11
        //     public FMoperator() {
        //         dt1=0; dt2=0; mult=0; TL=0; KeyScale=0; AR=0; LFO_AM_Enable=0; DR=0; SR=0; SL=0; RR=0; SSGEG_Enable = 0; SSGEG_Envelope = 0;
        //         KLS=0; FixedFreq=0; Waveform=0; //TODO warn if FixedFreq or waveform >0
        //     }
        // }

        // class FMpatch {
        //     public string name;
        //     public int idx, feedback, alg, LFO_RATE, LFO_AM_SENS, LFO_PM_SENS;
        //     public int LFO_AM_DEPTH, LFO_PM_DEPTH;
        //     public FMoperator Op1, Op2, Op3, Op4;
        //     public List<FMoperator> OpsList;

        //     public FMpatch() {
        //         this.name="N/A";
        //         this.idx = 0; // for VOPM input: Usually index is in order listed but it's also specified so whatever
        //         this.feedback = 0;
        //         this.alg = 0;
        //         // this.LFO_AM_DEPTH = 0; // 0+7bits, system wide, OPM only
        //         // this.LFO_PM_DEPTH = 0; // 1+7bits, system wide, OPM only
        //         this.LFO_RATE = 0; // 8 bit OPM, 3 bit OPNA
        //         this.LFO_AM_SENS = 0; // 2-bit value
        //         this.LFO_PM_SENS = 0; // 3-bit value
        //         Op1 = new FMoperator();
        //         Op2 = new FMoperator();
        //         Op3 = new FMoperator();
        //         Op4 = new FMoperator();
        //         OpsList = new List<FMoperator>(){Op1, Op2, Op3, Op4};
        //     }
            

        //     public string OutName {
        //         get {
        //             string s="";    // append LS(DX), DT2(OPM), FixedFreq(DX11), Waveform(DX11)
        //             bool flag=false;
        //             List<FMoperator> AllOps = new List<FMoperator>{Op1, Op2, Op3, Op4};
        //             foreach (FMoperator op in AllOps) {
        //                 if (op.KLS != 0) flag=true;
        //             }
        //             if (flag) s+=" LS="+Op1.KLS+","+Op2.KLS+","+Op3.KLS+","+Op4.KLS; flag=false;
        //             foreach (FMoperator op in AllOps) {
        //                 if (op.dt2 != 0) flag=true;
        //             }
        //             if (flag) s+=" DT2="+Op1.dt2+","+Op2.dt2+","+Op3.dt2+","+Op4.dt2; flag=false;
        //             foreach (FMoperator op in AllOps) {
        //                 if (op.FixedFreq != 0) flag=true;
        //             }
        //             if (flag) s+=" FixFreq="+Op1.FixedFreq+","+Op2.FixedFreq+","+Op3.FixedFreq+","+Op4.FixedFreq; flag=false;

        //             foreach (FMoperator op in AllOps) {
        //                 if (op.Waveform != 0) flag=true;
        //             }
        //             if (flag) s+=" wavefrm="+Op1.Waveform+","+Op2.Waveform+","+Op3.Waveform+","+Op4.Waveform; flag=false;

        //             return name+s;
        //         }
        //     }

        //     public void Report() {
        //         tb("FM Patch Report (VOPM style)");
        //         tb("@:"+this.idx+" "+this.name+"   ...   "+OutName);
        //         tb("LFO: "+ LFO_RATE + " (rate)");
        //         tb("CH: xx "+this.feedback+" "+this.alg+" "+LFO_AM_SENS+" "+LFO_PM_SENS+" xx xx");
        //         tb("M1: "+Op1.AR+" "+Op1.DR+" "+Op1.SR+" "+Op1.RR+" "+Op1.SL+" "+Op1.TL+" "+Op1.KeyScale+" "+Op1.mult+" "+Op1.dt1+" "+Op1.dt2+" "+Op1.LFO_AM_Enable);
        //         tb("C1: "+Op2.AR+" "+Op2.DR+" "+Op2.SR+" "+Op2.RR+" "+Op2.SL+" "+Op2.TL+" "+Op2.KeyScale+" "+Op2.mult+" "+Op2.dt1+" "+Op2.dt2+" "+Op2.LFO_AM_Enable);
        //         tb("M2: "+Op3.AR+" "+Op3.DR+" "+Op3.SR+" "+Op3.RR+" "+Op3.SL+" "+Op3.TL+" "+Op3.KeyScale+" "+Op3.mult+" "+Op3.dt1+" "+Op3.dt2+" "+Op3.LFO_AM_Enable);
        //         tb("C2: "+Op4.AR+" "+Op4.DR+" "+Op4.SR+" "+Op4.RR+" "+Op4.SL+" "+Op4.TL+" "+Op4.KeyScale+" "+Op4.mult+" "+Op4.dt1+" "+Op4.dt2+" "+Op4.LFO_AM_Enable);
        //     }
        //     // public void ReportDT2() { // report presence of DT2
        //     //     if (Op1.dt2 == 0 && Op2.dt2 == 0 & Op3.dt2 == 0 && Op4.dt2 == 0) {
        //     //         return;
        //     //     } else {
        //     //         tb("ReportDT2: Patch #"+this.idx+" \""+this.name+"\" WARNING! DT2 (Coarse Detune) found, but not supported in OPN!");
        //     //     }
        //     // }
        // }


        // public static void WriteBank(List<Dictionary<string,int>> inPatch, string filename)
        // // static void WriteYM2608ToneEditorBank(string filename, List<Dictionary<String,int>> patches, List<EXTT::Program.FMchannel> FMchannels)
        // {

        //     List<FMpatch> FMpatches = new List<FMpatch>();

        //     //** output YM2608editor .bank format
        //     //** The following are not supported by .bank: KLS DT2 ... some LFO things?

        //     // string outfilename=args[0]+".bank";
        //     string outfilename=filename+".bank";
        //     if (File.Exists(outfilename)) {
        //         File.Delete(outfilename);
        //     }
        //     using (FileStream NewFile = new FileStream(outfilename, FileMode.CreateNew))
        //     {
        //         // Bind the BinaryReader instance to the fs stream
        //         using (BinaryWriter bw = new BinaryWriter(NewFile))
        //         {
        //             bw.Write(System.Text.Encoding.ASCII.GetBytes("BKV3"));   // ident
        //             bw.Write(Convert.ToUInt16(FMpatches.Count() )); // number of patches, 2 bytes
        //             // Each Patch
        //             foreach (FMpatch patch in FMpatches) {
        //                 string OutputName = patch.OutName;
        //                 // tb(OutputName+" ... ");
        //                 bw.Write(Convert.ToByte(OutputName.Length)); // starts w/ 8 bit name length
        //                 bw.Write(System.Text.Encoding.ASCII.GetBytes(OutputName)); // then name
        //                 bw.Write(Convert.ToByte(patch.alg) ); // data, in this order
        //                 bw.Write(Convert.ToByte(patch.feedback) );
        //                 // per operator stuff
        //                 foreach (FMoperator op in patch.OpsList) {
        //                     bw.Write(Convert.ToByte(op.AR) );
        //                     bw.Write(Convert.ToByte(op.DR) );
        //                     bw.Write(Convert.ToByte(op.SR) );
        //                     bw.Write(Convert.ToByte(op.RR) );
        //                     bw.Write(Convert.ToByte(op.SL) );
        //                     bw.Write(Convert.ToByte(op.TL) );
        //                     bw.Write(Convert.ToByte(op.KeyScale) );
        //                     bw.Write(Convert.ToByte(op.mult) );
        //                     bw.Write(Convert.ToByte(op.dt1) );
        //                     bw.Write(Convert.ToByte(op.LFO_AM_Enable) );
        //                     bw.Write(Convert.ToByte(op.SSGEG_Enable) ); // Not in VOPM
        //                 }
        //                 // ... FREQ_LFO, PMS_LFO, AMS_LFO

        //                     // OPM LFO frequency is 8 bit. The manual says this is a range of 53Mhz to 0.008 Hz
        //                     // via YMFM
        //                         // treat the rate as a 4.4 floating-point step value with implied
        //                         // leading 1; this matches exactly the frequencies in the application
        //                         // manual, though it might not be implemented exactly this way on chip
        //                     // OPNA LFO frequencies are 0-7
        //                     // via the manual the settings are
        //                     // 3.98, 5.56, 6.02, 6.37, 6.88, 9.63, 48.1, 72.2 (all hz)
                            
        //                     // todo on OPNA this byte also includes the LFO enable flag, which is bit 5

        //                     // convert LFO freq the cheap quick way             44100
        //                     // double frq = pSlope(patch.LFO_RATE, 0, 0.008, 255, 530000);
        //                     double frq = pSlope(patch.LFO_RATE, 0, 0.0008, 255, 55); // not sure this might be more correct
        //                     // tb("test "+frq);
        //                     frq = pSlope(frq, 3.98, 0, 72.2, 7);
        //                     if (frq < 0) frq = 0;
        //                     if (frq > 7) frq = 7;
        //                     // byte finalfreq = Convert.ToByte(Math.Round(frq, MidpointRounding.AwayFromZero));
        //                     // tb("in freq "+patch.LFO_RATE +" psloped:"+Math.Round(frq,3)+" OPNA output:"+finalfreq);
        //                     // bw.Write(finalfreq);
        //                     bw.Write(Convert.ToByte(Math.Round(frq, MidpointRounding.AwayFromZero)) );

        //                     bw.Write(Convert.ToByte(patch.LFO_PM_SENS) );
        //                     bw.Write(Convert.ToByte(patch.LFO_AM_SENS) );
        //                 // 
        //                 // patch.ReportDT2(); // if DT2 <> 0, display a warning    old
        //                     patch.Report();

        //             }
        //         }
        //     }
            
        //     tb("processed "+FMpatches.Count+" patches");
        //     tb("out: "+outfilename);




        //     // first, remove comments
        //     // then, split into patches based on /r@:
        //     // parse through each patch, assign to object

        //     //LFO: LFRQ AMD PMD WF NFRQ
        //     //@:[Num] [Name]
        //     //CH: PAN FL CON AMS PMS SLOT NE
        //     //[OPname]: AR D1R D2R  RR D1L  TL  KS MUL DT1 DT2 AMS-EN





        //     tb("end of code");

        // }



    }



}



















