using System;
using System.Collections.Generic;
using SituationalPractice;

class FirstDownLineChecks
{
    static int checks,writes;
    const long image=0x140000000,root=0x300000000,processor=0x400000000;
    static readonly Dictionary<long,byte> memory=new Dictionary<long,byte>();
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static void Put(long address,byte[] bytes){for(int i=0;i<bytes.Length;i++)memory[address+i]=bytes[i];}
    static byte[] Read(long address,int length){var bytes=new byte[length];for(int i=0;i<length;i++)bytes[i]=memory[address+i];return bytes;}
    static void Write(long address,byte[] bytes){writes++;Check(address==processor+0x61 && bytes.Length==1,"Only the owned visibility byte may be written");Put(address,bytes);}
    static NativeFirstDownLine Setup(byte value=0)
    {
        memory.Clear();writes=0;
        Put(image+0xf8abf80,BitConverter.GetBytes(root));Put(root+0xea28,BitConverter.GetBytes(processor));
        Put(processor,BitConverter.GetBytes(image+0xb474928));Put(processor+0x20,BitConverter.GetBytes(26));
        Put(processor+0x61,new[]{value});
        return new NativeFirstDownLine(Read,Write,image,message=>{});
    }
    static NativeSnapshot Snap(float ball=35,float line=45,int down=1){return new NativeSnapshot{Ball=ball,Line=line,NativeDown=down};}
    static void Main()
    {
        var line=Setup();line.Update(Snap(),false);Check(writes==0,"Default Practice must keep native visibility");
        line.Update(Snap(),true);Check(Read(processor+0x61,1)[0]==1,"A custom drive enables the native yellow line");
        line.Update(Snap(),true);Check(writes==1,"An unchanged flag does not require repeated writes");
        Put(processor+0x61,new byte[]{0});line.Update(Snap(42,45,2),true);
        Check(Read(processor+0x61,1)[0]==1,"A native play reset cannot permanently clear the line");
        line.Update(Snap(),false);Check(Read(processor+0x61,1)[0]==0,"Returning to Default Practice restores the original flag");
        line=Setup();Put(processor+0x20,BitConverter.GetBytes(35));line.Update(Snap(42,44,4),true);
        Check(Read(processor+0x61,1)[0]==1,"The native fourth-down line style must also remain visible");
        line.Update(Snap(),false);Check(Read(processor+0x61,1)[0]==0,"Fourth-down visibility also restores in default mode");
        line=Setup();line.Update(Snap(),true);Put(processor+0x20,BitConverter.GetBytes(44));line.Update(Snap(),true);
        Check(Read(processor+0x61,1)[0]==0,"Transient line rebuilding must release its override safely");
        Put(processor+0x20,BitConverter.GetBytes(35));line.Update(Snap(42,44,4),true);
        Check(Read(processor+0x61,1)[0]==1,"A rebuilt fourth-down slot must regain visibility");
        line=Setup();line.Update(Snap(),true);line.Update(Snap(95,100),true);
        Check(Read(processor+0x61,1)[0]==0,"Goal-to-go hides the yellow line");
        line.Update(Snap(),true);line.Restore();Check(Read(processor+0x61,1)[0]==0,"Plugin shutdown restores its override");
        foreach(var bad in new[]{Snap(45,45),Snap(45,40),Snap(45,float.NaN),Snap(45,float.PositiveInfinity),Snap(35,45,0),Snap(35,45,5)}){
            line=Setup();line.Update(bad,true);Check(writes==0,"Incomplete native situations cannot enable the line");
        }
        line=Setup(1);line.Update(Snap(),true);line.Restore();Check(writes==0 && Read(processor+0x61,1)[0]==1,"Preserve an override already owned by the game or another plugin");
        line=Setup();Put(processor,BitConverter.GetBytes(image+0x12345));line.Update(Snap(),true);Check(writes==0,"Unknown processor layouts are skipped");
        line=Setup();line.Update(Snap(),true);Put(processor+0x61,new byte[]{2});line.Restore();Check(Read(processor+0x61,1)[0]==2,"Never overwrite another owner's changed value");
        line=Setup();line.Update(Snap(),true);Put(root+0xea28,BitConverter.GetBytes(0L));
        for(int i=0;i<8;i++)memory.Remove(processor+i);
        line.Restore();Check(writes==1,"Exit must not dereference or write a released processor");
        line=Setup();line.Update(Snap(),true);
        const long replacement=0x500000000;
        Put(root+0xea28,BitConverter.GetBytes(replacement));Put(replacement,BitConverter.GetBytes(image+0x12345));
        for(int i=0;i<8;i++)memory.Remove(processor+i);
        line.Update(Snap(),true);line.Restore();Check(writes==1,"Session replacement must not touch the old object");
        Console.WriteLine("PASS: "+checks+" first-down visibility, play reset, goal-to-go and ownership checks");
    }
}
