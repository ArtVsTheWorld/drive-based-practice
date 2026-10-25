using System;
using SituationalPractice;
class SessionStatsChecks
{
    static int count;
    static void Check(bool value){count++;if(!value)throw new Exception("Session check "+count+" failed.");}
    static NativeSnapshot Snap(int possession,int score0,int score1,int down,params int[] events){return new NativeSnapshot{Match=1,Possession=possession,OffenseScore=score0,DefenseScore=score1,NativeDown=down,Ball=30,Line=40,PlayEvents=events};}
    static void Main()
    {
        var s=new SessionStats();var before=Snap(0,0,0,4);var after=Snap(1,0,0,1,25,80);
        Check(SessionStats.Classify(before,after)==DefensiveResult.Downs);Check(s.Record(1,before,after,0)==2);Check(s.Teams[1].DownStops==1 && s.Teams[1].DefensivePoints==2);
        Check(s.Record(1,before,after,0)==0 && s.Teams[0].Plays==1);
        before=Snap(0,0,2,4);after=Snap(1,0,2,1,23);Check(s.Record(2,before,after,0)==4);Check(s.Teams[1].Interceptions==1);
        before=Snap(0,0,6,1);after=Snap(1,0,12,1,23);Check(s.Record(3,before,after,0)==0);Check(s.Teams[1].DefensivePoints==12 && s.Teams[1].ReturnTouchdowns==1 && s.Teams[1].Interceptions==2);
        before=Snap(1,0,12,2);after=Snap(0,0,12,1);Check(s.Record(4,before,after,-3)==4);Check(s.Teams[0].FumbleRecoveries==1 && s.Teams[1].Yards==-3);
        before=Snap(1,4,12,1);after=Snap(1,4,18,1,77);Check(s.Record(5,before,after,20)==0);Check(s.Teams[1].Touchdowns==1 && s.Teams[1].FirstDowns==1);
        Check(s.Rows()[8][1]=="4" && s.Rows()[8][2]=="12");Check(new SessionStats().Teams[0].Plays==0);
        before=Snap(0,4,18,4);after=Snap(0,4,18,1);Check(SessionStats.Classify(before,after)==DefensiveResult.Downs);
        var yardage=new SessionStats();before=Snap(0,0,0,4);before.Ball=27;before.Line=45;after=Snap(1,0,0,1,80);after.Ball=66;
        Check(yardage.Record(1,before,after,39)==2 && yardage.Teams[0].Yards==7);
        before.Ball=90;after.Ball=10;after.PlayEvents=new[]{25,80};yardage.Record(2,before,after,-80);Check(yardage.Teams[0].Yards==7);
        string handoff=System.IO.Path.GetTempFileName();s.WriteHandoff(handoff,42,1);var restored=SessionStats.ReadHandoff(handoff,42,1);
        Check(restored!=null && restored.Teams[1].DefensivePoints==12 && restored.Teams[0].Plays==3);Check(!System.IO.File.Exists(handoff));
        handoff=System.IO.Path.GetTempFileName();s.WriteHandoff(handoff,42,1);Check(SessionStats.ReadHandoff(handoff,43,1)==null);
        handoff=System.IO.Path.GetTempFileName();s.WriteHandoff(handoff,42,1);Check(SessionStats.ReadHandoff(handoff,42,2)==null);
        handoff=System.IO.Path.GetTempFileName();s.WriteHandoff(handoff,42,1);System.IO.File.SetLastWriteTimeUtc(handoff,DateTime.UtcNow.AddMinutes(-1));Check(SessionStats.ReadHandoff(handoff,42,1)==null);
        Console.WriteLine(count+" session scoring/stat checks passed.");
    }
}
