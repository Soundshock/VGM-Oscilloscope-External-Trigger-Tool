// namespace EXTT
// {

//     public partial class Program {

//         static void SetupData(byte chiptype) {
                //! old. see data2.cs
//             // setup: byte indexes, saved per channel
//             // constructor includes this.chip for VGM chip number
//             // (52/53 for OPN2, 54 for OPM, 55 for OPN, 56/57 for OPNA), 0x58/9 OPNB, 5A OPL2, 5B OPL, 5C Y8950
//             // TODO YMF262 OPL3
//             // FMchannel FM0,FM1,FM2,FM3,FM4,FM5,FM6,FM7,FM8;
//             FM0 = new FMchannel(4,0x55); FM1 = new FMchannel(4,0x55); FM2 = new FMchannel(4,0x55); 
//             FM3 = new FMchannel(4,0x55); FM4 = new FMchannel(4,0x55); FM5 = new FMchannel(4,0x55); 
//             FM6 = new FMchannel(4,0x55); FM7 = new FMchannel(4,0x55); FM8 = new FMchannel(2,0x55);


//             if (chiptype==0x55){          //* OPN
//                 FM0 = new FMchannel(4,0x55); FM1 = new FMchannel(4,0x55); FM2 = new FMchannel(4,0x55); 

//                 FM0.keyon = new byte[] {0x55, 0x28, 0xF0}; // keyon commands.
//                 FM1.keyon = new byte[] {0x55, 0x28, 0xF1}; //* These will be scanned as the starting point for patch searching
//                 FM2.keyon = new byte[] {0x55, 0x28, 0xF2};

//             } else if (chiptype==0x56){   //* OPNA
//                 FM0 = new FMchannel(4,0x56);  FM1 = new FMchannel(4,0x56);  FM2 = new FMchannel(4,0x56);
//                 FM3 = new FMchannel(4,0x57);  FM4 = new FMchannel(4,0x57);  FM5 = new FMchannel(4,0x57);

//                 FM0.keyon = new byte[] {0x56, 0x28, 0xF0}; // keyon commands.
//                 FM1.keyon = new byte[] {0x56, 0x28, 0xF1}; //* These will be scanned as the starting point for patch searching
//                 FM2.keyon = new byte[] {0x56, 0x28, 0xF2};
//                 FM3.keyon = new byte[] {0x56, 0x28, 0xF4}; // not 57 btw
//                 FM4.keyon = new byte[] {0x56, 0x28, 0xF5};
//                 FM5.keyon = new byte[] {0x56, 0x28, 0xF6};
//             } else if (chiptype==0x58){   //* OPNB (specifically YM2610B with 6 channels. this might break with 4-channel FMs)
//                 FM0 = new FMchannel(4,0x58);  FM1 = new FMchannel(4,0x58);  FM2 = new FMchannel(4,0x58);
//                 FM3 = new FMchannel(4,0x59);  FM4 = new FMchannel(4,0x59);  FM5 = new FMchannel(4,0x59);

//                 FM0.keyon = new byte[] {0x58, 0x28, 0xF0}; // keyon commands.
//                 FM1.keyon = new byte[] {0x58, 0x28, 0xF1}; //* These will be scanned as the starting point for patch searching
//                 FM2.keyon = new byte[] {0x58, 0x28, 0xF2};
//                 FM3.keyon = new byte[] {0x58, 0x28, 0xF4}; // not 57 btw
//                 FM4.keyon = new byte[] {0x58, 0x28, 0xF5};
//                 FM5.keyon = new byte[] {0x58, 0x28, 0xF6};
//             } else if (chiptype==0x52){   //* OPN2
//                 FM0 = new FMchannel(4,0x52);  FM1 = new FMchannel(4,0x52);  FM2 = new FMchannel(4,0x52);
//                 FM3 = new FMchannel(4,0x53);  FM4 = new FMchannel(4,0x53);  FM5 = new FMchannel(4,0x53);

