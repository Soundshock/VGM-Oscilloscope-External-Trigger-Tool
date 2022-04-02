
using System;
using System.Collections.Generic;
using System.Linq;


namespace EXTT

{

    public partial class Program {
        // public delegate void WriteDelegate(string msg, params object[] args); // shortcut commands
        // public static readonly WriteDelegate tb = Console.WriteLine; 
        // delegate string WriteDelegate2(byte msg, int tobase);

        public class FMchannel {
            public readonly string name="FMx";
            public byte chip = 0;    // or, bank
            public int operators = 0;
            public int channel=99;
            public Dictionary<string, byte> Op1;
            public Dictionary<string, byte> Op2;
            public Dictionary<string, byte> Op3;
            public Dictionary<string, byte> Op4;
            public FMchannel(string name, int ops, byte chip){
                this.name=name; this.operators = ops; this.chip = chip;
                channel = Int32.Parse(name.Substring(2) ); 
                // tb(name+": channel="+channel); // debug
                REF_LABEL_REG = new Dictionary<string, byte>();
                REF_REG_LABEL = new Dictionary<byte, string>();
                // REF_LABEL_REG_OPS = new Dictionary<string, byte>();
                // REF_REG_LABEL_OPS = new Dictionary<byte, string>();
                Op1 = new Dictionary<string, byte>();
                Op2 = new Dictionary<string, byte>();
                Op3 = new Dictionary<string, byte>();
                Op4 = new Dictionary<string, byte>();
            }
            public Dictionary<string, byte> REF_LABEL_REG;
            public Dictionary<byte, string> REF_REG_LABEL;
            // public Dictionary<string, byte> REF_LABEL_REG_OPS; // integrated these into REF_LABEL_REG
            // public Dictionary<byte, string> REF_REG_LABEL_OPS;



            // TODO there are some special split registers used by the patchkey system
            // TODO wave1, wave2, vibratio1, vibrato2, alg, mult1, mult2
            public void Initialize() { // populate reverse dictionaries. Must be run after data2 setup!

                // REF_LABEL_REG already initialized
                // tb("?"); Console.ReadKey();

                var tmp = new List<Dictionary<string,byte>>(){Op1, Op2, Op3, Op4};
                for (int i = 0; i < (tmp.Count); i++) {
                    // tb("tmp cnt="+tmp.Count); Console.ReadKey();
                    // REF_LABEL_REG_OPS.Add(tmp[0]);
                    foreach (var kv in tmp[i]) {
                        // REF_LABEL_REG_OPS[kv.Key+i] = kv.Value; // ex. TL1 AR_KSR3 or what have you
                        string t = kv.Key+(i+1);
                        REF_LABEL_REG[t] = kv.Value; // ex. TL1 AR_KSR3 or what have you
                        // tb(""+t);
                        // tb("Ch"+channel+" opdata: "+kv.Key+"=0x_"+Convert.ToString(kv.Value,16));
                    }
                }
                // REF_REG_LABEL_OPS = REF_LABEL_REG_OPS.ToDictionary(x => x.Value, x => x.Key);

                 REF_REG_LABEL = REF_LABEL_REG.ToDictionary(x => x.Value, x => x.Key);
                //  foreach (var kv in REF_REG_LABEL) {
                //      tb("Ch"+channel+": "+kv.Value+"=0x_"+Convert.ToString(kv.Key, 16));
                //  }
                        // Console.ReadKey();
                // additional (not ref) dictionaries:
                // REG_VAL LABEL_IDX LABEL_VAL

            }


            public void Add(string s, byte b) { // channel-wide additions
                // Ch.Add(s,b);
                REF_LABEL_REG[s] = b; // ??
                if (chip==0 || operators==0) {
                    tb("FMchannel2: Warning: FM chip not set!"); Console.ReadKey();
                }
            }

            public List<Dictionary<string,byte>> Ops() { // LABEL_REG
                if (operators == 2) {
                    return new List<Dictionary<string,byte>>(){Op1, Op2};
                } else {
                    return new List<Dictionary<string,byte>>(){Op1, Op2, Op3, Op4};
                }
            }

            public List<Dictionary<byte,string>> Ops_REG_LABEL() { // reverse of above
                var list = new List<Dictionary<byte,string>>();

                foreach (Dictionary<string,byte> op in Ops()) {
                    list.Add(op.ToDictionary(x => x.Value, x => x.Key) );
                }

                return list;
            }

            // public string Ops_REG_LABEL(byte b) {

            // }

            // public bool keyon(byte value) {
            //     // first four bits determine slot, last four bits determine channel

            // }

        }


    #region System-Wide Registers ------------
    internal const string TESTREGISTER = "TESTREGISTER"; // 0x21 OPM OPN, 0x01 OPL (third bit is OPL compatibility mode!)
    internal const string KEYON_OFF = "KEYON_OFF"; // -4321XXX Operator flags / channel select (OPN chs are 0-1-2 4-5-6)
    internal const string NOISEENABLE = "NOISEENABLE"; // OPM 0x0F
    internal const string TIMER_A_MSB = "TIMER_A_MSB"; // OPL only has 1 byte for this
    internal const string TIMER_A_LSB = "TIMER_A_LSB"; 
    internal const string TIMER_B = "TIMER_B"; 
    internal const string TIMER_LOAD_SAVE = "TIMER_LOAD_SAVE";   // OPM/OPN first bits from the left are CSM, Ch3mode (OPN only)
    internal const string OPM_LFO_FREQUENCY = "OPM_LFO_FREQUENCY"; // OPM 0x18
    internal const string OPM_LFO_AM_PM_DEPTH = "OPM_LFO_AM_PM_DEPTH";   // 7 bits, bit 1 determines AM or PM (0=AM 1=PM)
    internal const string OPM_LFO_WAVEFORM = "OPM_LFO_WAVEFORM"; // CT / LFO waveform OPM 0x1b
    internal const string OPNA_LFO_ENABLE = "OPNA_LFO_ENABLE"; // 0x22: ----xyyy x=enable y=rate
    internal const string ADPCM_A_KEYON = "ADPCM_A_KEYON"; // first bit 'Dump' (disable=1)
    internal const string ADPCM_A_TL = "ADPCM_A_TL"; 
    internal const string ADPCM_A_TEST = "ADPCM_A_TEST"; 
    internal const string RSS_TL1 = "RSS_TL1";   // L,R, 6bit volume - RHY_BD  
    internal const string RSS_TL2 = "RSS_TL2"; // L,R, 6bit volume - RHY_SD
    internal const string RSS_TL3 = "RSS_TL3"; // L,R, 6bit volume - RHY_TOP
    internal const string RSS_TL4 = "RSS_TL4"; // L,R, 6bit volume - RHY_HH
    internal const string RSS_TL5 = "RSS_TL5"; // L,R, 6bit volume - RHY_TOM
    internal const string RSS_TL6 = "RSS_TL6"; // L,R, 6bit volume - RHY_RIM
    internal const string RSS_VOLUME = "RSS_VOLUME"; // 0x11 - ADPCM Master Volume
    internal const string RSS_KEYON_OFF = "RSS_KEYON_OFF"; // ? 0xBF = Key Off all. Is first bit 'off'?  x-xxxxxx ?
    internal const string ADPCM_A_TL1 = "ADPCM_A_TL1"; // L,R, 6bit volume - RHY_BD
    internal const string ADPCM_A_TL2 = "ADPCM_A_TL2"; // L,R, 6bit volume - RHY_SD
    internal const string ADPCM_A_TL3 = "ADPCM_A_TL3"; // L,R, 6bit volume - RHY_TOP
    internal const string ADPCM_A_TL4 = "ADPCM_A_TL4"; // L,R, 6bit volume - RHY_HH
    internal const string ADPCM_A_TL5 = "ADPCM_A_TL5"; // L,R, 6bit volume - RHY_TOM
    internal const string ADPCM_A_TL6 = "ADPCM_A_TL6"; // L,R, 6bit volume - RHY_RIM
    internal const string ADPCM_A_STARTADDRESS1_LOW = "ADPCM_A_STARTADDRESS1_LOW"; // 0x10
    //    10-15 xxxxxxxx Start address (low)
    //    18-1D xxxxxxxx Start address (high)
    //    20-25 xxxxxxxx End address (low)
    //    28-2D xxxxxxxx End address (high)
    //     Y8950 & OPNA/OPNB - ADPCM_B
    internal const string ADPCM_B_OPTIONS1 = "ADPCM_B_OPTIONS1"; // start, record, external/manual driving, repeat playback, speaker off, -,-, reset
    internal const string ADPCM_B_OPTIONS2 = "ADPCM_B_OPTIONS2"; // panL,panR,-,-,start conversion,DAC enable, DRAM access, RAM/ROM
    internal const string ADPCM_B_STARTADDRESS_LOW = "ADPCM_B_STARTADDRESS_LOW"; 
    internal const string ADPCM_B_STARTADDRESS_HIGH = "ADPCM_B_STARTADDRESS_HIGH"; 
    internal const string ADPCM_B_ENDADDRESS_LOW = "ADPCM_B_ENDADDRESS_LOW"; 
    internal const string ADPCM_B_ENDADDRESS_HIGH = "ADPCM_B_ENDADDRESS_HIGH"; 
    internal const string ADPCM_B_PRESCALE_HIGH = "ADPCM_B_PRESCALE_HIGH"; 
    internal const string ADPCM_B_PRESCALE_LOW = "ADPCM_B_PRESCALE_LOW"; // -----xxx
    internal const string ADPCM_B_CPU_DATA_BUFFER = "ADPCM_B_CPU_DATA_BUFFER"; // CPU data/buffer
    internal const string ADPCM_B_DELTA_N_FREQUENCYSCALE_LOW = "ADPCM_B_DELTA_N_FREQUENCYSCALE_LOW"; 
    internal const string ADPCM_B_DELTA_N_FREQUENCYSCALE_HIGH = "ADPCM_B_DELTA_N_FREQUENCYSCALE_HIGH"; 
    internal const string ADPCM_B_LEVELCONTROL = "ADPCM_B_LEVELCONTROL"; 
    internal const string ADPCM_B_LIMIT_ADDRESS_LOW = "ADPCM_B_LIMIT_ADDRESS_LOW"; 
    internal const string ADPCM_B_LIMIT_ADDRESS_HIGH = "ADPCM_B_LIMIT_ADDRESS_HIGH"; 
    // these vvv
    internal const string ADPCM_B_OPNA_DAC_DATA = "ADPCM_B_OPNA_DAC_DATA"; 
    internal const string ADPCM_B_OPNA_PCM_DATA = "ADPCM_B_OPNA_PCM_DATA"; 
    // same registers as above:
    internal const string ADPCM_B_Y8950_DAC_DATA_HIGH = "ADPCM_B_Y8950_DAC_DATA_HIGH"; 
    internal const string ADPCM_B_Y8950_DAC_DATA_LOW = "ADPCM_B_Y8950_DAC_DATA_LOW"; // xx------
    internal const string ADPCM_B_Y8950_DAC_DATA_EXPONENT = "ADPCM_B_Y8950_DAC_DATA_EXPONENT"; // -----xxx
// note: while OPM registers are mostly the same as OPN, OPL registers are much different 
    internal const string CSM_MODE_OPL = "CSM_MODE_OPL"; // 0x08
    internal const string RHYTHM_OPL = "RHYTHM_OPL"; // first two bits AM depth / PM depth, third is Rhythm ENABLE, rest: BD-SNARE-TOM-TOP-HH

