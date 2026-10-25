using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Frosty.Core;
using FrostySdk.Interfaces;

namespace SituationalPractice
{
    public sealed class PracticeAction : ExecutionAction
    {
        private static int watching;
        public override Action<ILogger,PluginManagerType,CancellationToken> PreLaunchAction {get{return (log,type,token)=>Start(log);}}
        public override Action<ILogger,PluginManagerType,CancellationToken> PostLaunchAction {get{return (log,type,token)=>Start(log);}}
        private static void Start(ILogger log)
        {
            if(Interlocked.CompareExchange(ref watching,1,0)!=0)return;
            var diagnostics=new SafeFileLog(Path.Combine(BuildIdentity.Folder,"launcher.log"));
            new Thread(()=>{try{
                diagnostics.Write("INFO","Launch watcher started; plugin="+BuildIdentity.Version+"; MMC PID="+Process.GetCurrentProcess().Id);
                var launched=new HashSet<string>();
                while(true){
                    try{foreach(var p in Process.GetProcessesByName("CollegeFB27"))using(p){try{
                        if(p.HasExited)continue;
                        string key=p.Id+":"+p.StartTime.ToUniversalTime().Ticks;
                        if(launched.Contains(key))continue;
                        string folder=BuildIdentity.Folder;Directory.CreateDirectory(folder);
                        WriteLaunchContext(folder,p,diagnostics);
                        string helper=Path.Combine(folder,"SituationalPractice.Runtime.exe");
                        ExtractHelper(helper);
                        try{ExtractResource("SituationalPractice.Runtime.pdb",Path.ChangeExtension(helper,"pdb"));}
                        catch(Exception error){diagnostics.Error("Extract diagnostic symbols (helper launch continues)",error);}
                        int gamePid=p.Id;long gameStart=p.StartTime.ToUniversalTime().Ticks;
                        var child=Process.Start(new ProcessStartInfo(helper,gamePid.ToString()){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=folder});
                        if(child==null)throw new InvalidOperationException("Helper launch returned no process.");
                        diagnostics.Write("INFO","Helper started: game="+key+" helper PID="+child.Id+" path="+helper);
                        launched.Add(key);
                        child.Exited+=(sender,eventArgs)=>ObserveHelperExit(child,gamePid,gameStart,diagnostics);
                        child.EnableRaisingEvents=true;
                        if(log!=null)try{log.Log("[Drive-Based Practice] Ready. Enter offline Dynasty practice and run the first initialization play.",new object[0]);}catch(Exception error){diagnostics.Error("MMC readiness message",error);}
                    }catch(Exception error){
                        diagnostics.Error("Start practice helper for PID "+p.Id,error);
                        if(log!=null)try{log.Log("[Drive-Based Practice] Could not start the helper in "+BuildIdentity.Folder+": "+error.Message,new object[0]);}catch{}
                    }}}
                    catch(Exception error){diagnostics.Error("Enumerate game processes",error);}
                    Thread.Sleep(1000);
                }
            }catch(Exception error){diagnostics.Error("Launch watcher failed",error);}
            finally{Interlocked.Exchange(ref watching,0);}}){IsBackground=true,Name="Drive-Based Practice launch watcher"}.Start();
        }
        private static void ExtractHelper(string path)
        { ExtractResource("SituationalPractice.Runtime.exe",path); }
        private static void ExtractResource(string name,string path)
        {
            byte[] payload;
            using(var source=typeof(PracticeAction).Assembly.GetManifestResourceStream(name))
            using(var output=new MemoryStream()){
                if(source==null)throw new InvalidOperationException("Embedded practice resource is missing: "+name);
                source.CopyTo(output);payload=output.ToArray();
            }
            using(var sha=SHA256.Create()){
                string expected=Convert.ToBase64String(sha.ComputeHash(payload));
                if(File.Exists(path))using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
                    if(Convert.ToBase64String(sha.ComputeHash(input))==expected)return;
                File.WriteAllBytes(path,payload);
                using(var input=File.OpenRead(path))
                    if(Convert.ToBase64String(sha.ComputeHash(input))!=expected)throw new InvalidOperationException("Extracted resource does not match the embedded build: "+name);
            }
        }
        private static void ObserveHelperExit(Process child,int gamePid,long gameStart,SafeFileLog diagnostics)
        {
            try{
                bool gameAlive=false;
                try{using(var game=Process.GetProcessById(gamePid))gameAlive=!game.HasExited && game.StartTime.ToUniversalTime().Ticks==gameStart;}catch(ArgumentException){}
                diagnostics.Write(child.ExitCode==0?"INFO":"ERROR","Helper exited: helper PID="+child.Id+" game PID="+gamePid+" helper exit="+DiagnosticSession.ExitStatus(child.ExitCode)+" original game still running="+gameAlive+". Check this session's diagnostic report. An exited helper is not automatically restarted over existing native hooks.");
                if(child.ExitCode!=0 || gameAlive && !File.Exists(Path.Combine(BuildIdentity.Folder,"runtime.stop"))){
                    Thread.Sleep(1500);
                    DiagnosticSession.ObserveHelperFailure(BuildIdentity.Folder,gamePid,child.Id,child.ExitCode,gameAlive);
                }
            }catch(Exception error){diagnostics.Error("Observe helper exit",error);}
            finally{child.Dispose();}
        }
        private static void WriteLaunchContext(string folder,Process game,SafeFileLog diagnostics)
        {
            try{
                var text=new StringBuilder("Drive-Based Practice "+BuildIdentity.Version+Environment.NewLine);
                text.AppendLine("Captured: "+DateTimeOffset.Now.ToString("O"));
                text.AppendLine("Game PID/start UTC: "+game.Id+" / "+game.StartTime.ToUniversalTime().ToString("O"));
                using(var mmc=Process.GetCurrentProcess())text.AppendLine("MMC: "+mmc.MainModule.FileName+" / "+FileVersionInfo.GetVersionInfo(mmc.MainModule.FileName).FileVersion);
                text.AppendLine("DLL files in MMC's plugin directory (presence does not establish that a plugin is loaded):");
                string directory=Path.GetDirectoryName(typeof(PracticeAction).Assembly.Location);
                string[] files=Directory.GetFiles(directory,"*.dll");Array.Sort(files,StringComparer.OrdinalIgnoreCase);
                foreach(string path in files)try{
                    text.AppendLine(Path.GetFileName(path)+" | version="+FileVersionInfo.GetVersionInfo(path).FileVersion+" | bytes="+new FileInfo(path).Length);
                }catch(Exception error){text.AppendLine(Path.GetFileName(path)+" | metadata unavailable: "+error.Message);}
                DiagnosticSession.AtomicText(Path.Combine(folder,"launcher-context.txt"),text.ToString());
            }catch(Exception error){diagnostics.Error("Record MMC/plugin context",error);}
        }
    }
}