//                 FM0.keyon = new byte[] {0x52, 0x28, 0xF0}; // keyon commands.
//                 FM1.keyon = new byte[] {0x52, 0x28, 0xF1}; //* These will be scanned as the starting point for patch searching
//                 FM2.keyon = new byte[] {0x52, 0x28, 0xF2}; // copy pasted from OPNA. they probably work. 
//                 FM3.keyon = new byte[] {0x52, 0x28, 0xF4};
//                 FM4.keyon = new byte[] {0x52, 0x28, 0xF5};
//                 FM5.keyon = new byte[] {0x52, 0x28, 0xF6};
//             }
//             if (chiptype==0x52 || chiptype==0x55 || chiptype==0x56 || chiptype==0x58) { //* OPN2/OPN/OPNA/OPNB(?) ch#1 - ch#3
//                 FM0.name="FM0"; FM1.name="FM1"; FM2.name="FM2"; 

//                 FM0.op1_TL = 0x40; FM0.op2_TL = 0x48; FM0.op3_TL = 0x44; FM0.op4_TL = 0x4C;
//                 FM1.op1_TL = 0x41; FM1.op2_TL = 0x49; FM1.op3_TL = 0x45; FM1.op4_TL = 0x4D; 
//                 FM2.op1_TL = 0x42; FM2.op2_TL = 0x4A; FM2.op3_TL = 0x46; FM2.op4_TL = 0x4E;

//                 FM0.op1_DTML=0x30; FM0.op2_DTML=0x38; FM0.op3_DTML=0x34; FM0.op4_DTML=0x3C;
//                 FM1.op1_DTML=0x31; FM1.op2_DTML=0x39; FM1.op3_DTML=0x35; FM1.op4_DTML=0x3D;
//                 FM2.op1_DTML=0x32; FM2.op2_DTML=0x3A; FM2.op3_DTML=0x36; FM2.op4_DTML=0x3E;

//                 FM0.SetAR(0x50, 0x58, 0x54, 0x5C); FM1.SetAR(0x51, 0x59, 0x55, 0x5D); FM2.SetAR(0x52, 0x5A, 0x56, 0x5E);
//                 FM0.SetDR(0x60, 0x68, 0x64, 0x6C); FM1.SetDR(0x61, 0x69, 0x65, 0x6D); FM2.SetDR(0x62, 0x6A, 0x66, 0x6E);

//                 // Yamaha 4op synths use a 5-part envelope
//                 // Attack Rate - Decay Rate - Sustain Level - Sustain Rate - Release Rate
//                 // in OPN chips, SR / RR is the same byte (4bit-4bit) -- ???

//                 FM0.SR[0] = 0x70; FM0.SR[2] = 0x74; FM0.SR[1] = 0x78; FM0.SR[3] = 0x7C; 
//                 FM1.SR[0] = 0x71; FM1.SR[2] = 0x75; FM1.SR[1] = 0x79; FM1.SR[3] = 0x7D; 
//                 FM2.SR[0] = 0x72; FM2.SR[2] = 0x76; FM2.SR[1] = 0x7A; FM2.SR[3] = 0x7E; 


//                 FM0.RR[0] = 0x80; FM0.RR[2] = 0x84; FM0.RR[1] = 0x88; FM0.RR[3] = 0x8C; 
//                 FM1.RR[0] = 0x81; FM1.RR[2] = 0x85; FM1.RR[1] = 0x89; FM1.RR[3] = 0x8D; 
//                 FM2.RR[0] = 0x82; FM2.RR[2] = 0x86; FM2.RR[1] = 0x8A; FM2.RR[3] = 0x8E; 

//                 FM0.ALG = 0xB0; FM1.ALG = 0xB1; FM2.ALG = 0xB2;    // Alg shares a bit with feedback (feedback\ALG)

