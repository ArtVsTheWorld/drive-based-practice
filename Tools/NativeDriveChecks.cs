using System;
using SituationalPractice;
class NativeDriveChecks
{
    static int checks;
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static NativeSnapshot State(float ball,int down,float line,int score=0,int possession=0)
    {return new NativeSnapshot{Match=123,Ball=ball,NativeDown=down,Line=line,OffenseScore=score,Possession=possession};}
    static void Main()
    {
        Check(NativeDrivePolicy.Result(State(30,1,40),State(33,2,40))==DriveEnd.None,"short gain continues native drive");
        Check(NativeDrivePolicy.Result(State(33,2,40),State(33,3,40))==DriveEnd.None,"incomplete keeps native target");
        Check(NativeDrivePolicy.Result(State(33,3,40),State(28,4,40))==DriveEnd.None,"loss leaves fourth down playable");
        Check(NativeDrivePolicy.Result(State(28,4,40),State(45,1,55))==DriveEnd.None,"fourth down conversion continues");
        Check(NativeDrivePolicy.Result(State(28,4,40),State(34,5,40))==DriveEnd.Downs,"native failed fourth down");
        Check(NativeDrivePolicy.Result(State(28,4,40),State(34,1,44))==DriveEnd.Downs,"native down reset without reaching target");
        Check(NativeDrivePolicy.Result(State(28,4,40),State(28,4,40))==DriveEnd.None,"native replay of fourth down is retained");
        Check(NativeDrivePolicy.Result(State(91.9f,4,100),State(97,-1,100,6))==DriveEnd.Scored,"native touchdown recognized despite short recorded spot");
        Check(NativeDrivePolicy.Result(State(91.9f,4,100),State(97,-1,100))==DriveEnd.Downs,"invalid post-drive down restarts exercise");
        Check(NativeDrivePolicy.Result(State(33,2,40),State(67,1,77,0,1))==DriveEnd.Turnover,"possession change");
        Check(NativeDrivePolicy.Result(State(1,2,11),State(0,-1,11))==DriveEnd.Safety,"native safety");
        Check(NativeDrivePolicy.Result(State(30,1,40),State(float.NaN,2,40))==DriveEnd.Unsupported,"invalid native spot rejected");
        var s=NativeDrivePolicy.StartingSpot(State(33,3,40),true);
        Check(s.Down==3 && s.Ball==33 && s.Distance==7 && !s.ClockEnabled,"native Re-spot situation retained");
        s=NativeDrivePolicy.StartingSpot(State(97,0,100),false);
        Check(s.Down==1 && s.Distance==3,"new goal-to-go exercise");
        s=NativeDrivePolicy.StartingSpot(State(95.41297f,4,96),false);
        Check(s.Down==1 && Math.Abs(s.Distance-4.58703)<.0001,"menu opponent-five starts goal-to-go despite old fourth down");
        s=NativeDrivePolicy.StartingSpot(State(60,3,62),false);
        Check(s.Down==1 && s.Distance==10,"menu new spot starts first-and-ten rather than retaining old target");
        bool changed=false;var other=State(30,1,40);other.Match=124;
        try{NativeDrivePolicy.Result(State(30,1,40),other);}catch(InvalidOperationException){changed=true;}
        Check(changed,"session change rejected");
        Console.WriteLine("PASS: "+checks+" native drive checks");
    }
}
