using System;
using System.Collections.Generic;
using SituationalPractice;
class OpponentChecks
{
    static int checks;
    static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
    static void Main()
    {
        int team;
        Check(PracticeSide.TryBoundUserTeam(0,0,-1,255,out team) && team==0,"Home local controller");
        Check(PracticeSide.TryBoundUserTeam(-1,255,0,1,out team) && team==1,"Away local controller");
        foreach(int controller in new[]{-1,8,255})Check(!PracticeSide.TryBoundUserTeam(controller,0,-1,255,out team),"Invalid controller binding");
        Check(!PracticeSide.TryBoundUserTeam(0,0,1,1,out team),"Two local sides must not be auto-selected");
        Check(!PracticeSide.TryBoundUserTeam(-1,255,-1,255,out team),"Unbound sides");
        Check(!PracticeSide.TryBoundUserTeam(0,1,-1,255,out team),"Binding side mismatch");
        Check(!PracticeSide.TryBoundUserTeam(8,0,0,1,out team),"Invalid opposite binding must not resolve a user");
        foreach(int possession in new[]{0,1}){
            Check(PracticeSide.TryUserTeam(possession,5,out team) && team==possession,"Offensive user");
            Check(PracticeSide.TryUserTeam(possession,9,out team) && team==1-possession,"Defensive user");
            foreach(int flags in new[]{0,1,13,17})Check(!PracticeSide.TryUserTeam(possession,flags,out team),"Ambiguous/stale readiness");
            Check(!PracticeSide.TryUserTeam(possession,25,possession,out team),"Stale opposing defense readiness after timeout");
            Check(!PracticeSide.TryUserTeam(possession,21,1-possession,out team),"Stale opposing offense readiness after timeout");
            Check(PracticeSide.TryUserTeam(possession,25,1-possession,out team) && team==1-possession,"Known defensive user");
            Check(PracticeSide.TryUserTeam(possession,21,possession,out team) && team==possession,"Known offensive user");
        }
        foreach(int possession in new[]{-1,2})Check(!PracticeSide.TryUserTeam(possession,5,out team),"Invalid possession");
        foreach(int type in new[]{-1,0,21,22,23,24,25,37,38,105,106,107,154,155,216})Check(!OffenseChoice.Allowed(1,type),"Special/unknown offense admitted");
        Check(!OffenseChoice.Allowed(11,1),"Defensive formation admitted");
        var book=new List<OffensePlay>();
        for(int formation=0;formation<2;formation++)foreach(int type in new[]{1,11,21,105,154})
            book.Add(new OffensePlay{Formation=formation,Set=0,Play=type,Type=type});
        var random=new Random(42);int shortPass=0,longPass=0,firstFormation=0;
        for(int i=0;i<10000;i++){
            var shortCall=OffenseChoice.Pick(book,4,1,50,random);
            var longCall=OffenseChoice.Pick(book,3,12,50,random);
            Check(shortCall.Type==1 || shortCall.Type==11,"Short call unsafe");
            Check(longCall.Type==1 || longCall.Type==11,"Long call unsafe");
            if(OffenseChoice.Passing(shortCall.Type))shortPass++;
            if(OffenseChoice.Passing(longCall.Type))longPass++;
            if(longCall.Formation==0)firstFormation++;
        }
        Check(shortPass>2700 && shortPass<3300,"Short run/pass balance");
        Check(longPass>7700 && longPass<8300,"Long run/pass balance");
        Check(firstFormation>4700 && firstFormation<5300,"Formation balance");
        var onlyRun=new List<OffensePlay>{new OffensePlay{Type=11}};
        for(int i=0;i<100;i++)Check(OffenseChoice.Pick(onlyRun,3,20,50,random)!=null,"Narrow playbook fallback");
        Check(OffenseChoice.Pick(new List<OffensePlay>{new OffensePlay{Type=21}},1,10,50,random)==null,"Unsafe fallback");
        Check(OffenseChoice.Pick(new List<OffensePlay>(),1,10,50,random)==null,"Empty book");
        Console.WriteLine("PASS: "+checks+" user-side and CPU offense assertions");
    }
}