//             }
//             if (chiptype==0x52 || chiptype==0x56 || chiptype==0x58){  //* 6-ch OPNs, and also OPNB
//                 FM3.name="FM3"; FM4.name="FM4"; FM5.name="FM5"; // used for debugging
//                 FM3.op1_TL = 0x40; FM3.op2_TL = 0x48; FM3.op3_TL = 0x44; FM3.op4_TL = 0x4C; // chip=57 (opna) or 53 (opn2)
//                 FM4.op1_TL = 0x41; FM4.op2_TL = 0x49; FM4.op3_TL = 0x45; FM4.op4_TL = 0x4D; // chip=57 (opna) or 53 (opn2)
//                 FM5.op1_TL = 0x42; FM5.op2_TL = 0x4A; FM5.op3_TL = 0x46; FM5.op4_TL = 0x4E; // chip=57 (opna) or 53 (opn2)

//                 FM3.op1_DTML=0x30; FM3.op2_DTML=0x38; FM3.op3_DTML=0x34; FM3.op4_DTML=0x3C; // chip=57 (opna) or 53 (opn2)
//                 FM4.op1_DTML=0x31; FM4.op2_DTML=0x39; FM4.op3_DTML=0x35; FM4.op4_DTML=0x3D; // chip=57 (opna) or 53 (opn2)
//                 FM5.op1_DTML=0x32; FM5.op2_DTML=0x3A; FM5.op3_DTML=0x36; FM5.op4_DTML=0x3E; // chip=57 (opna) or 53 (opn2)

//                 //* decay rate shares a bit with AM. AM will be destroyed by this code but that's fine(?)
//                 FM3.SetAR(0x50, 0x58, 0x54, 0x5C); FM4.SetAR(0x51, 0x59, 0x55, 0x5D); FM5.SetAR(0x52, 0x5A, 0x56, 0x5E); // 53 / 57
//                 FM3.SetDR(0x60, 0x68, 0x64, 0x6C); FM4.SetDR(0x61, 0x69, 0x65, 0x6D); FM5.SetDR(0x62, 0x6A, 0x66, 0x6E);

//                 FM3.SR[0] = 0x70; FM3.SR[2] = 0x74; FM3.SR[1] = 0x78; FM3.SR[3] = 0x7C; 
//                 FM4.SR[0] = 0x71; FM4.SR[2] = 0x75; FM4.SR[1] = 0x79; FM4.SR[3] = 0x7D; 
//                 FM5.SR[0] = 0x72; FM5.SR[2] = 0x76; FM5.SR[1] = 0x7A; FM5.SR[3] = 0x7E; 

//                 FM3.RR[0] = 0x80; FM3.RR[2] = 0x84; FM3.RR[1] = 0x88; FM3.RR[3] = 0x8C; // 53 / 57
//                 FM4.RR[0] = 0x81; FM4.RR[2] = 0x85; FM4.RR[1] = 0x89; FM4.RR[3] = 0x8D; 
//                 FM5.RR[0] = 0x82; FM5.RR[2] = 0x86; FM5.RR[1] = 0x8A; FM5.RR[3] = 0x8E; 

//                 FM3.ALG = 0xB0; FM4.ALG = 0xB1; FM5.ALG = 0xB2;    // Alg shares a bit with feedback (feedback\ALG)


//             }
//             if (chiptype==0x54) { // 8-voice OPM YM2151
//                 FM0 = new FMchannel(4,0x54);  FM1 = new FMchannel(4,0x54);  FM2 = new FMchannel(4,0x54);
//                 FM3 = new FMchannel(4,0x54);  FM4 = new FMchannel(4,0x54);  FM5 = new FMchannel(4,0x54);
//                 FM6 = new FMchannel(4,0x54);  FM7 = new FMchannel(4,0x54);
//                 FM0.name="FM0";FM1.name="FM1";FM2.name="FM2";FM3.name="FM3";FM4.name="FM4";FM5.name="FM5";FM6.name="FM6";FM7.name="FM7";

//                 //* OPM & OPN keyons technically have flags to keyon each operator, but aside from ch3 mode they are rarely not simply F or 0
//                 FM0.keyon = new byte[] {0x54, 0x08, 0x78}; FM1.keyon = new byte[] {0x54, 0x08, 0x79}; // these values bit shift a lot
//                 FM2.keyon = new byte[] {0x54, 0x08, 0x7A}; FM3.keyon = new byte[] {0x54, 0x08, 0x7B}; // but we only need keyon so it's fine
//                 FM4.keyon = new byte[] {0x54, 0x08, 0x7C}; FM5.keyon = new byte[] {0x54, 0x08, 0x7D}; 
//                 FM6.keyon = new byte[] {0x54, 0x08, 0x7E}; FM7.keyon = new byte[] {0x54, 0x08, 0x7F};   	

