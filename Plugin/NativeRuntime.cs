using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace SituationalPractice
{
    public sealed class NativeSnapshot
    {
        public long Match, States;
        public int Phase, Possession, NativeDown, OffenseScore, DefenseScore;
        public float Ball, Line, Gain;
        public int[] PlayEvents=new int[0];
        public int Score(int team){return team==0?OffenseScore:DefenseScore;}
        public bool HasEvent(int type){return Array.IndexOf(PlayEvents,type)>=0;}
        internal static NativeSnapshot ReadStart(byte[] block,int command,out int completed)
        {
            completed=0;
            if(block==null || block.Length!=48)throw new InvalidOperationException("Invalid drive-start snapshot size.");
            if(BitConverter.ToInt32(block,44)!=command)return null;
            var snap=new NativeSnapshot{Match=BitConverter.ToInt64(block,0),States=BitConverter.ToInt64(block,8),Phase=1,
                Possession=BitConverter.ToInt32(block,16),NativeDown=BitConverter.ToInt32(block,20),
                Ball=BitConverter.ToSingle(block,24)+50,Line=BitConverter.ToSingle(block,28)+50,
                OffenseScore=BitConverter.ToInt32(block,32),DefenseScore=BitConverter.ToInt32(block,36)};
            if(snap.Match==0 || snap.States==0 || snap.Possession<0 || snap.Possession>1 || snap.NativeDown<1 || snap.NativeDown>4
                || !Scenario.Finite(snap.Ball) || !Scenario.Finite(snap.Line) || snap.Ball<=0 || snap.Ball>=100 || snap.Line<=snap.Ball || snap.Line>100)
                throw new InvalidOperationException("Invalid drive-start situation.");
            completed=BitConverter.ToInt32(block,40);return snap;
        }
    }
    public sealed class SpotEvent
    {
        public int Sequence, Phase, Down;
        public long Caller, Match;
        public float Y, OldY;
    }
    public sealed class NativeRuntime : IDisposable
    {
        private readonly Process game;
        private IntPtr handle;
        private long image, data, code;
        private int consumed;
        private byte[] hudVisibility;
        private readonly List<Patch> installed=new List<Patch>();
        private readonly Action<string> log;
        public int Pid { get { return game.Id; } }
        public long GameStart {get{return game.StartTime.ToUniversalTime().Ticks;}}
        public bool Exited { get { return game.HasExited; } }
        public int Sequence { get { return Int(data+64); } }
        public int Applied { get { return Int(data+28); } }
        public int Completed { get { return Int(data+72); } }
        public float CompletedGain { get { return Float(data+76); } }
        public void DiscardEvents(){consumed=Sequence;}
        public NativeSnapshot AppliedStart(int command,out int completed){return NativeSnapshot.ReadStart(Read(data+12400,48),command,out completed);}
        public long Controller { get { return Long(data+8); } }
        public int UserTeam {get{return Int(data+168);}}
        public int ModeRequests {get{return Int(data+12032);}}
        // Read-only, sampled by diagnostics. Never used to make gameplay decisions.
        public string DiagnosticState(NativeSnapshot snap)
        {
            byte[] block=Read(data,256);
            var text=new StringBuilder();
            text.AppendLine("image=0x"+image.ToString("x")+" code=0x"+code.ToString("x")+" code_bytes=20480 data=0x"+data.ToString("x"));
            text.AppendLine("early_picker_transitions="+Int(data+12360)+" last_endplay_caller=0x"+(Long(data+12080)-image).ToString("x")+" endplay_phase="+Int(data+12088));
            text.AppendLine("mode_requests="+ModeRequests+" possession_pending="+Int(data+12048)+" possession_target="+Int(data+12052));
            text.AppendLine("mode_menu_enabled="+Int(data+12368)+" mode_entry_pending="+Int(data+12372)+" mode_entries_applied="+Int(data+12384));
            text.AppendLine("drive_start_command="+Int(data+12444)+" drive_start_completed="+Int(data+12440));
            text.AppendLine("match=0x"+snap.Match.ToString("x")+" states=0x"+snap.States.ToString("x")+" controller=0x"+BitConverter.ToInt64(block,8).ToString("x"));
            text.AppendLine("phase="+snap.Phase+" possession="+snap.Possession+" down="+snap.NativeDown+" ball="+snap.Ball.ToString("R",CultureInfo.InvariantCulture)+" line="+snap.Line.ToString("R",CultureInfo.InvariantCulture)+" gain="+snap.Gain.ToString("R",CultureInfo.InvariantCulture)+" score="+snap.OffenseScore+":"+snap.DefenseScore);
            text.AppendLine("enabled="+BitConverter.ToInt32(block,0)+" queued_command="+BitConverter.ToInt32(block,24)+" applied_command="+BitConverter.ToInt32(block,28)+" spot_sequence="+BitConverter.ToInt32(block,64)+" completed="+BitConverter.ToInt32(block,72));
            text.AppendLine("chooser_requested="+BitConverter.ToInt32(block,164)+" user_team="+BitConverter.ToInt32(block,168)+" choice_consumed="+BitConverter.ToInt32(block,180)+" cpu_payload_ready="+BitConverter.ToInt32(block,200));
            text.AppendLine("user_identity_known="+Int(data+12012)+" cpu_readiness_mask="+Int(data+12008)+" cpu_payload_possession="+Int(data+12028)+" cpu_formation_kind="+BitConverter.ToInt32(block,220));
            text.AppendLine("cpu_choices="+BitConverter.ToInt32(block,184)+" setup_choices="+BitConverter.ToInt32(block,188)+" setup_consumed="+BitConverter.ToInt32(block,192)+" filtered_commits="+BitConverter.ToInt32(block,248)+" manual_spot_pending="+BitConverter.ToInt32(block,252));
            long manager=Long(image+0xfa582a0),service=Long(image+0xf8abb50);
            if(manager!=0){byte[] ready=Read(manager+0x50c,2);text.AppendLine("manager=0x"+manager.ToString("x")+" selected_teams="+ready[0]+","+ready[1]);}
            if(service!=0)text.AppendLine("playcall_service=0x"+service.ToString("x")+" service_flags=0x"+Int(service+0x28).ToString("x"));
            if(snap.Phase==5 || snap.Phase==6){
                long state=Long(snap.States+(snap.Phase==5?0x58:0x60));
                if(state!=0)text.AppendLine("selection_state=0x"+state.ToString("x")+" stage="+Int(state+(snap.Phase==5?0x10:0x38)));
            }
            var eventText=new List<string>();foreach(int type in snap.PlayEvents)eventText.Add(type.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("play_events="+String.Join(",",eventText.ToArray()));
            return text.ToString();
        }
        public float[] SavedPracticeSpot()
        {
            Snapshot();long configuration=Long(image+0xf6379e0);
            return new[]{Float(configuration+0x70),Float(configuration+0x74),Float(configuration+0x78)};
        }
        public static readonly long[] Sites={0x4ff39ce,0x4ff39fb,0x4ff3d66,0x5d595c0,0x369ed10,0x5d59570,0x377bb40,0x7b99d90,0x5d49e30,0x35ff120,0x7babdd0,0x35fd200,0x35feb00,0x7bcad50,0x25a3060,0x5d4a660};
        public static readonly byte[][] Originals={Hex("4038b3960000007424488b05d23c860a"),Hex("4038b3940000000f848f000000488b05a13c860a"),Hex("80bb94000000000f8490000000488b053639860a"),Hex("488bc44889580848897010574881ecd0000000"),Hex("40554883ec6048899c2480000000"),Hex("4883ec488b81c0010000c5f829742430"),Hex("40534883ec40488b01488d542430"),Hex("48895c240848896c24104889742418"),Hex("4883ec3880b9fc000000000f85ee010000"),Hex("4c8bdc5553564155498d6ba14881ecd8000000"),Hex("4883ec38c6814904000001e800bc35fc"),Hex("c5fa114c2410554157488d6c24b1"),Hex("405556488bec4883ec78488bf1e8fe984dff"),Hex("40534883ec2041b902000000488d0db5a9f103"),Hex("48895c24104889742418574883ec40"),Hex("48895c240848896c24104889742418")};
        private static readonly long[] Skips={0x4ff39fb,0x4ff3a97,0x4ff3e03};
        public NativeRuntime(int pid,Action<string> log):this(pid,log,true){}
        private NativeRuntime(int pid,Action<string> log,bool install)
        {
            this.log=log; game=Process.GetProcessById(pid);
            image=game.MainModule.BaseAddress.ToInt64(); handle=OpenProcess(0x438,false,pid);
            if(handle==IntPtr.Zero)throw new InvalidOperationException("Cannot access game: "+Marshal.GetLastWin32Error());
            try {
                if(!install)return;
                // Only inspect memory here. No EXE hash gate or remote writes.
                NativeCompatibility.Verify(Read,image);
                NativeSnapshot snap=Snapshot();
                if(Long(snap.Match)!=image+0xb4781e8 || Long(snap.States)!=image+0xb479070)
                    throw new InvalidOperationException("Native match/state layout does not match.");
                long playCall=Long(image+0xfa582a0);
                if(playCall==0 || Long(Long(playCall)+0x2c8)!=image+0x7b7e7e0)
                    throw new InvalidOperationException("Native CPU play selector does not match.");
                for(int i=0;i<Sites.Length;i++)
                    if(!Equal(Read(image+Sites[i],Originals[i].Length),Originals[i]))
                        throw new InvalidOperationException("Conflicting or unsupported code at "+Sites[i].ToString("x")+". No hooks installed.");
                log("Required native code and Dynasty practice layout verified; whole-EXE hash is not required.");
                data=VirtualAllocEx(handle,IntPtr.Zero,new UIntPtr(16384),0x3000,4).ToInt64();
                code=VirtualAllocEx(handle,IntPtr.Zero,new UIntPtr(20480),0x3000,4).ToInt64();
                if(data==0||code==0)throw new InvalidOperationException("Cannot allocate runtime.");
                for(int i=0;i<3;i++)Write(code+i*1024,BuildGate(image,data,i));
                Write(code+3072,BuildTrace(image,data));
                Write(code+4096,BuildHudEntry(image,data));
                Write(code+5120,BuildLineGuard(image,data));
                Write(code+6144,BuildStatsVisibility(image,data));
                Write(code+7168,BuildSessionStats(image,data));
                Write(code+8192,BuildReturnGuard(image,data));
                Write(code+9216,BuildPlayCallTick(image,data));
                Write(code+10240,BuildPracticePlayCallUi(image,data));
                Write(code+11264,BuildInitialPlayCallTick(image,data));
                Write(code+12288,BuildInitialSetupTick(image,data));
                Write(code+13312,BuildModeDispatch(image,data));
                Write(code+14336,BuildModeText(image,data));
                Write(code+15360,BuildEarlyPlayCall(image,data));
                Write(code+16384,BuildDefenseCommit(image,data));Write(data+12000,BitConverter.GetBytes(code+16384));
                Write(code+16896,BuildPlayCallRefresh(image,data));Write(data+12024,BitConverter.GetBytes(code+16896));
                Write(code+17920,BuildDefaultPracticeRestore(image,data));Write(data+12376,BitConverter.GetBytes(code+17920));
                uint old; if(!VirtualProtectEx(handle,new IntPtr(code),new UIntPtr(20480),0x20,out old))throw new InvalidOperationException("Cannot protect hook code.");
                FlushInstructionCache(handle,new IntPtr(code),new UIntPtr(20480));
                // Freeze threads only for the small patch transaction; always resume.
                using(var freeze=new FrozenThreads(game,image,Sites,Originals)) {
                    // Recheck ownership after suspension, immediately before writes.
                    NativeCompatibility.Verify(Read,image);
                    for(int i=0;i<Sites.Length;i++)
                        if(!Equal(Read(image+Sites[i],Originals[i].Length),Originals[i]))
                            throw new InvalidOperationException("Hook location changed before installation. No hooks installed.");
                    try { for(int i=0;i<Sites.Length;i++) {
                        var p=new Patch {Address=image+Sites[i],Original=Originals[i],Replacement=Pad(Jump(code+i*1024),Originals[i].Length)};
                        installed.Add(p); PatchBytes(p.Address,p.Replacement);
                    } } catch { foreach(var p in installed)PatchBytes(p.Address,p.Original); installed.Clear(); throw; }
                }
                log("Installed inactive practice hooks in PID "+pid+"; image=0x"+image.ToString("x")+" code=0x"+code.ToString("x")+" data=0x"+data.ToString("x")+"; awaiting practice menu binding.");
                for(int i=0;i<Sites.Length;i++)log("Hook "+i+": native RVA=0x"+Sites[i].ToString("x")+" address=0x"+(image+Sites[i]).ToString("x")+" trampoline=0x"+(code+i*1024).ToString("x"));
                log("CPU commit trampoline=0x"+(code+16384).ToString("x"));
            } catch { CloseHandle(handle); handle=IntPtr.Zero; game.Dispose(); throw; }
        }
        // Manual recovery after an interrupted helper. Requires all installed hooks
        // to be our exact generated code and the runtime to be inactive.
        public static void RecoverInactive(int pid,Action<string> log)
        {
            var r=new NativeRuntime(pid,log,false);
            try {
                var first=r.Read(r.image+Sites[0],Originals[0].Length);
                if(!Equal(Sub(first,0,6),Hex("ff2500000000")))throw new InvalidOperationException("No compatible orphan hook found.");
                r.code=BitConverter.ToInt64(first,6);
                var header=r.Read(r.code,14);
                if(!Equal(Sub(header,0,6),Hex("9c50415249ba")))throw new InvalidOperationException("Orphan hook header does not match.");
                r.data=BitConverter.ToInt64(header,6);
                if(r.Int(r.data)!=0)throw new InvalidOperationException("Runtime is active; stop its helper normally.");
                for(int i=0;i<Sites.Length;i++) {
                    var expected=i==15?BuildEarlyPlayCall(r.image,r.data):i==14?BuildModeText(r.image,r.data):i==13?BuildModeDispatch(r.image,r.data):i==12?BuildInitialSetupTick(r.image,r.data):i==11?BuildInitialPlayCallTick(r.image,r.data):i==10?BuildPracticePlayCallUi(r.image,r.data):i==9?BuildPlayCallTick(r.image,r.data):i==8?BuildReturnGuard(r.image,r.data):i==7?BuildSessionStats(r.image,r.data):i==6?BuildStatsVisibility(r.image,r.data):i==5?BuildLineGuard(r.image,r.data):i==4?BuildHudEntry(r.image,r.data):i==3?BuildTrace(r.image,r.data):BuildGate(r.image,r.data,i);
                    if(!Equal(r.Read(r.code+i*1024,expected.Length),expected))throw new InvalidOperationException("Orphan hook code does not match.");
                    r.installed.Add(new Patch{Address=r.image+Sites[i],Original=Originals[i],Replacement=Pad(Jump(r.code+i*1024),Originals[i].Length)});
                }
                var restore=BuildDefaultPracticeRestore(r.image,r.data);
                if(r.Long(r.data+12376)!=r.code+17920 || !Equal(r.Read(r.code+17920,restore.Length),restore))throw new InvalidOperationException("Default practice restoration code does not match.");
                r.Dispose();
            } finally {if(r.handle!=IntPtr.Zero){CloseHandle(r.handle);r.handle=IntPtr.Zero;r.game.Dispose();}}
        }
        public NativeSnapshot Snapshot()
        {
            long configuration=Long(image+0xf6379e0);
            if(Int(image+0xd9e3818)!=14 || Int(image+0xf3f5d48)!=3 || configuration==0 || Read(configuration+0x84,1)[0]!=1)
                throw new InvalidOperationException("Waiting for offline Dynasty practice.");
            long wrapper=Long(image+0xf8576b0), states=Long(image+0xf8575a0);
            if(wrapper==0 || states==0)throw new InvalidOperationException("Waiting for an offline Dynasty session.");
            long match=Long(wrapper+16);
            if(match==0)throw new InvalidOperationException("Waiting for an offline Dynasty session.");
            long play=Long(states+0x50);
            return new NativeSnapshot {Match=match,States=states,Phase=Int(states+0xa0),
                Possession=Int(match+0x1c8),NativeDown=Int(match+0x1d0),Ball=Float(match+0x16c)+50,Line=Float(match+0x1c0)+50,
                OffenseScore=Int(match+0x1d8),DefenseScore=Int(match+0x1dc),
                Gain=play==0?0:Float(play+0x234),PlayEvents=ReadPlayEvents()};
        }
        private int[] ReadPlayEvents()
        {
            long events=Long(image+0xf856ef8);if(events==0)return new int[0];
            int count=Int(events+0x116f8);if(count<0 || count>256)throw new InvalidOperationException("Invalid native event list.");
            var types=new List<int>();
            for(int i=count-1;i>=0;i--){int type=Int(events+0x10+i*0x110);types.Add(type);if(type==2)break;}
            return types.ToArray();
        }
        public void QueueScores(int team0,int team1)
        {
            Snapshot();if(team0<0 || team1<0)throw new InvalidOperationException("Invalid practice score.");
            Write(data+88,BitConverter.GetBytes(team0));Write(data+92,BitConverter.GetBytes(team1));Write(data+84,BitConverter.GetBytes(1));
        }
        private int statsBuffer;
        public void PublishStats(SessionStats session)
        {
            string[][] rows=session.Rows();long target=data+(statsBuffer==0?4608:8192);statsBuffer=1-statsBuffer;
            byte[] block=new byte[rows.Length*128];
            for(int row=0;row<rows.Length;row++)for(int column=0;column<3;column++){
                byte[] s=Encoding.ASCII.GetBytes(rows[row][column]);int size=column==0?64:32;
                if(s.Length>=size)throw new InvalidOperationException("Practice statistic exceeds native row size.");
                Array.Copy(s,0,block,row*128+(column==0?0:column==1?64:96),s.Length);
            }
            Write(target,block);Write(data+152,BitConverter.GetBytes(rows.Length));Write(data+144,BitConverter.GetBytes(target));
        }
        public void FindStatsMenu()
        {
            if(Long(data+120)!=0)return;
            long pause=Long(image+0xf553bb0);if(pause==0)return;
            long ui=Long(pause+0x78);if(ui==0)return;long repo=Long(ui+0xf8);if(repo==0)return;
            if(Long(repo)!=image+0xb6467d0)return;
            long begin=Long(repo+0x88),end=Long(repo+0x90);
            if(begin==0 || end<begin || end-begin>4096 || (end-begin)%16!=0)return;
            for(long entry=begin;entry<end;entry+=16){long panel=Long(entry);if(panel==0 || Long(panel)!=image+0xb646440)continue;
                string name=Encoding.ASCII.GetString(Read(panel+0x30,64)).Split('\0')[0];
                if(name=="nested_teamstats"){
                    long condition=Long(panel+0x29d8);if(condition==0)return;
                    Write(data+120,BitConverter.GetBytes(condition));log("Native Stats menu condition bound for this practice session.");return;
                }
            }
        }
        public void Queue(Scenario scenario,int command)
        { QueueScenario(scenario,command,null); }
        private void QueueScenario(Scenario scenario,int command,float[] vector,int possession=-1,bool openPlayCall=false)
        {
            scenario.Validate(); var snap=Snapshot();
            if(snap.Phase==3)throw new InvalidOperationException("Wait until the current play ends.");
            SetEnabled(false);
            Write(data+12372,BitConverter.GetBytes(0));
            Write(data+12052,BitConverter.GetBytes(possession));Write(data+12048,BitConverter.GetBytes(possession<0?0:1));
            Write(data+164,BitConverter.GetBytes(openPlayCall?1:0));
            Write(data+200,BitConverter.GetBytes(0));
            Write(data+16,BitConverter.GetBytes(snap.Match));
            Write(data+32,BitConverter.GetBytes(vector==null?0f:vector[0])); Write(data+36,BitConverter.GetBytes((float)scenario.Ball-50)); Write(data+40,BitConverter.GetBytes(vector==null?0f:vector[2]));
            Write(data+44,BitConverter.GetBytes(scenario.Down)); Write(data+48,BitConverter.GetBytes((float)(scenario.Ball-50+scenario.Distance)));
            Write(data+180,BitConverter.GetBytes(0));Write(data+192,BitConverter.GetBytes(0));Write(data+24,BitConverter.GetBytes(command)); Write(data+4,BitConverter.GetBytes(1));
            Write(data+252,BitConverter.GetBytes(vector==null?0:1));
            SetEnabled(true); log("Scenario queued for the next practice reset.");
        }
        public void QueueManualSpot(Scenario scenario,int command,float[] vector)
        {
            QueueScenario(scenario,command,vector);
            log("Re-spot drive reset queued for the first unpaused preplay update.");
        }
        public void QueuePossessionDrive(PossessionDrive drive,int command,bool openPlayCall=true)
        {
            if(drive.Possession<0 || drive.Possession>1)throw new InvalidOperationException("Invalid next possession.");
            QueueScores(drive.Score0,drive.Score1);
            QueueScenario(drive.Spot,command,new[]{0f,(float)drive.Spot.Ball-50,0f},drive.Possession,openPlayCall);
        }
        public void QueueCustomModeStart(PossessionDrive drive,int command)
        {
            QueuePossessionDrive(drive,command,false);
            Write(data+12012,BitConverter.GetBytes(0));
            // Rebuild the native picker when leaving the two-sided practice UI.
            Write(data+12372,BitConverter.GetBytes(2));
        }
        private sealed class MenuEdit {public long Owner,Address,Vtable;public byte[] Original,Replacement;}
        private readonly List<MenuEdit> menuEdits=new List<MenuEdit>();
        private long modePanel,modeTile,modeRepository;
        private readonly List<long> modeTexts=new List<long>();
        private void MenuText(long owner,long offset,long vtable,string text,int capacity)
        {
            byte[] bytes=new byte[capacity],encoded=Encoding.ASCII.GetBytes(text);
            if(encoded.Length>=capacity || Long(owner)!=image+vtable)throw new InvalidOperationException("Native mode menu layout changed.");
            Array.Copy(encoded,bytes,encoded.Length);long address=owner+offset;
            MenuEdit edit=menuEdits.Find(x=>x.Address==address);
            if(edit==null){edit=new MenuEdit{Owner=owner,Address=address,Vtable=image+vtable,Original=Read(address,capacity)};menuEdits.Add(edit);}
            if(edit.Replacement!=null && Equal(edit.Replacement,bytes))return;
            Write(address,bytes);edit.Replacement=bytes;
        }
        public void FindModeMenu(PracticeMode mode)
        {
            int phase=Snapshot().Phase;if(phase!=1 && phase!=5)return;
            long pause=Long(image+0xf553bb0);if(pause==0)return;
            long ui=Long(pause+0x78);if(ui==0)return;long repo=Long(ui+0xf8);if(repo==0 || Long(repo)!=image+0xb6467d0)return;
            if(modePanel!=0 && repo!=modeRepository){modePanel=modeTile=0;modeTexts.Clear();menuEdits.Clear();Write(data+12040,new byte[8]);Write(data+12056,new byte[16]);}
            if(modePanel==0){
                long begin=Long(repo+0x88),end=Long(repo+0x90);
                if(begin==0 || end<begin || end-begin>4096 || (end-begin)%16!=0)return;
                for(long entry=begin;entry<end;entry+=16){long panel=Long(entry);if(panel==0 || Long(panel)!=image+0xb646440)continue;
                    string name=Encoding.ASCII.GetString(Read(panel+0x30,64)).Split('\0')[0];if(name!="nested_practicetype")continue;
                    long head=panel+0x1e8;var nodes=new Stack<long>();var seenNodes=new HashSet<long>();nodes.Push(Long(head+16));
                    while(nodes.Count>0 && seenNodes.Count<64){long node=nodes.Pop();if(node==0 || node==head || !seenNodes.Add(node))continue;
                        nodes.Push(Long(node));nodes.Push(Long(node+8));long container=Long(node+0x38);
                        if(container==0 || Long(container)!=image+0xb646318)continue;
                        long tiles=Long(container+0xc58),tilesEnd=Long(container+0xc60);
                        if(tiles==0 || tilesEnd<tiles || tilesEnd-tiles>8192 || (tilesEnd-tiles)%16!=0)continue;
                        for(long t=tiles;t<tilesEnd;t+=16){long tile=Long(t);if(tile==0 || Long(tile)!=image+0xb6465a0)continue;
                            string action=Encoding.ASCII.GetString(Read(tile+0xd50,60)).Split('\0')[0];if(action!="PRACTICE_CHOOSE_TYPE")continue;
                            long texts=Long(tile+0xbf8),textsEnd=Long(tile+0xc00);
                            if(texts==0 || textsEnd-texts<32 || textsEnd-texts>256 || (textsEnd-texts)%16!=0)continue;
                            long title=Long(texts),descriptionText=Long(texts+16);
                            if(Long(title)!=image+0xb646598 || Long(descriptionText)!=image+0xb646598 || Int(title+0x188)!=0 || Int(descriptionText+0x188)!=0)continue;
                            long condition=Long(panel+0x29d8);if(condition==0)continue;
                            modePanel=panel;modeTile=tile;modeRepository=repo;modeTexts.Add(title);modeTexts.Add(descriptionText);
                            MenuText(tile,0xd50,0xb6465a0,"CHALLENGE_PLAY",60);
                            Write(data+12040,BitConverter.GetBytes(condition));log("Native practice mode toggle bound to pause menu.");break;
                        }
                    }
                    break;
                }
            }
            if(modePanel==0)return;
            string label=PracticeModes.Label(mode);
            string description=PracticeModes.Description(mode);
            MenuText(modePanel,0x70,0xb646440,label,64);
            // Native data binding refreshes these TextMedia objects on every
            // menu draw. Substitute only their serialized values, after that
            // refresh, rather than competing with its writes from this loop.
            byte[] labelBytes=new byte[64],descriptionBytes=new byte[192];
            Encoding.ASCII.GetBytes(label).CopyTo(labelBytes,0);Encoding.ASCII.GetBytes(description).CopyTo(descriptionBytes,0);
            Write(data+12100,labelBytes);Write(data+12164,descriptionBytes);
            if(Long(modeTexts[0])!=image+0xb646598 || Long(modeTexts[1])!=image+0xb646598)
                throw new InvalidOperationException("Native mode menu text ownership changed.");
            Write(data+12056,BitConverter.GetBytes(modeTexts[0]+0x38));Write(data+12064,BitConverter.GetBytes(modeTexts[1]+0x38));
        }
        public void ConfigureUserTeam(int userTeam)
        {
            if(userTeam<0 || userTeam>1)throw new InvalidOperationException("Invalid user team.");
            Write(data+168,BitConverter.GetBytes(userTeam));
        }
        public void BindPracticeMenus()
        {
            var snap=Snapshot();Write(data+16,BitConverter.GetBytes(snap.Match));
            // The selector stays available while all gameplay hooks are inactive.
            Write(data+12368,BitConverter.GetBytes(1));
        }
        public void UseDefaultPractice(bool restoreSelection)
        {
            SetEnabled(false);
            foreach(int offset in new[]{4,84,164,180,192,200,252,12012,12016,12048,12372})Write(data+offset,BitConverter.GetBytes(0));
            RequestNativeHud(false);
            if(restoreSelection){
                var vector=SavedPracticeSpot();var spot=NativeDrivePolicy.StartingSpot(new NativeSnapshot{Ball=vector[1]+50},false);
                Write(data+32,BitConverter.GetBytes(vector[0]));Write(data+36,BitConverter.GetBytes(vector[1]));Write(data+40,BitConverter.GetBytes(vector[2]));
                Write(data+44,BitConverter.GetBytes(1));Write(data+48,BitConverter.GetBytes((float)(spot.Ball-50+spot.Distance)));
                QueueScores(0,0);
                // The game thread restores the normal two-sided practice picker.
                Write(data+12372,BitConverter.GetBytes(1));
            }
            log("Default Practice: native ball reset and play selection; custom gameplay disabled.");
        }
        public void QueuePlayCall(int userTeam)
        {
            ConfigureUserTeam(userTeam);Write(data+180,BitConverter.GetBytes(0));Write(data+200,BitConverter.GetBytes(0));Write(data+164,BitConverter.GetBytes(1));
        }
        public void RefreshPlayCall(){Write(data+12016,BitConverter.GetBytes(1));}
        public void ResumeExistingSession(int userTeam)
        {
            var snap=Snapshot();if(snap.Phase==3)throw new InvalidOperationException("Wait until the current play ends.");
            Write(data+16,BitConverter.GetBytes(snap.Match));Write(data+8,BitConverter.GetBytes(Long(image+0xf6379e0)));
            ConfigureUserTeam(userTeam);Write(data+12012,BitConverter.GetBytes(1));SetEnabled(true);
        }
        private readonly Random defenseRandom=new Random();
        private long cachedDefenseRoot;
        private List<DefensePlay> defensiveBook;
        private long cachedOffenseRoot;
        private List<OffensePlay> offensiveBook;
        private long TreeFind(long root,int key)
        {
            for(int i=0;root!=0 && i<128;i++) {int current=Int(root+0x20);if(current==key)return root;root=Long(root+(key<current?8:0));}
            return 0;
        }
        private List<long> TreeNodes(long root)
        {
            var result=new List<long>();var stack=new Stack<long>();var seen=new HashSet<long>();stack.Push(root);
            while(stack.Count>0){long node=stack.Pop();if(node==0)continue;if(!seen.Add(node) || seen.Count>4096)throw new InvalidOperationException("Invalid native playbook tree.");result.Add(node);stack.Push(Long(node));stack.Push(Long(node+8));}
            return result;
        }
        private long NativeSet(long db,int team,int type,int form,int set)
        {
            long n=TreeFind(Long(db+0x320+team*0x30),type);if(n==0)return 0;
            n=TreeFind(Long(n+0x38),form);if(n==0)return 0;
            n=TreeFind(Long(n+0x38),set);return n==0?0:n+0x28;
        }
        private int CountPersonnel(long set,int package,bool defense)
        {
            if(set==0)return defense?-1:2;
            long begin=Long(set+0x2d0),end=Long(set+0x2d8);
            if(begin==0 || end<begin || end-begin>65536 || (end-begin)%64!=0)return defense?-1:2;
            int count=(int)((end-begin)/64);if(package<0 || package>=count)package=Int(set+0x248);
            if(package<0 || package>=count)return defense?-1:2;
            long entry=begin+package*64,players=Long(entry),playersEnd=Long(entry+8);
            if(players==0 || playersEnd-players!=11*80)return defense?-1:2;
            int found=0;for(int i=0;i<11;i++){int position=Int(players+i*80+0x28);if(defense?(position>=16 && position<=18 || position==34):(position==3 || position==28))found++;}
            return found;
        }
        public void PrepareOpponent(NativeSnapshot snap,Scenario nextSituation=null)
        {
            if(snap.Match!=Long(data+16) || snap.Phase!=5 || Int(data+180)!=0 || Int(data+200)!=0)return;
            long state=Long(snap.States+0x58);if(state==0 || Int(state+0x10)!=2)return;
            long manager=Long(image+0xfa582a0),service=Long(image+0xf8abb50),bus=Long(image+0xe2cb918);
            if(manager==0 || service==0 || bus==0)return;
            int flags=Int(service+0x28),user;
            if(!PracticeSide.TryUserTeam(snap.Possession,flags,Int(data+12012)!=0?UserTeam:-1,out user))return;
            bool userOffense=user==snap.Possession;
            if(manager==0 || service==0 || bus==0 || Read(manager+0x50c+user,1)[0]==0)return;
            // Native timeout/Choose New Play can retain last snap's manager
            // byte. Wait for this user's current service notification.
            byte[] offense=Read(service+0x70+user*36,20);
            // Submitted PlayData starts with formation type, then form/set/play;
            // the registration API's lookup keys use form/set/play/type order.
            if(BitConverter.ToInt32(offense,0)!=(userOffense?1:11))return;
            ConfigureUserTeam(user);
            int callDown=nextSituation==null?snap.NativeDown:nextSituation.Down;
            float callBall=nextSituation==null?snap.Ball:(float)nextSituation.Ball;
            float callDistance=nextSituation==null?Math.Max(.1f,snap.Line-snap.Ball):(float)nextSituation.Distance;
            long db=bus+0x1d18;int team=1-user;
            if(!userOffense){
                long offensiveRoot=TreeFind(Long(db+0x20+team*0x30),1);if(offensiveRoot==0)return;
                if(offensiveBook==null || cachedOffenseRoot!=offensiveRoot){
                    var book=new List<OffensePlay>();
                    foreach(long form in TreeNodes(Long(offensiveRoot+0x38)))foreach(long set in TreeNodes(Long(form+0x38)))
                        foreach(long play in TreeNodes(Long(set+0x38))){
                            long record=Long(play+0x30);int type=Int(record+0x2c);
                            if(OffenseChoice.Allowed(1,type))book.Add(new OffensePlay{Formation=Int(form+0x20),Set=Int(set+0x20),Play=Int(play+0x20),Type=type,Name=NativeString(record+8)});
                        }
                    offensiveBook=book;cachedOffenseRoot=offensiveRoot;log("CPU offense book: "+book.Count+" normal run/pass plays; special teams, kneels and spikes excluded.");
                }
                float toGo=callDistance;
                var chosen=OffenseChoice.Pick(offensiveBook,callDown,toGo,callBall,defenseRandom);if(chosen==null)return;
                if(PublishOpponent(snap,service,user,flags,offense,chosen.Formation,chosen.Set,chosen.Play,1))
                    log("CPU offense: "+chosen.Name+"; down "+callDown+", to go "+toGo.ToString("0.0")+"; user defending as team "+user);
                return;
            }
            long root=TreeFind(Long(db+0x20+team*0x30),11);if(root==0)return;
            if(defensiveBook==null || root!=cachedDefenseRoot) {
                var book=new List<DefensePlay>();
                foreach(long form in TreeNodes(Long(root+0x38)))foreach(long set in TreeNodes(Long(form+0x38))) {
                    int f=Int(form+0x20),s=Int(set+0x20),backs=CountPersonnel(NativeSet(db,team,11,f,s),-1,true);
                    foreach(long play in TreeNodes(Long(set+0x38))) {
                        long record=Long(play+0x30);int type=Int(record+0x30);
                        if(DefenseChoice.Allowed(11,type))book.Add(new DefensePlay{Formation=f,Set=s,Play=Int(play+0x20),Type=type,DefensiveBacks=backs,Name=NativeString(record+8)});
                    }
                }
                defensiveBook=book;cachedDefenseRoot=root;log("CPU defense book: "+book.Count+" normal plays; special teams excluded.");
            }
            int receivers=CountPersonnel(NativeSet(db,user,1,BitConverter.ToInt32(offense,4),BitConverter.ToInt32(offense,8)),BitConverter.ToInt32(offense,16),false);
            float distance=callDistance;
            var choice=DefenseChoice.Pick(defensiveBook,receivers,callDown,distance,callBall,defenseRandom);if(choice==null)return;
            // Publish the payload before the ready word; the game-thread hook
            // also verifies that the offensive selection still matches.
            if(PublishOpponent(snap,service,user,flags,offense,choice.Formation,choice.Set,choice.Play,11))
                log("CPU defense: "+choice.Name+"; "+choice.DefensiveBacks+" DB vs "+receivers+" WR; down "+callDown+", to go "+distance.ToString("0.0"));
        }
        private bool PublishOpponent(NativeSnapshot snap,long service,int user,int flags,byte[] selection,int formation,int set,int play,int kind)
        {
            if(Int(snap.States+0xa0)!=5 || (Int(service+0x28)&12)!=(flags&12) || Int(snap.Match+0x1c8)!=snap.Possession || !Equal(selection,Read(service+0x70+user*36,20)))return false;
            byte[] keys=new byte[20];Array.Copy(BitConverter.GetBytes(formation),0,keys,0,4);Array.Copy(BitConverter.GetBytes(set),0,keys,4,4);Array.Copy(BitConverter.GetBytes(play),0,keys,8,4);Array.Copy(BitConverter.GetBytes(kind),0,keys,12,4);
            Write(data+204,BitConverter.GetBytes(user));Write(data+208,keys);Write(data+228,selection);Write(data+12008,BitConverter.GetBytes(flags&12));Write(data+12028,BitConverter.GetBytes(snap.Possession));Write(data+200,BitConverter.GetBytes(1));
            return true;
        }
        private string NativeString(long address)
        {
            byte[] local=Read(address,16);return Encoding.UTF8.GetString(Read((local[15]&128)!=0?BitConverter.ToInt64(local,0):address,128)).Split('\0')[0];
        }
        public void SetEnabled(bool enabled) { if(data!=0)Write(data,BitConverter.GetBytes(enabled?1:0)); }
        public void RequestNativeHud(bool enabled)
        {
            Snapshot();
            if(enabled && hudVisibility==null) {
                hudVisibility=new byte[]{Read(image+0xdadd921,1)[0],Read(image+0xdadd924,1)[0]};
                Write(image+0xdadd921,new byte[]{0});Write(image+0xdadd924,new byte[]{0});
                log("Native score-HUD and down/distance suppression flags cleared for this practice session.");
            }
            Write(data+80,BitConverter.GetBytes(enabled?1:0));
            if(!enabled)RestoreHudVisibility();
        }
        private void RestoreHudVisibility()
        {
            if(hudVisibility==null)return;
            if(Read(image+0xdadd921,1)[0]==0)Write(image+0xdadd921,new byte[]{hudVisibility[0]});
            if(Read(image+0xdadd924,1)[0]==0)Write(image+0xdadd924,new byte[]{hudVisibility[1]});
            hudVisibility=null;
        }
        public static byte[] BuildHudEntry(long image,long data)
        {
            var b=new Emitter();b.H("9c50415249ba");b.Q(data);
            b.H("41833a01");b.J("0f85","native");
            b.H("41837a5001");b.J("0f85","native");
            b.H("83fa14");b.J("0f85","native");
            b.H("48b8");b.Q(image+0xd9e3818);b.H("83380e");b.J("0f85","native");
            b.H("48b8");b.Q(image+0xf3f5d48);b.H("833803");b.J("0f85","native");
            b.H("48b8");b.Q(image+0xf6379e0);b.H("488b004885c0");b.J("0f84","native");
            b.H("80b88400000001");b.J("0f85","native");
            b.H("48b8");b.Q(image+0xf8576b0);b.H("488b004885c0");b.J("0f84","native");
            b.H("488b4010493b4210");b.J("0f85","native");
            // The native pause manager's first byte is the active pause flag.
            b.H("48b8");b.Q(image+0xf547a00);b.H("488b004885c0");b.J("0f84","phase");
            b.H("803800");b.J("0f85","native");
            b.Label("phase");b.H("48b8");b.Q(image+0xf8575a0);b.H("488b004885c0");b.J("0f84","native");
            b.H("8b80a000000083f801");b.J("0f84","preplay");
            b.H("83f803");b.J("0f84","play");b.H("83f804");b.J("0f85","native");
            b.H("ba04000000");b.J("e9","selected");
            b.Label("preplay");b.H("ba02000000");b.J("e9","selected");
            b.Label("play");b.H("ba03000000");
            b.Label("selected");b.H("f041ff426c");
            b.Label("native");b.H("415a589d");b.Raw(Originals[4]);b.Raw(Jump(image+Sites[4]+Originals[4].Length));return b.Finish();
        }
        public static byte[] BuildLineGuard(long image,long data)
        {
            var b=new Emitter();b.H("9c50415249ba");b.Q(data);
            b.H("41833a01");b.J("0f85","native");
            b.H("493b4a10");b.J("0f85","native");
            b.H("48b8");b.Q(image+0xd9e3818);b.H("83380e");b.J("0f85","native");
            b.H("48b8");b.Q(image+0xf3f5d48);b.H("833803");b.J("0f85","native");
            // Suppress only the practice controller's unconditional target reset.
            // Normal first-down, goal-to-go and scenario line setters still run.
            b.H("48b8");b.Q(image+0x4ff4133);b.H("4839442418");b.J("0f85","native");
            b.H("f041ff4270415a589dc3");
            b.Label("native");b.H("415a589d");b.Raw(Originals[5]);b.Raw(Jump(image+Sites[5]+Originals[5].Length));return b.Finish();
        }
        private static Emitter StatsScope(long image,long data,bool menu=false,bool restore=false)
        {
            var b=new Emitter();
            if(restore){
                b.H("9c5048b8");b.Q(data+12376);b.H("488b004885c0");b.J("0f84","restoreDone");
                b.H("4883ec28ffd04883c428");b.Label("restoreDone");b.H("589d");
            }
            b.H("9c50415249ba");b.Q(data);
            b.H("41833a01");
            if(menu){b.J("0f84","menuEnabled");b.H("4183ba");b.I(12368);b.H("01");}
            b.J("0f85","native");
            if(menu)b.Label("menuEnabled");
            b.H("48b8");b.Q(image+0xd9e3818);b.H("83380e");b.J("0f85","native");
            b.H("48b8");b.Q(image+0xf3f5d48);b.H("833803");b.J("0f85","native");
            b.H("48b8");b.Q(image+0xf6379e0);b.H("488b004885c0");b.J("0f84","native");
            b.H("80b88400000001");b.J("0f85","native");
            b.H("48b8");b.Q(image+0xf8576b0);b.H("488b004885c0");b.J("0f84","native");
            b.H("488b4010493b4210");b.J("0f85","native");return b;
        }
        public static byte[] BuildReturnGuard(long image,long data)
        {
            var b=StatsScope(image,data);
            b.H("493b4a10");b.J("0f85","native");
            // Diagnostic only: record the caller before the native end-play transition.
            b.H("488b442418498982");b.I(12080);
            b.H("48b8");b.Q(image+0xf8575a0);b.H("488b008b80a0000000418982");b.I(12088);
            b.H("80b9f101000000");b.J("0f85","native"); // Never suppress a scored play.
            b.H("48b8");b.Q(image+0xf8575a0);b.H("488b004885c0");b.J("0f84","native");
            b.H("83b8a000000003");b.J("0f85","native");
            b.H("48b8");b.Q(image+0x5d5ad16);b.H("4839442418");b.J("0f84","skip");
            b.H("48b8");b.Q(image+0x5d72356);b.H("4839442418");b.J("0f85","native");
            b.H("8b81c80100003b81cc010000");b.J("0f84","native");
            b.Label("skip");b.H("f041ff82a0000000415a589dc3");
            // Relocate the original conditional branch; its displacement cannot
            // be copied verbatim into the remote allocation.
            b.Label("native");b.H("415a589d");b.Raw(Sub(Originals[8],0,11));b.J("0f85","alreadyEnding");
            b.Raw(Jump(image+Sites[8]+Originals[8].Length));
            b.Label("alreadyEnding");b.Raw(Jump(image+0x5d4a02f));return b.Finish();
        }
        public static byte[] BuildPracticePlayCallUi(long image,long data)
        {
            var b=StatsScope(image,data);
            b.H("48b8");b.Q(image+0xdadd380);b.H("483bc8");b.J("0f85","native");
            b.H("48b8");b.Q(image+0xf8575a0);b.H("488b004885c0");b.J("0f84","native");
            b.H("83b8a000000005");b.J("0f85","native");
            b.H("f041ff82b0000000415a589d33d2");b.Raw(Jump(image+0x7babb30));
            // State entry also sends a practice notification. Redirect it before
            // the UI queues the opposite side, preserving the normal UI flow.
            b.Label("native");b.H("415a589d");b.Raw(Sub(Originals[10],0,11));
            b.H("ff1502000000eb08");b.Q(image+0x3f079e0);b.Raw(Jump(image+Sites[10]+Originals[10].Length));return b.Finish();
        }
        public static byte[] BuildInitialPlayCallTick(long image,long data)
        {
            return BuildInitialChoiceTick(image,data,false);
        }
        public static byte[] BuildInitialSetupTick(long image,long data)
        {
            return BuildInitialChoiceTick(image,data,true);
        }
        private static byte[] BuildInitialChoiceTick(long image,long data,bool setup)
        {
            var b=StatsScope(image,data,false,!setup);
            if(setup){b.H("41837a0401");b.J("0f85","native");}
            if(setup){b.H("4183bac000000000");b.J("0f85","native");}
            b.H("48b8");b.Q(image+0xf8575a0);b.H("488b004885c0");b.J("0f84","native");
            b.H(setup?"83b8a000000006":"83b8a000000005");b.J("0f85","native");
            b.H(setup?"483b4860":"483b4858");b.J("0f85","native");
            // Native startup stages can clear choices between player setup and
            // play calling, so each state has its own one-shot flag.
            // Native stages 0/1 initialize the service and clear prior choices.
            // Stage 2 is the actual selection wait, after those resets finish.
            if(setup){b.H("8b413883e80283f801");b.J("0f87","native");}
            else {
                // Timeout and Pause -> Choose New Play enter the same native
                // state independently of our end-of-play queue. Each native
                // initialization must start a fresh one-shot CPU request.
                b.H("83791001");b.J("0f87","selection");
                b.H("41c782b40000000000000041c782c80000000000000041c782a400000000000000");b.J("e9","native");
                b.Label("selection");b.H("83791002");b.J("0f85","native");
                b.H("4183ba");b.I(12016);b.H("01");b.J("0f85","registered");
                b.H("4883ec20498b82");b.I(12024);b.H("ffd04883c420");b.J("e9","native");
                b.Label("registered");
                b.H("4183bab400000000");b.J("0f85","native");
            }
            // Fresh practice can change possession after helper attachment.
            // At the actual selection wait, use the initialized match's side.
            if(!setup){
                b.H("4183ba");b.I(12012);b.H("00");b.J("0f85","sideReady");
                b.H("498b42108b80c801000083f801");b.J("0f87","native");
                b.H("5248ba");b.Q(image+0xf8abb50);b.H("488b124885d2");b.J("0f84","unknownSide");
                b.H("8b522883e20c83fa04");b.J("0f84","userOffense");
                b.H("83fa08");b.J("0f85","unknownSide");b.H("83f001");
                b.Label("userOffense");b.H("418982a80000005a");b.J("e9","sideReady");
                b.Label("unknownSide");b.H("5a41c782c800000000000000");b.J("e9","native");
                b.Label("sideReady");
            }
            b.H("48b8");b.Q(image+0xfa582a0);b.H("488b004885c0");b.J("0f84","native");
            b.H("4183baa800000001");b.J("0f87","native");
            // The first UI can clear an opponent choice made while waiting for
            // the user. Supply it only after the user's native choice exists.
            if(!setup){b.H("52418b92a800000080bc100c050000005a");b.J("0f84","native");}
            if(!setup) {
                b.H("4183bac800000001");b.J("0f85","native");
                b.H("52418b92a8000000413b92cc000000");b.J("0f85","stale");
                b.H("48b8");b.Q(image+0xf8abb50);b.H("488b004885c0");b.J("0f84","stale");
                b.H("50498b42108b80c8010000413b82");b.I(12028);b.H("58");b.J("0f85","stale");
                b.H("528b502883e20c413b92");b.I(12008);b.H("5a");b.J("0f85","stale");
                b.H("486bd224488d441070");
                for(int i=0;i<5;i++){b.H("8b50");b.B((byte)(i*4));b.H("413b92");b.I(228+i*4);b.J("0f85","stale");}
                // Restore RAX to the native play-call manager before committing.
                b.H("5a48b8");b.Q(image+0xfa582a0);b.H("488b00");
            }
            b.H(setup?"41c782c000000001000000":"41c782b400000001000000");
            if(!setup){b.H("41c782");b.I(12012);b.I(1);}
            b.H("51524150415141534881ec88000000");
            for(int x=0;x<6;x++){b.H("f30f7f");b.B((byte)(0x84+x*8));b.H("24");b.I(32+x*16);}
            b.H("488bc8ba01000000412b92a8000000");
            if(setup){b.H("48b8");b.Q(image+0x7b7e7e0);}
            else b.H("498b82e02e0000");
            b.H("ffd0");
            b.H("49ba");b.Q(data);b.H("f041ff82b8000000");
            if(setup)b.H("f041ff82bc000000");
            for(int x=0;x<6;x++){b.H("f30f6f");b.B((byte)(0x84+x*8));b.H("24");b.I(32+x*16);}
            b.H("4881c488000000415b415941585a59");
            if(!setup){b.J("e9","native");b.Label("stale");b.H("5a41c782c800000000000000");}
            b.Label("native");b.H("415a589d");
            if(setup){b.Raw(Sub(Originals[12],0,13));b.H("ff1502000000eb08");b.Q(image+0x2ad8410);b.Raw(Jump(image+Sites[12]+Originals[12].Length));}
            else {b.Raw(Originals[11]);b.Raw(Jump(image+Sites[11]+Originals[11].Length));}
            return b.Finish();
        }
        public static byte[] BuildDefenseCommit(long image,long data)
        {
            // RCX manager, EDX CPU side. Use the same native registration and
            // service submission as CallRandomPlay, with our filtered keys.
            var b=new Emitter();b.H("53564881ec88000000488bd94863f2");
            b.H("48b9");b.Q(image+0xf6379e0);b.H("488b098bd641b80100000041b9ffffffffc7442420ffffffff48b8");b.Q(image+0x4ff5520);b.H("ffd0");
            b.H("48b8");b.Q(image+0xae33f50);b.H("f30f6f00f30f7f44243048c74424400000000066c74424480000c744244ce7030000c7442450ffffffff");
            b.H("48b9");b.Q(image+0xe2cb918);b.H("488b094c8d44243049b9");b.Q(data+208);b.H("8bd6c7442420ffffffff488b0148b8");b.Q(image+0xe2cb918);b.H("488b00488b00ff9018040000");
            b.H("48b9");b.Q(image+0xf8abb50);b.H("488b098bd64c8d4424304c8d4c243048b8");b.Q(image+0x5f09980);b.H("ffd0c684330c0500000148b8");b.Q(data);b.H("c780c800000000000000f0ff80f80000004881c4880000005e5bc3");return b.Finish();
        }
        public static byte[] BuildPlayCallRefresh(long image,long data)
        {
            // Explicit one-shot development recovery on the game update thread.
            // Reopen the native picker; never fabricate a completed selection.
            var b=new Emitter();b.H("9c50515241504151415241534881ec88000000");
            for(int x=0;x<6;x++){b.H("f30f7f");b.B((byte)(0x84+x*8));b.H("24");b.I(32+x*16);}
            b.H("49ba");b.Q(data);b.H("41c782");b.I(12016);b.I(0);
            b.H("41c782a40000000000000048b9");b.Q(image+0xdadd380);b.H("33d248b8");b.Q(image+0x7babb30);b.H("ffd0");
            for(int x=0;x<6;x++){b.H("f30f6f");b.B((byte)(0x84+x*8));b.H("24");b.I(32+x*16);}
            b.H("4881c488000000415b415a415941585a59589dc3");return b.Finish();
        }
        // Native player pre-snap penalties run before the normal preplay UI
        // tick when possession changes with the previous alignment still on
        // the field. Complete an already requested picker transition here,
        // after the reset is applied, before those players can be penalized.
        public static byte[] BuildEarlyPlayCall(long image,long data)
        {
            var b=StatsScope(image,data,false,true);
            b.H("48b8");b.Q(image+0xf8575a0);b.H("488b004885c0");b.J("0f84","native");
            b.H("83b8a000000001");b.J("0f85","native");
            b.H("48b8");b.Q(image+0xf547a00);b.H("488b004885c0");b.J("0f84","unpaused");
            b.H("803800");b.J("0f85","native");
            b.Label("unpaused");
            b.H("4183baa400000001");b.J("0f85","native");
            b.H("41837a0400");b.J("0f85","native");
            b.H("48b8");b.Q(image+0xfa582a0);b.H("488b004885c0");b.J("0f84","native");
            b.H("4183baa800000001");b.J("0f87","native");
            b.H("41c782a40000000000000051524150415141534881ec88000000");
            for(int x=0;x<6;x++){b.H("f30f7f");b.B((byte)(0x84+x*8));b.H("24");b.I(32+x*16);}
            // Enter the complete native state first: it initializes play calling
            // and routes controller input away from the players on the field.
            b.H("48b8");b.Q(image+0xf8575a0);b.H("488b08ba0500000048b8");b.Q(image+0x5d0e950);b.H("ffd0");
            // The CPU choice is committed by the phase-5 wait hook after the
            // user picks this snap's offense, rather than before opening UI.
            // The ordinary notification avoids the practice service's second
            // user-controlled play-call screen. CPU choice uses its playbook.
            b.H("48b9");b.Q(image+0xdadd380);b.H("33d248b8");b.Q(image+0x7babb30);b.H("ffd0");
            b.H("49ba");b.Q(data);b.H("f041ff82ac000000f041ff82");b.I(12360);
            for(int x=0;x<6;x++){b.H("f30f6f");b.B((byte)(0x84+x*8));b.H("24");b.I(32+x*16);}
            b.H("4881c488000000415b415941585a59415a589dc3");
            b.Label("native");b.H("415a589d");b.Raw(Originals[15]);b.Raw(Jump(image+Sites[15]+Originals[15].Length));return b.Finish();
        }
        public static byte[] BuildPlayCallTick(long image,long data)
        {
            var b=StatsScope(image,data);
            b.H("48b8");b.Q(image+0xf8575a0);b.H("488b004885c0");b.J("0f84","native");
            b.H("83b8a000000001");b.J("0f85","native");
            b.H("48b8");b.Q(image+0xf547a00);b.H("488b004885c0");b.J("0f84","unpaused");
            b.H("803800");b.J("0f85","native");
            b.Label("unpaused");
            b.H("4183bafc00000001");b.J("0f85","playcall");
            b.H("41837a0401");b.J("0f84","manual");
            b.Label("playcall");
            b.H("4183baa400000001");b.J("0f85","native");
            b.H("41837a0400");b.J("0f85","native");
            b.H("48b8");b.Q(image+0xfa582a0);b.H("488b004885c0");b.J("0f84","native");
            b.H("4183baa800000001");b.J("0f87","native");
            b.H("41c782a40000000000000051524150415141534881ec88000000");
            for(int x=0;x<6;x++){b.H("f30f7f");b.B((byte)(0x84+x*8));b.H("24");b.I(32+x*16);}
            // Enter the complete native state first: it initializes play calling
            // and routes controller input away from the players on the field.
            b.H("48b8");b.Q(image+0xf8575a0);b.H("488b08ba0500000048b8");b.Q(image+0x5d0e950);b.H("ffd0");
            // The CPU choice is committed by the phase-5 wait hook after the
            // user picks this snap's offense, rather than before opening UI.
            // The ordinary notification avoids the practice service's second
            // user-controlled play-call screen. CPU choice uses its playbook.
            b.H("48b9");b.Q(image+0xdadd380);b.H("33d248b8");b.Q(image+0x7babb30);b.H("ffd0");
            b.H("49ba");b.Q(data);b.H("f041ff82ac000000");
            for(int x=0;x<6;x++){b.H("f30f6f");b.B((byte)(0x84+x*8));b.H("24");b.I(32+x*16);}
            b.H("4881c488000000415b415941585a59415a589dc3");
            b.Label("manual");
            // Apply menu spotting on the game thread before the next snap;
            // no synthetic play result or score change is generated.
            b.H("41c742040000000041c782fc0000000000000051524150415141534881ec88000000");
            for(int x=0;x<6;x++){b.H("f30f7f");b.B((byte)(0x84+x*8));b.H("24");b.I(32+x*16);}
            EmitPossession(b,image,data);
            b.H("498b4a10418b522c48b8");b.Q(image+0x5d59530);b.H("ffd0");
            b.H("49ba");b.Q(data);b.H("498b4a10f3410f104a3048b8");b.Q(image+0x5d59570);b.H("ffd0");
            b.H("49ba");b.Q(data);b.H("498b4a10498d522041b00148b8");b.Q(image+0x5d595c0);b.H("ffd0");
            EmitScores(b,image,data);
            b.H("49ba");b.Q(data);b.H("418b42184189421c");
            for(int x=0;x<6;x++){b.H("f30f6f");b.B((byte)(0x84+x*8));b.H("24");b.I(32+x*16);}
            b.H("4881c488000000415b415941585a59");
            // A possession handoff must enter the chooser in this same
            // preplay update, before native practice resumes the old play.
            b.J("e9","playcall");
            b.Label("native");b.H("415a589d");b.Raw(Originals[9]);b.Raw(Jump(image+Sites[9]+Originals[9].Length));return b.Finish();
        }
        public static byte[] BuildStatsVisibility(long image,long data)
        {
            var b=StatsScope(image,data,true);
            b.H("493b8a");b.I(12040);b.J("0f84","visible");
            b.H("41833a01");b.J("0f85","native");
            b.H("493b4a78");b.J("0f85","native");
            b.Label("visible");
            b.H("f041ff8280000000415a589db801000000c3");
            b.Label("native");b.H("415a589d");b.Raw(Originals[6]);b.Raw(Jump(image+Sites[6]+Originals[6].Length));return b.Finish();
        }
        public static byte[] BuildModeDispatch(long image,long data)
        {
            var b=StatsScope(image,data,true);
            b.H("4983ba");b.I(12040);b.H("00");b.J("0f84","native");
            // This adapter action is unused in Dynasty practice. The custom
            // tile invokes it and its native script resumes the paused game.
            b.H("f041ff82");b.I(12032);b.H("415a589d");b.Raw(Jump(image+0x7c17d20));
            b.Label("native");b.H("415a589d");b.Raw(Sub(Originals[13],0,12));
            b.H("48b9");b.Q(image+0xbae5718);b.Raw(Jump(image+Sites[13]+Originals[13].Length));return b.Finish();
        }
        public static byte[] BuildModeText(long image,long data)
        {
            var b=StatsScope(image,data,true);
            b.H("4d3b82");b.I(12056);b.J("0f85","description");
            b.H("4d8d82");b.I(12100);b.J("e9","native");
            b.Label("description");b.H("4d3b82");b.I(12064);b.J("0f85","native");
            b.H("4d8d82");b.I(12164);
            b.Label("native");b.H("415a589d");b.Raw(Originals[14]);b.Raw(Jump(image+Sites[14]+Originals[14].Length));return b.Finish();
        }
        public static byte[] BuildDefaultPracticeRestore(long image,long data)
        {
            var b=StatsScope(image,data,true);
            // 1 returns to native practice; 2 enters a custom mode from native
            // practice. Check the requested activation before changing any UI.
            b.H("418b82");b.I(12372);b.H("83f801");b.J("0f85","customRequest");
            b.H("41833a00");b.J("0f85","native");b.J("e9","checkPhase");
            b.Label("customRequest");b.H("83f802");b.J("0f85","native");
            b.H("41833a01");b.J("0f85","native");
            b.Label("checkPhase");
            b.H("48b8");b.Q(image+0xf8575a0);b.H("488b004885c0");b.J("0f84","native");
            b.H("8b80a000000083f801");b.J("0f84","safePhase");
            b.H("83f805");b.J("0f85","native");
            b.Label("safePhase");
            b.H("48b8");b.Q(image+0xf547a00);b.H("488b004885c0");b.J("0f84","restore");
            b.H("803800");b.J("0f85","native");
            b.Label("restore");b.H("41c782");b.I(12372);b.I(0);
            b.H("41c742040000000041c782fc0000000000000041c782a40000000000000041c782c800000000000000");
            b.H("51524150415141534881ec88000000");
            for(int x=0;x<6;x++){b.H("f30f7f");b.B((byte)(0x84+x*8));b.H("24");b.I(32+x*16);}
            // Setters and the complete native practice notification run only on
            // the game thread. Return to the picker without committing a CPU play.
            EmitPossession(b,image,data);
            b.H("498b4a10418b522c48b8");b.Q(image+0x5d59530);b.H("ffd0");
            b.H("49ba");b.Q(data);b.H("498b4a10f3410f104a3048b8");b.Q(image+0x5d59570);b.H("ffd0");
            b.H("49ba");b.Q(data);b.H("498b4a10498d522041b00148b8");b.Q(image+0x5d595c0);b.H("ffd0");
            EmitScores(b,image,data);
            // Keep the actual pre-snap situation and completion counter. The
            // worker may next run after the user has already snapped the ball.
            b.H("498b4210498982");b.I(12400);
            foreach(var field in new[]{new[]{0x1c8,12416},new[]{0x1d0,12420},new[]{0x16c,12424},new[]{0x1c0,12428},new[]{0x1d8,12432},new[]{0x1dc,12436}}){
                b.H("8b88");b.I(field[0]);b.H("41898a");b.I(field[1]);
            }
            b.H("48b9");b.Q(image+0xf8575a0);b.H("488b0949898a");b.I(12408);
            b.H("418b4a4841898a");b.I(12440);
            b.H("418b4a1841898a");b.I(12444);
            b.H("48b8");b.Q(image+0xf547540);b.H("488b084885c9");b.J("0f84","defaultHudDone");
            b.H("ba1400000041b00148b8");b.Q(image+0x369ed10);b.H("ffd0");
            b.Label("defaultHudDone");
            b.H("48b8");b.Q(image+0xf8575a0);b.H("488b08ba0500000048b8");b.Q(image+0x5d0e950);b.H("ffd0");
            b.H("49ba");b.Q(data);b.H("41833a01");b.J("0f84","customUi");
            b.H("48b9");b.Q(image+0xdadd380);b.H("33d248b8");b.Q(image+0x7babdd0);b.H("ffd0");b.J("e9","uiDone");
            b.Label("customUi");b.H("48b9");b.Q(image+0xdadd380);b.H("33d248b8");b.Q(image+0x7babb30);b.H("ffd0");
            b.Label("uiDone");
            // Publish acknowledgement last, after the full UI transition.
            b.H("49ba");b.Q(data);b.H("f041ff82");b.I(12384);b.H("418b42184189421c");
            for(int x=0;x<6;x++){b.H("f30f6f");b.B((byte)(0x84+x*8));b.H("24");b.I(32+x*16);}
            b.H("4881c488000000415b415941585a59");
            b.Label("native");b.H("415a589dc3");return b.Finish();
        }
        private static void EmitPossession(Emitter b,long image,long data)
        {
            b.H("49ba");b.Q(data);b.H("4183ba");b.I(12048);b.H("01");b.J("0f85","possessionDone");
            b.H("41c782");b.I(12048);b.I(0);
            b.H("498b4a10418b92");b.I(12052);b.H("488b81c801000039d0");b.J("0f84","possessionBaseline");
            b.H("48b8");b.Q(image+0x5d59f20);b.H("ffd0");
            b.Label("possessionBaseline");b.H("49ba");b.Q(data);b.H("498b4a10418b82");b.I(12052);b.H("8981cc010000");
            b.Label("possessionDone");b.H("49ba");b.Q(data);
        }
        private static void EmitScores(Emitter b,long image,long data)
        {
            b.H("49ba");b.Q(data);b.H("41837a5401");b.J("0f85","scoresDone");
            b.H("41c7425400000000498b4a1033d2458b425848b8");b.Q(image+0x35e2110);b.H("ffd0");
            b.H("49ba");b.Q(data);b.H("498b4a10ba01000000458b425c48b8");b.Q(image+0x35e2110);b.H("ffd0");
            b.Label("scoresDone");b.H("49ba");b.Q(data);
        }
        public static byte[] BuildSessionStats(long image,long data)
        {
            var b=StatsScope(image,data);b.H("4983ba9000000000");b.J("0f84","native");
            b.H("415a589d53565741544883ec284c8be2"); // Preserve nonvolatile registers; R12 = output.
            b.H("48b8");b.Q(data);b.H("f0ff809c000000488b989000000033f6");
            b.Label("row");
            for(int column=0;column<3;column++){
                b.H("4c89e18bd64c8d8b");b.I(column==0?0:column==1?64:96);
                b.H("49b8");b.Q(image+(column==0?0xbab08c0:column==1?0xbab08d0:0xbab08e0));
                b.H("488b014c8b503041ffd2");
            }
            b.H("4c89e18bd64533c949b8");b.Q(image+0xbab08f0);b.H("488b01ff5020");
            b.H("ffc64881c38000000048b8");b.Q(data);b.H("3bb098000000");b.J("0f8c","row");
            b.H("4883c428415c5f5e5bc3");
            b.Label("native");b.H("415a589d");b.Raw(Originals[7]);b.Raw(Jump(image+Sites[7]+Originals[7].Length));return b.Finish();
        }
        public List<SpotEvent> Events()
        {
            int current=Sequence;
            if(current-consumed>64)throw new InvalidOperationException("Play trace overflowed; restart the situation to resynchronize.");
            var result=new List<SpotEvent>();
            while(consumed<current) {
                int next=consumed+1; byte[] b=Read(data+256+(next&63)*64,64);
                if(BitConverter.ToInt32(b,0)!=next)break;
                result.Add(new SpotEvent{Sequence=next,Caller=BitConverter.ToInt64(b,8)-image,Match=BitConverter.ToInt64(b,16),
                    Y=BitConverter.ToSingle(b,28),OldY=BitConverter.ToSingle(b,36),Down=BitConverter.ToInt32(b,40),Phase=BitConverter.ToInt32(b,44)});
                consumed=next;
            }
            return result;
        }
        // The gate changes only the three reset decisions in the observed practice
        // controller. Native spotting, first downs and play outcomes continue to run.
        public static byte[] BuildGate(long image,long data,int index)
        {
            var b=new Emitter(); b.H("9c50415249ba"); b.Q(data);
            b.H("49895a0841833a01"); b.J("0f85","native");
            b.H("48b8"); b.Q(image+0xd9e3818); b.H("83380e"); b.J("0f85","native");
            b.H("48b8"); b.Q(image+0xf3f5d48); b.H("833803"); b.J("0f85","native");
            b.H("48b8"); b.Q(image+0xf8576b0); b.H("488b004885c0"); b.J("0f84","native");
            b.H("488b4010493b4210"); b.J("0f85","native");
            if(index==0) {
                b.H("41837a0401"); b.J("0f85","active");
                // Pending scenario runs on the game thread at the verified reset.
                b.H("41c742040000000041c782fc0000000000000051524150415141534881ec80000000");
                for(int x=0;x<6;x++) { b.H("f30f7f"); b.B((byte)(0x84+x*8)); b.H("24"); b.I(32+x*16); }
                EmitPossession(b,image,data);
                b.H("498b4a10418b522c48b8"); b.Q(image+0x5d59530); b.H("ffd0");
                b.H("49ba"); b.Q(data); b.H("498b4a10f3410f104a3048b8"); b.Q(image+0x5d59570); b.H("ffd0");
                b.H("49ba"); b.Q(data); b.H("498b4a10498d522041b00148b8"); b.Q(image+0x5d595c0); b.H("ffd0");
                b.H("49ba");b.Q(data);b.H("41837a5401");b.J("0f85","scoresDone");
                b.H("41c7425400000000498b4a1033d2458b425848b8");b.Q(image+0x35e2110);b.H("ffd0");
                b.H("49ba");b.Q(data);b.H("498b4a10ba01000000458b425c48b8");b.Q(image+0x35e2110);b.H("ffd0");
                b.Label("scoresDone");
                // Use the complete native HUD selector: state, visual style and
                // script HUD update, rather than the isolated typed message.
                b.H("49ba"); b.Q(data);b.H("41837a5001");b.J("0f85","hudDone");
                b.H("48b8");b.Q(image+0xf547540);b.H("488b084885c9");b.J("0f84","hudDone");
                b.H("ba1400000041b00148b8");b.Q(image+0x369ed10);b.H("ffd0");
                b.Label("hudDone");
                b.H("49ba"); b.Q(data); b.H("418b42184189421c");
                for(int x=0;x<6;x++) { b.H("f30f6f"); b.B((byte)(0x84+x*8)); b.H("24"); b.I(32+x*16); }
                b.H("4881c480000000415b415941585a59");
            }
            if(index==2) {
                b.H("48b8"); b.Q(image+0xf8575a0); b.H("488b004885c0"); b.J("0f84","active");
                b.H("83b8a000000004"); b.J("0f85","active");
                b.H("488b40504885c0"); b.J("0f84","active");
                b.H("83b85002000000"); b.J("0f85","zeroGain");
                b.H("80b8a803000000"); b.J("0f85","zeroGain");
                b.H("8b8034020000"); b.J("e9","publishGain");
                b.Label("zeroGain"); b.H("33c0");
                b.Label("publishGain"); b.H("4189424cf041ff4248");
            }
            b.Label("active"); b.H("415a589d"); b.Raw(Jump(image+Skips[index]));
            b.Label("native"); b.H("415a589d"); b.Raw(Sub(Originals[index],0,7)); b.J("0f84","skip");
            b.H("48b8"); b.Q(image+0xf8576b0); b.H("488b00"); b.Raw(Jump(image+Sites[index]+Originals[index].Length));
            b.Label("skip"); b.Raw(Jump(image+Skips[index])); return b.Finish();
        }
        public static byte[] BuildTrace(long image,long data)
        {
            var b=new Emitter(); b.H("9c5041524153415149ba"); b.Q(data);
            // Each writer owns a sequence obtained by atomic fetch-add. Publish
            // that sequence last, rather than rereading a moving global counter.
            b.H("b801000000f0410fc14240ffc0448bc883e03fc1e0064d8d9c020001000045894b04");
            b.H("488b4424284989430849894b10488b0249894318488b8168010000498943208b81d001000041894328");
            b.H("48b8"); b.Q(image+0xf8575a0); b.H("488b004885c0"); b.J("0f84","publish");
            b.H("448b88a000000045894b2c448b88a400000045894b30");
            b.Label("publish"); b.H("418b43044189034159415b415a589d"); b.Raw(Originals[3]); b.Raw(Jump(image+Sites[3]+Originals[3].Length)); return b.Finish();
        }
        private byte[] Read(long address,int count) { var b=new byte[count]; IntPtr n; if(!ReadProcessMemory(handle,new IntPtr(address),b,new IntPtr(count),out n)||n.ToInt64()!=count)throw new InvalidOperationException("Native read failed."); return b; }
        private int Int(long a) { return BitConverter.ToInt32(Read(a,4),0); }
        private long Long(long a) { return BitConverter.ToInt64(Read(a,8),0); }
        private float Float(long a) { return BitConverter.ToSingle(Read(a,4),0); }
        private void Write(long a,byte[] b) { IntPtr n; if(!WriteProcessMemory(handle,new IntPtr(a),b,new IntPtr(b.Length),out n)||n.ToInt64()!=b.Length)throw new InvalidOperationException("Native write failed."); }
        private void PatchBytes(long a,byte[] b) { uint old; if(!VirtualProtectEx(handle,new IntPtr(a),new UIntPtr((uint)b.Length),0x40,out old))throw new InvalidOperationException("Cannot protect patch."); try {Write(a,b); FlushInstructionCache(handle,new IntPtr(a),new UIntPtr((uint)b.Length));} finally {uint unused; VirtualProtectEx(handle,new IntPtr(a),new UIntPtr((uint)b.Length),old,out unused);} }
        public void Dispose()
        {
            if(handle==IntPtr.Zero)return;
            if(!game.HasExited) {
                SetEnabled(false);
                Write(data+12368,BitConverter.GetBytes(0));Write(data+12372,BitConverter.GetBytes(0));
                foreach(var edit in menuEdits){
                    try{if(Long(edit.Owner)==edit.Vtable && edit.Replacement!=null && Equal(Read(edit.Address,edit.Replacement.Length),edit.Replacement))Write(edit.Address,edit.Original);}
                    catch(InvalidOperationException){log("Mode menu object was released during practice exit; skipped restoring its former text.");}
                }
                using(var freeze=new FrozenThreads(game,image,Sites,Originals)) {
                    foreach(var p in installed)
                        if(!Equal(Read(p.Address,p.Replacement.Length),p.Replacement))throw new InvalidOperationException("A hook changed ownership; cannot restore it.");
                    while(installed.Count>0) {
                        var p=installed[installed.Count-1];
                        PatchBytes(p.Address,p.Original); installed.RemoveAt(installed.Count-1);
                    }
                }
                log("Disabled; all practice hook sites restored.");
                RestoreHudVisibility();
                // Do not free code: suspended threads can be inside a trampoline.
                // On a failed idle/ownership check, retain the handle so Disable
                // can be retried. Hooks are already in their inactive state.
            }
            CloseHandle(handle);handle=IntPtr.Zero;game.Dispose();
        }
        private sealed class Patch { public long Address; public byte[] Original,Replacement; }
        public static byte[] Hex(string s) { var b=new byte[s.Length/2]; for(int i=0;i<b.Length;i++)b[i]=Convert.ToByte(s.Substring(i*2,2),16);return b; }
        public static byte[] Jump(long a) { var b=new List<byte>(Hex("ff2500000000")); b.AddRange(BitConverter.GetBytes(a));return b.ToArray(); }
        private static byte[] Pad(byte[] b,int n) { var a=new byte[n]; for(int i=0;i<n;i++)a[i]=i<b.Length?b[i]:(byte)0x90;return a; }
        private static byte[] Sub(byte[] b,int a,int n) { var r=new byte[n];Array.Copy(b,a,r,0,n);return r; }
        private static bool Equal(byte[] a,byte[] b) { if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true; }
        private sealed class Emitter
        {
            readonly List<byte> bytes=new List<byte>(); readonly Dictionary<string,int> labels=new Dictionary<string,int>(); readonly List<KeyValuePair<int,string>> jumps=new List<KeyValuePair<int,string>>();
            public void H(string s) {Raw(Hex(s));} public void B(byte b){bytes.Add(b);} public void Q(long n){Raw(BitConverter.GetBytes(n));} public void I(int n){Raw(BitConverter.GetBytes(n));} public void Raw(byte[] b){bytes.AddRange(b);} public void Label(string s){labels.Add(s,bytes.Count);} public void J(string op,string label){H(op);jumps.Add(new KeyValuePair<int,string>(bytes.Count,label));I(0);}
            public byte[] Finish(){var b=bytes.ToArray();foreach(var j in jumps)Array.Copy(BitConverter.GetBytes(labels[j.Value]-j.Key-4),0,b,j.Key,4);return b;}
        }
        private sealed class FrozenThreads : IDisposable
        {
            readonly List<IntPtr> threads=new List<IntPtr>();
            public FrozenThreads(Process game,long image,long[] sites,byte[][] bytes)
            {
                try { game.Refresh(); foreach(ProcessThread t in game.Threads) {
                    IntPtr h=OpenThread(0x4a,false,t.Id);
                    if(h==IntPtr.Zero){int error=Marshal.GetLastWin32Error();if(error==87)continue;throw new InvalidOperationException("Cannot suspend game thread: "+error);}
                    if(SuspendThread(h)==uint.MaxValue){CloseHandle(h);throw new InvalidOperationException("Thread suspension failed.");}threads.Add(h);
                    IntPtr raw=Marshal.AllocHGlobal(1248);
                    try { long a=(raw.ToInt64()+15)&~15L; for(int i=0;i<1232;i++)Marshal.WriteByte(new IntPtr(a),i,0); Marshal.WriteInt32(new IntPtr(a+48),0x100001);
                        if(!GetThreadContext(h,new IntPtr(a)))throw new InvalidOperationException("Thread context failed.");
                        long rip=Marshal.ReadInt64(new IntPtr(a+248));for(int i=0;i<sites.Length;i++)if(image+sites[i]<rip&&rip<image+sites[i]+bytes[i].Length)throw new InvalidOperationException("A thread is inside the patch area; retry while idle.");
                    } finally {Marshal.FreeHGlobal(raw);}
                } } catch { Dispose();throw; }
            }
            public void Dispose(){for(int i=threads.Count-1;i>=0;i--){ResumeThread(threads[i]);CloseHandle(threads[i]);}threads.Clear();}
        }
        [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(int rights,bool inherit,int pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool ReadProcessMemory(IntPtr h,IntPtr a,byte[] b,IntPtr n,out IntPtr read);
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool WriteProcessMemory(IntPtr h,IntPtr a,byte[] b,IntPtr n,out IntPtr written);
        [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr VirtualAllocEx(IntPtr h,IntPtr a,UIntPtr n,uint type,uint protection);
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool VirtualProtectEx(IntPtr h,IntPtr a,UIntPtr n,uint protection,out uint old);
        [DllImport("kernel32.dll")] static extern bool FlushInstructionCache(IntPtr h,IntPtr a,UIntPtr n);
        [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenThread(int rights,bool inherit,int tid);
        [DllImport("kernel32.dll")] static extern uint SuspendThread(IntPtr h);
        [DllImport("kernel32.dll")] static extern uint ResumeThread(IntPtr h);
        [DllImport("kernel32.dll")] static extern bool GetThreadContext(IntPtr h,IntPtr context);
    }
}
