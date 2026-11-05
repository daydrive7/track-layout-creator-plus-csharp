using TLCPlus;
using System.Text.Json;
using System.Security.Cryptography;
string root=args.Length>0?Path.GetFullPath(args[0]):@"D:\TLCPlus-CSharp";
Directory.CreateDirectory(Path.Combine(root,"analysis"));
Terrain.AssetsPath=Path.Combine(root,"src/TLCPlus.Core/Assets");
string fixtures=Path.Combine(root,"tests/fixtures");
int pass=0,failed=0,roundtrips=0;var results=new List<object>();
// The date occupies an unsigned 32-bit word; dates from 2002 set its sign bit.
if((Exporter.PackedDate(new DateTime(2026,11,4,21,30,15))>>26)!=56)throw new Exception("Current-year timestamp packing failed");
foreach(string path in Directory.GetFiles(fixtures,"*.trk5").Order()){
    string name=Path.GetFileNameWithoutExtension(path);
    try{
        byte[] raw=File.ReadAllBytes(path);var p=TrkFormat.Read(raw);byte[] saved=TrkFormat.Write(p);
        if(!raw.AsSpan().SequenceEqual(saved))throw new Exception("TRK5 roundtrip differs");roundtrips++;
        using var extra=JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(path,"json")));
        foreach(var kv in extra.RootElement.GetProperty("settings").EnumerateObject()){
            var s=p.Settings;switch(kv.Name){case "manualHeight":s.ManualHeight=kv.Value.GetBoolean();break;case "manualHeightIn":s.ManualHeightIn=kv.Value.GetDouble();break;case "manualHeightOut":s.ManualHeightOut=kv.Value.GetDouble();break;case "manualHeightEase":s.ManualHeightEase=kv.Value.GetBoolean();break;case "smoothMix":s.SmoothMix=kv.Value.GetDouble();break;}
        }
        foreach(var d in extra.RootElement.GetProperty("decorations").EnumerateArray())p.Decorations.Add(new(){Uuid=d.GetProperty("uuid").GetUInt64(),Type=d.GetProperty("type").GetInt32(),Side=d.GetProperty("side").GetString()!,Track=d.GetProperty("track").GetString()!,Vpos=d.GetProperty("vpos").GetDouble()});
        byte[] actual=Exporter.Write(p,timestamp:0xE245A123),expected=File.ReadAllBytes(Path.ChangeExtension(path,"ted"));
        var parsed=TedDocument.Read(expected);if(!expected.AsSpan().SequenceEqual(parsed.Write()))throw new Exception("TED roundtrip differs");roundtrips++;
        bool equal=actual.AsSpan().SequenceEqual(expected);
        if(equal){pass++;Console.WriteLine("PASS "+name);}else{failed++;Directory.CreateDirectory(Path.Combine(root,"work/actual"));File.WriteAllBytes(Path.Combine(root,"work/actual",name+".ted"),actual);var diffs=Enumerable.Range(0,Math.Min(actual.Length,expected.Length)).Where(i=>actual[i]!=expected[i]).Take(12).ToArray();Console.WriteLine($"FAIL {name}: sizes {actual.Length}/{expected.Length}, bytes {string.Join(',',diffs)}");}
        results.Add(new{name,identical=equal,trk5Sha256=Convert.ToHexStringLower(SHA256.HashData(saved)),tedSha256=Convert.ToHexStringLower(SHA256.HashData(actual))});
    }catch(Exception e){failed++;Console.WriteLine($"FAIL {name}: {e}");}
}
RoadTests.Run(root);
RoadPlacementTests.Run(root);
// Real user files exercise preservation of older/unknown TED table bytes.
string corpus=Path.Combine(root,"tests/corpus");if(Directory.Exists(corpus))foreach(string path in Directory.GetFiles(corpus)){try{byte[] raw=File.ReadAllBytes(path);byte[] output=Path.GetExtension(path).Equals(".ted",StringComparison.OrdinalIgnoreCase)?TedDocument.Read(raw).Write():TrkFormat.Write(TrkFormat.Read(raw));if(!raw.AsSpan().SequenceEqual(output))throw new Exception("Preservation differs");roundtrips++;}catch(Exception e){failed++;Console.WriteLine($"CORPUS FAIL {Path.GetFileName(path)}: {e.Message}");}}
// Rejected malformed inputs must fail before allocating or exporting.
foreach(var bad in new[]{new byte[0],new byte[155],"BAD!"u8.ToArray()}){try{TedDocument.Read(bad);failed++;}catch(InvalidDataException){}}
var report=new{referenceVersion="1.3.0",passed=pass,failed,roundtrips,timestamp="E245A123",results};
File.WriteAllText(Path.Combine(root,"analysis/compatibility-results.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"{pass} byte-identical TED exports; {roundtrips} exact roundtrips; {failed} failures");return failed==0?0:1;