    internal const string TESTREGISTER2_OPL3 = "TESTREGISTER2_OPL3"; // --xxxxxxxx (?)
    internal const string OPL3_4OP_ENABLE = "OPL3_4OP_ENABLE"; // --654321 
    internal const string OPL3_NEW = "OPL3_NEW"; // ??
    #endregion

    #region Channel-Wide Registers ------------

    // OPM - LRXXXYYY Stereo / Feedback / Alg
    // OPNA- --XXXYYY Feedback / Alg
    // OPL - XXXXYYYZ - CHD/CHC/CHB/CHA output (OPL3 only) / Feedback / ALG
    internal const string FEEDBACK_ALG = "FEEDBACK_ALG"; // on OPM, first two bits are Stereo. OPL3+ contains stereo / channel routing
    internal const string KEY_CODE = "KEY_CODE"; // OPM
    internal const string KEY_FRACTION = "KEY_FRACTION"; // OPM


    // OPM 38-3F -xxx--yy LFO PM sensitivity / LFO AM shift  
    // OPNA: B4-B7 XXYY-ZZZ LR Pan / AM Shift / PM Depth
    internal const string LFO_CHANNEL_SENSITIVITY = "LFO_CHANNEL_SENSITIVITY"; // different for OPM and OPNA. OPNA contains stereo

    internal const string FNUM_LSB = "FNUM_LSB"; // OPN
    internal const string FNUM_MSB = "FNUM_MSB"; // OPN   A4-A7 --xxxyyy Block (0-7) / Frequency number upper 3 bits
    internal const string FNUM_MSB_KEYON_OPL = "FNUM_MSB_KEYON_OPL"; // OPL - --XYYYZZ KeyOn / Block / 2-bit FNUM MSB
    #endregion

    #region Operator-Wide Registers -----------

    internal const string DTML = "DTML"; // -XXXYYYY     OPL no DT, instead XXXXYYYY AM enable / PM enable / EG type / KSR - MULT
    internal const string TL = "TL"; // -XXXXXXX     OPL is XXYYYYYY KSL / TL
    internal const string AR_KSR = "AR_KSR"; // XX-YYYYY OPM/OPN ONLY
    internal const string AR_DR_OPL = "AR_DR_OPL"; //! OPL only
    internal const string DR_LFO_AM_ENABLE = "DR_LFO_AM_ENABLE"; // X--YYYYY LFO AM Enable / Decay Rate. Same LFO reg for OPNA and OPM
    internal const string SR_DT2 = "SR_DT2"; //! NO OPL. XX-YYYYY DT2 / SR. (DT2 cut from OPN to make Chowning cry)
    internal const string SL_RR = "SL_RR"; // XXXXYYYY
    internal const string SSGEG_ENABLE_ENVELOPE = "SSGEG_ENABLE_ENVELOPE"; // ----XYYY SSG-EG enable / SSG-EG envelope (0-7) OPN series only
    internal const string WAVEFORM = "WAVEFORM"; // -----XXX OPL3, ------XX OPL2, also -XXX---- OPZ...

    #endregion


