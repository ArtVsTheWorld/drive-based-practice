using System;
using System.IO;
using SituationalPractice;
class ModeChecks
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Main()
    {
        Check(new SessionStats().Mode==PracticeMode.DefaultPractice,"A fresh practice session must be native practice");
        Check((int)PracticeMode.DrivePractice==0 && (int)PracticeMode.Scrimmage==1,"Existing handoff values stay compatible");
        var mode=PracticeMode.DefaultPractice;
        foreach(var expected in new[]{PracticeMode.DrivePractice,PracticeMode.Scrimmage,PracticeMode.DefaultPractice}){
            mode=PracticeModes.Next(mode);Check(mode==expected,"All three modes remain accessible");
        }
        foreach(PracticeMode start in Enum.GetValues(typeof(PracticeMode))){
            foreach(int presses in new[]{0,1,2,3,4,5,6,17,100}){
                var sequential=start;for(int i=0;i<presses;i++)sequential=PracticeModes.Next(sequential);
                Check(PracticeModes.Next(start,presses)==sequential,"Batched menu presses must not use the former two-mode toggle");
            }
            Check(PracticeModes.Label(start).Length<64 && PracticeModes.Description(start).Length<192,"Menu strings fit native buffers");
            Check(PracticeModes.IsDrive(start)==(start!=PracticeMode.DefaultPractice),"Default has no drive lifecycle");
            var stats=new SessionStats{Mode=start,ResumeSpot=new Scenario{Ball=75,Distance=10,Down=1}};
            string path=Path.GetTempFileName();stats.WriteHandoff(path,123,456);
            var resumed=SessionStats.ReadHandoff(path,123,456);
            Check(resumed!=null && resumed.Mode==start && !File.Exists(path),"All modes survive a one-use development restart");
        }
        var empty=new SessionStats();
        var before=new NativeSnapshot{Possession=0,NativeDown=4,Ball=30,Line=40};
        var after=new NativeSnapshot{Possession=1,NativeDown=1,Ball=70,PlayEvents=new[]{80}};
        Check(empty.Record(1,before,after,0,PracticeMode.DefaultPractice)==0 && empty.LastPlay==-1 && empty.Teams[0].Plays==0 && empty.Teams[1].DownStops==0,"Native practice neither records a drive nor grants stop points");
        Check(empty.Record(1,before,after,0,PracticeMode.DrivePractice)==2 && empty.Teams[0].Plays==1,"Default practice must not consume the first actual drive result");
        const long image=0x140000000,data=0x200000000;
        foreach(byte[] hook in new[]{NativeRuntime.BuildGate(image,data,0),NativeRuntime.BuildGate(image,data,1),NativeRuntime.BuildGate(image,data,2),NativeRuntime.BuildTrace(image,data),NativeRuntime.BuildHudEntry(image,data),NativeRuntime.BuildLineGuard(image,data),NativeRuntime.BuildStatsVisibility(image,data),NativeRuntime.BuildSessionStats(image,data),NativeRuntime.BuildReturnGuard(image,data),NativeRuntime.BuildPlayCallTick(image,data),NativeRuntime.BuildPracticePlayCallUi(image,data),NativeRuntime.BuildInitialPlayCallTick(image,data),NativeRuntime.BuildInitialSetupTick(image,data),NativeRuntime.BuildModeDispatch(image,data),NativeRuntime.BuildModeText(image,data),NativeRuntime.BuildEarlyPlayCall(image,data)})
            Check(hook.Length<=1024,"A hook must not overwrite the next trampoline slot");
        Check(NativeRuntime.BuildDefenseCommit(image,data).Length<=512,"CPU commit fits before the refresh helper");
        Check(NativeRuntime.BuildPlayCallRefresh(image,data).Length<=1024,"Refresh helper fits before mode entry");
        Check(NativeRuntime.BuildDefaultPracticeRestore(image,data).Length<=2048,"Mode entry fits in its reserved block");
        Console.WriteLine("PASS: "+checks+" default mode, menu cycle, scoring isolation and handoff checks");
    }
}