//                 FM0.op1_TL = 0x60; FM0.op3_TL = 0x68; FM0.op2_TL = 0x70; FM0.op4_TL = 0x78;  // remember fully off TL is 7f. 127 is max value.
//                 FM1.op1_TL = 0x61; FM1.op3_TL = 0x69; FM1.op2_TL = 0x71; FM1.op4_TL = 0x79; 
//                 FM2.op1_TL = 0x62; FM2.op3_TL = 0x6A; FM2.op2_TL = 0x72; FM2.op4_TL = 0x7A;  
//                 FM3.op1_TL = 0x63; FM3.op3_TL = 0x6B; FM3.op2_TL = 0x73; FM3.op4_TL = 0x7B;  
//                 FM4.op1_TL = 0x64; FM4.op3_TL = 0x6C; FM4.op2_TL = 0x74; FM4.op4_TL = 0x7C;  
//                 FM5.op1_TL = 0x65; FM5.op3_TL = 0x6D; FM5.op2_TL = 0x75; FM5.op4_TL = 0x7D;  
//                 FM6.op1_TL = 0x66; FM6.op3_TL = 0x6E; FM6.op2_TL = 0x76; FM6.op4_TL = 0x7E;  
//                 FM7.op1_TL = 0x67; FM7.op3_TL = 0x6F; FM7.op2_TL = 0x77; FM7.op4_TL = 0x7F; 
                
//                 FM0.op1_DTML = 0x40; FM0.op3_DTML = 0x48; FM0.op2_DTML = 0x50; FM0.op4_DTML = 0x58; 
//                 FM1.op1_DTML = 0x41; FM1.op3_DTML = 0x49; FM1.op2_DTML = 0x51; FM1.op4_DTML = 0x59; 
//                 FM2.op1_DTML = 0x42; FM2.op3_DTML = 0x4A; FM2.op2_DTML = 0x52; FM2.op4_DTML = 0x5A; 
//                 FM3.op1_DTML = 0x43; FM3.op3_DTML = 0x4B; FM3.op2_DTML = 0x53; FM3.op4_DTML = 0x5B; 
//                 FM4.op1_DTML = 0x44; FM4.op3_DTML = 0x4C; FM4.op2_DTML = 0x54; FM4.op4_DTML = 0x5C; 
//                 FM5.op1_DTML = 0x45; FM5.op3_DTML = 0x4D; FM5.op2_DTML = 0x55; FM5.op4_DTML = 0x5D; 
//                 FM6.op1_DTML = 0x46; FM6.op3_DTML = 0x4E; FM6.op2_DTML = 0x56; FM6.op4_DTML = 0x5E; 
//                 FM7.op1_DTML = 0x47; FM7.op3_DTML = 0x4F; FM7.op2_DTML = 0x57; FM7.op4_DTML = 0x5F;

//                 FM0.AR[0] = 0x80; FM0.AR[1] = 0x88; FM0.AR[2] = 0x90; FM0.AR[3] = 0x98; 
//                 FM1.AR[0] = 0x81; FM1.AR[1] = 0x89; FM1.AR[2] = 0x91; FM1.AR[3] = 0x99;
//                 FM2.AR[0] = 0x82; FM2.AR[1] = 0x8A; FM2.AR[2] = 0x92; FM2.AR[3] = 0x9A; 
//                 FM3.AR[0] = 0x83; FM3.AR[1] = 0x8B; FM3.AR[2] = 0x93; FM3.AR[3] = 0x9B; 
//                 FM4.AR[0] = 0x84; FM4.AR[1] = 0x8C; FM4.AR[2] = 0x94; FM4.AR[3] = 0x9C; 
//                 FM5.AR[0] = 0x85; FM5.AR[1] = 0x8D; FM5.AR[2] = 0x95; FM5.AR[3] = 0x9D; 
//                 FM6.AR[0] = 0x86; FM6.AR[1] = 0x8E; FM6.AR[2] = 0x96; FM6.AR[3] = 0x9E; 
//                 FM7.AR[0] = 0x87; FM7.AR[1] = 0x8F; FM7.AR[2] = 0x97; FM7.AR[3] = 0x9F; 	                    