    public static void SetupData2(byte chipcode, out List<Dictionary<string,byte>> FMSystemList, out List<FMchannel> FMChannel2List) {
//         // FM system registers, FM channel byteisters
        var FMsystem = new Dictionary<string,byte>(); // usually just one of these except OPL3 which has two
        FMSystemList = new List<Dictionary<string,byte>>();
        FMSystemList.Add(FMsystem);
        FMChannel2List = new List<FMchannel>();

        #region OPM YM2151 / OPP YM2164 --------------
        if (chipcode==0x54) {
            FMsystem.Add(TESTREGISTER, 0x01);
            FMsystem.Add(KEYON_OFF, 0x08);
            FMsystem.Add(NOISEENABLE, 0x0F);
            FMsystem.Add(TIMER_A_MSB, 0x10);
            FMsystem.Add(TIMER_A_LSB, 0x11);
            FMsystem.Add(TIMER_B, 0x12);
            FMsystem.Add(TIMER_LOAD_SAVE, 0x14);
            FMsystem.Add(OPM_LFO_FREQUENCY, 0x18);
            FMsystem.Add(OPM_LFO_AM_PM_DEPTH, 0x19);
            FMsystem.Add(OPM_LFO_WAVEFORM, 0x1B);

            var ch0 = new FMchannel("FM0",4, 0x54); var ch1 = new FMchannel("FM1",4, 0x54); var ch2 = new FMchannel("FM2",4, 0x54); var ch3 = new FMchannel("FM3",4, 0x54);
            var ch4 = new FMchannel("FM4",4, 0x54); var ch5 = new FMchannel("FM5",4, 0x54); var ch6 = new FMchannel("FM6",4, 0x54); var ch7 = new FMchannel("FM7",4, 0x54);
            FMChannel2List = new List<FMchannel>(){ch0, ch1, ch2, ch3, ch4, ch5, ch6, ch7};

            ch0.Add(FEEDBACK_ALG, 0x20); ch1.Add(FEEDBACK_ALG, 0x21); ch2.Add(FEEDBACK_ALG, 0x22); ch3.Add(FEEDBACK_ALG, 0x23);
            ch4.Add(FEEDBACK_ALG, 0x24); ch5.Add(FEEDBACK_ALG, 0x25); ch6.Add(FEEDBACK_ALG, 0x26); ch7.Add(FEEDBACK_ALG, 0x27);

            ch0.Add(KEY_CODE, 0x28); 
            ch1.Add(KEY_CODE, 0x29); 
            ch2.Add(KEY_CODE, 0x2a); 
            ch3.Add(KEY_CODE, 0x2b); 
            ch4.Add(KEY_CODE, 0x2c); 
            ch5.Add(KEY_CODE, 0x2d); 
            ch6.Add(KEY_CODE, 0x2e); 
            ch7.Add(KEY_CODE, 0x2f); 

            ch0.Add(KEY_FRACTION, 0x30);
            ch1.Add(KEY_FRACTION, 0x31);
            ch2.Add(KEY_FRACTION, 0x32);
            ch3.Add(KEY_FRACTION, 0x33);
            ch4.Add(KEY_FRACTION, 0x34);
            ch5.Add(KEY_FRACTION, 0x35);
            ch6.Add(KEY_FRACTION, 0x36);
            ch7.Add(KEY_FRACTION, 0x37);

            ch0.Add(LFO_CHANNEL_SENSITIVITY, 0x38);
            ch1.Add(LFO_CHANNEL_SENSITIVITY, 0x39);
            ch2.Add(LFO_CHANNEL_SENSITIVITY, 0x3A);
            ch3.Add(LFO_CHANNEL_SENSITIVITY, 0x3B);
            ch4.Add(LFO_CHANNEL_SENSITIVITY, 0x3C);
            ch5.Add(LFO_CHANNEL_SENSITIVITY, 0x3D);
            ch6.Add(LFO_CHANNEL_SENSITIVITY, 0x3E);
            ch7.Add(LFO_CHANNEL_SENSITIVITY, 0x3F);


            ch0.Op1.Add(DTML, 0x40); ch0.Op3.Add(DTML, 0x48); ch0.Op2.Add(DTML, 0x50); ch0.Op4.Add(DTML, 0x58);
            ch1.Op1.Add(DTML, 0x41); ch1.Op3.Add(DTML, 0x49); ch1.Op2.Add(DTML, 0x51); ch1.Op4.Add(DTML, 0x59);
            ch2.Op1.Add(DTML, 0x42); ch2.Op3.Add(DTML, 0x4A); ch2.Op2.Add(DTML, 0x52); ch2.Op4.Add(DTML, 0x5A);
            ch3.Op1.Add(DTML, 0x43); ch3.Op3.Add(DTML, 0x4B); ch3.Op2.Add(DTML, 0x53); ch3.Op4.Add(DTML, 0x5B);
            ch4.Op1.Add(DTML, 0x44); ch4.Op3.Add(DTML, 0x4C); ch4.Op2.Add(DTML, 0x54); ch4.Op4.Add(DTML, 0x5C);
            ch5.Op1.Add(DTML, 0x45); ch5.Op3.Add(DTML, 0x4D); ch5.Op2.Add(DTML, 0x55); ch5.Op4.Add(DTML, 0x5D);
            ch6.Op1.Add(DTML, 0x46); ch6.Op3.Add(DTML, 0x4E); ch6.Op2.Add(DTML, 0x56); ch6.Op4.Add(DTML, 0x5E);
            ch7.Op1.Add(DTML, 0x47); ch7.Op3.Add(DTML, 0x4F); ch7.Op2.Add(DTML, 0x57); ch7.Op4.Add(DTML, 0x5F);

            ch0.Op1.Add(TL, 0x60); ch0.Op3.Add(TL, 0x68); ch0.Op2.Add(TL, 0x70); ch0.Op4.Add(TL, 0x78);
            ch1.Op1.Add(TL, 0x61); ch1.Op3.Add(TL, 0x69); ch1.Op2.Add(TL, 0x71); ch1.Op4.Add(TL, 0x79);
            ch2.Op1.Add(TL, 0x62); ch2.Op3.Add(TL, 0x6A); ch2.Op2.Add(TL, 0x72); ch2.Op4.Add(TL, 0x7A);
            ch3.Op1.Add(TL, 0x63); ch3.Op3.Add(TL, 0x6B); ch3.Op2.Add(TL, 0x73); ch3.Op4.Add(TL, 0x7B);
            ch4.Op1.Add(TL, 0x64); ch4.Op3.Add(TL, 0x6C); ch4.Op2.Add(TL, 0x74); ch4.Op4.Add(TL, 0x7C);
            ch5.Op1.Add(TL, 0x65); ch5.Op3.Add(TL, 0x6D); ch5.Op2.Add(TL, 0x75); ch5.Op4.Add(TL, 0x7D);
            ch6.Op1.Add(TL, 0x66); ch6.Op3.Add(TL, 0x6E); ch6.Op2.Add(TL, 0x76); ch6.Op4.Add(TL, 0x7E);
            ch7.Op1.Add(TL, 0x67); ch7.Op3.Add(TL, 0x6F); ch7.Op2.Add(TL, 0x77); ch7.Op4.Add(TL, 0x7F);

            ch0.Op1.Add(AR_KSR, 0x80); ch0.Op3.Add(AR_KSR, 0x88); ch0.Op2.Add(AR_KSR, 0x90); ch0.Op4.Add(AR_KSR, 0x98);
            ch1.Op1.Add(AR_KSR, 0x81); ch1.Op3.Add(AR_KSR, 0x89); ch1.Op2.Add(AR_KSR, 0x91); ch1.Op4.Add(AR_KSR, 0x99);
            ch2.Op1.Add(AR_KSR, 0x82); ch2.Op3.Add(AR_KSR, 0x8A); ch2.Op2.Add(AR_KSR, 0x92); ch2.Op4.Add(AR_KSR, 0x9A);
            ch3.Op1.Add(AR_KSR, 0x83); ch3.Op3.Add(AR_KSR, 0x8B); ch3.Op2.Add(AR_KSR, 0x93); ch3.Op4.Add(AR_KSR, 0x9B);
            ch4.Op1.Add(AR_KSR, 0x84); ch4.Op3.Add(AR_KSR, 0x8C); ch4.Op2.Add(AR_KSR, 0x94); ch4.Op4.Add(AR_KSR, 0x9C);
            ch5.Op1.Add(AR_KSR, 0x85); ch5.Op3.Add(AR_KSR, 0x8D); ch5.Op2.Add(AR_KSR, 0x95); ch5.Op4.Add(AR_KSR, 0x9D);
            ch6.Op1.Add(AR_KSR, 0x86); ch6.Op3.Add(AR_KSR, 0x8E); ch6.Op2.Add(AR_KSR, 0x96); ch6.Op4.Add(AR_KSR, 0x9E);
            ch7.Op1.Add(AR_KSR, 0x87); ch7.Op3.Add(AR_KSR, 0x8F); ch7.Op2.Add(AR_KSR, 0x97); ch7.Op4.Add(AR_KSR, 0x9F);

            ch0.Op1.Add(DR_LFO_AM_ENABLE, 0xA0); ch0.Op3.Add(DR_LFO_AM_ENABLE, 0xA8); ch0.Op2.Add(DR_LFO_AM_ENABLE, 0xB0); ch0.Op4.Add(DR_LFO_AM_ENABLE, 0xB8);
            ch1.Op1.Add(DR_LFO_AM_ENABLE, 0xA1); ch1.Op3.Add(DR_LFO_AM_ENABLE, 0xA9); ch1.Op2.Add(DR_LFO_AM_ENABLE, 0xB1); ch1.Op4.Add(DR_LFO_AM_ENABLE, 0xB9);
            ch2.Op1.Add(DR_LFO_AM_ENABLE, 0xA2); ch2.Op3.Add(DR_LFO_AM_ENABLE, 0xAA); ch2.Op2.Add(DR_LFO_AM_ENABLE, 0xB2); ch2.Op4.Add(DR_LFO_AM_ENABLE, 0xBA);
            ch3.Op1.Add(DR_LFO_AM_ENABLE, 0xA3); ch3.Op3.Add(DR_LFO_AM_ENABLE, 0xAB); ch3.Op2.Add(DR_LFO_AM_ENABLE, 0xB3); ch3.Op4.Add(DR_LFO_AM_ENABLE, 0xBB);
            ch4.Op1.Add(DR_LFO_AM_ENABLE, 0xA4); ch4.Op3.Add(DR_LFO_AM_ENABLE, 0xAC); ch4.Op2.Add(DR_LFO_AM_ENABLE, 0xB4); ch4.Op4.Add(DR_LFO_AM_ENABLE, 0xBC);
            ch5.Op1.Add(DR_LFO_AM_ENABLE, 0xA5); ch5.Op3.Add(DR_LFO_AM_ENABLE, 0xAD); ch5.Op2.Add(DR_LFO_AM_ENABLE, 0xB5); ch5.Op4.Add(DR_LFO_AM_ENABLE, 0xBD);
            ch6.Op1.Add(DR_LFO_AM_ENABLE, 0xA6); ch6.Op3.Add(DR_LFO_AM_ENABLE, 0xAE); ch6.Op2.Add(DR_LFO_AM_ENABLE, 0xB6); ch6.Op4.Add(DR_LFO_AM_ENABLE, 0xBE);
            ch7.Op1.Add(DR_LFO_AM_ENABLE, 0xA7); ch7.Op3.Add(DR_LFO_AM_ENABLE, 0xAF); ch7.Op2.Add(DR_LFO_AM_ENABLE, 0xB7); ch7.Op4.Add(DR_LFO_AM_ENABLE, 0xBF);

            ch0.Op1.Add(SR_DT2, 0xC0); ch0.Op3.Add(SR_DT2, 0xC8); ch0.Op2.Add(SR_DT2, 0xD0); ch0.Op4.Add(SR_DT2, 0xD8);
            ch1.Op1.Add(SR_DT2, 0xC1); ch1.Op3.Add(SR_DT2, 0xC9); ch1.Op2.Add(SR_DT2, 0xD1); ch1.Op4.Add(SR_DT2, 0xD9);
            ch2.Op1.Add(SR_DT2, 0xC2); ch2.Op3.Add(SR_DT2, 0xCA); ch2.Op2.Add(SR_DT2, 0xD2); ch2.Op4.Add(SR_DT2, 0xDA);
            ch3.Op1.Add(SR_DT2, 0xC3); ch3.Op3.Add(SR_DT2, 0xCB); ch3.Op2.Add(SR_DT2, 0xD3); ch3.Op4.Add(SR_DT2, 0xDB);
            ch4.Op1.Add(SR_DT2, 0xC4); ch4.Op3.Add(SR_DT2, 0xCC); ch4.Op2.Add(SR_DT2, 0xD4); ch4.Op4.Add(SR_DT2, 0xDC);
            ch5.Op1.Add(SR_DT2, 0xC5); ch5.Op3.Add(SR_DT2, 0xCD); ch5.Op2.Add(SR_DT2, 0xD5); ch5.Op4.Add(SR_DT2, 0xDD);
            ch6.Op1.Add(SR_DT2, 0xC6); ch6.Op3.Add(SR_DT2, 0xCE); ch6.Op2.Add(SR_DT2, 0xD6); ch6.Op4.Add(SR_DT2, 0xDE);
            ch7.Op1.Add(SR_DT2, 0xC7); ch7.Op3.Add(SR_DT2, 0xCF); ch7.Op2.Add(SR_DT2, 0xD7); ch7.Op4.Add(SR_DT2, 0xDF);

            ch0.Op1.Add(SL_RR, 0xE0); ch0.Op3.Add(SL_RR, 0xE8); ch0.Op2.Add(SL_RR, 0xF0); ch0.Op4.Add(SL_RR, 0xF8);
            ch1.Op1.Add(SL_RR, 0xE1); ch1.Op3.Add(SL_RR, 0xE9); ch1.Op2.Add(SL_RR, 0xF1); ch1.Op4.Add(SL_RR, 0xF9);
            ch2.Op1.Add(SL_RR, 0xE2); ch2.Op3.Add(SL_RR, 0xEA); ch2.Op2.Add(SL_RR, 0xF2); ch2.Op4.Add(SL_RR, 0xFA);
            ch3.Op1.Add(SL_RR, 0xE3); ch3.Op3.Add(SL_RR, 0xEB); ch3.Op2.Add(SL_RR, 0xF3); ch3.Op4.Add(SL_RR, 0xFB);
            ch4.Op1.Add(SL_RR, 0xE4); ch4.Op3.Add(SL_RR, 0xEC); ch4.Op2.Add(SL_RR, 0xF4); ch4.Op4.Add(SL_RR, 0xFC);
            ch5.Op1.Add(SL_RR, 0xE5); ch5.Op3.Add(SL_RR, 0xED); ch5.Op2.Add(SL_RR, 0xF5); ch5.Op4.Add(SL_RR, 0xFD);
            ch6.Op1.Add(SL_RR, 0xE6); ch6.Op3.Add(SL_RR, 0xEE); ch6.Op2.Add(SL_RR, 0xF6); ch6.Op4.Add(SL_RR, 0xFE);
            ch7.Op1.Add(SL_RR, 0xE7); ch7.Op3.Add(SL_RR, 0xEF); ch7.Op2.Add(SL_RR, 0xF7); ch7.Op4.Add(SL_RR, 0xFF);

        }

        #endregion

        #region OPN series- YM2203 YM2608 YM2610 YM2612 YM3438 ----------------

        if (chipcode==0x52 || chipcode==0x55 || chipcode==0x56 || chipcode==0x58) {   // OPN2 | OPN | OPNA | OPNB -- 3+ channels
            // SSG 0x00 -> ??
            FMsystem.Add(TESTREGISTER, 0x21);
            FMsystem.Add(TIMER_A_MSB, 0x24);
            FMsystem.Add(TIMER_A_LSB, 0x25);
            FMsystem.Add(TIMER_B, 0x26);
            FMsystem.Add(TIMER_LOAD_SAVE, 0x27); // second bit is ch3 mode
            FMsystem.Add(KEYON_OFF, 0x28);

            var ch0 = new FMchannel("FM0",4, chipcode); var ch1 = new FMchannel("FM1",4, chipcode); var ch2 = new FMchannel("FM2",4, chipcode);
            FMChannel2List = new List<FMchannel>(){ch0, ch1, ch2};
            
            ch0.Add(FNUM_LSB, 0xA0); ch1.Add(FNUM_LSB, 0xA1); ch2.Add(FNUM_LSB, 0xA2);
            ch0.Add(FNUM_MSB, 0xA4); ch1.Add(FNUM_MSB, 0xA5); ch2.Add(FNUM_MSB, 0xA6); // a7?

            ch0.Add(FEEDBACK_ALG, 0xB0); ch1.Add(FEEDBACK_ALG, 0xB1); ch2.Add(FEEDBACK_ALG, 0xB2);
            ch0.Add(LFO_CHANNEL_SENSITIVITY, 0xB4); ch1.Add(LFO_CHANNEL_SENSITIVITY, 0xB5); ch2.Add(LFO_CHANNEL_SENSITIVITY, 0xB6); // pan too, OPNA+

            ch0.Op1.Add(DTML, 0x30); ch0.Op3.Add(DTML, 0x34); ch0.Op2.Add(DTML, 0x38); ch0.Op4.Add(DTML, 0x3C); 
            ch1.Op1.Add(DTML, 0x31); ch1.Op3.Add(DTML, 0x35); ch1.Op2.Add(DTML, 0x39); ch1.Op4.Add(DTML, 0x3D); 
            ch2.Op1.Add(DTML, 0x32); ch2.Op3.Add(DTML, 0x36); ch2.Op2.Add(DTML, 0x3A); ch2.Op4.Add(DTML, 0x3E); 

            ch0.Op1.Add(TL, 0x40); ch0.Op3.Add(TL, 0x44); ch0.Op2.Add(TL, 0x48); ch0.Op4.Add(TL, 0x4C); 
            ch1.Op1.Add(TL, 0x41); ch1.Op3.Add(TL, 0x45); ch1.Op2.Add(TL, 0x49); ch1.Op4.Add(TL, 0x4D); 
            ch2.Op1.Add(TL, 0x42); ch2.Op3.Add(TL, 0x46); ch2.Op2.Add(TL, 0x4A); ch2.Op4.Add(TL, 0x4E); 

            ch0.Op1.Add(AR_KSR, 0x50); ch0.Op3.Add(AR_KSR, 0x54); ch0.Op2.Add(AR_KSR, 0x58); ch0.Op4.Add(AR_KSR, 0x5C); 
            ch1.Op1.Add(AR_KSR, 0x51); ch1.Op3.Add(AR_KSR, 0x55); ch1.Op2.Add(AR_KSR, 0x59); ch1.Op4.Add(AR_KSR, 0x5D); 
            ch2.Op1.Add(AR_KSR, 0x52); ch2.Op3.Add(AR_KSR, 0x56); ch2.Op2.Add(AR_KSR, 0x5A); ch2.Op4.Add(AR_KSR, 0x5E); 

            ch0.Op1.Add(DR_LFO_AM_ENABLE, 0x60); ch0.Op3.Add(DR_LFO_AM_ENABLE, 0x64); ch0.Op2.Add(DR_LFO_AM_ENABLE, 0x68); ch0.Op4.Add(DR_LFO_AM_ENABLE, 0x6C); 
            ch1.Op1.Add(DR_LFO_AM_ENABLE, 0x61); ch1.Op3.Add(DR_LFO_AM_ENABLE, 0x65); ch1.Op2.Add(DR_LFO_AM_ENABLE, 0x69); ch1.Op4.Add(DR_LFO_AM_ENABLE, 0x6D); 
            ch2.Op1.Add(DR_LFO_AM_ENABLE, 0x62); ch2.Op3.Add(DR_LFO_AM_ENABLE, 0x66); ch2.Op2.Add(DR_LFO_AM_ENABLE, 0x6A); ch2.Op4.Add(DR_LFO_AM_ENABLE, 0x6E); 

            ch0.Op1.Add(SR_DT2, 0x70); ch0.Op3.Add(SR_DT2, 0x74); ch0.Op2.Add(SR_DT2, 0x78); ch0.Op4.Add(SR_DT2, 0x7C); 
            ch1.Op1.Add(SR_DT2, 0x71); ch1.Op3.Add(SR_DT2, 0x75); ch1.Op2.Add(SR_DT2, 0x79); ch1.Op4.Add(SR_DT2, 0x7D); 
            ch2.Op1.Add(SR_DT2, 0x72); ch2.Op3.Add(SR_DT2, 0x76); ch2.Op2.Add(SR_DT2, 0x7A); ch2.Op4.Add(SR_DT2, 0x7E); 

            ch0.Op1.Add(SL_RR, 0x80); ch0.Op3.Add(SL_RR, 0x84); ch0.Op2.Add(SL_RR, 0x88); ch0.Op4.Add(SL_RR, 0x8C); 
            ch1.Op1.Add(SL_RR, 0x81); ch1.Op3.Add(SL_RR, 0x85); ch1.Op2.Add(SL_RR, 0x89); ch1.Op4.Add(SL_RR, 0x8D); 
            ch2.Op1.Add(SL_RR, 0x82); ch2.Op3.Add(SL_RR, 0x86); ch2.Op2.Add(SL_RR, 0x8A); ch2.Op4.Add(SL_RR, 0x8E); 

            ch0.Op1.Add(SSGEG_ENABLE_ENVELOPE, 0x90); ch0.Op3.Add(SSGEG_ENABLE_ENVELOPE, 0x94); ch0.Op2.Add(SSGEG_ENABLE_ENVELOPE, 0x98); ch0.Op4.Add(SSGEG_ENABLE_ENVELOPE, 0x9C); 
            ch1.Op1.Add(SSGEG_ENABLE_ENVELOPE, 0x91); ch1.Op3.Add(SSGEG_ENABLE_ENVELOPE, 0x95); ch1.Op2.Add(SSGEG_ENABLE_ENVELOPE, 0x99); ch1.Op4.Add(SSGEG_ENABLE_ENVELOPE, 0x9D); 
            ch2.Op1.Add(SSGEG_ENABLE_ENVELOPE, 0x92); ch2.Op3.Add(SSGEG_ENABLE_ENVELOPE, 0x96); ch2.Op2.Add(SSGEG_ENABLE_ENVELOPE, 0x9A); ch2.Op4.Add(SSGEG_ENABLE_ENVELOPE, 0x9E); 

            if (chipcode < 0x55 || chipcode > 0x55) { // 0x55=opn1. rest have 6 channels. YM2610 only has four, but it still works like YM2608 with two banks of registers

                byte bank2 = Convert.ToByte(chipcode+1);
                // tb("data2.cs: bank2 = "+Convert.ToString(bank2,16) );
                // FMsystem.Add() // todo ADPCM-A / RSS ... ADPCM-B

                var ch3 = new FMchannel("FM3",4, bank2); var ch4 = new FMchannel("FM4",4, bank2); var ch5 = new FMchannel("FM5",4, bank2);
                
                FMChannel2List.Add(ch3); FMChannel2List.Add(ch4); FMChannel2List.Add(ch5);
                FMsystem.Add(OPNA_LFO_ENABLE, 0x22); 
                

                ch3.Add(FNUM_LSB, 0xA0); ch4.Add(FNUM_LSB, 0xA1); ch5.Add(FNUM_LSB, 0xA2);
                ch3.Add(FNUM_MSB, 0xA4); ch4.Add(FNUM_MSB, 0xA5); ch5.Add(FNUM_MSB, 0xA6); // a7?

                ch3.Add(FEEDBACK_ALG, 0xB0); ch4.Add(FEEDBACK_ALG, 0xB1); ch5.Add(FEEDBACK_ALG, 0xB2);
                ch3.Add(LFO_CHANNEL_SENSITIVITY, 0xB4); ch4.Add(LFO_CHANNEL_SENSITIVITY, 0xB5); ch5.Add(LFO_CHANNEL_SENSITIVITY, 0xB6); // pan too, OPNA+

                ch3.Op1.Add(DTML, 0x30); ch3.Op3.Add(DTML, 0x34); ch3.Op2.Add(DTML, 0x38); ch3.Op4.Add(DTML, 0x3C); 
                ch4.Op1.Add(DTML, 0x31); ch4.Op3.Add(DTML, 0x35); ch4.Op2.Add(DTML, 0x39); ch4.Op4.Add(DTML, 0x3D); 
                ch5.Op1.Add(DTML, 0x32); ch5.Op3.Add(DTML, 0x36); ch5.Op2.Add(DTML, 0x3A); ch5.Op4.Add(DTML, 0x3E); 

                ch3.Op1.Add(TL, 0x40); ch3.Op3.Add(TL, 0x44); ch3.Op2.Add(TL, 0x48); ch3.Op4.Add(TL, 0x4C); 
                ch4.Op1.Add(TL, 0x41); ch4.Op3.Add(TL, 0x45); ch4.Op2.Add(TL, 0x49); ch4.Op4.Add(TL, 0x4D); 
                ch5.Op1.Add(TL, 0x42); ch5.Op3.Add(TL, 0x46); ch5.Op2.Add(TL, 0x4A); ch5.Op4.Add(TL, 0x4E); 

                ch3.Op1.Add(AR_KSR, 0x50); ch3.Op3.Add(AR_KSR, 0x54); ch3.Op2.Add(AR_KSR, 0x58); ch3.Op4.Add(AR_KSR, 0x5C); 
                ch4.Op1.Add(AR_KSR, 0x51); ch4.Op3.Add(AR_KSR, 0x55); ch4.Op2.Add(AR_KSR, 0x59); ch4.Op4.Add(AR_KSR, 0x5D); 
                ch5.Op1.Add(AR_KSR, 0x52); ch5.Op3.Add(AR_KSR, 0x56); ch5.Op2.Add(AR_KSR, 0x5A); ch5.Op4.Add(AR_KSR, 0x5E); 

                ch3.Op1.Add(DR_LFO_AM_ENABLE, 0x60); ch3.Op3.Add(DR_LFO_AM_ENABLE, 0x64); ch3.Op2.Add(DR_LFO_AM_ENABLE, 0x68); ch3.Op4.Add(DR_LFO_AM_ENABLE, 0x6C); 
                ch4.Op1.Add(DR_LFO_AM_ENABLE, 0x61); ch4.Op3.Add(DR_LFO_AM_ENABLE, 0x65); ch4.Op2.Add(DR_LFO_AM_ENABLE, 0x69); ch4.Op4.Add(DR_LFO_AM_ENABLE, 0x6D); 
                ch5.Op1.Add(DR_LFO_AM_ENABLE, 0x62); ch5.Op3.Add(DR_LFO_AM_ENABLE, 0x66); ch5.Op2.Add(DR_LFO_AM_ENABLE, 0x6A); ch5.Op4.Add(DR_LFO_AM_ENABLE, 0x6E); 

                ch3.Op1.Add(SR_DT2, 0x70); ch3.Op3.Add(SR_DT2, 0x74); ch3.Op2.Add(SR_DT2, 0x78); ch3.Op4.Add(SR_DT2, 0x7C); 
                ch4.Op1.Add(SR_DT2, 0x71); ch4.Op3.Add(SR_DT2, 0x75); ch4.Op2.Add(SR_DT2, 0x79); ch4.Op4.Add(SR_DT2, 0x7D); 
                ch5.Op1.Add(SR_DT2, 0x72); ch5.Op3.Add(SR_DT2, 0x76); ch5.Op2.Add(SR_DT2, 0x7A); ch5.Op4.Add(SR_DT2, 0x7E); 

                ch3.Op1.Add(SL_RR, 0x80); ch3.Op3.Add(SL_RR, 0x84); ch3.Op2.Add(SL_RR, 0x88); ch3.Op4.Add(SL_RR, 0x8C); 
                ch4.Op1.Add(SL_RR, 0x81); ch4.Op3.Add(SL_RR, 0x85); ch4.Op2.Add(SL_RR, 0x89); ch4.Op4.Add(SL_RR, 0x8D); 
                ch5.Op1.Add(SL_RR, 0x82); ch5.Op3.Add(SL_RR, 0x86); ch5.Op2.Add(SL_RR, 0x8A); ch5.Op4.Add(SL_RR, 0x8E); 

                ch3.Op1.Add(SSGEG_ENABLE_ENVELOPE, 0x90); ch3.Op3.Add(SSGEG_ENABLE_ENVELOPE, 0x94); ch3.Op2.Add(SSGEG_ENABLE_ENVELOPE, 0x98); ch3.Op4.Add(SSGEG_ENABLE_ENVELOPE, 0x9C); 
                ch4.Op1.Add(SSGEG_ENABLE_ENVELOPE, 0x91); ch4.Op3.Add(SSGEG_ENABLE_ENVELOPE, 0x95); ch4.Op2.Add(SSGEG_ENABLE_ENVELOPE, 0x99); ch4.Op4.Add(SSGEG_ENABLE_ENVELOPE, 0x9D); 
                ch5.Op1.Add(SSGEG_ENABLE_ENVELOPE, 0x92); ch5.Op3.Add(SSGEG_ENABLE_ENVELOPE, 0x96); ch5.Op2.Add(SSGEG_ENABLE_ENVELOPE, 0x9A); ch5.Op4.Add(SSGEG_ENABLE_ENVELOPE, 0x9E); 



            }


            // todo not present: multi-frequency registers A8-AB AC-AF

        }
        #endregion

        #region OPL series YM3526/"OPL" Y8950/"MSX-AUDIO" YM3812/"OPL2" YMF262/"OPL3"


        if (chipcode >= 0x5A && chipcode <= 0x5F) { // 5A OPL2 5B OPL1 5C Y8950 5E/5F YMF262 OPL3

        // 5b 5c 5a 5e/5f 0xd0 (ymf278 has a 4 byte command)


            var ch0 = new FMchannel("FM0",2, chipcode); var ch1 = new FMchannel("FM1",2, chipcode); var ch2 = new FMchannel("FM2",2, chipcode); var ch3 = new FMchannel("FM3",2, chipcode);
            var ch4 = new FMchannel("FM4",2, chipcode); var ch5 = new FMchannel("FM5",2, chipcode); var ch6 = new FMchannel("FM6",2, chipcode); var ch7 = new FMchannel("FM7",2, chipcode);
            var ch8 = new FMchannel("FM8",2, chipcode);

            FMChannel2List = new List<FMchannel>(){ch0, ch1, ch2, ch3, ch4, ch5, ch6, ch7, ch8};

            FMsystem.Add(TESTREGISTER, 0x01); // third bit OPL compatibility mode (OPL2 only)
            FMsystem.Add(TIMER_A_MSB, 0x02);
            FMsystem.Add(TIMER_B, 0x03);
            FMsystem.Add(RHYTHM_OPL, 0x04);
            FMsystem.Add(CSM_MODE_OPL, 0x08);

            ch0.Add(FNUM_LSB,0xA0);
            ch1.Add(FNUM_LSB,0xA1);
            ch2.Add(FNUM_LSB,0xA2);
            ch3.Add(FNUM_LSB,0xA3);
            ch4.Add(FNUM_LSB,0xA4);
            ch5.Add(FNUM_LSB,0xA5);
            ch6.Add(FNUM_LSB,0xA6);
            ch7.Add(FNUM_LSB,0xA7);
            ch8.Add(FNUM_LSB,0xA8);

            ch0.Add(FNUM_MSB_KEYON_OPL,0xB0);
            ch1.Add(FNUM_MSB_KEYON_OPL,0xB1);
            ch2.Add(FNUM_MSB_KEYON_OPL,0xB2);
            ch3.Add(FNUM_MSB_KEYON_OPL,0xB3);
            ch4.Add(FNUM_MSB_KEYON_OPL,0xB4);
            ch5.Add(FNUM_MSB_KEYON_OPL,0xB5);
            ch6.Add(FNUM_MSB_KEYON_OPL,0xB6);
            ch7.Add(FNUM_MSB_KEYON_OPL,0xB7);
            ch8.Add(FNUM_MSB_KEYON_OPL,0xB8);

            ch0.Add(FEEDBACK_ALG,0xC0); // OPL - XXXXYYYZ - CHD/CHC/CHB/CHA output (OPL3 only) / Feedback / ALG
            ch1.Add(FEEDBACK_ALG,0xC1);
            ch2.Add(FEEDBACK_ALG,0xC2);
            ch3.Add(FEEDBACK_ALG,0xC3);
            ch4.Add(FEEDBACK_ALG,0xC4);
            ch5.Add(FEEDBACK_ALG,0xC5);
            ch6.Add(FEEDBACK_ALG,0xC6);
            ch7.Add(FEEDBACK_ALG,0xC7);
            ch8.Add(FEEDBACK_ALG,0xC8);

            ch0.Op1.Add(DTML, 0x20); ch0.Op2.Add(DTML, 0x23);
            ch1.Op1.Add(DTML, 0x21); ch1.Op2.Add(DTML, 0x24);
            ch2.Op1.Add(DTML, 0x22); ch2.Op2.Add(DTML, 0x25);
            ch3.Op1.Add(DTML, 0x28); ch3.Op2.Add(DTML, 0x2B);
            ch4.Op1.Add(DTML, 0x29); ch4.Op2.Add(DTML, 0x2C);
            ch5.Op1.Add(DTML, 0x2A); ch5.Op2.Add(DTML, 0x2D);
            ch6.Op1.Add(DTML, 0x30); ch6.Op2.Add(DTML, 0x33);
            ch7.Op1.Add(DTML, 0x31); ch7.Op2.Add(DTML, 0x34);
            ch8.Op1.Add(DTML, 0x32); ch8.Op2.Add(DTML, 0x35);

            ch0.Op1.Add(TL, 0x40); ch0.Op2.Add(TL, 0x43);
            ch1.Op1.Add(TL, 0x41); ch1.Op2.Add(TL, 0x44);
            ch2.Op1.Add(TL, 0x42); ch2.Op2.Add(TL, 0x45);
            ch3.Op1.Add(TL, 0x48); ch3.Op2.Add(TL, 0x4B);
            ch4.Op1.Add(TL, 0x49); ch4.Op2.Add(TL, 0x4C);
            ch5.Op1.Add(TL, 0x4A); ch5.Op2.Add(TL, 0x4D);
            ch6.Op1.Add(TL, 0x50); ch6.Op2.Add(TL, 0x53);
            ch7.Op1.Add(TL, 0x51); ch7.Op2.Add(TL, 0x54);
            ch8.Op1.Add(TL, 0x52); ch8.Op2.Add(TL, 0x55);

            ch0.Op1.Add(AR_DR_OPL, 0x60); ch0.Op2.Add(AR_DR_OPL, 0x63);
            ch1.Op1.Add(AR_DR_OPL, 0x61); ch1.Op2.Add(AR_DR_OPL, 0x64);
            ch2.Op1.Add(AR_DR_OPL, 0x62); ch2.Op2.Add(AR_DR_OPL, 0x65);
            ch3.Op1.Add(AR_DR_OPL, 0x68); ch3.Op2.Add(AR_DR_OPL, 0x6B);
            ch4.Op1.Add(AR_DR_OPL, 0x69); ch4.Op2.Add(AR_DR_OPL, 0x6C);
            ch5.Op1.Add(AR_DR_OPL, 0x6A); ch5.Op2.Add(AR_DR_OPL, 0x6D);
            ch6.Op1.Add(AR_DR_OPL, 0x70); ch6.Op2.Add(AR_DR_OPL, 0x73);
            ch7.Op1.Add(AR_DR_OPL, 0x71); ch7.Op2.Add(AR_DR_OPL, 0x74);
            ch8.Op1.Add(AR_DR_OPL, 0x72); ch8.Op2.Add(AR_DR_OPL, 0x75);

            ch0.Op1.Add(SL_RR, 0x80); ch0.Op2.Add(SL_RR, 0x83);
            ch1.Op1.Add(SL_RR, 0x81); ch1.Op2.Add(SL_RR, 0x84);
            ch2.Op1.Add(SL_RR, 0x82); ch2.Op2.Add(SL_RR, 0x85);
            ch3.Op1.Add(SL_RR, 0x88); ch3.Op2.Add(SL_RR, 0x8B);
            ch4.Op1.Add(SL_RR, 0x89); ch4.Op2.Add(SL_RR, 0x8C);
            ch5.Op1.Add(SL_RR, 0x8A); ch5.Op2.Add(SL_RR, 0x8D);
            ch6.Op1.Add(SL_RR, 0x90); ch6.Op2.Add(SL_RR, 0x93);
            ch7.Op1.Add(SL_RR, 0x91); ch7.Op2.Add(SL_RR, 0x94);
            ch8.Op1.Add(SL_RR, 0x92); ch8.Op2.Add(SL_RR, 0x95);

            ch0.Op1.Add(WAVEFORM, 0xE0); ch0.Op2.Add(WAVEFORM, 0xE3);
            ch1.Op1.Add(WAVEFORM, 0xE1); ch1.Op2.Add(WAVEFORM, 0xE4);
            ch2.Op1.Add(WAVEFORM, 0xE2); ch2.Op2.Add(WAVEFORM, 0xE5);
            ch3.Op1.Add(WAVEFORM, 0xE8); ch3.Op2.Add(WAVEFORM, 0xEB);
            ch4.Op1.Add(WAVEFORM, 0xE9); ch4.Op2.Add(WAVEFORM, 0xEC);
            ch5.Op1.Add(WAVEFORM, 0xEA); ch5.Op2.Add(WAVEFORM, 0xED);
            ch6.Op1.Add(WAVEFORM, 0xF0); ch6.Op2.Add(WAVEFORM, 0xF3);
            ch7.Op1.Add(WAVEFORM, 0xF1); ch7.Op2.Add(WAVEFORM, 0xF4);
            ch8.Op1.Add(WAVEFORM, 0xF2); ch8.Op2.Add(WAVEFORM, 0xF5);

            if (chipcode == 0x5e) { //  OPL3
                var FMsystem2 = new Dictionary<string,byte>(); // OPL3 second bank
                FMSystemList.Add(FMsystem2);

                FMsystem2.Add(TESTREGISTER2_OPL3, 0x01); // bank 2
                FMsystem2.Add(OPL3_4OP_ENABLE, 0x04); // bank 2
                FMsystem2.Add(OPL3_NEW, 0x05); // bank 2
                
                byte bank2 = Convert.ToByte(chipcode+1); // OPL3 bank 2: 0x5F
                var ch9 = new FMchannel("FM9",2, bank2); var ch10 = new FMchannel("FM10",2, bank2); var ch11 = new FMchannel("FM11",2, bank2); var ch12 = new FMchannel("FM12",2, bank2);
                var ch13 = new FMchannel("FM13",2, bank2); var ch14 = new FMchannel("FM14",2, bank2); var ch15 = new FMchannel("FM15",2, bank2); var ch16 = new FMchannel("FM16",2, bank2);
                var ch17 = new FMchannel("FM17",2, bank2);

                FMChannel2List.Add(ch9); FMChannel2List.Add(ch10); FMChannel2List.Add(ch11);
                FMChannel2List.Add(ch12); FMChannel2List.Add(ch13); FMChannel2List.Add(ch14);
                FMChannel2List.Add(ch15); FMChannel2List.Add(ch16); FMChannel2List.Add(ch17);

                 ch9.Add(FNUM_LSB,0xA0);
                ch10.Add(FNUM_LSB,0xA1);
                ch11.Add(FNUM_LSB,0xA2);
                ch12.Add(FNUM_LSB,0xA3);
                ch13.Add(FNUM_LSB,0xA4);
                ch14.Add(FNUM_LSB,0xA5);
                ch15.Add(FNUM_LSB,0xA6);
                ch16.Add(FNUM_LSB,0xA7);
                ch17.Add(FNUM_LSB,0xA8);

                 ch9.Add(FNUM_MSB_KEYON_OPL,0xB0);
                ch10.Add(FNUM_MSB_KEYON_OPL,0xB1);
                ch11.Add(FNUM_MSB_KEYON_OPL,0xB2);
                ch12.Add(FNUM_MSB_KEYON_OPL,0xB3);
                ch13.Add(FNUM_MSB_KEYON_OPL,0xB4);
                ch14.Add(FNUM_MSB_KEYON_OPL,0xB5);
                ch15.Add(FNUM_MSB_KEYON_OPL,0xB6);
                ch16.Add(FNUM_MSB_KEYON_OPL,0xB7);
                ch17.Add(FNUM_MSB_KEYON_OPL,0xB8);

                 ch9.Add(FEEDBACK_ALG,0xC0); // OPL - XXXXYYYZ - CHD/CHC/CHB/CHA output (OPL3 only) / Feedback / ALG
                ch10.Add(FEEDBACK_ALG,0xC1);
                ch11.Add(FEEDBACK_ALG,0xC2);
                ch12.Add(FEEDBACK_ALG,0xC3);
                ch13.Add(FEEDBACK_ALG,0xC4);
                ch14.Add(FEEDBACK_ALG,0xC5);
                ch15.Add(FEEDBACK_ALG,0xC6);
                ch16.Add(FEEDBACK_ALG,0xC7);
                ch17.Add(FEEDBACK_ALG,0xC8);

                ch9.Op1.Add(DTML, 0x20);  ch9.Op2.Add(DTML, 0x23);
                ch10.Op1.Add(DTML, 0x21); ch10.Op2.Add(DTML, 0x24);
                ch11.Op1.Add(DTML, 0x22); ch11.Op2.Add(DTML, 0x25);
                ch12.Op1.Add(DTML, 0x28); ch12.Op2.Add(DTML, 0x2B);
                ch13.Op1.Add(DTML, 0x29); ch13.Op2.Add(DTML, 0x2C);
                ch14.Op1.Add(DTML, 0x2A); ch14.Op2.Add(DTML, 0x2D);
                ch15.Op1.Add(DTML, 0x30); ch15.Op2.Add(DTML, 0x33);
                ch16.Op1.Add(DTML, 0x31); ch16.Op2.Add(DTML, 0x34);
                ch17.Op1.Add(DTML, 0x32); ch17.Op2.Add(DTML, 0x35);

                ch9.Op1.Add(TL, 0x40);  ch9.Op2.Add(TL, 0x43);
                ch10.Op1.Add(TL, 0x41); ch10.Op2.Add(TL, 0x44);
                ch11.Op1.Add(TL, 0x42); ch11.Op2.Add(TL, 0x45);
                ch12.Op1.Add(TL, 0x48); ch12.Op2.Add(TL, 0x4B);
                ch13.Op1.Add(TL, 0x49); ch13.Op2.Add(TL, 0x4C);
                ch14.Op1.Add(TL, 0x4A); ch14.Op2.Add(TL, 0x4D);
                ch15.Op1.Add(TL, 0x50); ch15.Op2.Add(TL, 0x53);
                ch16.Op1.Add(TL, 0x51); ch16.Op2.Add(TL, 0x54);
                ch17.Op1.Add(TL, 0x52); ch17.Op2.Add(TL, 0x55);

                ch9.Op1.Add(AR_DR_OPL, 0x60);  ch9.Op2.Add(AR_DR_OPL, 0x63);
                ch10.Op1.Add(AR_DR_OPL, 0x61); ch10.Op2.Add(AR_DR_OPL, 0x64);
                ch11.Op1.Add(AR_DR_OPL, 0x62); ch11.Op2.Add(AR_DR_OPL, 0x65);
                ch12.Op1.Add(AR_DR_OPL, 0x68); ch12.Op2.Add(AR_DR_OPL, 0x6B);
                ch13.Op1.Add(AR_DR_OPL, 0x69); ch13.Op2.Add(AR_DR_OPL, 0x6C);
                ch14.Op1.Add(AR_DR_OPL, 0x6A); ch14.Op2.Add(AR_DR_OPL, 0x6D);
                ch15.Op1.Add(AR_DR_OPL, 0x70); ch15.Op2.Add(AR_DR_OPL, 0x73);
                ch16.Op1.Add(AR_DR_OPL, 0x71); ch16.Op2.Add(AR_DR_OPL, 0x74);
                ch17.Op1.Add(AR_DR_OPL, 0x72); ch17.Op2.Add(AR_DR_OPL, 0x75);

                ch9.Op1.Add(SL_RR, 0x80);  ch9.Op2.Add(SL_RR, 0x83);
                ch10.Op1.Add(SL_RR, 0x81); ch10.Op2.Add(SL_RR, 0x84);
                ch11.Op1.Add(SL_RR, 0x82); ch11.Op2.Add(SL_RR, 0x85);
                ch12.Op1.Add(SL_RR, 0x88); ch12.Op2.Add(SL_RR, 0x8B);
                ch13.Op1.Add(SL_RR, 0x89); ch13.Op2.Add(SL_RR, 0x8C);
                ch14.Op1.Add(SL_RR, 0x8A); ch14.Op2.Add(SL_RR, 0x8D);
                ch15.Op1.Add(SL_RR, 0x90); ch15.Op2.Add(SL_RR, 0x93);
                ch16.Op1.Add(SL_RR, 0x91); ch16.Op2.Add(SL_RR, 0x94);
                ch17.Op1.Add(SL_RR, 0x92); ch17.Op2.Add(SL_RR, 0x95);

                ch9.Op1.Add(WAVEFORM, 0xE0);  ch9.Op2.Add(WAVEFORM, 0xE3);
                ch10.Op1.Add(WAVEFORM, 0xE1); ch10.Op2.Add(WAVEFORM, 0xE4);
                ch11.Op1.Add(WAVEFORM, 0xE2); ch11.Op2.Add(WAVEFORM, 0xE5);
                ch12.Op1.Add(WAVEFORM, 0xE8); ch12.Op2.Add(WAVEFORM, 0xEB);
                ch13.Op1.Add(WAVEFORM, 0xE9); ch13.Op2.Add(WAVEFORM, 0xEC);
                ch14.Op1.Add(WAVEFORM, 0xEA); ch14.Op2.Add(WAVEFORM, 0xED);
                ch15.Op1.Add(WAVEFORM, 0xF0); ch15.Op2.Add(WAVEFORM, 0xF3);
                ch16.Op1.Add(WAVEFORM, 0xF1); ch16.Op2.Add(WAVEFORM, 0xF4);
                ch17.Op1.Add(WAVEFORM, 0xF2); ch17.Op2.Add(WAVEFORM, 0xF5);


            }


        }

        #endregion

        // TODO: YM2413 OPLL
        // TODO: YMF278/"OPL4" (uses 4 bytes instead of 3)
        // TODO: YMF271 OPX? I know nothing about this chip. Does YMFM even support it?

    }
    

