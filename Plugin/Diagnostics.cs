using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace SituationalPractice
{
    internal static class BuildIdentity
    {
        internal const string Version="0.8.14";
        internal static string Folder {get{return FolderForAssembly(typeof(BuildIdentity).Assembly.Location);}}
        internal static string FolderForAssembly(string assemblyPath)
        {
            string directory=Path.GetDirectoryName(Path.GetFullPath(assemblyPath));
            // MMC loads the DLL from Plugins. The extracted helper already
            // lives in the data folder; do not create a nested folder for it.
            return String.Equals(Path.GetExtension(assemblyPath),".exe",StringComparison.OrdinalIgnoreCase)
                ? directory : Path.Combine(directory,"SituationalPractice");
        }
    }

    // Logging must never throw into launch, gameplay, or hook restoration.
    internal sealed class SafeFileLog
    {
        readonly object sync=new object();
        readonly string path;
        readonly long limit;
        readonly Stopwatch timer=Stopwatch.StartNew();
        readonly Dictionary<string,long> lastErrors=new Dictionary<string,long>();
        readonly Dictionary<string,int> repeats=new Dictionary<string,int>();
        internal int Failures {get;private set;}
        internal SafeFileLog(string path,long limit=1048576){this.path=path;this.limit=limit;}
        internal bool Write(string level,string message)
        {
            lock(sync)try{
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if(File.Exists(path) && new FileInfo(path).Length>=limit){
                    if(File.Exists(path+".3"))File.Delete(path+".3");
                    for(int i=2;i>=1;i--)if(File.Exists(path+"."+i))File.Move(path+"."+i,path+"."+(i+1));
                    File.Move(path,path+".1");
                }
                if(message.Length>32768)message=message.Substring(0,32768)+" [truncated]";
                byte[] bytes=new UTF8Encoding(false).GetBytes(DateTimeOffset.Now.ToString("O")+" ["+level+"] "+message+Environment.NewLine);
                using(var stream=new FileStream(path,FileMode.Append,FileAccess.Write,FileShare.ReadWrite)){
                    stream.Write(bytes,0,bytes.Length);stream.Flush(true);
                }
                return true;
            }catch(Exception error){Failures++;Debug.WriteLine("Drive-Based Practice logging: "+error.Message);return false;}
        }
        internal void Error(string operation,Exception error,long elapsed=-1)
        {
            lock(sync){
                long now=elapsed<0?timer.ElapsedMilliseconds:elapsed;
                string key=operation+"|"+error.GetType().FullName+"|"+error.Message;
                long last;int count;
                if(lastErrors.TryGetValue(key,out last) && now-last<60000){
                    repeats.TryGetValue(key,out count);repeats[key]=count+1;return;
                }
                repeats.TryGetValue(key,out count);
                if(lastErrors.Count>=256){lastErrors.Clear();repeats.Clear();}
                lastErrors[key]=now;repeats[key]=0;
                Write("ERROR",operation+(count>0?" (suppressed "+count+" repeated occurrences)":"")+Environment.NewLine+error);
            }
        }
    }

    internal sealed class DiagnosticSession
    {
        readonly object sync=new object();
        readonly string root,run;
        readonly int gamePid;
        readonly DateTime started=DateTime.UtcNow;
        readonly SafeFileLog events,aggregate;
        readonly Queue<string> recent=new Queue<string>();
        readonly Stopwatch clock=Stopwatch.StartNew();
        long nextState;
        bool completed;
        string lastState="No native practice state captured yet.";
        string lastError="None recorded.";
        internal string RunFolder {get{return run;}}
        internal string ReportZip {get{return Path.Combine(run,"report.zip");}}
        internal DiagnosticSession(string folder,int pid)
        {
            root=folder;gamePid=pid;
            string name=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-game-"+pid+"-helper-"+Process.GetCurrentProcess().Id+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
            run=Path.Combine(root,"Diagnostics",name);
            try{Directory.CreateDirectory(run);}catch{run=Path.Combine(Path.GetTempPath(),"Drive-Based-Practice-Diagnostics",name);}
            events=new SafeFileLog(Path.Combine(run,"events.log"));
            aggregate=new SafeFileLog(Path.Combine(root,"SituationalPractice.log"));
            Log("Session started; plugin="+BuildIdentity.Version+"; game PID="+pid+"; helper PID="+Process.GetCurrentProcess().Id+"; OS="+Environment.OSVersion+"; CLR="+Environment.Version+"; 64-bit="+Environment.Is64BitProcess+"; report folder="+run);
            try{AtomicText(Path.Combine(root,"latest-session.txt"),"Started UTC: "+started.ToString("O")+Environment.NewLine+"Game PID: "+pid+Environment.NewLine+"Current diagnostic folder: "+run);}catch(Exception error){Error("Write current session pointer",error);}
            try{if(File.Exists(Path.Combine(root,"launcher-context.txt")))AtomicText(Path.Combine(run,"launcher-context.txt"),File.ReadAllText(Path.Combine(root,"launcher-context.txt")));}catch(Exception error){Error("Copy launcher context",error);}
        }
        internal void Log(string message)
        {
            lock(sync){
                string tagged="game="+gamePid+" "+message;
                recent.Enqueue(DateTimeOffset.Now.ToString("O")+" "+message);
                while(recent.Count>80)recent.Dequeue();
                events.Write("INFO",tagged);aggregate.Write("INFO",tagged);
            }
        }
        internal void Error(string operation,Exception error)
        {
            lock(sync){
                lastError=operation+Environment.NewLine+error;
                events.Error(operation,error);aggregate.Error("game="+gamePid+" "+operation,error);
            }
        }
        internal void UpdateState(Func<string> capture,bool force=false)
        {
            lock(sync){
                if(!force && clock.ElapsedMilliseconds<nextState)return;
                nextState=clock.ElapsedMilliseconds+2000;
                try{
                    string state="Captured: "+DateTimeOffset.Now.ToString("O")+Environment.NewLine+capture();
                    AtomicText(Path.Combine(run,"last-state.txt"),state);lastState=state;
                }catch(Exception error){Error("Capture diagnostic state (gameplay continues)",error);}
            }
        }
        internal static void AtomicText(string path,string text)
        {
            string temporary=path+".tmp";
            try{
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                byte[] bytes=new UTF8Encoding(false).GetBytes(text);
                using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.Read)){
                    stream.Write(bytes,0,bytes.Length);stream.Flush(true);
                }
                if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
            }finally{try{if(File.Exists(temporary))File.Delete(temporary);}catch{}}
        }
        internal static string ExitStatus(int? code)
        {
            return code.HasValue?code.Value.ToString(CultureInfo.InvariantCulture)+" (0x"+unchecked((uint)code.Value).ToString("X8")+")":"unavailable";
        }
        internal void GameIdentity(Process game)
        {
            try{
                string path=game.MainModule.FileName;var file=new FileInfo(path);var version=FileVersionInfo.GetVersionInfo(path);
                Log("Game start UTC="+game.StartTime.ToUniversalTime().ToString("O")+"; EXE="+path+"; file version="+version.FileVersion+"; product version="+version.ProductVersion+"; bytes="+file.Length);
                // Diagnostic identity only; this hash is never an admission check.
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
                using(var sha=SHA256.Create())Log("EXE SHA-256 (diagnostic only)="+BitConverter.ToString(sha.ComputeHash(stream)).Replace("-",""));
            }catch(Exception error){Error("Read diagnostic game identity",error);}
        }
        internal void Complete(Process game,bool manualStop,bool helperFailed)
        {
            bool exited=false;int? code=null;
            try{exited=game!=null && game.HasExited;if(exited)code=game.ExitCode;}catch(Exception error){Error("Read game exit status",error);}
            string reason=helperFailed?"Helper failed":manualStop?"Requested helper stop":exited?"Game process exited":"Helper ended while game exit was unconfirmed";
            if(exited)Thread.Sleep(1500); // Allow Windows to publish its error event.
            CompleteForExit(reason,code,exited?ReadWindowsEvents(started):"Game exit not observed; Windows crash events were not queried.");
        }
        internal void CompleteForExit(string reason,int? exitCode,string windowsEvents)
        {
            lock(sync){
                if(completed)return;completed=true;
                try{
                    Log(reason+"; game exit status="+ExitStatus(exitCode)+"; final report="+ReportZip);
                    string report="Drive-Based Practice "+BuildIdentity.Version+" diagnostic report"+Environment.NewLine
                        +"Game PID: "+gamePid+Environment.NewLine+"Started UTC: "+started.ToString("O")+Environment.NewLine
                        +"Ended UTC: "+DateTime.UtcNow.ToString("O")+Environment.NewLine+"Reason: "+reason+Environment.NewLine
                        +"Game exit status: "+ExitStatus(exitCode)+Environment.NewLine
                        +"A nonzero exit or related Windows event is evidence to investigate; it does not identify this plugin as the cause."+Environment.NewLine
                        +"Logging failures: session="+events.Failures+", aggregate="+aggregate.Failures+Environment.NewLine
                        +Environment.NewLine+"LAST OBSERVED STATE"+Environment.NewLine+lastState
                        +Environment.NewLine+Environment.NewLine+"LAST RECORDED ERROR (may predate recovery)"+Environment.NewLine+lastError
                        +Environment.NewLine+Environment.NewLine+"RECENT BREADCRUMBS"+Environment.NewLine+String.Join(Environment.NewLine,recent.ToArray())
                        +Environment.NewLine+Environment.NewLine+"NEARBY WINDOWS EVENTS"+Environment.NewLine+windowsEvents;
                    AtomicText(Path.Combine(run,"report.txt"),report);
                    AtomicText(Path.Combine(run,"windows-events.txt"),windowsEvents);
                    Bundle();
                }catch(Exception error){Error("Write diagnostic report/bundle",error);}
            }
        }
        void Bundle()
        { BundleFiles(root,run); }
        static void BundleFiles(string root,string run)
        {
            string reportZip=Path.Combine(run,"report.zip"),temporary=reportZip+".tmp";
            using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
            using(var zip=new ZipArchive(stream,ZipArchiveMode.Create)){
                foreach(string name in new[]{"report.txt","last-state.txt","windows-events.txt","observer-exit.txt","windows-events-helper.txt","launcher-context.txt","events.log","events.log.1","events.log.2","events.log.3"})
                    AddFile(zip,Path.Combine(run,name),name);
                foreach(string name in new[]{"launcher.log","launcher.log.1","launcher.log.2","launcher.log.3"})
                    AddFile(zip,Path.Combine(root,name),name);
            }
            if(File.Exists(reportZip))File.Replace(temporary,reportZip,null);else File.Move(temporary,reportZip);
            string latest=Path.Combine(root,"latest-report.zip"),latestTemp=latest+"."+Guid.NewGuid().ToString("N")+".tmp";
            File.Copy(reportZip,latestTemp,true);
            if(File.Exists(latest))File.Replace(latestTemp,latest,null);else File.Move(latestTemp,latest);
        }
        // An MMC-side observer can finalize the durable breadcrumbs even when
        // the helper was killed before its own exception/finally handler ran.
        internal static void ObserveHelperFailure(string root,int gamePid,int helperPid,int code,bool gameAlive,string windowsEvents=null)
        {
            string diagnostics=Path.Combine(root,"Diagnostics");Directory.CreateDirectory(diagnostics);
            string[] matches=Directory.GetDirectories(diagnostics,"*-game-"+gamePid+"-helper-"+helperPid+"-*");
            Array.Sort(matches,StringComparer.Ordinal);
            string run=matches.Length>0?matches[matches.Length-1]:Path.Combine(diagnostics,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-game-"+gamePid+"-helper-"+helperPid+"-observer");
            Directory.CreateDirectory(run);
            string observed="MMC observed a helper exit."+Environment.NewLine+"Recorded: "+DateTimeOffset.Now.ToString("O")
                +Environment.NewLine+"Game PID: "+gamePid+Environment.NewLine+"Helper PID: "+helperPid
                +Environment.NewLine+"Helper exit: "+ExitStatus(code)+Environment.NewLine+"Original game still running: "+gameAlive
                +Environment.NewLine+"No game exit status was established by this helper observation."+Environment.NewLine;
            AtomicText(Path.Combine(run,"observer-exit.txt"),observed);
            AtomicText(Path.Combine(run,"windows-events-helper.txt"),windowsEvents??ReadWindowsEvents(DateTime.UtcNow.AddMinutes(-3)));
            if(!File.Exists(Path.Combine(run,"report.txt"))){
                string state=Path.Combine(run,"last-state.txt");
                AtomicText(Path.Combine(run,"report.txt"),"Drive-Based Practice "+BuildIdentity.Version+" recovered diagnostic report"+Environment.NewLine+observed
                    +Environment.NewLine+"LAST OBSERVED STATE"+Environment.NewLine+(File.Exists(state)?File.ReadAllText(state):"The helper did not persist a native state.")
                    +Environment.NewLine+"See bundled events.log and launcher.log for the preceding operations/errors.");
                string context=Path.Combine(root,"launcher-context.txt");
                if(!File.Exists(Path.Combine(run,"launcher-context.txt")) && File.Exists(context))AtomicText(Path.Combine(run,"launcher-context.txt"),File.ReadAllText(context));
            }
            BundleFiles(root,run);
        }
        static void AddFile(ZipArchive zip,string path,string name)
        {
            if(!File.Exists(path))return;
            using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
            using(var output=zip.CreateEntry(name).Open())input.CopyTo(output);
        }
        internal static bool RelatedWindowsEvent(int id,DateTime timestamp,DateTime since,string message)
        {
            return (id==1000 || id==1001 || id==1026) && timestamp>=since
                && (message.IndexOf("CollegeFB27",StringComparison.OrdinalIgnoreCase)>=0
                    || message.IndexOf("SituationalPractice.Runtime",StringComparison.OrdinalIgnoreCase)>=0);
        }
        internal static string ReadWindowsEvents(DateTime sessionStart)
        {
            try{
                var result=new StringBuilder("Matched by executable name and a three-minute time window; a match may belong to another process instance."+Environment.NewLine);
                DateTime since=DateTime.UtcNow.AddMinutes(-3);if(sessionStart>since)since=sessionStart;
                int matches=0;
                using(var log=new EventLog("Application")){
                    int count=log.Entries.Count;
                    for(int i=count-1;i>=0 && i>=count-1000 && matches<12;i--){
                        var entry=log.Entries[i];DateTime time=entry.TimeGenerated.ToUniversalTime();
                        if(time<since)break;
                        int id=(int)(entry.InstanceId & 65535);
                        if(id!=1000 && id!=1001 && id!=1026)continue;
                        string message=entry.Message;
                        if(!RelatedWindowsEvent(id,time,since,message))continue;
                        if(message.Length>16384)message=message.Substring(0,16384)+" [truncated]";
                        result.AppendLine(time.ToString("O")+" "+entry.Source+" event "+id).AppendLine(message);matches++;
                    }
                }
                if(matches==0)result.AppendLine("No matching Windows crash events available at collection time. This does not rule out a crash.");
                return result.ToString();
            }catch(Exception error){return "Windows crash events could not be read: "+error;}
        }
    }
}

