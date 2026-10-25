using System;
using System.Collections.Generic;

namespace SituationalPractice
{
    public static class PracticeSide
    {
        // Native service bits 4/8 acknowledge offense/defense registration.
        // Only one current user selection may be ready before CPU submission.
        public static bool TryUserTeam(int possession,int flags,out int team)
        {
            team=-1;if(possession<0 || possession>1)return false;
            int ready=flags&12;
            if(ready==4){team=possession;return true;}
            if(ready==8){team=1-possession;return true;}
            return false;
        }
        public static bool TryUserTeam(int possession,int flags,int knownTeam,out int team)
        {
            if(knownTeam==-1)return TryUserTeam(possession,flags,out team);
            team=-1;
            if(possession<0 || possession>1 || knownTeam<0 || knownTeam>1)return false;
            if((flags&12)!=(knownTeam==possession?4:8))return false;
            team=knownTeam;return true;
        }
    }
    public sealed class OffensePlay
    {
        public int Formation,Set,Play,Type;
        public string Name;
    }
    public static class OffenseChoice
    {
        public static bool Allowed(int formationType,int type)
        {
            if(formationType!=1)return false;
            switch(type){
                case 1:case 2:case 3:case 4:case 5:case 6:case 7:
                case 11:case 12:case 13:case 14:case 15:case 16:case 17:case 18:case 19:case 20:case 41:
                case 100:case 101:case 102:case 103:case 104:
                case 151:case 152:case 153:case 157:case 158:case 159:case 161:
                case 162:case 163:case 164:case 165:case 166:case 167:case 169:case 170:case 171:case 172:
                case 195:case 196:case 197:case 198:case 200:case 201:case 202:case 203:
                case 204:case 205:case 206:case 207:case 208:case 209:case 212:case 213:case 214:case 215:return true;
                default:return false;
            }
        }
        public static bool Passing(int type)
        {
            return type>=1 && type<=7 || type>=100 && type<=104 || type==157 || type==158 || type==159
                || type>=204 && type<=208 || type==212 || type==213;
        }
        public static OffensePlay Pick(IList<OffensePlay> plays,int down,float distance,float ball,Random random)
        {
            int passPercent=distance<=2?30:down>=3 && distance>=5?80:ball>=95?40:55;
            bool pass=random.Next(100)<passPercent;
            var groups=new Dictionary<string,List<OffensePlay>>();
            foreach(var p in plays)if(Allowed(1,p.Type) && Passing(p.Type)==pass){
                string key=p.Formation+":"+p.Set;
                if(!groups.ContainsKey(key))groups[key]=new List<OffensePlay>();groups[key].Add(p);
            }
            if(groups.Count==0)foreach(var p in plays)if(Allowed(1,p.Type)){
                string key=p.Formation+":"+p.Set;
                if(!groups.ContainsKey(key))groups[key]=new List<OffensePlay>();groups[key].Add(p);
            }
            if(groups.Count==0)return null;
            int group=random.Next(groups.Count);
            foreach(var list in groups.Values)if(group--==0)return list[random.Next(list.Count)];
            throw new InvalidOperationException("Invalid offensive selection pool.");
        }
    }
    public sealed class DefensePlay
    {
        public int Formation, Set, Play, Type, DefensiveBacks;
        public string Name;
    }
    public static class DefenseChoice
    {
        public static bool Allowed(int formationType,int type)
        {
            if(formationType!=11)return false;
            switch(type) {
                case 31:case 33:case 34:case 36:case 42:case 43:case 44:case 45:
                case 46:case 47:case 48:case 174:case 175:case 176:case 177:
                case 178:case 179:case 180:case 181:case 182:case 183:case 184:
                case 185:case 186:case 187:case 188:case 189:case 190:case 191:
                case 192:case 194:case 199:case 210:case 211:return true;
                default:return false;
            }
        }
        public static int TargetBacks(int receivers,int down,float distance,float ball)
        {
            if(ball>=95 && distance<=3 && receivers<=2)return 3;
            int target=receivers>=5?7:receivers>=4?6:receivers==3?5:4;
            if(down>=3 && distance>=8)target=Math.Max(target,6);
            return target;
        }
        public static int PersonnelWeight(int backs,int target)
        {
            if(backs<3 || backs>8)return 0;
            if(backs==3 && target!=3)return 0;
            int gap=Math.Abs(backs-target);
            return gap==0?24:gap==1?6:gap==2?1:0;
        }
        public static int CoverageWeight(int type,int down,float distance)
        {
            bool blitz=type==31 || type==46 || type==47 || type==48 || type>=174 && type<=177;
            bool deep=type==44 || type==45 || type==183 || type==192 || type==194;
            if(type==199)return down>=3 && distance>=15?2:0;
            if(type==184)return distance<=3?8:1;
            if(down>=3 && distance>=8)return blitz?4:deep?14:8;
            if(distance<=2)return blitz?10:deep?2:8;
            return blitz?5:deep?5:10;
        }
        public static DefensePlay Pick(IList<DefensePlay> plays,int receivers,int down,float distance,float ball,Random random)
        {
            var groups=new Dictionary<string,List<DefensePlay>>();
            foreach(var p in plays)if(Allowed(11,p.Type)) {
                string key=p.Formation+":"+p.Set;
                if(!groups.ContainsKey(key))groups[key]=new List<DefensePlay>();
                groups[key].Add(p);
            }
            int target=TargetBacks(receivers,down,distance,ball),total=0;
            var eligible=new List<List<DefensePlay>>();var weights=new List<int>();
            foreach(var group in groups.Values) {
                int weight=PersonnelWeight(group[0].DefensiveBacks,target);
                bool available=false;foreach(var p in group)if(CoverageWeight(p.Type,down,distance)>0)available=true;
                if(weight>0 && available){eligible.Add(group);weights.Add(weight);total+=weight;}
            }
            // Narrow playbooks still get a normal defensive call. Never fall
            // back to the unrestricted native practice random selector.
            if(total==0)foreach(var group in groups.Values)if(group[0].DefensiveBacks>=4 && group[0].DefensiveBacks<=8){eligible.Add(group);weights.Add(1);total++;}
            if(total==0)return null;
            int roll=random.Next(total),index=0;while(roll>=weights[index])roll-=weights[index++];
            var selected=eligible[index];total=0;foreach(var p in selected)total+=CoverageWeight(p.Type,down,distance);
            if(total==0)return selected[0];
            roll=random.Next(total);foreach(var p in selected){roll-=CoverageWeight(p.Type,down,distance);if(roll<0)return p;}
            throw new InvalidOperationException("Invalid defensive selection weights.");
        }
    }
}
