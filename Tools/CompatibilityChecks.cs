using System;
using System.Collections.Generic;
using System.IO;
using SituationalPractice;

class CompatibilityChecks
{
    static int checks;
    static readonly Dictionary<long,byte[]> reference=new Dictionary<long,byte[]>();
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static byte[] Read(long address,int length,long image)
    {
        byte[] blob;
        if(!reference.TryGetValue(address-image,out blob))throw new Exception("Read outside required regions");
        Check(blob.Length==length,"Incorrect region length");
        return (byte[])blob.Clone();
    }
    static void Reject(Func<long,int,byte[]> read,long image,string expected)
    {
        bool rejected=false;
        try{NativeCompatibility.Verify(read,image);}
        catch(InvalidOperationException error){rejected=error.Message.Contains(expected) && error.Message.Contains("no hooks installed") || error.Message.Contains(expected) && error.Message.Contains("No hooks installed");}
        Check(rejected,"Did not safely reject "+expected);
    }
    static byte[] ReadCoexist(long address,int length,long image,Dictionary<long,byte[]> memory)
    {
        long rva=address-image;
        foreach(long gap in new[]{0x35ff6efL,0x7b7e964L})
            Check(rva+length<=gap || rva>=gap+5,"Verifier read a third-party call instruction");
        foreach(var block in memory){
            if(rva<block.Key || rva+length>block.Key+block.Value.Length)continue;
            byte[] result=new byte[length];Array.Copy(block.Value,(int)(rva-block.Key),result,0,length);return result;
        }
        return Read(address,length,image);
    }
    static void CheckCallSheetCompatibility()
    {
        byte[] calls=File.ReadAllBytes("Tools/callsheet-call-reference.bin");
        Check(calls.Length==10 && calls[0]==0xe8 && calls[5]==0xe8,"Expected complete native CALL instructions");
        long[] starts={0x35ff120,0x7b7e7e0},gaps={0x35ff6ef,0x7b7e964};
        int[] totals={1978,437};
        // A compatible reference capture may already contain Call Sheet's
        // redirects. These two displacements are deliberately outside our
        // contracts; only our own hook sites and the surrounding code are owned.
        foreach(long image in new[]{0x140000000L,0x7ff600000000L,0x180000000L})
        for(int patched=0;patched<4;patched++){
            var memory=new Dictionary<long,byte[]>();
            for(int index=0;index<2;index++){
                byte[] before=reference[starts[index]],after=reference[gaps[index]+5];
                Check(before.Length==(int)(gaps[index]-starts[index]) && before.Length+5+after.Length==totals[index],"Split excludes more than five bytes");
                byte[] block=new byte[totals[index]];Array.Copy(before,block,before.Length);
                Array.Copy(calls,index*5,block,before.Length,5);Array.Copy(after,0,block,before.Length+5,after.Length);
                // Simulate either/both plugins redirecting the original calls.
                // Displacements differ by ASLR base, as real wrappers do.
                if((patched&(1<<index))!=0){
                    byte[] redirected=BitConverter.GetBytes(unchecked((int)(image+0x10000*(index+1))));
                    Array.Copy(redirected,0,block,before.Length+1,4);
                }
                memory.Add(starts[index],block);
                for(int hook=0;hook<NativeRuntime.Sites.Length;hook++)
                    Check(NativeRuntime.Sites[hook]+NativeRuntime.Originals[hook].Length<=gaps[index] || NativeRuntime.Sites[hook]>=gaps[index]+5,"Third-party CALL overlaps an owned hook");
            }
            NativeCompatibility.Verify((address,length)=>ReadCoexist(address,length,image,memory),image);
            Check(true,"Original and redirected calls accepted together");
            // Changes immediately on either side of each allowed call remain
            // rejected, including when both third-party wrappers are present.
            for(int index=0;index<2;index++)foreach(int boundary in new[]{-1,5}){
                byte[] block=memory[starts[index]];int at=(int)(gaps[index]-starts[index])+boundary;
                block[at]^=0x80;
                Reject((address,length)=>ReadCoexist(address,length,image,memory),image,(boundary<0?starts[index]:gaps[index]+5).ToString("x"));
                block[at]^=0x80;
            }
        }
    }
    static void Main()
    {
        byte[] fixture=File.ReadAllBytes("Tools/compatibility-reference.bin");int offset=0;
        foreach(var region in NativeCompatibility.Regions){
            byte[] blob=new byte[region.Length];Array.Copy(fixture,offset,blob,0,blob.Length);
            reference.Add(region.Rva,blob);offset+=blob.Length;
        }
        Check(offset==fixture.Length,"Fixture boundaries differ");
        // Different ASLR bases must not change compatibility.
        foreach(long image in new[]{0x140000000L,0x7ff600000000L,0x180000000L}){
            NativeCompatibility.Verify((address,length)=>Read(address,length,image),image);
            Check(true,"Inspected layout accepted with ASLR");
        }
        long baseAddress=0x140000000L;
        // Irrelevant file bytes/headers are never requested by the verifier.
        byte[] unrelated=new byte[256];unrelated[0]=0xa5;
        NativeCompatibility.Verify((address,length)=>{if(address==baseAddress)return unrelated;return Read(address,length,baseAddress);},baseAddress);
        unrelated[0]=0x5a;
        NativeCompatibility.Verify((address,length)=>{if(address==baseAddress)return unrelated;return Read(address,length,baseAddress);},baseAddress);
        foreach(var region in NativeCompatibility.Regions){
            // Changes beyond hook prologues must also be rejected.
            foreach(int at in new[]{0,region.Length/2,region.Length-1}){
                int changedAt=at;
                Reject((address,length)=>{
                    byte[] blob=Read(address,length,baseAddress);
                    if(address-baseAddress==region.Rva)blob[changedAt]^=0x80;
                    return blob;
                },baseAddress,region.Rva.ToString("x"));
            }
            Reject((address,length)=>address-baseAddress==region.Rva?null:Read(address,length,baseAddress),baseAddress,region.Rva.ToString("x"));
            Reject((address,length)=>address-baseAddress==region.Rva?new byte[length-1]:Read(address,length,baseAddress),baseAddress,region.Rva.ToString("x"));
        }
        // Every hook and its complete overwritten instruction sequence is covered.
        for(int i=0;i<NativeRuntime.Sites.Length;i++){
            long site=NativeRuntime.Sites[i];byte[] original=NativeRuntime.Originals[i];bool found=false;
            foreach(var region in NativeCompatibility.Regions){
                if(site<region.Rva || site+original.Length>region.Rva+region.Length)continue;
                found=true;byte[] blob=reference[region.Rva];int at=(int)(site-region.Rva);
                for(int j=0;j<original.Length;j++)Check(blob[at+j]==original[j],"Hook originals differ from capture");
                Reject((address,length)=>{
                    byte[] changed=Read(address,length,baseAddress);
                    if(address-baseAddress==region.Rva)changed[at]=0xe9;
                    return changed;
                },baseAddress,region.Rva.ToString("x"));
            }
            Check(found,"Uncovered hook RVA "+site.ToString("x"));
        }
        CheckCallSheetCompatibility();
        Console.WriteLine("PASS: "+checks+" compatibility assertions; all "+NativeCompatibility.Regions.Length+" required regions, changes at start/middle/end, short/null reads, ASLR and all hooks checked.");
    }
}
