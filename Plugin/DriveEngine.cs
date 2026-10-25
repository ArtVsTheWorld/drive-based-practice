using System;

namespace SituationalPractice
{
    public enum PracticeMode { DrivePractice, Scrimmage }
    public sealed class PossessionDrive
    {
        public int Possession, Score0, Score1;
        public Scenario Spot;
    }
    public enum DriveEnd { None, Touchdown, Safety, Turnover, Downs, ClockExpired, Unsupported, Scored }
    public enum EndBehavior { Pause, RepeatScenario }
    public sealed class Scenario
    {
        // Distance from the offense's own goal. Independent of world direction.
        public double Ball = 70, Distance = 10, Seconds = 120;
        public int Down = 1, OffenseScore, DefenseScore;
        public EndBehavior EndBehavior = EndBehavior.Pause;
        public bool ClockEnabled = true, RunBetweenPlays;
        public void Validate()
        {
            if (!Finite(Ball) || Ball <= 0 || Ball >= 100) throw new ArgumentException("Ball position must be between the goal lines.");
            if (Down < 1 || Down > 4) throw new ArgumentException("Choose down 1 through 4.");
            if (!Finite(Distance) || Distance <= 0 || Distance > 100-Ball) throw new ArgumentException("Distance must be positive and cannot exceed the distance to the goal.");
            if (!Finite(Seconds) || Seconds < 0 || Seconds > 3600) throw new ArgumentException("Time must be between 0 and 3600 seconds.");
            if (OffenseScore < 0 || DefenseScore < 0) throw new ArgumentException("Scores cannot be negative.");
        }
        internal static bool Finite(double n) { return !double.IsNaN(n) && !double.IsInfinity(n); }
        public Scenario Copy() { return (Scenario)MemberwiseClone(); }
    }
    public sealed class PlayResult
    {
        public long Id;
        public double Gain;
        public bool Incomplete, Turnover, Unsupported;
    }
    public sealed class DriveEngine
    {
        private Scenario scenario;
        private long lastId;
        public double Ball { get; private set; }
        public double LineToGain { get; private set; }
        public double Seconds { get; private set; }
        public int Down { get; private set; }
        public int OffenseScore { get; private set; }
        public int DefenseScore { get; private set; }
        public long Plays { get; private set; }
        public bool Running { get; private set; }
        public bool Enabled { get; private set; }
        public DriveEnd End { get; private set; }
        public double Distance { get { return Math.Max(0,LineToGain-Ball); } }
        public bool GoalToGo { get { return LineToGain >= 100; } }
        public Scenario CurrentScenario { get { return scenario.Copy(); } }
        public void Start(Scenario value, long observedPlayId)
        {
            value.Validate(); scenario=value.Copy(); lastId=observedPlayId;
            Ball=value.Ball; Down=value.Down; LineToGain=Math.Min(100,Ball+value.Distance);
            Seconds=value.Seconds; OffenseScore=value.OffenseScore; DefenseScore=value.DefenseScore;
            Plays=0; End=DriveEnd.None; Enabled=true; Running=true;
        }
        public void Pause() { Running=false; }
        public void Resume() { if (Enabled && End==DriveEnd.None) Running=true; }
        public void Disable() { Enabled=false; Running=false; }
        public void Reset(long observedPlayId) { Start(scenario,observedPlayId); }
        // Called with monotonic elapsed time, never wall-clock calendar time.
        // Expiry during a live play lets that play finish before ending the drive.
        public void Tick(double elapsed, bool live, bool paused)
        {
            if (!Enabled || !Running || paused || !scenario.ClockEnabled || (!live && !scenario.RunBetweenPlays)) return;
            if (!Scenario.Finite(elapsed) || elapsed < 0) throw new ArgumentException("Invalid elapsed time.");
            Seconds=Math.Max(0,Seconds-elapsed);
            if (Seconds==0 && !live) Finish(DriveEnd.ClockExpired);
        }
        public bool Apply(PlayResult result)
        {
            if (!Enabled || !Running || result.Id <= lastId) return false;
            if (!Scenario.Finite(result.Gain) || Math.Abs(result.Gain)>120) throw new ArgumentException("Invalid play gain.");
            lastId=result.Id;
            if (result.Unsupported) { Finish(DriveEnd.Unsupported); return true; }
            Plays++;
            if (result.Turnover) { Finish(DriveEnd.Turnover); return true; }
            Ball += result.Incomplete ? 0 : result.Gain;
            if (Ball >= 100) { Ball=100; OffenseScore+=6; Finish(DriveEnd.Touchdown); }
            else if (Ball <= 0) { Ball=0; DefenseScore+=2; Finish(DriveEnd.Safety); }
            else if (Ball >= LineToGain) { Down=1; LineToGain=Math.Min(100,Ball+10); }
            else if (Down==4) Finish(DriveEnd.Downs);
            else Down++;
            if (End==DriveEnd.None && scenario.ClockEnabled && Seconds==0) Finish(DriveEnd.ClockExpired);
            return true;
        }
        private void Finish(DriveEnd reason) { End=reason; Running=false; }
        public bool Repeat(long observedPlayId)
        {
            if (End==DriveEnd.None || End==DriveEnd.Unsupported || scenario.EndBehavior!=EndBehavior.RepeatScenario) return false;
            int offense=OffenseScore, defense=DefenseScore; long count=Plays;
            Start(scenario,observedPlayId); OffenseScore=offense; DefenseScore=defense; Plays=count;
            return true;
        }
    }
}
