using TLCPlus;
static class RoadTests
{
    public static void Run(string root)
    {
        byte[] original=File.ReadAllBytes(Path.Combine(root,"tests/fixtures/3_1_normal.ted"));var doc=TedDocument.Read(original);ulong normal=Catalog.Roads(3).GetProperty("normal")[0][0].GetUInt64();
        int index=doc.Course.Roads.FindIndex(r=>r.Flag==0&&r.Uuid==normal);doc.SetRestaurantRoad(index,true);byte[] edited=doc.Write();int offset=(int)Binary.U32(original,132)+index*20;
        for(int i=0;i<original.Length;i++)if((i<offset||i>=offset+8)&&original[i]!=edited[i])throw new Exception("Road edit altered unrelated bytes");
        if(TedDocument.Read(edited).Course.Roads[index].Uuid!=0xB4CB0024E8474511UL)throw new Exception("Restaurant UUID differs");
        doc.SetRestaurantRoad(index,false);if(!doc.Write().AsSpan().SequenceEqual(original))throw new Exception("Normal-road restore differs");
        byte[] sideOne=(byte[])original.Clone();Binary.W32(sideOne,offset+8,1);var sideDoc=TedDocument.Read(sideOne);sideDoc.SetRestaurantRoad(index,true);if(sideDoc.Course.Roads[index].Flag!=1)throw new Exception("UUID edit changed the existing road side flag");sideDoc.SetRestaurantRoad(index,false);if(!sideDoc.Write().AsSpan().SequenceEqual(sideOne))throw new Exception("Side-one road did not restore exactly");
        int protectedIndex=doc.Course.Roads.FindIndex(r=>r.Flag==2);byte[] resizedStart=(byte[])original.Clone();Binary.W32(resizedStart,(int)Binary.U32(resizedStart,132)+protectedIndex*20+8,0);var protectedDoc=TedDocument.Read(resizedStart);bool protectedRejected=false;try{protectedDoc.SetRestaurantRoad(protectedIndex,true);}catch(InvalidOperationException){protectedRejected=true;}if(!protectedRejected)throw new Exception("Start/finish UUID with flag0 was editable");
        bool rejected=false;try{doc.SetRestaurantRoad(doc.Course.Roads.FindIndex(r=>r.Flag!=0),true);}catch(InvalidOperationException){rejected=true;}if(!rejected)throw new Exception("Protected road was editable");
        var other=TedDocument.Read(File.ReadAllBytes(Path.Combine(root,"tests/fixtures/2_1_normal.ted")));rejected=false;try{other.SetRestaurantRoad(index,true);}catch(InvalidOperationException){rejected=true;}if(!rejected)throw new Exception("Restaurant allowed in unverified scenery");
        string reference=Path.Combine(root,"work/restaurant-reference.ted");if(File.Exists(reference)){
            byte[] bytes=File.ReadAllBytes(reference);var sample=TedDocument.Read(bytes);var ids=sample.Course.Roads.Select((r,i)=>(r,i)).Where(x=>x.r.Uuid==0xB4CB0024E8474511UL).Select(x=>x.i).ToArray();if(ids.Length!=2)throw new Exception("Reference does not contain two restaurants");foreach(int id in ids)sample.SetRestaurantRoad(id,false);foreach(int id in ids)sample.SetRestaurantRoad(id,true);if(!bytes.AsSpan().SequenceEqual(sample.Write()))throw new Exception("Reference restoration differs");
        }
        Console.WriteLine("PASS restaurant road UUID edits preserve every unrelated byte, restore exactly and reject protected/unverified sections");
    }
}
