using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using TLCPlus;
namespace TLCPlus.Desktop;
static class OverallBenchmark
{
    static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    static object Measure(Action action,int batch=1)
    {
        for(int i=0;i<2;i++)action();var samples=new List<double>();
        for(int i=0;i<9;i++){var timer=Stopwatch.StartNew();for(int j=0;j<batch;j++)action();samples.Add(timer.Elapsed.TotalMilliseconds/batch);}
        samples.Sort();return new{medianMs=samples[4],p95Ms=samples[8],samplesMs=samples};
    }
    public static int Run(string root,Stopwatch startup)
    {
        try{
            string dir=Path.Combine(root,"work/benchmark");Directory.CreateDirectory(dir);
            using var form=new MainForm();form.Width=1400;form.Height=850;form.Show();Application.DoEvents();
            double ready=startup.Elapsed.TotalMilliseconds;var cases=new List<object>();
            foreach(var (name,count,decos) in new[]{("small",12,0),("large",120,0),("decorated",12,1000)}){
                var project=new Project{Settings=new(){Scenery=2}};
                for(int i=0;i<count;i++){double a=i*Math.PI*2/count;project.Nodes.Add(new(){X=Math.Cos(a)*2100,Y=Math.Sin(a)*1600,Radius=45,Segments=3});}
                project=TrkFormat.Read(TrkFormat.Write(project));
                var variant=Catalog.Variants(2).First(v=>v.GetProperty("type").GetInt32()==1);double length=Exporter.Build(project).PlanLength;
                for(int i=0;i<decos;i++)project.Decorations.Add(new(){Type=1,Uuid=variant.GetProperty("uuid").GetUInt64(),Vpos=(i+.5)*length/Math.Max(1,decos),Side=i%2==0?"left":"right"});
                string path=Path.Combine(dir,name+".trk5");DocumentFiles.SaveLayout(path,project);project.Save(Path.Combine(dir,name+".json"));form.OpenFile(path);form.Canvas.Fit();Application.DoEvents();
                form.Canvas.ShowGrid=false;form.Canvas.ShowTerrain=true;form.Canvas.ShowCorners=false;form.Canvas.ShowRoadWidth=true;
                double initialZoom=form.Canvas.Zoom;
                void Paint(){using var image=form.Canvas.Render();}
                void Full(){form.Bind();form.Refresh();}
                byte[] bytes=File.ReadAllBytes(path);
                var metrics=new Dictionary<string,object>{
                    ["geometry"]=Measure(()=>Geometry.ControlPoints(form.Project),20),
                    ["fullPreview"]=Measure(()=>Exporter.Build(form.Project)),
                    ["canvasRedraw"]=Measure(Paint),
                    ["zoomRedraw"]=Measure(()=>{form.Canvas.Zoom*=1.01;Paint();}),
                    ["fullEditRefresh"]=Measure(()=>{form.Project.Nodes[0].X+=.25;Full();}),
                    ["loadAndRefresh"]=Measure(()=>{form.OpenFile(path);form.Refresh();}),
                    ["trk5Save"]=Measure(()=>File.WriteAllBytes(Path.Combine(dir,"csharp-save.trk5"),TrkFormat.Write(form.Project)),20),
                    ["tedExport"]=Measure(()=>File.WriteAllBytes(Path.Combine(dir,"csharp-export.ted"),Exporter.Write(form.Project,timestamp:0xE245A123)))
                };
                // Export again from the unmodified input for cross-language byte checks.
                form.OpenFile(path);File.WriteAllBytes(Path.Combine(dir,name+"-csharp.ted"),Exporter.Write(form.Project,timestamp:0xE245A123));
                cases.Add(new{name,nodes=count,decorations=decos,canvasWidth=form.Canvas.Width,canvasHeight=form.Canvas.Height,zoom=initialZoom,metrics});
            }
            var generation=Measure(()=>{form.Generate("oval");using var image=form.Canvas.Render();});
            using var process=Process.GetCurrentProcess();process.Refresh();
            File.WriteAllText(Path.Combine(root,"analysis/benchmark-csharp.json"),JsonSerializer.Serialize(new{version="C# "+Application.ProductVersion,startupToReadyMs=ready,startupScope="in-process initialization to first layout/paint; excludes process launch",workingSetBytes=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,generatorOval=generation,cases},Json));return 0;
        }catch(Exception e){File.WriteAllText(Path.Combine(root,"analysis/benchmark-error.txt"),e.ToString());return 1;}
    }
}