//                 FM0.DR[0] = 0xA0; FM0.DR[1] = 0xA8; FM0.DR[2] = 0xB0; FM0.DR[3] = 0xB8; // LFO amp  modul / DR 1. Not sure how these bits are split up.
//                 FM1.DR[0] = 0xA1; FM1.DR[1] = 0xA9; FM1.DR[2] = 0xB1; FM1.DR[3] = 0xB9; 
//                 FM2.DR[0] = 0xA2; FM2.DR[1] = 0xAA; FM2.DR[2] = 0xB2; FM2.DR[3] = 0xBA; 
//                 FM3.DR[0] = 0xA3; FM3.DR[1] = 0xAB; FM3.DR[2] = 0xB3; FM3.DR[3] = 0xBB; 
//                 FM4.DR[0] = 0xA4; FM4.DR[1] = 0xAC; FM4.DR[2] = 0xB4; FM4.DR[3] = 0xBC; 
//                 FM5.DR[0] = 0xA5; FM5.DR[1] = 0xAD; FM5.DR[2] = 0xB5; FM5.DR[3] = 0xBD; 
//                 FM6.DR[0] = 0xA6; FM6.DR[1] = 0xAE; FM6.DR[2] = 0xB6; FM6.DR[3] = 0xBE; 
//                 FM7.DR[0] = 0xA7; FM7.DR[1] = 0xAF; FM7.DR[2] = 0xB7; FM7.DR[3] = 0xBF; 	

//                 // SR opm first four bits are Detune 2

//                 FM0.SR[0]=0xC0; FM0.SR[2]=0xC8; FM0.SR[1]=0xD0; FM0.SR[3]=0xD8; // proper
//                 FM1.SR[0]=0xC1; FM1.SR[2]=0xC9; FM1.SR[1]=0xD1; FM1.SR[3]=0xD9; 
//                 FM2.SR[0]=0xC2; FM2.SR[2]=0xCA; FM2.SR[1]=0xD2; FM2.SR[3]=0xDA; 
//                 FM3.SR[0]=0xC3; FM3.SR[2]=0xCB; FM3.SR[1]=0xD3; FM3.SR[3]=0xDB; 
//                 FM4.SR[0]=0xC4; FM4.SR[2]=0xCC; FM4.SR[1]=0xD4; FM4.SR[3]=0xDC; 
//                 FM5.SR[0]=0xC5; FM5.SR[2]=0xCD; FM5.SR[1]=0xD5; FM5.SR[3]=0xDD; 
//                 FM6.SR[0]=0xC6; FM6.SR[2]=0xCE; FM6.SR[1]=0xD6; FM6.SR[3]=0xDE; 
//                 FM7.SR[0]=0xC7; FM7.SR[2]=0xCF; FM7.SR[1]=0xD7; FM7.SR[3]=0xDF; 


//                 FM0.RR[0] = 0xE0; FM0.RR[1] = 0xE8; FM0.RR[2] = 0xF0; FM0.RR[3] = 0xF8; // D1L / RR. Not sure how these bits are split up.
//                 FM1.RR[0] = 0xE1; FM1.RR[1] = 0xE9; FM1.RR[2] = 0xF1; FM1.RR[3] = 0xF9; // 
//                 FM2.RR[0] = 0xE2; FM2.RR[1] = 0xEA; FM2.RR[2] = 0xF2; FM2.RR[3] = 0xFA; 
//                 FM3.RR[0] = 0xE3; FM3.RR[1] = 0xEB; FM3.RR[2] = 0xF3; FM3.RR[3] = 0xFB; 
//                 FM4.RR[0] = 0xE4; FM4.RR[1] = 0xEC; FM4.RR[2] = 0xF4; FM4.RR[3] = 0xFC; 
//                 FM5.RR[0] = 0xE5; FM5.RR[1] = 0xED; FM5.RR[2] = 0xF5; FM5.RR[3] = 0xFD; 
//                 FM6.RR[0] = 0xE6; FM6.RR[1] = 0xEE; FM6.RR[2] = 0xF6; FM6.RR[3] = 0xFE; 
//                 FM7.RR[0] = 0xE7; FM7.RR[1] = 0xEF; FM7.RR[2] = 0xF7; FM7.RR[3] = 0xFF; 

