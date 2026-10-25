using System;
using System.IO;
using SituationalPractice;
class ScrimmageChecks
{
    static int checks;
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static NativeSnapshot S(int possession,float ball,int down=1,int a=0,int b=0,params int[] events){return new NativeSnapshot{Match=1,Possession=possession,Ball=ball,Line=ball+10,NativeDown=down,OffenseScore=a,DefenseScore=b,PlayEvents=events};}
    static void Main(){
        foreach(int offense in new[]{0,1}){
            int defense=1-offense;
            var before=S(offense,40,4);var after=S(defense,57,1,0,0,80);
            var next=ScrimmagePolicy.Next(before,after,DriveEnd.Downs);
            Check(next.Possession==defense && next.Spot.Ball==57 && next.Spot.Down==1 && next.Spot.Distance==10,"Native fourth-down spot");
            after=S(offense,43,1,0,0,80);next=ScrimmagePolicy.Next(before,after,DriveEnd.Downs);
            Check(next.Possession==defense && next.Spot.Ball==57,"Fallback fourth-down mirror");
            after=S(defense,78,1,0,0,23);next=ScrimmagePolicy.Next(before,after,DriveEnd.Turnover);
            Check(next.Spot.Ball==78 && next.Possession==defense,"Interception return endpoint");
            var stats=new SessionStats();Check(stats.Record(1,before,after,0,PracticeMode.Scrimmage)==0 && stats.Teams[defense].Interceptions==1 && stats.Teams[defense].DefensivePoints==0,"No interception bonus");
            after=S(defense,12);Check(stats.Record(2,before,after,0,PracticeMode.Scrimmage)==0 && stats.Teams[defense].FumbleRecoveries==1,"No recovery bonus");
            after=S(defense,50,1,0,0,80);Check(stats.Record(3,before,after,0,PracticeMode.Scrimmage)==0 && stats.Teams[defense].DownStops==1,"No stop bonus");
            after=S(defense,0);next=ScrimmagePolicy.Next(before,after,DriveEnd.Turnover);
            Check(next.Spot.Ball==20,"Turnover touchback");
            after=S(offense,100,1,offense==0?6:0,offense==1?6:0);next=ScrimmagePolicy.Next(before,after,DriveEnd.Scored);
            Check(next.Possession==defense && next.Spot.Ball==25 && next.Score0+next.Score1==6,"Offensive TD handoff");
            after=S(defense,100,1,defense==0?6:0,defense==1?6:0,23);next=ScrimmagePolicy.Next(before,after,DriveEnd.Scored);
            Check(next.Possession==offense && next.Spot.Ball==25 && next.Score0+next.Score1==6,"Defensive TD handoff");
            stats=new SessionStats();Check(stats.Record(1,before,after,0,PracticeMode.Scrimmage)==0 && stats.Teams[defense].ReturnTouchdowns==1 && stats.Teams[defense].DefensivePoints==6,"Native return TD not doubled");
            after=S(offense,0,1,defense==0?2:0,defense==1?2:0);next=ScrimmagePolicy.Next(before,after,DriveEnd.Scored);
            Check(next.Possession==defense && next.Spot.Ball==25 && next.Score0+next.Score1==2,"Safety possession");
            stats=new SessionStats();Check(stats.Record(1,before,after,-40,PracticeMode.Scrimmage)==0 && stats.Teams[defense].Safeties==1 && stats.Teams[defense].DefensivePoints==2,"Safety stats");
            after=S(offense,100);next=ScrimmagePolicy.Next(before,after,DriveEnd.Touchdown);
            Check(next.Score0+next.Score1==6 && next.Possession==defense,"Fallback TD scoring");
            after=S(offense,0);next=ScrimmagePolicy.Next(before,after,DriveEnd.Safety);
            Check(next.Score0+next.Score1==2 && next.Possession==defense,"Fallback safety scoring");
            Check(ScrimmagePolicy.Next(before,S(offense,44,2),DriveEnd.None)==null,"Normal play keeps drive");
        }
        var saved=new SessionStats{Mode=PracticeMode.Scrimmage,ResumeSpot=new Scenario{Ball=45,Distance=10,Down=1}};saved.Teams[1].Safeties=2;
        string path=Path.GetTempFileName();saved.WriteHandoff(path,4,1);var read=SessionStats.ReadHandoff(path,4,1);
        Check(read.Mode==PracticeMode.Scrimmage && read.Teams[1].Safeties==2 && read.ResumeSpot.Ball==45,"Mode/safety handoff");
        Console.WriteLine("PASS: "+checks+" scrimmage scoring, possession, placement and handoff checks");
    }
}
