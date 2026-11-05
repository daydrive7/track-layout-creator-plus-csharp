using System.Security.Cryptography;
using System.Text.Json;
namespace TLCPlus;

public static class DocumentFiles
{
    sealed class Metadata
    {
        public Metadata(){}
        public int Version {get;set;}=1;
        public string TrkSha256 {get;set;}="";
        public bool ManualHeight {get;set;}
        public double ManualHeightIn {get;set;}=1;
        public double ManualHeightOut {get;set;}=1;
        public bool ManualHeightEase {get;set;}=true;
        public double SmoothMix {get;set;}=1;
        public List<Decoration> Decorations {get;set;}=[];
    }
    public static void SaveLayout(string path,Project project)
    {
        byte[] bytes=TrkFormat.Write(project);File.WriteAllBytes(path,bytes);
        var s=project.Settings;var metadata=new Metadata{TrkSha256=Convert.ToHexString(SHA256.HashData(bytes)),ManualHeight=s.ManualHeight,ManualHeightIn=s.ManualHeightIn,ManualHeightOut=s.ManualHeightOut,ManualHeightEase=s.ManualHeightEase,SmoothMix=s.SmoothMix,Decorations=project.Decorations};
        string extra=path+".tlcplus.json";
        if(project.Decorations.Count>0||s.ManualHeight||s.ManualHeightIn!=1||s.ManualHeightOut!=1||!s.ManualHeightEase||s.SmoothMix!=1||File.Exists(extra))File.WriteAllText(extra,JsonSerializer.Serialize(metadata,new JsonSerializerOptions{WriteIndented=true}));
    }
    public static Project LoadLayout(string path)
    {
        byte[] bytes=File.ReadAllBytes(path);var project=TrkFormat.Read(bytes);string extra=path+".tlcplus.json";
        if(!File.Exists(extra))return project;
        try{
            var metadata=JsonSerializer.Deserialize<Metadata>(File.ReadAllText(extra));
            if(metadata==null||metadata.Version!=1||metadata.TrkSha256!=Convert.ToHexString(SHA256.HashData(bytes)))return project;
            var s=project.Settings;s.ManualHeight=metadata.ManualHeight;s.ManualHeightIn=metadata.ManualHeightIn;s.ManualHeightOut=metadata.ManualHeightOut;s.ManualHeightEase=metadata.ManualHeightEase;s.SmoothMix=metadata.SmoothMix;project.Decorations=metadata.Decorations;
        }catch(JsonException){}
        return project;
    }
}
