using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SituationalPractice
{
    public enum DefensiveResult { None, Interception, FumbleRecovery, Downs, ReturnTouchdown }
    public sealed class TeamPracticeStats
    {
        public int Plays, FirstDowns, Touchdowns, Interceptions, FumbleRecoveries, DownStops, ReturnTouchdowns, DefensivePoints, Safeties;
        public double Yards;
    }
    public sealed class SessionStats
    {
        public readonly TeamPracticeStats[] Teams={new TeamPracticeStats(),new TeamPracticeStats()};
        private int lastPlay=-1;
        public Scenario ResumeSpot;
        public PracticeMode Mode;
        public static DefensiveResult Classify(NativeSnapshot before,NativeSnapshot after)
        {
            bool changed=before.Possession!=after.Possession;
            bool interception=after.HasEvent(23);
            bool downs=after.HasEvent(80);
            int defense=1-before.Possession;
            int added=after.Score(defense)-before.Score(defense);
            // A native defensive touchdown already awards six. Never stack four.
            if(changed && added>=6 && !downs)return DefensiveResult.ReturnTouchdown;
            if(after.Score(before.Possession)-before.Score(before.Possession)>=6 || added==2 && !downs && !interception)return DefensiveResult.None;
            if(interception && changed)return DefensiveResult.Interception;
            if(downs)return DefensiveResult.Downs;
            // During supported run/pass practice, a non-interception change of
            // possession is a recovered fumble. Failed fourth downs take priority.
            if(changed)return DefensiveResult.FumbleRecovery;
            if(before.NativeDown==4 && after.NativeDown==1 && after.Ball<before.Line)return DefensiveResult.Downs;
            return DefensiveResult.None;
        }
        public int Record(int play,NativeSnapshot before,NativeSnapshot after,float gain,PracticeMode mode=PracticeMode.DrivePractice)
        {
            if(play<=lastPlay)return 0;
            if(before.Possession<0 || before.Possession>1)throw new InvalidOperationException("Invalid practice possession.");
            lastPlay=play;var offense=Teams[before.Possession];var defense=Teams[1-before.Possession];
            DefensiveResult result=Classify(before,after);
            offense.Plays++;
            // The fourth-down turnover flips native field coordinates. Its raw
            // gain can therefore include the flip instead of the play's yardage.
            double yards=result==DefensiveResult.Downs && before.Possession!=after.Possession ? after.HasEvent(25)?0:100-after.Ball-before.Ball : gain;
            if(Scenario.Finite(yards))offense.Yards+=yards;
            if(after.HasEvent(77))offense.FirstDowns++;
            if(after.Score(before.Possession)-before.Score(before.Possession)>=6)offense.Touchdowns++;
            int points=0;
            if(result==DefensiveResult.Interception){defense.Interceptions++;points=4;}
            if(result==DefensiveResult.FumbleRecovery){defense.FumbleRecoveries++;points=4;}
            if(result==DefensiveResult.Downs){defense.DownStops++;points=2;}
            if(result==DefensiveResult.ReturnTouchdown){defense.ReturnTouchdowns++;points=6;if(after.HasEvent(23))defense.Interceptions++;else defense.FumbleRecoveries++;}
            int nativeDefensePoints=Math.Max(0,after.Score(1-before.Possession)-before.Score(1-before.Possession));
            if(nativeDefensePoints==2)defense.Safeties++;
            if(mode==PracticeMode.Scrimmage){defense.DefensivePoints+=nativeDefensePoints;return 0;}
            defense.DefensivePoints+=points;
            return Math.Max(0,points-Math.Max(0,after.Score(1-before.Possession)-before.Score(1-before.Possession)));
        }
        // A short-lived, one-use handoff preserves the active session during a
        // clean development restart. It cannot become a saved practice history.
        public static SessionStats ReadHandoff(string path,long gameStart,long match)
        {
            if(!File.Exists(path))return null;
            try{
                if(DateTime.UtcNow-File.GetLastWriteTimeUtc(path)>TimeSpan.FromSeconds(30))return null;
                using(var r=new BinaryReader(File.OpenRead(path))){
                    if(r.ReadInt64()!=0x315453545350 || r.ReadInt64()!=gameStart || r.ReadInt64()!=match)return null;
                    var s=new SessionStats();s.lastPlay=r.ReadInt32();
                    foreach(var t in s.Teams){t.Plays=r.ReadInt32();t.Yards=r.ReadDouble();t.FirstDowns=r.ReadInt32();t.Touchdowns=r.ReadInt32();t.Interceptions=r.ReadInt32();t.FumbleRecoveries=r.ReadInt32();t.DownStops=r.ReadInt32();t.ReturnTouchdowns=r.ReadInt32();t.DefensivePoints=r.ReadInt32();}
                    if(r.BaseStream.Position<r.BaseStream.Length){s.ResumeSpot=new Scenario{Ball=r.ReadDouble(),Distance=r.ReadDouble(),Down=r.ReadInt32(),Seconds=0,ClockEnabled=false,EndBehavior=EndBehavior.RepeatScenario};s.ResumeSpot.Validate();}
                    if(r.BaseStream.Position<r.BaseStream.Length){s.Mode=(PracticeMode)r.ReadInt32();if(s.Mode!=PracticeMode.DrivePractice && s.Mode!=PracticeMode.Scrimmage)return null;foreach(var t in s.Teams)t.Safeties=r.ReadInt32();}
                    return s;
                }
            }catch(IOException){return null;}finally{File.Delete(path);}
        }
        public int LastPlay {get{return lastPlay;}}
        public void WriteHandoff(string path,long gameStart,long match)
        {
            using(var w=new BinaryWriter(File.Create(path))){
                w.Write(0x315453545350L);w.Write(gameStart);w.Write(match);w.Write(lastPlay);
                foreach(var t in Teams){w.Write(t.Plays);w.Write(t.Yards);w.Write(t.FirstDowns);w.Write(t.Touchdowns);w.Write(t.Interceptions);w.Write(t.FumbleRecoveries);w.Write(t.DownStops);w.Write(t.ReturnTouchdowns);w.Write(t.DefensivePoints);}
                var spot=ResumeSpot??new Scenario{Ball=25,Distance=10,Down=1};w.Write(spot.Ball);w.Write(spot.Distance);w.Write(spot.Down);
                w.Write((int)Mode);foreach(var t in Teams)w.Write(t.Safeties);
            }
        }
        public string[][] Rows()
        {
            var names=new[]{"Practice plays","Net offensive yards","First downs","Offensive touchdowns","Interceptions","Fumble recoveries","Stops on downs","Defensive touchdowns","Defensive points","Safeties"};
            var result=new string[names.Length][];
            for(int i=0;i<names.Length;i++)result[i]=new[]{names[i],Value(Teams[0],i),Value(Teams[1],i)};
            return result;
        }
        private static string Value(TeamPracticeStats t,int row)
        {
            if(row==1)return t.Yards.ToString("0.0",CultureInfo.InvariantCulture);
            int[] values={t.Plays,0,t.FirstDowns,t.Touchdowns,t.Interceptions,t.FumbleRecoveries,t.DownStops,t.ReturnTouchdowns,t.DefensivePoints,t.Safeties};
            return values[row].ToString(CultureInfo.InvariantCulture);
        }
    }
}
