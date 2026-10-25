using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using SituationalPractice;

class DiagnosticsChecks
{
    static int checks;
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static string Entry(ZipArchive zip,string name)
    {
        var entry=zip.GetEntry(name);Check(entry!=null,"Missing bundled "+name);
        using(var reader=new StreamReader(entry.Open()))return reader.ReadToEnd();
    }
    static Exception Failure()
    {
        try{throw new InvalidOperationException("outer diagnostic failure",new IOException("inner IO failure"));}
        catch(Exception error){return error;}
    }
    static void Main(string[] args)
    {
        if(args.Length>0 && args[0]=="--child"){Environment.Exit(unchecked((int)0xc0000005));return;}
        string root=Path.GetFullPath(Path.Combine("Evidence","DiagnosticsChecks-"+Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        string pluginDirectory=Path.Combine(root,"MMC folder with spaces","Plugins");
        string portableFolder=Path.Combine(pluginDirectory,"SituationalPractice");
        Check(BuildIdentity.FolderForAssembly(Path.Combine(pluginDirectory,"SituationalPractice.dll"))==portableFolder,"Plugin did not resolve its sibling data folder");
        Check(BuildIdentity.FolderForAssembly(Path.Combine(portableFolder,"SituationalPractice.Runtime.exe"))==portableFolder,"Runtime created a nested data folder");
        Check(BuildIdentity.FolderForAssembly(Path.Combine(pluginDirectory,"renamed-plugin.DLL"))==portableFolder,"Data location depends on the plugin file name");
        string relocated=Path.Combine(root,"Moved MMC","Plugins");
        Check(BuildIdentity.FolderForAssembly(Path.Combine(relocated,"SituationalPractice.dll"))==Path.Combine(relocated,"SituationalPractice"),"Relocating MMC did not relocate runtime storage");
        string ratePath=Path.Combine(root,"rate.log");
        var rate=new SafeFileLog(ratePath);
        Exception failure=Failure();
        rate.Error("test operation",failure,0);
        rate.Error("test operation",failure,1);
        string first=File.ReadAllText(ratePath);
        Check(first.Contains("inner IO failure") && first.Contains("at DiagnosticsChecks.Failure"),"Full exception/inner/stack lost");
        Check(first.IndexOf("test operation")==first.LastIndexOf("test operation"),"Duplicate errors flooded log");
        rate.Error("test operation",failure,60000);
        Check(File.ReadAllText(ratePath).Contains("suppressed 1 repeated occurrences"),"Repeated error count lost");
        string rotatePath=Path.Combine(root,"rotate.log");var rotate=new SafeFileLog(rotatePath,128);
        for(int i=0;i<12;i++)Check(rotate.Write("INFO",new string('x',160)),"Rotation write failed");
        Check(File.Exists(rotatePath+".3") && !File.Exists(rotatePath+".4"),"Rotation exceeded retention bound");
        string blocked=Path.Combine(root,"blocked");File.WriteAllText(blocked,"file, not a directory");
        var bad=new SafeFileLog(Path.Combine(blocked,"events.log"));
        Check(!bad.Write("ERROR","cannot write") && bad.Failures==1,"Disk error escaped the safe logger");
        var session=new DiagnosticSession(root,12345);
        session.Log("user picked offensive play");
        session.UpdateState(()=>"phase=5 down=3 ball=47 service_flags=0x19 operation=prepare CPU defense",true);
        string statePath=Path.Combine(session.RunFolder,"last-state.txt");
        string state=File.ReadAllText(statePath);
        Check(state.Contains("phase=5") && state.Contains("service_flags=0x19"),"Native state missing");
        int captures=0;session.UpdateState(()=>{captures++;return "unexpected";});
        Check(captures==0,"State cadence was not bounded");
        session.UpdateState(()=>{throw new IOException("sample failure");},true);
        Check(File.ReadAllText(statePath)==state,"Failed sample replaced last valid state");
        session.Error("recording result",failure);
        session.CompleteForExit("Game process exited",unchecked((int)0xc0000005),"Faulting module: test-module.dll; offset 0x123");
        Check(File.Exists(session.ReportZip) && File.Exists(Path.Combine(root,"latest-report.zip")),"Report ZIP not generated");
        using(var stream=File.OpenRead(session.ReportZip))
        using(var zip=new ZipArchive(stream,ZipArchiveMode.Read)){
            string report=Entry(zip,"report.txt");
            Check(report.Contains("0xC0000005"),"Signed exit code formatted incorrectly");
            Check(report.Contains("phase=5") && report.Contains("user picked offensive play"),"Crash breadcrumbs/state lost");
            Check(report.Contains("inner IO failure") && report.Contains("test-module.dll"),"Exception/Windows fault evidence lost");
            Check(Entry(zip,"last-state.txt")==state,"Bundled state differs");
            Entry(zip,"windows-events.txt");Entry(zip,"events.log");
            Check(zip.GetEntry("CollegeFB27.exe")==null && zip.GetEntry("session-handoff.bin")==null,"Unrelated game files bundled");
        }
        string reportPath=Path.Combine(session.RunFolder,"report.txt");
        string reportBefore=File.ReadAllText(reportPath);
        session.CompleteForExit("duplicate completion",0,"other");
        Check(File.ReadAllText(reportPath)==reportBefore,"Duplicate completion overwrote report");
        DiagnosticSession.ObserveHelperFailure(root,12345,Process.GetCurrentProcess().Id,unchecked((int)0xc0000005),true,"Helper faulting module: example.dll");
        using(var stream=File.OpenRead(session.ReportZip))
        using(var zip=new ZipArchive(stream,ZipArchiveMode.Read)){
            Check(Entry(zip,"observer-exit.txt").Contains("Helper exit: -1073741819 (0xC0000005)"),"Observer lost killed-helper status");
            Check(Entry(zip,"windows-events-helper.txt").Contains("example.dll"),"Observer crash evidence missing");
            Check(Entry(zip,"report.txt")==reportBefore,"Observer overwrote original helper report");
        }
        var abandoned=new DiagnosticSession(Path.Combine(root,"abandoned"),24680);
        abandoned.UpdateState(()=>"phase=3 ball=80",true);
        DiagnosticSession.ObserveHelperFailure(Path.Combine(root,"abandoned"),24680,Process.GetCurrentProcess().Id,5,true,"No event yet");
        Check(File.ReadAllText(Path.Combine(abandoned.RunFolder,"report.txt")).Contains("phase=3 ball=80"),"Killed-helper report did not recover durable state");
        Check(File.Exists(abandoned.ReportZip),"Killed-helper report not bundled");
        DiagnosticSession.ObserveHelperFailure(Path.Combine(root,"before-startup"),13579,123,7,true,"Startup fault");
        Check(File.Exists(Path.Combine(root,"before-startup","latest-report.zip")),"Pre-startup helper failure was not reported");
        Check(DiagnosticSession.ExitStatus(0)=="0 (0x00000000)" && DiagnosticSession.ExitStatus(null)=="unavailable","Normal/unknown exits incorrect");
        DateTime now=DateTime.UtcNow;
        Check(DiagnosticSession.RelatedWindowsEvent(1000,now,now.AddSeconds(-5),"Faulting application COLLEGEFB27.EXE"),"Game crash event filtered out");
        Check(DiagnosticSession.RelatedWindowsEvent(1026,now,now.AddSeconds(-5),"SituationalPractice.Runtime.exe"),"Managed helper event filtered out");
        Check(!DiagnosticSession.RelatedWindowsEvent(1000,now,now.AddSeconds(-5),"OtherApp.exe"),"Unrelated application included");
        Check(!DiagnosticSession.RelatedWindowsEvent(1000,now.AddMinutes(-5),now.AddSeconds(-5),"CollegeFB27.exe"),"Stale crash included");
        Check(!DiagnosticSession.RelatedWindowsEvent(999,now,now.AddSeconds(-5),"CollegeFB27.exe"),"Non-crash event included");
        // Observe a real child exit; never attach to or access the actual game.
        using(var child=Process.Start(new ProcessStartInfo(typeof(DiagnosticsChecks).Assembly.Location,"--child"){UseShellExecute=false,CreateNoWindow=true})){
            child.WaitForExit();
            Check(child.ExitCode==unchecked((int)0xc0000005),"Controlled child status differed");
            var exitSession=new DiagnosticSession(Path.Combine(root,"child-exit"),child.Id);
            exitSession.Complete(child,false,false);
            Check(File.ReadAllText(Path.Combine(exitSession.RunFolder,"report.txt")).Contains("Game process exited"),"Observed child exit not recorded");
        }
        Console.WriteLine("PASS: "+checks+" diagnostic checks; full traces, throttling, rotation, failed writes/sampling, ZIPs, event filtering and controlled child exit. No game access.");
    }
}
