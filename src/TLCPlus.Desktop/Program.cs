using TLCPlus;
namespace TLCPlus.Desktop;
static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        var startup=System.Diagnostics.Stopwatch.StartNew();
        ApplicationConfiguration.Initialize();
        if(args.Length>1&&args[0]=="--benchmark")return OverallBenchmark.Run(args[1],startup);
        if(args.Length>0&&args[0]=="--smoke-test"){
            string root=args.Length>1?args[1]:AppContext.BaseDirectory;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException+=(_,e)=>{Directory.CreateDirectory(Path.Combine(root,"analysis"));File.WriteAllText(Path.Combine(root,"analysis/desktop-smoke-results.json"),System.Text.Json.JsonSerializer.Serialize(new{passed=false,error=e.Exception.ToString()}));Environment.Exit(1);};
            return Smoke.Run(root);
        }
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException+=(_,e)=>MessageBox.Show(e.Exception.Message,"TLC+",MessageBoxButtons.OK,MessageBoxIcon.Error);
        Application.Run(new MainForm(args.FirstOrDefault()));return 0;
    }
}
