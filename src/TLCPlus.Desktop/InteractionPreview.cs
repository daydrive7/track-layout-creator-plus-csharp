using System.Diagnostics;
using TLCPlus;
namespace TLCPlus.Desktop;
public partial class MainForm
{
    readonly System.Windows.Forms.Timer interactionTimer=new(){Interval=16};
    bool interactionPending,interactionTimerReady;
    internal int FullPreviewBuilds {get;private set;}
    internal int InteractivePreviewBuilds {get;private set;}
    internal double LastInteractivePreviewMs {get;private set;}
    void ScheduleInteractivePreview()
    {
        interactionPending=true;
        if(!interactionTimerReady){interactionTimer.Tick+=(_,_)=>{if(!gesture){interactionTimer.Stop();return;}if(interactionPending)BuildInteractivePreview();};Disposed+=(_,_)=>interactionTimer.Dispose();interactionTimerReady=true;}
        if(!interactionTimer.Enabled){BuildInteractivePreview();interactionTimer.Start();}
    }
    internal void BuildInteractivePreview(bool invalidate=true)
    {
        interactionPending=false;var timer=Stopwatch.StartNew();
        try{
            if(Ted!=null){Canvas.Course=Ted.Course;return;}
            if(Canvas.Tool=="decorate"){Canvas.Course=Preview;return;}
            if(Project.Nodes.Count<(Project.Settings.Circuit?3:2)){Canvas.Course=null;return;}
            Canvas.Course=Exporter.BuildPlanPreview(Project);
            RefreshCanvasStats(Canvas.Course);
            InteractivePreviewBuilds++;
        }catch{Canvas.Course=BaseRoadPreview(true);}
        finally{if(invalidate)Canvas.Invalidate();LastInteractivePreviewMs=timer.Elapsed.TotalMilliseconds;}
    }
    void EndInteractivePreview(){interactionPending=false;interactionTimer.Stop();}
}
