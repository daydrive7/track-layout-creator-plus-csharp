using System.Diagnostics;
using System.Reflection;
namespace TLCPlus.Desktop;

static class ErrorNoticeChecks
{
    internal static void Run(MainForm form,string root,List<string> checks)
    {
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        var preferences=(AppPreferences)typeof(MainForm).GetField("uiPreferences",flags)!.GetValue(form)!;
        double original=preferences.ErrorNoticeSeconds;
        string? saved=File.Exists(AppPreferences.FilePath)?File.ReadAllText(AppPreferences.FilePath):null;
        try{
            File.WriteAllText(AppPreferences.FilePath,"{}");
            if(AppPreferences.Load().ErrorNoticeSeconds!=3.5)throw new Exception("Older preferences do not default to 3.5 seconds");
            foreach(var (value,expected) in new[]{(-2.0,.5),(9.0,5.0),(2.5,2.5)}){
                new AppPreferences{ErrorNoticeSeconds=value}.Save();
                if(AppPreferences.Load().ErrorNoticeSeconds!=expected)throw new Exception("Error notice duration did not clamp or persist");
            }
            if(AppPreferences.NoticeDuration(double.NaN)!=3.5||AppPreferences.NoticeDuration(double.PositiveInfinity)!=3.5)throw new Exception("Non-finite notice duration was accepted");
            preferences.ErrorNoticeSeconds=3.5;form.Canvas.Focus();bool focus=form.Canvas.ContainsFocus;
            LocalizedMessageBox.Show(form,"Place road types outside the start/finish and other protected road sections.","TLC+",MessageBoxButtons.OK,MessageBoxIcon.Error);
            Application.DoEvents();var notice=form.ErrorNotice??throw new Exception("Error did not reach the canvas notice");
            if(!notice.Visible||notice.Duration!=3.5||notice.Owner!=form||focus&&!form.Canvas.ContainsFocus)throw new Exception("Notice is missing, modal or stole canvas focus");
            var canvas=form.Canvas.RectangleToScreen(form.Canvas.ClientRectangle);
            if(!canvas.Contains(notice.Bounds))throw new Exception("Notice extends outside the canvas");
            int oldWidth=form.Width;form.Width-=80;Application.DoEvents();
            if(!form.Canvas.RectangleToScreen(form.Canvas.ClientRectangle).Contains(notice.Bounds))throw new Exception("Notice did not follow the resized canvas");
            form.Width=oldWidth;Application.DoEvents();
            notice.Present("Create or load a track first.",UiLanguage.T("Error"),3.5);
            using(var frame=new Bitmap(form.Width,form.Height)){
                form.DrawToBitmap(frame,new(0,0,frame.Width,frame.Height));
                using var overlay=new Bitmap(notice.Width,notice.Height);notice.DrawToBitmap(overlay,new(0,0,overlay.Width,overlay.Height));
                using var graphics=Graphics.FromImage(frame);graphics.DrawImageUnscaled(overlay,form.PointToClient(notice.Location));
                frame.Save(Path.Combine(root,"analysis/error-notice-preview.png"));
            }
            if(CanvasErrorNotice.FadeOpacity(0,3.5)!=1||CanvasErrorNotice.FadeOpacity(3.5,3.5)!=0||CanvasErrorNotice.FadeOpacity(3,3.5)>=CanvasErrorNotice.FadeOpacity(2,3.5))throw new Exception("Notice fade does not reach zero at its configured duration");
            notice.Advance(2.5);if(notice.Opacity<=0||notice.Opacity>=1)throw new Exception("Notice did not fade gradually");
            LocalizedMessageBox.Show("Replacement error","TLC+",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            if(form.ErrorNotice!=notice||notice.Message!="Replacement error"||notice.Opacity!=1||Application.OpenForms.OfType<CanvasErrorNotice>().Count()!=1)throw new Exception("Repeated errors stack windows or do not restart the fade");
            notice.Advance(3.5);if(notice.Visible||notice.Animating)throw new Exception("Expired notice remains visible or keeps animating");
            preferences.ErrorNoticeSeconds=.5;
            form.ShowErrorNotice("Timer check");var watch=Stopwatch.StartNew();
            while(notice.Visible&&watch.ElapsedMilliseconds<1200){Application.DoEvents();Thread.Sleep(5);}
            if(notice.Visible||watch.ElapsedMilliseconds<450||watch.ElapsedMilliseconds>1100)throw new Exception("Real notice timer did not dismiss a 0.5-second notice");
            using(var timer=new System.Windows.Forms.Timer{Interval=50}){
                bool edited=false;
                timer.Tick+=(_,_)=>{
                    var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f!=form&&f.Text==UiLanguage.T("Preferences"));if(dialog==null)return;
                    timer.Stop();
                    IEnumerable<Control> All(Control parent){foreach(Control child in parent.Controls){yield return child;foreach(var nested in All(child))yield return nested;}}
                    if(All(dialog).Any(c=>UiLanguage.OriginalText(c) is "CUSTOM COLORS" or "Canvas color" or "Panel color" or "Reset custom colors")||!All(dialog).Any(c=>UiLanguage.OriginalText(c)=="Custom…"))throw new Exception("Preferences removed accent customization or retained panel/canvas colors");
                    Control? Find(Control parent){foreach(Control child in parent.Controls){if(child is NumericUpDown)return child;var found=Find(child);if(found!=null)return found;}return null;}
                    var duration=Find(dialog) as NumericUpDown??throw new Exception("Duration preference is missing");
                    if(duration.Minimum!=.5m||duration.Maximum!=5m||duration.DecimalPlaces!=1)throw new Exception("Notice preference exposes the wrong range");
                    duration.Value=5;edited=AppPreferences.Load().ErrorNoticeSeconds==5;dialog.Close();
                };
                timer.Start();typeof(MainForm).GetMethod("Preferences",flags)!.Invoke(form,null);
                if(!edited)throw new Exception("Notice duration preference did not save immediately");
            }
            checks.Add("Nonmodal canvas error notices retain focus, follow resize, replace repeated errors, fade completely at the configured time and stop their timer");
            checks.Add("Error notice preference defaults to 3.5 seconds, clamps to 0.5–5 seconds and persists through the Preferences control");
        }finally{
            form.ErrorNotice?.Advance(5);preferences.ErrorNoticeSeconds=original;
            if(saved==null)File.Delete(AppPreferences.FilePath);else File.WriteAllText(AppPreferences.FilePath,saved);
        }
    }
}
