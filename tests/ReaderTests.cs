using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using InzoneH9Widget;
class ReaderTests
{
    static List<string> passed = new List<string>();
    static byte[] Frame(int percent, int charging)
    {
        byte[] bytes = { 4,255,11,0,0x96,0xC3,0x14,4,0x10,1,0,(byte)charging,(byte)percent,0 };
        int sum = 0; for (int i=3; i<13; i++) sum += bytes[i]; bytes[13]=(byte)sum; return bytes;
    }
    static void Check(string name, bool condition) { if (!condition) throw new Exception(name); passed.Add(name); }
    static int Main(string[] args)
    {
        Check("zero_is_valid_not_unknown", Reader.Parse(Frame(0,0),"test").battery == 0);
        Check("hundred_is_valid", Reader.Parse(Frame(100,0),"test").battery == 100);
        Check("charging_one", Reader.Parse(Frame(50,1),"test").charging == true);
        Check("unknown_charge_status", Reader.Parse(Frame(50,255),"test").charging == null);
        Check("ff_is_not_reporting", Reader.Parse(Frame(255,0),"test").status == "not_reporting");
        Check("over_hundred_rejected", Reader.Parse(Frame(101,0),"test").battery == null);
        var bad = Frame(80,0); bad[13] ^= 1;
        Check("checksum_mismatch_rejected", Reader.Parse(bad,"test").battery == null);
        Check("truncated_frame_rejected", Reader.Parse(Frame(80,0).Take(13).ToArray(),"test").battery == null);
        Check("empty_response_rejected", Reader.Parse(new byte[0],"test").status == "no_response");
        Check("noise_resynchronization", Reader.Parse(new byte[]{0,1,2,4,255}.Concat(Frame(73,0)).ToArray(),"test").battery == 73);
        Check("latest_frame_wins", Reader.Parse(Frame(80,0).Concat(Frame(70,0)).ToArray(),"test").battery == 70);
        Check("latest_unknown_clears_value", Reader.Parse(Frame(80,0).Concat(Frame(255,0)).ToArray(),"test").battery == null);
        bad = Frame(80,0); bad[7]=0x21;
        Check("other_event_rejected", Reader.Parse(bad,"test").battery == null);
        bad = Frame(80,0); bad[6]=0x12;
        Check("other_source_rejected", Reader.Parse(bad,"test").battery == null);
        Check("live_packet_from_receiver", Reader.Parse(new byte[]{4,255,11,0,150,195,20,4,16,1,0,0,100,230},"COM7").battery == 100);
        File.WriteAllText(args[0], new JavaScriptSerializer().Serialize(new { passed = passed.Count, tests = passed, note = "Synthetic packets used only for parser regression; final case uses the observed hardware packet." }));
        Console.WriteLine(passed.Count + " parser tests passed"); return 0;
    }
}
