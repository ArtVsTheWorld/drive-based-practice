using System;

namespace SituationalPractice
{
    public static class ScrimmagePolicy
    {
        public static PossessionDrive Next(NativeSnapshot before,NativeSnapshot after,DriveEnd end)
        {
            if(end==DriveEnd.None)return null;
            if(end==DriveEnd.Unsupported)throw new InvalidOperationException("Invalid scrimmage outcome.");
            int offense=before.Possession,defense=1-offense;
            int offensivePoints=after.Score(offense)-before.Score(offense);
            int defensivePoints=after.Score(defense)-before.Score(defense);
            int next=defense,score0=after.OffenseScore,score1=after.DefenseScore;
            bool scored=end==DriveEnd.Scored || end==DriveEnd.Touchdown || end==DriveEnd.Safety;
            double ball;
            if(scored){
                // A defensive TD sends the ball back to the original offense;
                // a safety gives possession to the team that scored the safety.
                if(defensivePoints>=6)next=offense;
                if(offensivePoints==0 && defensivePoints==0){
                    int scoringTeam=end==DriveEnd.Safety?defense:offense;
                    int points=end==DriveEnd.Safety?2:6;
                    if(scoringTeam==0)score0+=points;else score1+=points;
                }
                ball=25;
            }else{
                // Native turnover placement is already mirrored when possession
                // has changed. Mirror only the fallback fourth-down result.
                ball=after.Possession==next?after.Ball:100-after.Ball;
                if(!Scenario.Finite(ball))throw new InvalidOperationException("Invalid scrimmage ball spot.");
                if(ball<=0 || ball>=100)ball=20; // turnover touchback
            }
            var spot=NativeDrivePolicy.StartingSpot(new NativeSnapshot{Ball=(float)ball},false);
            return new PossessionDrive{Possession=next,Score0=score0,Score1=score1,Spot=spot};
        }
    }
    // Decisions use settled native outcomes, including scores and possession.
    // Raw yards gained alone can miss touchdowns near the goal line.
    public static class NativeDrivePolicy
    {
        public static Scenario StartingSpot(NativeSnapshot snapshot, bool preserveSituation)
        {
            double ball=Math.Max(1,Math.Min(99,snapshot.Ball));
            if(!Scenario.Finite(snapshot.Ball))throw new InvalidOperationException("Invalid native ball spot.");
            int down=preserveSituation && snapshot.NativeDown>=1 && snapshot.NativeDown<=4 ? snapshot.NativeDown : 1;
            double distance=preserveSituation ? snapshot.Line-ball : Math.Min(10,100-ball);
            if(!Scenario.Finite(distance) || distance<=0 || distance>100-ball)distance=Math.Min(10,100-ball);
            return new Scenario{Ball=ball,Down=down,Distance=distance,Seconds=0,ClockEnabled=false,EndBehavior=EndBehavior.RepeatScenario};
        }
        public static DriveEnd Result(NativeSnapshot previous, NativeSnapshot current)
        {
            if(previous.Match!=current.Match)throw new InvalidOperationException("Practice session changed.");
            if(current.OffenseScore!=previous.OffenseScore || current.DefenseScore!=previous.DefenseScore)
                return DriveEnd.Scored;
            if(current.HasEvent(80))return DriveEnd.Downs;
            if(current.Possession!=previous.Possession)return DriveEnd.Turnover;
            if(!Scenario.Finite(current.Ball) || !Scenario.Finite(current.Line))return DriveEnd.Unsupported;
            if(current.Ball<=0)return DriveEnd.Safety;
            if(current.Ball>=100)return DriveEnd.Touchdown;
            if(current.NativeDown<1 || current.NativeDown>4)return DriveEnd.Downs;
            if(previous.NativeDown==4 && current.Ball<previous.Line && current.NativeDown==1)return DriveEnd.Downs;
            return DriveEnd.None;
        }
    }
}