//                 // DR 1 - LFO amplitude modulation / Decay Rate 1

//                 // ALG: first two bits Stereo (11, 10, 01) Next 3 bits FEEDBACK, Next 3 bits ALG. Feedback 0 / Alg 7 is 0xC7
//                 FM0.ALG=0x20; FM1.ALG=0x21; FM2.ALG=0x22; FM3.ALG=0x23; FM4.ALG=0x24; FM5.ALG=0x25; FM6.ALG=0x26; FM7.ALG=0x27; // ALG/FEEDBACK

//             }
//             if (chiptype>=0x5A) { // 5A - OPL2 5B - OPL 5C - Y8950 (MSX AUDIO)
//                 FM0 = new FMchannel(2,chiptype);  FM1 = new FMchannel(2,chiptype);  FM2 = new FMchannel(2,chiptype);
//                 FM3 = new FMchannel(2,chiptype);  FM4 = new FMchannel(2,chiptype);  FM5 = new FMchannel(2,chiptype);
//                 FM6 = new FMchannel(2,chiptype);  FM7 = new FMchannel(2,chiptype);  FM8 = new FMchannel(2,chiptype);
//                 FM0.name="FM0";FM1.name="FM1";FM2.name="FM2";FM3.name="FM3";FM4.name="FM4";FM5.name="FM5";FM6.name="FM6";FM7.name="FM7";FM8.name="FM8"; 

//                 // opl keyon works a bit differently. FM0 5A B0 xy    x  0|1=keyoff rest keyon?  >0x20 on, <0x20 off?
//                 FM0.keyon = new byte[] {chiptype, 0xB0}; FM1.keyon = new byte[] {chiptype, 0xB1}; FM2.keyon = new byte[] {chiptype, 0xB2}; 
//                 FM3.keyon = new byte[] {chiptype, 0xB3}; FM4.keyon = new byte[] {chiptype, 0xB4}; FM5.keyon = new byte[] {chiptype, 0xB5}; 
//                 FM6.keyon = new byte[] {chiptype, 0xB6}; FM7.keyon = new byte[] {chiptype, 0xB7}; FM8.keyon = new byte[] {chiptype, 0xB7}; 
//                 // tl  - first two bits are key scale, 0,1,2= 00, 01, 10. Rest is TL, a 6-bit value of 0-63 (3F = muted)
//                 FM0.op1_TL=0x40; FM0.op2_TL=0x43;
//                 FM1.op1_TL=0x41; FM1.op2_TL=0x44;
//                 FM2.op1_TL=0x42; FM2.op2_TL=0x45;
//                 FM3.op1_TL=0x48; FM3.op2_TL=0x4B;
//                 FM4.op1_TL=0x49; FM4.op2_TL=0x4C;
//                 FM5.op1_TL=0x4A; FM5.op2_TL=0x4D;
//                 FM6.op1_TL=0x50; FM6.op2_TL=0x53;
//                 FM7.op1_TL=0x51; FM7.op2_TL=0x54;
//                 FM8.op1_TL=0x52; FM8.op2_TL=0x55;
//                 // MULTIPLIER... aka AM|VIBRATO|KSR|EG / MULT - No Detune with OPL2
//                 FM0.op1_DTML=0x20; FM0.op2_DTML=0x23;
//                 FM1.op1_DTML=0x21; FM1.op2_DTML=0x24;
//                 FM2.op1_DTML=0x22; FM2.op2_DTML=0x25;
//                 FM3.op1_DTML=0x28; FM3.op2_DTML=0x2B;
//                 FM4.op1_DTML=0x29; FM4.op2_DTML=0x2C;
//                 FM5.op1_DTML=0x2A; FM5.op2_DTML=0x2D;
//                 FM6.op1_DTML=0x30; FM6.op2_DTML=0x33;
//                 FM7.op1_DTML=0x31; FM7.op2_DTML=0x34;
//                 FM8.op1_DTML=0x32; FM8.op2_DTML=0x35;

