using System;
using System.IO;
using SituationalPractice;
class StartChecks
{
    static int checks;
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static byte[] Block(int team,int command)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){
            writer.Write(123L);writer.Write(456L);writer.Write(team);writer.Write(1);
            writer.Write(-15f);writer.Write(-5f);writer.Write(0);writer.Write(0);
            writer.Write(7);writer.Write(command);return stream.ToArray();
        }
    }
    static void Main()
    {
        foreach(var mode in new[]{PracticeMode.DrivePractice,PracticeMode.Scrimmage})foreach(int team in new[]{0,1}){
            int completed;var start=NativeSnapshot.ReadStart(Block(team,11),11,out completed);
            Check(start!=null && completed==7 && start.Match==123 && start.States==456,"Command captures the actual starting situation");
            Check(start.Ball==35 && start.Line==45 && start.NativeDown==1 && start.Possession==team,"First snap starts first-and-ten at the selected spot");
            var after=new NativeSnapshot{Match=123,States=456,Possession=team,NativeDown=2,Ball=39,Line=45};
            var stats=new SessionStats{Mode=mode};
            Check(NativeDrivePolicy.Result(start,after)==DriveEnd.None,"First short gain keeps the drive");
            Check(stats.Record(1,start,after,4,mode)==0 && stats.Teams[team].Plays==1 && stats.Teams[team].Yards==4,"The first snap counts even when the worker first sees the completed play");
            Check(stats.Record(1,start,after,4,mode)==0 && stats.Teams[team].Plays==1,"The same completed first snap cannot count twice");
            after=new NativeSnapshot{Match=123,States=456,Possession=team,NativeDown=2,Ball=35,Line=45,PlayEvents=new[]{25}};
            stats=new SessionStats{Mode=mode};stats.Record(1,start,after,0,mode);
            Check(stats.Teams[team].Plays==1 && stats.Teams[team].Yards==0 && after.NativeDown==2,"An incomplete first snap counts and advances to second down");
        }
        int ignored;
        Check(NativeSnapshot.ReadStart(Block(0,11),12,out ignored)==null,"An old acknowledgement must not supply the next drive's baseline");
        bool rejected=false;var invalid=Block(2,11);
        try{NativeSnapshot.ReadStart(invalid,11,out ignored);}catch(InvalidOperationException){rejected=true;}
        Check(rejected,"An invalid captured team is rejected");
        rejected=false;
        try{NativeSnapshot.ReadStart(new byte[10],11,out ignored);}catch(InvalidOperationException){rejected=true;}
        Check(rejected,"A short capture is rejected");
        Console.WriteLine("PASS: "+checks+" first-snap baseline, counting and startup checks");
    }
}
