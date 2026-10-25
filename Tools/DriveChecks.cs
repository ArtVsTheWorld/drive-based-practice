using System;
using SituationalPractice;
class DriveChecks
{
    static int checks;
    static void Check(bool ok,string label) { checks++; if(!ok)throw new Exception(label); }
    static DriveEngine Create(double ball,int down,double distance) { var e=new DriveEngine(); e.Start(new Scenario{Ball=ball,Down=down,Distance=distance},100); return e; }
    static void Main()
    {
        var e=Create(70,1,10);
        Check(!e.Apply(new PlayResult{Id=100,Gain=17}),"discard existing play");
        Check(e.Apply(new PlayResult{Id=101,Gain=0}),"zero gain"); Check(e.Down==2 && e.Distance==10,"second and ten");
        Check(e.Apply(new PlayResult{Id=102,Gain=17.58}),"completion"); Check(e.Down==1 && Math.Abs(e.Ball-87.58)<.001 && e.Distance==10,"first down exact spot");
        Check(!e.Apply(new PlayResult{Id=102,Gain=17.58}) && e.Plays==2,"duplicate result");
        e.Apply(new PlayResult{Id=103,Gain=-5}); Check(e.Down==2 && e.Distance==15,"sack preserves line to gain");
        e.Apply(new PlayResult{Id=104,Gain=9,Incomplete=true}); Check(e.Down==3 && e.Distance==15,"incompletion ignores endpoint");
        e=Create(95,1,5); Check(e.GoalToGo,"goal to go"); e.Apply(new PlayResult{Id=101,Gain=-3}); Check(e.GoalToGo && e.Distance==8,"goal to go after loss");
        e.Apply(new PlayResult{Id=102,Gain=8}); Check(e.End==DriveEnd.Touchdown && e.OffenseScore==6,"touchdown");
        e=Create(20,4,10); e.Apply(new PlayResult{Id=101,Gain=10}); Check(e.Down==1 && e.End==DriveEnd.None,"fourth-down conversion");
        e=Create(20,4,10); e.Apply(new PlayResult{Id=101,Gain=9.99}); Check(e.End==DriveEnd.Downs,"fourth-down failure");
        e=Create(2,1,10); e.Apply(new PlayResult{Id=101,Gain=-3}); Check(e.End==DriveEnd.Safety && e.DefenseScore==2,"safety");
        e=Create(50,1,10); e.Apply(new PlayResult{Id=101,Turnover=true}); Check(e.End==DriveEnd.Turnover && e.Ball==50,"turnover");
        e=Create(50,1,10); e.Tick(121,true,false); Check(e.End==DriveEnd.None && e.Seconds==0,"finish final live play"); e.Apply(new PlayResult{Id=101,Gain=6}); Check(e.End==DriveEnd.ClockExpired && e.Down==2,"expire after result");
        e=Create(50,1,10); e.Tick(30,false,false); Check(e.Seconds==120,"clock waits between plays"); e.Tick(30,true,true); Check(e.Seconds==120,"pause stops clock");
        e.Pause(); Check(!e.Apply(new PlayResult{Id=101,Gain=4}),"pause prevents drive writes"); e.Resume(); e.Disable(); Check(!e.Apply(new PlayResult{Id=102,Gain=4}),"disable prevents drive writes");
        e=new DriveEngine(); e.Start(new Scenario{Ball=90,Distance=10,EndBehavior=EndBehavior.RepeatScenario},0); e.Apply(new PlayResult{Id=1,Gain=10}); Check(e.Repeat(1) && e.Ball==90 && e.OffenseScore==6,"repeat keeps score");
        bool rejected=false; try { Create(98,1,10); } catch(ArgumentException) { rejected=true; } Check(rejected,"invalid distance");
        e=Create(50,1,10); e.Apply(new PlayResult{Id=101,Unsupported=true}); Check(e.End==DriveEnd.Unsupported && e.Plays==0,"unsupported result freezes drive");
        Console.WriteLine("PASS: "+checks+" drive checks");
    }
}