//                 // OPL2 waveform. this gets it's own byte, but only overwrite second 4-bit value to be safe. 0x00 for sine wave
//                 FM0.op1_waveform=0xE0; FM0.op2_waveform=0xE3;
//                 FM1.op1_waveform=0xE1; FM1.op2_waveform=0xE4;
//                 FM2.op1_waveform=0xE2; FM2.op2_waveform=0xE5;
//                 FM3.op1_waveform=0xE8; FM3.op2_waveform=0xEB;
//                 FM4.op1_waveform=0xE9; FM4.op2_waveform=0xEC;
//                 FM5.op1_waveform=0xEA; FM5.op2_waveform=0xED;
//                 FM6.op1_waveform=0xF0; FM6.op2_waveform=0xF3;
//                 FM7.op1_waveform=0xF1; FM7.op2_waveform=0xF4;
//                 FM8.op1_waveform=0xF2; FM8.op2_waveform=0xF5;

//                 // Algorithm / feedback (use 0x00) vv via ymfm source vv
//                 // C0-C8 x------- CHD output (to DO0 pin) [OPL3+ only]
//                 // -x------ CHC output (to DO0 pin) [OPL3+ only]
//                 // --x----- CHB output (mixed right, to DO2 pin) [OPL3+ only]
//                 // ---x---- CHA output (mixed left, to DO2 pin) [OPL3+ only]
//                 // ----xxx- Feedback level for operator 1 (0-7)
//                 // -------x Operator connection algorithm
//                 FM0.ALG = 0xC0; FM1.ALG = 0xC1; FM2.ALG = 0xC2;
//                 FM3.ALG = 0xC3; FM4.ALG = 0xC4; FM5.ALG = 0xC5;
//                 FM6.ALG = 0xC6; FM7.ALG = 0xC7; FM8.ALG = 0xC8;

//                 // FM0.SetDR(0x80, 0x83); // Sustain 'level', Release Rate (redundant)

//                 FM0.AR[0] = 0x60; FM0.AR[1] = 0x63; // AR / DR share the same byte in OPL2
//                 FM1.AR[0] = 0x61; FM1.AR[1] = 0x64; 
//                 FM2.AR[0] = 0x62; FM2.AR[1] = 0x65; 
//                 FM3.AR[0] = 0x68; FM3.AR[1] = 0x6B; 
//                 FM4.AR[0] = 0x69; FM4.AR[1] = 0x6C; 
//                 FM5.AR[0] = 0x6A; FM5.AR[1] = 0x6D; 
//                 FM6.AR[0] = 0x70; FM6.AR[1] = 0x73; 
//                 FM7.AR[0] = 0x71; FM7.AR[1] = 0x74; 
//                 FM8.AR[0] = 0x72; FM8.AR[1] = 0x75; 	


//                 FM0.RR[0] = 0x80; FM0.RR[1] = 0x83; // SR / RR
//                 FM1.RR[0] = 0x81; FM1.RR[1] = 0x84; 
//                 FM2.RR[0] = 0x82; FM2.RR[1] = 0x85; 
//                 FM3.RR[0] = 0x88; FM3.RR[1] = 0x8B; 
//                 FM4.RR[0] = 0x89; FM4.RR[1] = 0x8C; 
//                 FM5.RR[0] = 0x8A; FM5.RR[1] = 0x8D; 
//                 FM6.RR[0] = 0x90; FM6.RR[1] = 0x93; 
//                 FM7.RR[0] = 0x91; FM7.RR[1] = 0x94; 
//                 FM8.RR[0] = 0x92; FM8.RR[1] = 0x95; 	

//             }

            
//         }   

//     }   

// }