        static double DT2ratio(byte dt2, byte mult) {
            double m = mult;
            switch (dt2) {
                case 0: return m;
                case 1: return m*1.41;
                case 2: return m*1.57;
                case 3: return m*1.73;
                default: return m; // shouldn't happen
            }

        }

        // this is from a DX vmem format converter, just here for reference
        static int DecodeDXfreq(byte frq, out int dt2) { // Yamaha keyboards combine frequency and DT2 in one big indexed list 
			dt2=0;
            switch (frq) {		// I'm assuming the numbers are in order...
                case 0: dt2=0; return 0; // 0.5
                case 1: dt2=1; return 0; // 0.71
                case 2: dt2=2; return 0; // 0.78
                case 3: dt2=3; return 0; // 0.87
                case 4: dt2=0; return 1; // 1.00
                case 5: dt2=1; return 1; // 1.41
                case 6: dt2=2; return 1; // 1.57
                case 7: dt2=3; return 1; // 1.73
                case 8: dt2=0; return 2; // 2
                case 9: dt2=1; return 2; // 2.82
                case 10: dt2=0; return 3; // 3.0
                case 11: dt2=2; return 2; // 3.14
                case 12: dt2=3; return 2; // 3.46
                case 13: dt2=0; return 4; // 4.0
                case 14: dt2=1; return 3; // 4.24
                case 15: dt2=2; return 3; // 4.71
                case 16: dt2=0; return 5; // 5.0
                case 17: dt2=3; return 3; // 5.19
                case 18: dt2=1; return 4; // 5.65
                case 19: dt2=0; return 6; // 6
                case 20: dt2=2; return 4; // 6.28
                case 21: dt2=3; return 4; // 6.92
                case 22: dt2=0; return 7; // 7
                case 23: dt2=1; return 5; // 7.07
                case 24: dt2=2; return 5; // 7.85
                case 25: dt2=0; return 8; // 8
                case 26: dt2=1; return 6; // 8.48
                case 27: dt2=3; return 5; // 8.65
                case 28: dt2=0; return 9; // 9
                case 29: dt2=2; return 6; // 9.42
                case 30: dt2=1; return 7; // 9.89
                case 31: dt2=0; return 10; // 10
                case 32: dt2=3; return 6; // 10.38
                case 33: dt2=2; return 7; // 10.99
                case 34: dt2=0; return 11; // 11
                case 35: dt2=1; return 8; // 11.30
                case 36: dt2=0; return 12; // 12
                case 37: dt2=3; return 7; // 12.11
                case 38: dt2=2; return 8; // 12.56
                case 39: dt2=1; return 9; // 12.72
                case 40: dt2=0; return 13; // 13
                case 41: dt2=3; return 8; // 13.84
                case 42: dt2=0; return 14; // 14
                case 43: dt2=1; return 10; // 14.1
                case 44: dt2=2; return 9; // 14.13
                case 45: dt2=0; return 15; // 15
                case 46: dt2=1; return 11; // 15.55
                case 47: dt2=3; return 9; // 15.57
                case 48: dt2=2; return 10; // 15.7
                case 49: dt2=1; return 12; // 16.96
                case 50: dt2=2; return 11; // 17.27
                case 51: dt2=3; return 10; // 17.30
                case 52: dt2=1; return 13; // 18.37
                case 53: dt2=2; return 12; // 18.84
                case 54: dt2=3; return 11; // 19.03
                case 55: dt2=1; return 14; // 19.78
                case 56: dt2=2; return 13; // 20.41
                case 57: dt2=3; return 12; // 20.76
                case 58: dt2=1; return 15; // 21.2
                case 59: dt2=2; return 14; // 21.98
                case 60: dt2=3; return 13; // 22.49
                case 61: dt2=2; return 15; // 23.55
                case 62: dt2=3; return 14; // 24.22
                case 63: dt2=3; return 15; // 25.95
            }
			tb("DX100 bad input "+frq);
            return 0;
        }




}

}



















// // }



