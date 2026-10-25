using System;
using System.Collections.Generic;
using SituationalPractice;
class DefenseChecks
{
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    static void Main()
    {
        foreach(int type in new[]{32,35,49,50,51,23,24,25,-1,216})Check(!DefenseChoice.Allowed(11,type),"Special/unknown type admitted");
        foreach(int kind in new[]{1,2,3,12,13})Check(!DefenseChoice.Allowed(kind,188),"Wrong formation kind admitted");
        var plays=new List<DefensePlay>();
        for(int backs=3;backs<=7;backs++)foreach(int type in new[]{188,178,175,194,199,51})plays.Add(new DefensePlay{Formation=backs,Set=0,Play=type,Type=type,DefensiveBacks=backs});
        var rng=new Random(42);int baseFronts=0,nickel=0,dime=0,heavy=0;
        for(int i=0;i<10000;i++) {
            var b=DefenseChoice.Pick(plays,2,1,10,50,rng);Check(b!=null && b.Type!=51 && b.Type!=199 && b.DefensiveBacks!=3,"Base filter");if(b.DefensiveBacks==4)baseFronts++;
            var n=DefenseChoice.Pick(plays,3,1,10,50,rng);if(n.DefensiveBacks==5)nickel++;
            var d=DefenseChoice.Pick(plays,3,3,12,50,rng);if(d.DefensiveBacks==6)dime++;
            var h=DefenseChoice.Pick(plays,2,4,1,98,rng);if(h.DefensiveBacks==3)heavy++;
        }
        Check(baseFronts>7000 && nickel>6000 && dime>6000 && heavy>7000,"Personnel weighting");
        Check(DefenseChoice.Pick(new List<DefensePlay>{new DefensePlay{Formation=0,Type=188,DefensiveBacks=4}},5,3,20,50,rng)!=null,"Narrow book fallback");
        Check(DefenseChoice.Pick(new List<DefensePlay>{new DefensePlay{Type=51,DefensiveBacks=4}},2,1,10,50,rng)==null,"Special-only book fallback");
        Check(DefenseChoice.CoverageWeight(194,3,12)>DefenseChoice.CoverageWeight(175,3,12),"Long-down coverage");
        Check(DefenseChoice.CoverageWeight(175,4,1)>DefenseChoice.CoverageWeight(194,4,1),"Short-yardage pressure");
        Console.WriteLine("PASS: defensive filtering and 40,000 situational selections");
    }
}
