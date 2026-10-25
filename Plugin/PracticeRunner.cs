using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SituationalPractice
{
    // No window or external HUD. Native practice menus select plays and spots.
    public sealed class PracticeRunner
    {
        private NativeRuntime runtime;
        private readonly int pid;
        private readonly string folder;
        private readonly DiagnosticSession diagnostics;
        private readonly bool refreshPicker;
        private int resumeUserTeam;
        private string operation="startup";
        private int diagnosticPhase=-1,attachAttempts;
        private Scenario starting,pending;
        private NativeSnapshot previous;
        private int command,seen,plays,waitingResult;
        private bool outcomeSpotted,manualSpot;
        private bool sideReset;
        private bool sideResetRequested;
        private long sideResetAt;
        private bool livePlay;
        private long settleAt;
        private readonly Stopwatch timer=Stopwatch.StartNew();
        private string lastError;
        private SessionStats session;
        private long sessionMatch;
        private int sessionPlay;
        private int userTeam;
        private int modeRequests;
        private float[] savedSpot;
        private void RestartAtSavedSpot(NativeSnapshot snap,float[] vector)
        {
            var selected=new NativeSnapshot{Ball=vector[1]+50};
            starting=NativeDrivePolicy.StartingSpot(selected,false);session.ResumeSpot=starting.Copy();
            operation="manual re-spot";Sample(null,true);
            runtime.QueueManualSpot(starting,++command,vector);pending=starting.Copy();previous=null;
            sideReset=false;
            waitingResult=0;outcomeSpotted=false;manualSpot=false;livePlay=false;seen=runtime.Completed;plays=0;
            Log("Native Re-spot Ball: fresh drive at "+starting.Ball.ToString("0.0")+", first down, distance "+starting.Distance.ToString("0.0")+"; session scores and stats retained.");
        }
        private void RestartForSideChange()
        {
            var fresh=starting.Copy();fresh.Down=1;fresh.Distance=Math.Min(10,100-fresh.Ball);
            operation="Flip Sides drive reset";Sample(null,true);
            runtime.QueueManualSpot(fresh,++command,new[]{savedSpot[0],(float)fresh.Ball-50,savedSpot[2]});
            pending=fresh;sideReset=true;previous=null;waitingResult=0;outcomeSpotted=false;manualSpot=false;livePlay=false;seen=runtime.Completed;plays=0;
            Log("Flip Sides: fresh drive at selected starting spot "+fresh.Ball.ToString("0.0")+", first down; scores and session stats retained. Native menu supplies the next user selection.");
        }
        public PracticeRunner(int pid):this(pid,new DiagnosticSession(AppDomain.CurrentDomain.BaseDirectory,pid)){}
        internal PracticeRunner(int pid,DiagnosticSession diagnostics,bool refreshPicker=false,int resumeUserTeam=-1){this.pid=pid;folder=AppDomain.CurrentDomain.BaseDirectory;this.diagnostics=diagnostics;this.refreshPicker=refreshPicker;this.resumeUserTeam=resumeUserTeam;}
        private void Log(string message){diagnostics.Log(message);}
        private string Context()
        {
            return "operation="+operation+" attach_attempts="+attachAttempts+" command="+command+" seen="+seen+" waiting_result="+waitingResult+" session_play="+sessionPlay+" user_team="+userTeam
                +" live_play="+livePlay+" manual_spot="+manualSpot+" pending_drive="+(pending!=null)+" practice_mode="+(session==null?"unset":session.Mode.ToString())
                +Environment.NewLine+"starting_spot="+(starting==null?"unset":starting.Ball.ToString(System.Globalization.CultureInfo.InvariantCulture))
                +" session_match=0x"+sessionMatch.ToString("x")
                +Environment.NewLine+"session_plays="+(session==null?"unset":session.Teams[0].Plays+","+session.Teams[1].Plays);
        }
        private void Sample(NativeSnapshot snap,bool force=false)
        {
            diagnostics.UpdateState(()=>Context()+Environment.NewLine+(runtime==null?"Native runtime not attached.":runtime.DiagnosticState(snap??runtime.Snapshot())),force);
        }
        private void Queue(Scenario scenario)
        {
            operation="queue drive";Log("Queue drive: ball="+scenario.Ball+" down="+scenario.Down+" distance="+scenario.Distance+" next command="+(command+1));Sample(null,true);
            runtime.Queue(scenario,++command);pending=scenario.Copy();sideReset=false;previous=null;waitingResult=0;outcomeSpotted=false;manualSpot=false;livePlay=false;
        }
        private void ChoosePlay()
        {
            operation="request play-call screen";Sample(null,true);
            runtime.QueuePlayCall(userTeam);Log("Play-call requested for user team "+userTeam+"; CPU chooses the other side.");
        }
        private void ChangeMode(PracticeMode next,NativeSnapshot snap)
        {
            bool wasDefault=session.Mode==PracticeMode.DefaultPractice;
            if(wasDefault)starting=NativeDrivePolicy.StartingSpot(new NativeSnapshot{Ball=savedSpot[1]+50},false);
            session=new SessionStats{Mode=next,ResumeSpot=starting.Copy()};sessionPlay=0;
            runtime.DiscardEvents();
            runtime.PublishStats(session);runtime.FindModeMenu(next);
            previous=null;pending=null;sideResetRequested=false;waitingResult=0;outcomeSpotted=false;manualSpot=false;livePlay=false;seen=runtime.Completed;plays=0;
            if(next==PracticeMode.DefaultPractice){
                sideReset=false;runtime.UseDefaultPractice(true);
            }else{
                runtime.RequestNativeHud(true);
                var fresh=starting.Copy();fresh.Down=1;fresh.Distance=Math.Min(10,100-fresh.Ball);
                var drive=new PossessionDrive{Possession=snap.Possession,Spot=fresh,Score0=0,Score1=0};
                if(wasDefault)runtime.QueueCustomModeStart(drive,++command);
                else runtime.QueuePossessionDrive(drive,++command,snap.Phase!=1 && snap.Phase!=5);
                pending=fresh;sideReset=true;
            }
            Log("Practice mode changed to "+next+"; fresh session, scores and stats reset.");
        }
        private void Attach()
        {
            operation="attach native hooks";attachAttempts++;
            runtime=new NativeRuntime(pid,Log);
            operation="initialize native practice session";
            var snap=runtime.Snapshot();
            userTeam=snap.Possession;
            runtime.ConfigureUserTeam(userTeam);
            if(session==null || sessionMatch!=snap.Match){session=SessionStats.ReadHandoff(Path.Combine(folder,"session-handoff.bin"),runtime.GameStart,snap.Match)??new SessionStats();sessionMatch=snap.Match;sessionPlay=Math.Max(0,session.LastPlay);}
            runtime.BindPracticeMenus();
            runtime.PublishStats(session);runtime.FindStatsMenu();
            runtime.FindModeMenu(session.Mode);modeRequests=runtime.ModeRequests;
            starting=session.ResumeSpot??NativeDrivePolicy.StartingSpot(snap,false);session.ResumeSpot=starting.Copy();
            savedSpot=runtime.SavedPracticeSpot();
            lastError=null;
            if(session.Mode==PracticeMode.DefaultPractice){
                runtime.UseDefaultPractice(false);seen=runtime.Completed;resumeUserTeam=-1;
                Log("Default Practice ready. Pause > Mode selects Drive Practice or Scrimmage. No initialization play needed for normal practice.");return;
            }
            runtime.RequestNativeHud(true);
            if(resumeUserTeam>=0){
                runtime.ResumeExistingSession(resumeUserTeam);userTeam=resumeUserTeam;previous=snap;seen=runtime.Completed;pending=null;
                resumeUserTeam=-1;
                if(refreshPicker)runtime.QueuePlayCall(userTeam);
                Log("Explicit clean development handoff: current drive retained for user team "+userTeam+".");return;
            }
            Queue(starting);
            if(refreshPicker && snap.Phase==5){runtime.RefreshPlayCall();Log("Explicit live recovery: reopen native play picker once on the game thread.");}
            Log("Automatic drive queued at native spot "+starting.Ball.ToString("0.0")+". No control window.");
        }
        public void Run()
        {
            using(var game=Process.GetProcessById(pid))
            {
                diagnostics.GameIdentity(game);
                bool requestedStop=false,failed=false;
                try {
                    while(!game.HasExited && !(requestedStop=File.Exists(Path.Combine(folder,"runtime.stop")))) {
                        try {
                            if(runtime==null)Attach();
                            operation="read practice snapshot";
                            var snap=runtime.Snapshot();
                            bool phaseChanged=snap.Phase!=diagnosticPhase;
                            if(phaseChanged){Log("Native phase "+diagnosticPhase+" -> "+snap.Phase);diagnosticPhase=snap.Phase;}
                            runtime.FindModeMenu(session.Mode);
                            if(runtime.ModeRequests!=modeRequests && snap.Phase!=3){
                                int requests=runtime.ModeRequests-modeRequests;modeRequests=runtime.ModeRequests;
                                PracticeMode next=PracticeModes.Next(session.Mode,requests);
                                if(next!=session.Mode)ChangeMode(next,snap);
                            }
                            operation="check saved practice spot";
                            var selectedSpot=runtime.SavedPracticeSpot();
                            if(snap.Match!=sessionMatch)throw new InvalidOperationException("Waiting for offline Dynasty practice session to settle.");
                            if(session.Mode==PracticeMode.DefaultPractice){
                                // No drive commands, CPU picks, Stats or custom
                                // scoring in the game's original practice mode.
                                savedSpot=selectedSpot;runtime.DiscardEvents();Sample(snap,phaseChanged);
                                Thread.Sleep(40);continue;
                            }
                            bool changed=selectedSpot[0]!=savedSpot[0] || selectedSpot[1]!=savedSpot[1] || selectedSpot[2]!=savedSpot[2];
                            if(snap.Phase!=3){savedSpot=selectedSpot;if(changed && previous!=null && pending==null)RestartAtSavedSpot(snap,selectedSpot);}
                            // Flip Sides can update possession before the native
                            // field/target finishes settling. Use the actual
                            // pre-snap situation for the next result's baseline.
                            if(previous!=null && pending==null && snap.Phase==3 && !livePlay){
                                livePlay=true;
                                Log("Snap baseline: possession "+previous.Possession+", down "+previous.NativeDown+", ball "+previous.Ball.ToString("0.0")+", line "+previous.Line.ToString("0.0"));
                            }
                            // Flip Sides changes possession between snaps. It is
                            // a menu action, not a turnover from the previous play.
                            if(previous!=null && pending==null && !livePlay && snap.Phase!=3 && snap.Possession!=previous.Possession){
                                previous=snap;seen=runtime.Completed;waitingResult=0;outcomeSpotted=false;
                                sideResetRequested=true;sideResetAt=timer.ElapsedMilliseconds+80;
                            }
                            // Wait until the native flip finishes field changes.
                            // The spot is then applied at the next safe preplay
                            // update, before the CPU can snap the selected play.
                            if(sideResetRequested && (snap.Phase==5 || snap.Phase==1 && timer.ElapsedMilliseconds>=sideResetAt)){
                                sideResetRequested=false;RestartForSideChange();
                            }
                            if(previous!=null && pending==null && !livePlay && snap.Phase==1)previous=snap;
                            operation="prepare CPU opponent";runtime.PrepareOpponent(snap,sideReset?pending:null);
                            userTeam=runtime.UserTeam;
                            operation="bind Stats menu";
                            runtime.FindStatsMenu();
                            if(pending!=null && runtime.Applied==command) {
                                snap=runtime.Snapshot();
                                pending=null;seen=runtime.Completed;previous=snap;plays=0;
                                userTeam=runtime.UserTeam;
                                if(!sideReset)ChoosePlay();
                                sideReset=false;
                                Log("Active: user team "+userTeam+", native down "+snap.NativeDown+", ball "+snap.Ball.ToString("0.0")+", line "+snap.Line.ToString("0.0"));
                            }
                            int completed=runtime.Completed;
                            if(previous!=null && snap.Match!=previous.Match)throw new InvalidOperationException("Practice session changed.");
                            if(previous!=null && completed>seen && waitingResult==0) {
                                // Timeout/menu resets can use the same native
                                // reset routine without an actual snap.
                                if(!livePlay){seen=completed;previous=snap;}
                                else {
                                if(completed!=seen+1)throw new InvalidOperationException("Missed play results; reattaching.");
                                waitingResult=completed;outcomeSpotted=false;
                                }
                            }
                            operation="read native play completion";var events=runtime.Events();
                            foreach(var spot in events) {
                                if(spot.Match!=snap.Match)continue;
                                if(waitingResult!=0 && spot.Phase==4 && spot.Caller==0x5d6ccd1) {outcomeSpotted=true;settleAt=timer.ElapsedMilliseconds+80;}
                                if(previous!=null && pending==null && spot.Phase==1 && spot.Caller==0x35e0cb2) {manualSpot=true;settleAt=timer.ElapsedMilliseconds+80;}
                            }
                            if(previous!=null && pending==null && manualSpot && snap.Phase==1 && timer.ElapsedMilliseconds>=settleAt) {
                                RestartAtSavedSpot(snap,new[]{0f,snap.Ball-50,0f});
                            }
                            if(previous!=null && pending==null && waitingResult!=0 && (snap.Phase==1 || snap.Phase==4 && outcomeSpotted && timer.ElapsedMilliseconds>=settleAt)) {
                                operation="classify and record play result";Sample(snap,true);
                                DriveEnd end=NativeDrivePolicy.Result(previous,snap);
                                if(end==DriveEnd.Unsupported)throw new InvalidOperationException("Invalid native outcome; reattaching.");
                                int defense=1-previous.Possession;
                                var nextDrive=session.Mode==PracticeMode.Scrimmage?ScrimmagePolicy.Next(previous,snap,end):null;
                                int award=session.Record(++sessionPlay,previous,snap,runtime.CompletedGain,session.Mode);
                                int team0=snap.OffenseScore+(defense==0?award:0),team1=snap.DefenseScore+(defense==1?award:0);
                                runtime.PublishStats(session);
                                seen=waitingResult;waitingResult=0;previous=snap;plays++;livePlay=false;
                                Log("Play "+plays+": gain "+runtime.CompletedGain.ToString("0.0")+"; native down "+snap.NativeDown+", ball "+snap.Ball.ToString("0.0")+", line "+snap.Line.ToString("0.0"));
                                if(nextDrive!=null){
                                    operation="scrimmage possession handoff";Sample(snap,true);
                                    runtime.QueuePossessionDrive(nextDrive,++command);pending=nextDrive.Spot.Copy();previous=null;sideReset=true;
                                    Log("Scrimmage drive ended: "+end+"; next possession team "+nextDrive.Possession+", first down at "+nextDrive.Spot.Ball.ToString("0.0")+"; user remains team "+userTeam+".");
                                }
                                else if(end!=DriveEnd.None) {Log("Drive ended: "+end+"; restarting at the selected spot.");if(award>0){runtime.QueueScores(team0,team1);Log("Defense team "+defense+" awarded "+award+" points.");}Queue(starting);}
                                else ChoosePlay();
                            }
                            operation="waiting for next native update";Sample(snap,phaseChanged);
                        } catch(Exception error) {
                            bool expected=error.Message.StartsWith("Waiting for offline") || error.Message.StartsWith("Waiting for an offline");
                            if(!expected)diagnostics.Error("Runtime operation: "+operation,error);
                            if(runtime!=null){
                                if(!expected)Sample(null,true);
                                try{runtime.Dispose();runtime=null;}catch(Exception cleanup){diagnostics.Error("Hook restoration after runtime error",cleanup);throw;}
                            }
                            previous=null;pending=null;waitingResult=0;manualSpot=false;livePlay=false;sideReset=false;sideResetRequested=false;
                            if(expected){session=null;sessionMatch=0;}
                            if(lastError!=error.Message){lastError=error.Message;Log(lastError);}
                        }
                        Thread.Sleep(runtime==null?1000:40);
                    }
                } catch(Exception error){failed=true;diagnostics.Error("Practice runner failed",error);throw;}
                finally {
                    try{
                        if(runtime!=null){
                            try{if(session!=null && !game.HasExited && requestedStop)session.WriteHandoff(Path.Combine(folder,"session-handoff.bin"),runtime.GameStart,sessionMatch);}
                            catch(Exception error){diagnostics.Error("Save development session handoff",error);}
                            runtime.Dispose();runtime=null;
                        }
                    }catch(Exception error){failed=true;diagnostics.Error("Final hook restoration",error);throw;}
                    finally{diagnostics.Complete(game,requestedStop,failed);}
                }
            }
        }
        [STAThread] public static void Main(string[] args)
        {
            DiagnosticSession diagnostics=null;
            try{
                int pid=args.Length>0?int.Parse(args[0]):0;
                if(pid==0){var games=Process.GetProcessesByName("CollegeFB27");try{if(games.Length!=1)return;pid=games[0].Id;}finally{foreach(var game in games)game.Dispose();}}
                diagnostics=new DiagnosticSession(AppDomain.CurrentDomain.BaseDirectory,pid);
                AppDomain.CurrentDomain.UnhandledException+=(sender,eventArgs)=>{
                    try{
                        diagnostics.Error("Unhandled helper exception",eventArgs.ExceptionObject as Exception??new Exception(Convert.ToString(eventArgs.ExceptionObject)));
                        diagnostics.CompleteForExit("Unhandled helper exception; terminating="+eventArgs.IsTerminating,null,"No game exit was established by this helper exception.");
                    }catch{}
                };
                bool created;using(var mutex=new Mutex(true,"Local\\SituationalPractice-"+pid,out created)) {
                    if(!created){diagnostics.Log("Another practice helper owns this game; duplicate helper exiting.");return;}
                    int resume=args.Length>2 && (args[1]=="resume" || args[1]=="resume-pick")?int.Parse(args[2]):-1;
                    new PracticeRunner(pid,diagnostics,args.Length>1 && (args[1]=="refresh" || args[1]=="resume-pick"),resume).Run();
                }
            }catch(Exception error){
                if(diagnostics==null)diagnostics=new DiagnosticSession(AppDomain.CurrentDomain.BaseDirectory,0);
                diagnostics.Error("Fatal helper startup/runtime failure",error);
                diagnostics.CompleteForExit("Helper failed",null,"No game exit was established by this helper exception.");
                Environment.ExitCode=1;
            }
        }
    }
}
