"""Execute DLL-generated x64 gates locally with native calls replaced by mocks."""

import ctypes as c, struct

from pathlib import Path

from capstone import Cs,CS_ARCH_X86,CS_MODE_64

k=c.WinDLL('kernel32',use_last_error=True)

k.VirtualAlloc.argtypes=[c.c_void_p,c.c_size_t,c.c_ulong,c.c_ulong];k.VirtualAlloc.restype=c.c_void_p

B=0x140000000;D=0x200000000;M=0x50000000

sites=[0x4ff39ce,0x4ff39fb,0x4ff3d66]; skips=[0x4ff39fb,0x4ff3a97,0x4ff3e03]; sizes=[16,20,20]

pages={ (B+r)&~0xffff for r in sites+skips+[0xd9e3818,0xf3f5d48,0xf8576b0,0xf8575a0,0xf6379e0,0xf547a00,0xf547540,0x369ed10,0x5d59530,0x5d59570,0x5d595c0,0x35e2110,0x377bb40,0x7b99d90,0x5d49e30,0x5d4a02f,0x5d72491,0x5d5ae51,0x35ff120,0x7b7e7e0,0x7babb30,0xfa582a0,0xdadd380,0x5d0e950,0x7babdd0,0x3f079e0,0x2ad8410,0xf8abb50,0x5d59f20,0x7bcad50,0x7c17d20,0xbae5718,0x25a3060,0x5d4a660] }

for a,n in [(M,0x10000),(D,0x10000)]+[(a,0x10000) for a in pages]:

    if k.VirtualAlloc(a,n,0x3000,0x40)!=a:raise c.WinError(c.get_last_error())

def put(a,b):c.memmove(a,b,len(b))

def q(a,n):put(a,struct.pack('<Q',n))

def i(a,n):put(a,struct.pack('<i',n))

def f(a,n):put(a,struct.pack('<f',n))

def get(a):return c.c_int.from_address(a).value

def imm(op,n):return bytes.fromhex(op)+struct.pack('<Q',n)

def jump(n):return imm('ff2500000000',n)

obj=M+0x2000;match=M+0x3000;wrapper=M+0x4000;states=M+0x5000;telemetry=M+0x6000;play=M+0x8000

q(B+0xf8576b0,wrapper);q(wrapper+16,match);q(B+0xf8575a0,states)

q(D+16,match);i(states+0xa0,1);i(states+0xa4,4)

checks=0

epilogue=bytes.fromhex('4883c4285e5bc3')

def ending(marker):return imm('48b8',telemetry)+bytes.fromhex('c700')+struct.pack('<i',marker)+epilogue

for gate in range(3):

    body=Path('Evidence/Gate%d.bin'%gate).read_bytes();put(M+0x100,body)

    Path('Evidence/Gate%d.asm'%gate).write_text('\n'.join('%x %s %s'%(x.address,x.mnemonic,x.op_str) for x in Cs(CS_ARCH_X86,CS_MODE_64).disasm(body,M+0x100)))

    put(B+skips[gate],ending(1));put(B+sites[gate]+sizes[gate],ending(2))

    entry=bytes.fromhex('53564883ec28')+imm('48bb',obj)+bytes.fromhex('33f6')+jump(M+0x100)

    put(M,entry)

    for enabled in [0,1]:

      for mode in [13,14]:

       for kind in [3,4]:

        for bound in [match,match+16]:

         for flag in [0,1]:

          i(D,enabled);i(D+4,0);i(B+0xd9e3818,mode);i(B+0xf3f5d48,kind);q(D+16,bound)

          c.c_ubyte.from_address(obj+(0x96 if gate==0 else 0x94)).value=flag

          c.WINFUNCTYPE(None)(M)()

          active=enabled==1 and mode==14 and kind==3 and bound==match

          assert get(telemetry)==(1 if active or flag==0 else 2),(gate,enabled,mode,kind,bound,flag)

          checks+=1

# End-of-play capture accepts the same valid-gain flags as the native getter.

put(M+0x100,Path('Evidence/Gate2.bin').read_bytes())

put(M,bytes.fromhex('53564883ec28')+imm('48bb',obj)+bytes.fromhex('33f6')+jump(M+0x100))

put(B+skips[2],ending(1));i(D,1);q(D+16,match);i(B+0xd9e3818,14);i(B+0xf3f5d48,3);q(states+0x50,play)

for phase in [1,3,4]:

 for classification in [0,1]:

  for excluded in [0,1]:

   for gain in [0,7.192678,-3.269924,17.58]:

    i(states+0xa0,phase);i(play+0x250,classification);c.c_ubyte.from_address(play+0x3a8).value=excluded;f(play+0x234,gain);i(D+72,0);f(D+76,123)

    c.WINFUNCTYPE(None)(M)()

    assert get(D+72)==(1 if phase==4 else 0)

    expected=123 if phase!=4 else gain if classification==0 and excluded==0 else 0

    assert abs(c.c_float.from_address(D+76).value-expected)<.00001

    checks+=1

q(states+0x50,0);i(states+0xa0,1)

# Execute initial scenario commands at the safe gate using mocked native setters.

put(B+0x5d59530,bytes.fromhex('8991d0010000c3'))

put(B+0x5d59570,bytes.fromhex('f30f1189c0010000c3'))

put(B+0x5d595c0,bytes.fromhex('488b02488981680100008b4208898170010000c3'))

put(M+0x100,Path('Evidence/Gate0.bin').read_bytes());put(M,bytes.fromhex('53564883ec28')+imm('48bb',obj)+bytes.fromhex('33f6')+jump(M+0x100))

put(B+skips[0],ending(1));i(D,1);i(D+4,1);q(D+16,match);i(D+24,19);i(B+0xd9e3818,14);i(B+0xf3f5d48,3)

f(D+32,0);f(D+36,35);f(D+40,0);i(D+44,3);f(D+48,42)

c.WINFUNCTYPE(None)(M)()

assert get(match+0x1d0)==3 and c.c_float.from_address(match+0x16c).value==35 and c.c_float.from_address(match+0x1c0).value==42

assert get(D+4)==0 and get(D+28)==19;checks+=1

# The full HUD selector receives the existing UI manager and native auto-state.

ui=M+0x9000;q(B+0xf547540,ui)

put(B+0x369ed10,bytes.fromhex('89512044884124c3'))

i(D+80,1);i(D+4,1);i(ui+0x20,-1);i(ui+0x24,0)

c.WINFUNCTYPE(None)(M)()

assert get(ui+0x20)==20 and c.c_ubyte.from_address(ui+0x24).value==1;checks+=1

q(B+0xf547540,0);i(D+4,1)

c.WINFUNCTYPE(None)(M)();assert get(D+4)==0;checks+=1

i(D+80,0)

# Absolute score updates execute once with the scenario on the game thread.

put(B+0x35e2110,bytes.fromhex('44898491d8010000c3'))

i(D+4,1);i(D+84,1);i(D+88,6);i(D+92,10)

c.WINFUNCTYPE(None)(M)()

assert get(match+0x1d8)==6 and get(match+0x1dc)==10 and get(D+84)==0;checks+=1

i(D+4,1);i(D+92,14);c.WINFUNCTYPE(None)(M)()

assert get(match+0x1dc)==10;checks+=1

# Trace wrapper replays the real prologue, then a mock epilogue restores it.

put(M+0x100,Path('Evidence/Trace.bin').read_bytes())

put(B+0x5d595c0+19,bytes.fromhex('4881c4d00000005f488b5c2408488b742410c3'))

v=M+0x7000;f(v,2);f(v+4,24);i(match+0x1d0,2);f(match+0x168,0);f(match+0x16c,20)

entry=bytes.fromhex('4883ec28')+imm('48b9',match)+imm('48ba',v)+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3')

put(M,entry);c.WINFUNCTYPE(None)(M)();slot=D+256+64

assert get(D+64)==1 and get(slot)==1 and c.c_float.from_address(slot+28).value==24 and c.c_float.from_address(slot+36).value==20 and get(slot+44)==1;checks+=1

# The native HUD adapter changes automatic requests only inside the bound

# practice session, during preplay/play/postplay, and while the game is unpaused.

put(M+0x100,Path('Evidence/HudEntry.bin').read_bytes())

put(B+0x369ed10+14,bytes.fromhex('8911488b9c24800000004883c4605dc3'))

cfg=M+0xa000;pause=M+0xb000;q(B+0xf6379e0,cfg);q(B+0xf547a00,pause)

for enabled in [0,1]:

 for hud in [0,1]:

  for mode in [13,14]:

   for kind in [3,4]:

    for bound in [match,match+16]:

     for practice in [0,1]:

      for paused in [0,1]:

       for phase in [1,3,4,5]:

        for requested in [0,2,20]:

         i(D,enabled);i(D+80,hud);i(B+0xd9e3818,mode);i(B+0xf3f5d48,kind);q(D+16,bound)

         c.c_ubyte.from_address(cfg+0x84).value=practice;c.c_ubyte.from_address(pause).value=paused;i(states+0xa0,phase)

         entry=bytes.fromhex('4883ec28')+imm('48b9',telemetry)+bytes.fromhex('ba')+struct.pack('<i',requested)+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3')

         put(M,entry);c.WINFUNCTYPE(None)(M)()

         active=enabled==hud==practice==1 and mode==14 and kind==3 and bound==match and not paused and requested==20 and phase in [1,3,4]

         assert get(telemetry)==({1:2,3:3,4:4}[phase] if active else requested)

         checks+=1

# Keep the target fixed for the practice-only reset caller, while allowing

# normal first-down and scenario updates through the native setter.

put(M+0x100,Path('Evidence/LineGuard.bin').read_bytes())

put(B+0x5d59570,jump(M+0x100))

put(B+0x5d59570+16,bytes.fromhex('f30f1189c0010000c5f8287424304883c448c3'))

put(B+0x4ff412e,bytes.fromhex('e83d54d600c3'))

for enabled in [0,1]:

 for mode in [13,14]:

  for kind in [3,4]:

   for bound in [match,match+16]:

    for practice_caller in [False,True]:

     i(D,enabled);i(B+0xd9e3818,mode);i(B+0xf3f5d48,kind);q(D+16,bound);f(match+0x1c0,44)

     target=B+0x4ff412e if practice_caller else B+0x5d59570

     # The extra call frame at the fake practice site must remain aligned.

     adjust=0x20 if practice_caller else 0x28

     entry=bytes.fromhex('4883ec')+bytes([adjust])+imm('48b9',match)+imm('48b8',M+0xc000)+bytes.fromhex('f30f1008')+imm('48b8',target)+bytes.fromhex('ffd04883c4')+bytes([adjust])+bytes.fromhex('c3')

     f(M+0xc000,13);put(M,entry);c.WINFUNCTYPE(None)(M)()

     active=enabled==1 and mode==14 and kind==3 and bound==match and practice_caller

     assert c.c_float.from_address(match+0x1c0).value==(44 if active else 13)

     checks+=1

# Stats visibility changes only the bound native expression in Dynasty practice.

put(M+0x100,Path('Evidence/StatsVisibility.bin').read_bytes())

put(B+0x377bb40+14,bytes.fromhex('31c04883c4405bc3'))

expression=M+0xd000;q(expression,M+0xd100);q(D+120,expression)

for enabled in [0,1]:

 for mode in [13,14]:

  for kind in [3,4]:

   for bound in [match,match+16]:

    for practice in [0,1]:

     for selected in [expression,expression+16]:

      i(D,enabled);i(B+0xd9e3818,mode);i(B+0xf3f5d48,kind);q(D+16,bound);c.c_ubyte.from_address(cfg+0x84).value=practice

      entry=bytes.fromhex('4883ec28')+imm('48b9',selected)+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3')

      put(M,entry);value=c.WINFUNCTYPE(c.c_int)(M)()

      assert value==int(enabled==practice==1 and mode==14 and kind==3 and bound==match and selected==expression);checks+=1

# Run the actual generated native UI row serializer against a mock UI object.

stat_ui=M+0xe000;stat_vt=M+0xe100;out=[]

StringSetter=c.WINFUNCTYPE(None,c.c_void_p,c.c_int,c.c_void_p,c.c_void_p)

IntSetter=c.WINFUNCTYPE(None,c.c_void_p,c.c_int,c.c_void_p,c.c_int)

string_setter=StringSetter(lambda this,row,prop,value:out.append((row,prop,c.string_at(value).decode())))

int_setter=IntSetter(lambda this,row,prop,value:out.append((row,prop,value)))

q(stat_ui,stat_vt);q(stat_vt+0x30,c.cast(string_setter,c.c_void_p).value);q(stat_vt+0x20,c.cast(int_setter,c.c_void_p).value)

rows=D+4608;put(rows,b'Practice plays\0');put(rows+64,b'7\0');put(rows+96,b'0\0')

put(rows+128,b'Defensive points\0');put(rows+128+64,b'0\0');put(rows+128+96,b'10\0');q(D+144,rows);i(D+152,2)

i(D,1);i(B+0xd9e3818,14);i(B+0xf3f5d48,3);q(D+16,match);c.c_ubyte.from_address(cfg+0x84).value=1

put(M+0x100,Path('Evidence/SessionStats.bin').read_bytes())

put(M,bytes.fromhex('4883ec28')+imm('48ba',stat_ui)+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))

c.WINFUNCTYPE(None)(M)()

assert out==[(0,B+0xbab08c0,'Practice plays'),(0,B+0xbab08d0,'7'),(0,B+0xbab08e0,'0'),(0,B+0xbab08f0,0),(1,B+0xbab08c0,'Defensive points'),(1,B+0xbab08d0,'0'),(1,B+0xbab08e0,'10'),(1,B+0xbab08f0,0)];checks+=1

# Outside the practice scope, the original native provider remains selected.

put(B+0x7b99d90+15,imm('48b8',telemetry)+bytes.fromhex('c70003000000488b5c2408488b6c2410488b742418c3'))

for disabled in ['enabled','mode','kind','match','practice','rows']:

 i(D,1);i(B+0xd9e3818,14);i(B+0xf3f5d48,3);q(D+16,match);c.c_ubyte.from_address(cfg+0x84).value=1;q(D+144,rows)

 if disabled=='enabled':i(D,0)

 elif disabled=='mode':i(B+0xd9e3818,13)

 elif disabled=='kind':i(B+0xf3f5d48,4)

 elif disabled=='match':q(D+16,match+16)

 elif disabled=='practice':c.c_ubyte.from_address(cfg+0x84).value=0

 else:q(D+144,0)

 i(telemetry,0);out.clear();c.WINFUNCTYPE(None)(M)();assert get(telemetry)==3 and not out;checks+=1

# Only the two observed turnover-ending callers are suppressed; tackles, scores,

# inactive modes and the native already-ending branch retain their behavior.

put(M+0x100,Path('Evidence/ReturnGuard.bin').read_bytes())

put(B+0x5d49e30,jump(M+0x100))

put(B+0x5d49e41,imm('48b8',telemetry)+bytes.fromhex('c700010000004883c438c3'))

put(B+0x5d4a02f,imm('48b8',telemetry)+bytes.fromhex('c700020000004883c438c3'))

for caller in [0x5d72356,0x5d5ad16]:

 put(B+caller-5,b'\xe8'+struct.pack('<i',0x5d49e30-caller)+b'\xc3')

def practice_scope(disabled=None):

 i(D+12012,0);i(D+12016,0)

 i(D,1);i(B+0xd9e3818,14);i(B+0xf3f5d48,3);q(D+16,match);c.c_ubyte.from_address(cfg+0x84).value=1

 if disabled=='enabled':i(D,0)

 elif disabled=='mode':i(B+0xd9e3818,13)

 elif disabled=='kind':i(B+0xf3f5d48,4)

 elif disabled=='match':q(D+16,match+16)

 elif disabled=='practice':c.c_ubyte.from_address(cfg+0x84).value=0

for disabled in [None,'enabled','mode','kind','match','practice']:

 for phase in [1,3,4]:

  for scored in [0,1]:

   for changed in [False,True]:

    for caller in [0x5d72356,0x5d5ad16,None]:

     for already_ending in [0,1]:

      practice_scope(disabled);i(states+0xa0,phase);i(match+0x1c8,int(changed));i(match+0x1cc,0)

      c.c_ubyte.from_address(match+0x1f1).value=scored;c.c_ubyte.from_address(match+0xfc).value=already_ending

      i(telemetry,0);i(D+160,0)

      target=B+caller-5 if caller else B+0x5d49e30;adjust=0x20 if caller else 0x28

      put(M,bytes.fromhex('4883ec')+bytes([adjust])+imm('48b9',match)+imm('48b8',target)+bytes.fromhex('ffd04883c4')+bytes([adjust])+b'\xc3')

      c.WINFUNCTYPE(None)(M)()

      suppressed=disabled is None and phase==3 and not scored and (caller==0x5d5ad16 or caller==0x5d72356 and changed)

      assert get(D+160)==int(suppressed)

      assert get(telemetry)==(0 if suppressed else 2 if already_ending else 1),(disabled,phase,scored,changed,caller,already_ending)

      checks+=1

# Open the regular UI at preplay; defer CPU choice until offense is selected.

# Mocks deliberately clobber volatile registers and XMM0; hook restores them.

put(M+0x100,Path('Evidence/PlayCallTick.bin').read_bytes())

put(B+0x35ff120+19,bytes.fromhex('4881c4d8000000415d5e5b5dc3'))

play_manager=M+0xa800;q(B+0xfa582a0,play_manager)

cpu_calls=[];ui_calls=[]

transition_calls=[]

CpuChooser=c.WINFUNCTYPE(None,c.c_void_p,c.c_int)

UiShow=c.WINFUNCTYPE(None,c.c_void_p,c.c_int)

StateTransition=c.WINFUNCTYPE(None,c.c_void_p,c.c_int)

def transition(this,phase):

 transition_calls.append((this,phase));i(this+0xa0,phase)

transition_mock=StateTransition(transition)

cpu_mock=CpuChooser(lambda this,team:cpu_calls.append((this,team)))

ui_mock=UiShow(lambda this,delay:ui_calls.append((this,delay)))

# The callbacks also validate actual Win64 argument passing and stack alignment.

put(B+0x7b7e7e0,jump(c.cast(cpu_mock,c.c_void_p).value));put(B+0x7babb30,jump(c.cast(ui_mock,c.c_void_p).value))

put(B+0x5d0e950,jump(c.cast(transition_mock,c.c_void_p).value))

for disabled in [None,'enabled','mode','kind','match','practice']:

 for phase in [1,3,4,5]:

  for paused in [0,1]:

   for pending_scenario in [0,1]:

    for user_team in [0,1]:

     practice_scope(disabled);i(states+0xa0,phase);i(D+4,pending_scenario);i(D+164,1);i(D+168,user_team);i(D+172,0)

     c.c_ubyte.from_address(pause).value=paused;cpu_calls.clear();ui_calls.clear();transition_calls.clear()

     put(M,bytes.fromhex('4883ec28')+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))

     c.WINFUNCTYPE(None)(M)()

     active=disabled is None and phase==1 and not paused and not pending_scenario

     assert cpu_calls==[]

     assert ui_calls==([(B+0xdadd380,0)] if active else [])

     assert transition_calls==([(states,5)] if active else [])

     assert get(D+164)==(0 if active else 1) and get(D+172)==int(active)

     c.WINFUNCTYPE(None)(M)()

     assert len(cpu_calls)==0 and len(ui_calls)==int(active);checks+=1

# Reproduce the live handoff failure: native player penalty processing runs

# before the ordinary play-call tick. Only a fully applied, unpaused request

# enters phase 5 here; every other call retains the native player check.

put(M+0x100,Path('Evidence/EarlyPlayCall.bin').read_bytes())

put(B+0x5d4a660+15,imm('48b8',telemetry)+bytes.fromhex('ff4010c3'))

for disabled in [None,'enabled','mode','kind','match','practice']:

 for phase in [1,3,4,5]:

  for paused in [0,1]:

   for scenario in [0,1]:

    for request in [0,1]:

     for user in [0,1,255]:

      practice_scope(disabled);i(states+0xa0,phase);i(D+4,scenario);i(D+164,request);i(D+168,user);i(D+172,0);i(D+12360,0)

      i(telemetry+16,0);c.c_ubyte.from_address(pause).value=paused

      ui_calls.clear();transition_calls.clear()

      put(M,bytes.fromhex('4883ec28')+imm('48b9',obj)+imm('48ba',play)+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))

      c.WINFUNCTYPE(None)(M)()

      active=disabled is None and phase==1 and not paused and not scenario and request==1 and user<=1

      assert get(telemetry+16)==int(not active),(disabled,phase,paused,scenario,request,user)

      assert ui_calls==([(B+0xdadd380,0)] if active else [])

      assert transition_calls==([(states,5)] if active else [])

      assert get(D+12360)==int(active) and get(D+172)==int(active)

      assert get(D+164)==(0 if active else request)

      if active:

       # A subsequent player's check follows native phase-5 behavior; it does

       # not reopen the picker or consume a second request.

       c.WINFUNCTYPE(None)(M)()

       assert len(ui_calls)==1 and get(D+12360)==1

      checks+=1

put(M+0x100,Path('Evidence/PlayCallTick.bin').read_bytes())

for name in ['ReturnGuard','PlayCallTick','EarlyPlayCall']:

 body=Path('Evidence/'+name+'.bin').read_bytes();assert len(body)<=1024

 Path('Evidence/'+name+'.asm').write_text('\n'.join('%x %s %s'%(x.address,x.mnemonic,x.op_str) for x in Cs(CS_ARCH_X86,CS_MODE_64).disasm(body,M+0x100)))

# Exercise the Win64 ABI directly, including six vector registers and volatile

# integer registers, while the two added game calls clobber those registers.

saved=M+0x7000;vectors=M+0xb000;manager=M+0xa800

q(B+0xfa582a0,manager)

clobber=bytes.fromhex('31c031c931d24531c04531c94531d24531db')

for x in range(6):clobber+=bytes.fromhex('660fef')+bytes([0xc0+x*9])

for site,offset in [(0x7b7e7e0,0x200),(0x7babb30,0x220)]:

 put(B+site,imm('48b8',saved+offset)+bytes.fromhex('4889088950084889e14883e10f89480c')+clobber+b'\xc3')

capture=imm('49bb',saved)

for opcode,offset in [('498903',0),('49894b',8),('498953',16),('4d8943',24),('4d894b',32),('4d8953',40)]:

 capture+=bytes.fromhex(opcode)+(bytes([offset]) if offset else b'')

for x in range(6):capture+=bytes.fromhex('f3410f7f')+bytes([0x83+x*8])+struct.pack('<i',64+x*16)

capture+=bytes.fromhex('4883c428c3')

expected=[0x1111222233334444,0x2222333344445555,0x3333444455556666,0x4444555566667777,0x5555666677778888,0x6666777788889999]

put(vectors,bytes(range(96)))

entry=bytes.fromhex('4883ec28')+imm('48b8',vectors)

for x in range(6):entry+=bytes.fromhex('f30f6f')+bytes([0x80+x*8])+struct.pack('<i',x*16)

for opcode,value in zip(['48b8','48b9','48ba','49b8','49b9','49ba'],expected):entry+=imm(opcode,value)

entry+=bytes.fromhex('ff15')+struct.pack('<i',len(capture))+capture+struct.pack('<Q',M+0x100)

for picker in ['PlayCallTick','EarlyPlayCall']:

 put(M+0x100,Path('Evidence/'+picker+'.bin').read_bytes())

 for user_team in [0,1]:

  practice_scope();i(states+0xa0,1);i(D+4,0);i(D+164,1);i(D+168,user_team);c.c_ubyte.from_address(pause).value=0

  put(M,entry);c.WINFUNCTYPE(None)(M)()

  assert [c.c_uint64.from_address(saved+x*8).value for x in range(6)]==expected

  assert c.string_at(saved+64,96)==bytes(range(96))

  assert c.c_uint64.from_address(saved+0x220).value==B+0xdadd380 and get(saved+0x228)==0

  assert get(saved+0x22c)==8;checks+=1

# State entry's practice UI notification must use the regular menu in phase 5.

# Other modes, adapters and phases replay the original call and practice action.

put(M+0x100,Path('Evidence/PracticePlayCallUi.bin').read_bytes())

put(B+0x7babb30,imm('48b8',telemetry)+bytes.fromhex('c70001000000895004c3'))

put(B+0x3f079e0,imm('48b8',telemetry)+bytes.fromhex('ff4008c3'))

put(B+0x7babdd0+16,imm('48b8',telemetry)+bytes.fromhex('c700020000004883c438c3'))

for disabled in [None,'enabled','mode','kind','match','practice']:

 for phase in [1,3,4,5]:

  for adapter in [B+0xdadd380,M+0xa800]:

   practice_scope(disabled);i(D+12448,0);i(states+0xa0,phase);i(telemetry,0);i(telemetry+4,99);i(telemetry+8,0);i(D+176,0)

   put(M,bytes.fromhex('4883ec28')+imm('48b9',adapter)+bytes.fromhex('baff000000')+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))

   c.WINFUNCTYPE(None)(M)()

   active=disabled is None and phase==5 and adapter==B+0xdadd380

   assert get(telemetry)==(1 if active else 2)

   assert get(D+176)==int(active) and get(telemetry+8)==int(not active)

   if active:assert get(telemetry+4)==0

   else:assert c.c_ubyte.from_address(adapter+0x449).value==1

   checks+=1

assert len(Path('Evidence/PracticePlayCallUi.bin').read_bytes())<=1024

# The native initialization notification is suppressed only while an owned
# mode transaction is rebuilding the lineups. Ordinary practice keeps its UI.
for enabled in [0,1]:
 for request in [0,1,2,3]:
  for phase in [1,5]:
   practice_scope();i(D,enabled);i(D+12368,1);i(D+12448,request);i(states+0xa0,phase)
   i(telemetry,0);i(telemetry+8,0);i(D+176,0)
   put(M,bytes.fromhex('4883ec28')+imm('48b9',B+0xdadd380)+bytes.fromhex('33d2')+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))
   c.WINFUNCTYPE(None)(M)()
   suppressed=phase==5 and request in [1,2]
   custom=not suppressed and enabled==1 and phase==5
   assert get(telemetry)==(0 if suppressed else 1 if custom else 2)
   assert get(telemetry+8)==int(not suppressed and not custom)
   assert get(D+176)==int(custom)
   checks+=1
i(D+12448,0);i(D+12368,0)

# Published filtered choice and matching offense payload for the phase-5 hook.

service=M+0xc000;q(B+0xf8abb50,service);q(D+12000,B+0x7b7e7e0)

def ready_choice(user,defending=False):

 i(service+0x28,9 if defending else 5);i(D+12008,8 if defending else 4)

 i(D+12028,get(match+0x1c8))

 i(D+200,1);i(D+204,user);put(D+228,b'\0'*20)

 put(service+0x70,b'\0'*72)

# Fresh practice initializes the play-call service in stages 0/1. Select the

# opponent only once stage 2 begins, after those initialization resets.

put(M+0x100,Path('Evidence/InitialPlayCallTick.bin').read_bytes())

put(B+0x35fd200+14,bytes.fromhex('415f5dc3'))

initial_state=M+0xa600;q(states+0x58,initial_state);q(B+0xfa582a0,manager)

i(manager+0x50c,0x101)

put(B+0x7b7e7e0,jump(c.cast(cpu_mock,c.c_void_p).value))

for disabled in [None,'enabled','mode','kind','match','practice']:

 for phase in [1,3,4,5]:

  for stage in [0,1,2]:

   for pending_scenario in [0,1]:

    for handled in [0,1]:

     for user_team in [0,1]:

      practice_scope(disabled);i(states+0xa0,phase);i(initial_state+0x10,stage);i(match+0x1c8,user_team);ready_choice(user_team)

      i(D+4,pending_scenario);i(D+180,handled);i(D+184,0);i(D+168,user_team);cpu_calls.clear()

      put(M,bytes.fromhex('4883ec28')+imm('48b9',initial_state)+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))

      c.WINFUNCTYPE(None)(M)()

      active=disabled is None and phase==5 and stage==2 and not handled

      assert cpu_calls==([(manager,1-user_team)] if active else [])

      assert get(D+184)==int(active) and get(D+180)==(1 if active else 0 if disabled is None and phase==5 and stage<=1 else handled)

      c.WINFUNCTYPE(None)(M)()

      assert len(cpu_calls)==int(active)

      checks+=1

for owner in [False,True]:

 for manager_ready in [False,True]:

  for user_team in [-1,0,1,2]:

   practice_scope();i(states+0xa0,5);i(initial_state+0x10,2);i(D+4,1);i(D+180,0);i(D+184,0);i(D+168,user_team);i(match+0x1c8,user_team);ready_choice(user_team)

   q(B+0xfa582a0,manager if manager_ready else 0);cpu_calls.clear()

   put(M,bytes.fromhex('4883ec28')+imm('48b9',initial_state if owner else M+0xa800)+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))

   c.WINFUNCTYPE(None)(M)()

   active=owner and manager_ready and user_team in [0,1]

   assert cpu_calls==([(manager,1-user_team)] if active else []) and get(D+184)==int(active)

   assert get(D+180)==int(active)

   checks+=1

# A realistic reset sequence: stage 0 and 1 clear choices, then stage 2 waits.

# No CPU choice is lost to that reset; repeated waiting ticks do not reroll it.

practice_scope();i(states+0xa0,5);i(D+4,1);i(D+180,0);i(D+184,0);i(D+168,0);i(match+0x1c8,0);ready_choice(0);q(B+0xfa582a0,manager);cpu_calls.clear()

put(M,bytes.fromhex('4883ec28')+imm('48b9',initial_state)+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))

for stage in [0,1,2,2]:

 i(initial_state+0x10,stage);ready_choice(0) if stage==2 else None;c.WINFUNCTYPE(None)(M)()

 assert len(cpu_calls)==int(stage==2)

checks+=1

# Native UI initialization can clear an early CPU choice. Until the user has

# selected their play, stage 2 must not consume the one-shot request.

for user_team in [0,1]:

 practice_scope();i(states+0xa0,5);i(initial_state+0x10,2);i(D+4,1);i(D+180,0);i(D+184,0);i(D+168,1-user_team);i(match+0x1c8,user_team);ready_choice(user_team);q(B+0xfa582a0,manager);cpu_calls.clear()

 i(manager+0x50c,1<<(8*(1-user_team)))

 put(M,bytes.fromhex('4883ec28')+imm('48b9',initial_state)+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))

 for _ in range(3):

  c.WINFUNCTYPE(None)(M)();assert cpu_calls==[] and get(D+180)==0

 c.c_ubyte.from_address(manager+0x50c+user_team).value=1

 c.WINFUNCTYPE(None)(M)();c.WINFUNCTYPE(None)(M)()

 assert cpu_calls==[(manager,1-user_team)] and get(D+180)==1 and get(D+184)==1 and get(D+168)==user_team

 checks+=1

i(manager+0x50c,0x101)

# Calls may clobber every volatile register. Verify the update's own inputs

# and native VMOVSS prologue survive both user-team indices.

put(B+0x7b7e7e0,imm('48b8',saved+0x200)+bytes.fromhex('4889088950084889e14883e10f89480c')+clobber+b'\xc3')

for user_team in [0,1]:

 practice_scope();i(states+0xa0,5);i(initial_state+0x10,2);i(D+4,1);i(D+180,0);i(D+168,user_team);i(match+0x1c8,user_team);ready_choice(user_team)

 initial_expected=expected.copy();initial_expected[1]=initial_state

 initial_entry=bytes.fromhex('4883ec28')+imm('48b8',vectors)

 for x in range(6):initial_entry+=bytes.fromhex('f30f6f')+bytes([0x80+x*8])+struct.pack('<i',x*16)

 for opcode,value in zip(['48b8','48b9','48ba','49b8','49b9','49ba'],initial_expected):initial_entry+=imm(opcode,value)

 initial_entry+=bytes.fromhex('ff15')+struct.pack('<i',len(capture))+capture+struct.pack('<Q',M+0x100)

 put(M,initial_entry);c.WINFUNCTYPE(None)(M)()

 assert [c.c_uint64.from_address(saved+x*8).value for x in range(6)]==initial_expected

 assert c.string_at(saved+64,96)==bytes(range(96))

 assert c.c_uint64.from_address(saved+0x200).value==manager and get(saved+0x208)==1-user_team

 assert get(saved+0x20c)==8

 checks+=1

assert len(Path('Evidence/InitialPlayCallTick.bin').read_bytes())<=1024

# Fresh startup can enter the separate player-setup state (phase 6). Its

# stage lives at +0x38 and waits after setup stages 0/1. The separate latch allows

# play calling to supply a choice again after its own reset.

put(M+0x100,Path('Evidence/InitialSetupTick.bin').read_bytes())

put(B+0x35feb00+18,bytes.fromhex('4883c4785e5dc3'))

put(B+0x2ad8410,b'\x50'+imm('48b8',telemetry)+bytes.fromhex('ff0058c3'))

setup_state=M+0xa700;q(states+0x60,setup_state);q(B+0xfa582a0,manager)

put(B+0x7b7e7e0,jump(c.cast(cpu_mock,c.c_void_p).value))

for disabled in [None,'enabled','mode','kind','match','practice']:

 for phase in [1,3,4,5,6]:

  for stage in [0,1,2,3,4]:

   for pending_scenario in [0,1]:

    for handled in [0,1]:

     for user_team in [0,1]:

      practice_scope(disabled);i(states+0xa0,phase);i(setup_state+0x38,stage)

      i(D+4,pending_scenario);i(D+192,handled);i(D+184,0);i(D+188,0);i(D+168,user_team);i(telemetry,0);cpu_calls.clear()

      put(M,bytes.fromhex('4883ec28')+imm('48b9',setup_state)+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))

      c.WINFUNCTYPE(None)(M)()

      active=disabled is None and phase==6 and stage in [2,3] and pending_scenario and not handled

      assert cpu_calls==([(manager,1-user_team)] if active else [])

      assert get(D+184)==get(D+188)==int(active) and get(D+192)==(1 if active else handled)

      assert get(telemetry)==1

      c.WINFUNCTYPE(None)(M)()

      assert len(cpu_calls)==int(active) and get(telemetry)==2

      checks+=1

for owner in [False,True]:

 for manager_ready in [False,True]:

  for user_team in [-1,0,1,2]:

   practice_scope();i(states+0xa0,6);i(setup_state+0x38,2);i(D+4,1);i(D+192,0);i(D+184,0);i(D+188,0);i(D+168,user_team)

   q(B+0xfa582a0,manager if manager_ready else 0);cpu_calls.clear()

   put(M,bytes.fromhex('4883ec28')+imm('48b9',setup_state if owner else M+0xa800)+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))

   c.WINFUNCTYPE(None)(M)()

   active=owner and manager_ready and user_team in [0,1]

   assert cpu_calls==([(manager,1-user_team)] if active else []) and get(D+188)==int(active)

   assert get(D+192)==int(active)

   checks+=1

put(B+0x7b7e7e0,imm('48b8',saved+0x200)+bytes.fromhex('4889088950084889e14883e10f89480c')+clobber+b'\xc3')

q(B+0xfa582a0,manager)

for user_team in [0,1]:

 practice_scope();i(states+0xa0,6);i(setup_state+0x38,2);i(D+4,1);i(D+192,0);i(D+168,user_team)

 setup_expected=expected.copy();setup_expected[1]=setup_state

 setup_entry=bytes.fromhex('4883ec28')+imm('48b8',vectors)

 for x in range(6):setup_entry+=bytes.fromhex('f30f6f')+bytes([0x80+x*8])+struct.pack('<i',x*16)

 for opcode,value in zip(['48b8','48b9','48ba','49b8','49b9','49ba'],setup_expected):setup_entry+=imm(opcode,value)

 setup_entry+=bytes.fromhex('ff15')+struct.pack('<i',len(capture))+capture+struct.pack('<Q',M+0x100)

 put(M,setup_entry);c.WINFUNCTYPE(None)(M)()

 assert [c.c_uint64.from_address(saved+x*8).value for x in range(6)]==setup_expected

 assert c.string_at(saved+64,96)==bytes(range(96))

 assert c.c_uint64.from_address(saved+0x200).value==manager and get(saved+0x208)==1-user_team

 assert get(saved+0x20c)==8

 checks+=1

assert len(Path('Evidence/InitialSetupTick.bin').read_bytes())<=1024

# Fresh entry's placeholder possession can flip after player setup. The later

# play-call hook must use the initialized match, not the attachment-time team.

put(M+0x1000,Path('Evidence/InitialPlayCallTick.bin').read_bytes())

put(B+0x7b7e7e0,jump(c.cast(cpu_mock,c.c_void_p).value))

for user_team in [0,1]:

 practice_scope();i(D+4,1);i(D+180,0);i(D+192,0);i(D+184,0);i(D+188,0);i(D+168,1-user_team);i(match+0x1c8,1-user_team);cpu_calls.clear()

 q(B+0xfa582a0,manager);i(setup_state+0x38,2);i(initial_state+0x10,2);i(manager+0x50c,0)

 i(states+0xa0,6)

 put(M,bytes.fromhex('4883ec28')+imm('48b9',setup_state)+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'));c.WINFUNCTYPE(None)(M)()

 i(match+0x1c8,user_team);ready_choice(user_team);i(states+0xa0,5)

 put(M,bytes.fromhex('4883ec28')+imm('48b9',initial_state)+imm('48b8',M+0x1000)+bytes.fromhex('ffd04883c428c3'));c.WINFUNCTYPE(None)(M)()

 assert cpu_calls==[(manager,user_team)] and get(D+180)==0 and get(D+168)==user_team

 c.c_ubyte.from_address(manager+0x50c+user_team).value=1

 c.WINFUNCTYPE(None)(M)();c.WINFUNCTYPE(None)(M)()

 assert cpu_calls==[(manager,user_team),(manager,1-user_team)] and get(D+180)==1 and get(D+184)==2

 checks+=1

i(manager+0x50c,0x101)

# Regression: player setup's choice cannot suppress the later play-call

# choice, because native play-call entry can clear the setup registration.

put(M+0x1000,Path('Evidence/InitialPlayCallTick.bin').read_bytes())

put(B+0x7b7e7e0,jump(c.cast(cpu_mock,c.c_void_p).value))

for user_team in [0,1]:

 practice_scope();i(D+4,1);i(D+180,0);i(D+192,0);i(D+184,0);i(D+188,0);i(D+168,user_team);i(match+0x1c8,user_team);ready_choice(user_team);cpu_calls.clear()

 q(B+0xfa582a0,manager);i(setup_state+0x38,2);i(initial_state+0x10,2)

 for phase,obj,target in [(6,setup_state,M+0x100),(5,initial_state,M+0x1000),(6,setup_state,M+0x100),(5,initial_state,M+0x1000)]:

  i(states+0xa0,phase)

  put(M,bytes.fromhex('4883ec28')+imm('48b9',obj)+imm('48b8',target)+bytes.fromhex('ffd04883c428c3'))

  c.WINFUNCTYPE(None)(M)()

 assert cpu_calls==[(manager,1-user_team)]*2

 assert get(D+180)==get(D+192)==1 and get(D+184)==2 and get(D+188)==1

 checks+=1

# Bounded development recovery uses the actual phase-5 update and calls the

# corrected UI action once; its native prologue and registers stay intact.

from recovery_fixture import build as recovery_build

recovery_state=M+0xa600

put(M+0x100,recovery_build(D,B,recovery_state))

put(B+0x35fd200+14,bytes.fromhex('415f5dc3'))

put(B+0x7babdd0,imm('48b8',telemetry)+bytes.fromhex('ff00')+clobber+b'\xc3')

for phase in [1,3,4,5]:

 for owner in [False,True]:

  for requested in [0,1]:

   i(states+0xa0,phase);i(D,requested);i(D+4,0);i(telemetry,0)

   put(M,bytes.fromhex('4883ec28')+imm('48b9',recovery_state if owner else M+0xa800)+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))

   c.WINFUNCTYPE(None)(M)()

   active=phase==5 and owner and requested==1

   assert get(telemetry)==int(active) and get(D+4)==int(active)

   c.WINFUNCTYPE(None)(M)()

   assert get(telemetry)==int(active)

   checks+=1

i(states+0xa0,5);i(D,1);i(D+4,0);i(telemetry,0)

recovery_expected=expected.copy();recovery_expected[1]=recovery_state

recovery_entry=bytes.fromhex('4883ec28')+imm('48b8',vectors)

for x in range(6):recovery_entry+=bytes.fromhex('f30f6f')+bytes([0x80+x*8])+struct.pack('<i',x*16)

for opcode,value in zip(['48b8','48b9','48ba','49b8','49b9','49ba'],recovery_expected):recovery_entry+=imm(opcode,value)

recovery_entry+=bytes.fromhex('ff15')+struct.pack('<i',len(capture))+capture+struct.pack('<Q',M+0x100)

put(M,recovery_entry);c.WINFUNCTYPE(None)(M)()

assert [c.c_uint64.from_address(saved+x*8).value for x in range(6)]==recovery_expected

assert c.string_at(saved+64,96)==bytes(range(96))

assert get(telemetry)==get(D+4)==1

checks+=1

# Never consume a stale payload or choose before the helper publishes it.

put(M+0x100,Path('Evidence/InitialPlayCallTick.bin').read_bytes())

put(B+0x7b7e7e0,jump(c.cast(cpu_mock,c.c_void_p).value));q(B+0xfa582a0,manager)

for user in [0,1]:

 for fault in ['not-ready','wrong-team','changed-form','changed-set','changed-play','changed-kind','changed-package']:

  practice_scope();i(states+0xa0,5);i(initial_state+0x10,2);i(match+0x1c8,user);i(D+180,0);ready_choice(user);cpu_calls.clear();i(manager+0x50c,0x101)

  if fault=='not-ready':i(D+200,0)

  elif fault=='wrong-team':i(D+204,1-user)

  else:i(service+0x70+user*36+4*['changed-form','changed-set','changed-play','changed-kind','changed-package'].index(fault),99)

  put(M,bytes.fromhex('4883ec28')+imm('48b9',initial_state)+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))

  c.WINFUNCTYPE(None)(M)();assert not cpu_calls and get(D+180)==0

  assert get(D+200)==0;checks+=1

# Execute the real filtered submission wrapper with native registration mocked.

for rva in [0xe2cb918,0xae33f50,0x4ff5520,0x5f09980]:

 page=(B+rva)&~0xffff

 if page not in pages:

  assert k.VirtualAlloc(page,0x10000,0x3000,0x40)==page;pages.add(page)

bus=M+0xd000;vtable=M+0xd800;q(bus,vtable);q(B+0xe2cb918,bus)

register_calls=[];submit_calls=[];configure_calls=[]

Register=c.WINFUNCTYPE(None,c.c_void_p,c.c_int,c.c_void_p,c.c_void_p,c.c_int)

Submit=c.WINFUNCTYPE(None,c.c_void_p,c.c_int,c.c_void_p,c.c_void_p)

Configure=c.WINFUNCTYPE(None,c.c_void_p,c.c_int,c.c_int,c.c_int,c.c_int)

def register(this,team,dest,keys,package):

 register_calls.append((this,team,c.string_at(keys,20),package))

 assert c.string_at(dest+16,10)==b'\x00'*10 and c.string_at(dest+28,8)==struct.pack('<ii',999,-1)

 raw=c.string_at(keys,16);put(dest,raw[12:16]+raw[:12]+b'\x00'*20)

def submit(this,team,p1,p2):

 assert p1==p2;submit_calls.append((this,team,c.string_at(p1,36)))

registration_mock=Register(register);submission_mock=Submit(submit)

configuration_mock=Configure(lambda this,team,mode,form,set:configure_calls.append((this,team,mode,form,set)))

q(vtable+0x418,c.cast(registration_mock,c.c_void_p).value)

put(B+0x5f09980,jump(c.cast(submission_mock,c.c_void_p).value));put(B+0x4ff5520,jump(c.cast(configuration_mock,c.c_void_p).value))

put(B+0xae33f50,struct.pack('<iiii',-1,-1,-1,0))

put(M+0x100,Path('Evidence/DefenseCommit.bin').read_bytes())

for team,kind in [(team,kind) for team in [0,1] for kind in [1,11]]:

 keys=struct.pack('<iiiii',4,2,7,kind,0);put(D+208,keys);i(D+200,1);i(D+248,0);i(manager+0x50c,0)

 register_calls.clear();submit_calls.clear();configure_calls.clear()

 put(M,bytes.fromhex('4883ec28')+imm('48b9',manager)+bytes.fromhex('ba')+struct.pack('<i',team)+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))

 c.WINFUNCTYPE(None)(M)()

 assert register_calls==[(bus,team,keys,-1)]

 assert configure_calls==[(cfg,team,1,-1,-1)]

 assert submit_calls==[(service,team,keys[12:16]+keys[:12]+b'\x00'*20)]

 assert c.c_ubyte.from_address(manager+0x50c+team).value==1 and get(D+200)==0 and get(D+248)==1;checks+=1

assert len(Path('Evidence/DefenseCommit.bin').read_bytes())<=1024

assert len(Path('Evidence/DefenseCommit.bin').read_bytes())<=512

assert len(Path('Evidence/PlayCallRefresh.bin').read_bytes())<=512

# The explicit recovery redraw preserves volatile inputs and is consumed once.

put(M+0x100,Path('Evidence/InitialPlayCallTick.bin').read_bytes())

put(M+0x1000,Path('Evidence/PlayCallRefresh.bin').read_bytes());q(D+12392,M+0x1000)

put(B+0x7babb30,imm('48b8',saved+0x200)+bytes.fromhex('4889088950084889e14883e10f89480c')+clobber+b'\xc3')

practice_scope();i(states+0xa0,5);i(initial_state+0x10,2);i(D+12016,1);i(D+164,1);i(D+180,1)

refresh_expected=expected.copy();refresh_expected[1]=initial_state

refresh_entry=bytes.fromhex('4883ec28')+imm('48b8',vectors)

for x in range(6):refresh_entry+=bytes.fromhex('f30f6f')+bytes([0x80+x*8])+struct.pack('<i',x*16)

for opcode,value in zip(['48b8','48b9','48ba','49b8','49b9','49ba'],refresh_expected):refresh_entry+=imm(opcode,value)

refresh_entry+=bytes.fromhex('ff15')+struct.pack('<i',len(capture))+capture+struct.pack('<Q',M+0x100)

put(M,refresh_entry);c.WINFUNCTYPE(None)(M)()

assert [c.c_uint64.from_address(saved+x*8).value for x in range(6)]==refresh_expected

assert c.string_at(saved+64,96)==bytes(range(96))

assert c.c_uint64.from_address(saved+0x200).value==B+0xdadd380 and get(saved+0x208)==0 and get(saved+0x20c)==8

assert get(D+12016)==get(D+164)==0 and get(D+180)==1

i(saved+0x208,999);c.WINFUNCTYPE(None)(M)();assert get(saved+0x208)==999;checks+=1

# A defensive user registers bit 8, and the CPU must pick offense for the

# possession team. Verify both team indices, reentry, and stale payloads.

put(M+0x100,Path('Evidence/InitialPlayCallTick.bin').read_bytes())

put(B+0x7b7e7e0,jump(c.cast(cpu_mock,c.c_void_p).value))

for user in [0,1]:

 for fault in [None,'not-ready','wrong-team','wrong-role','both-ready','no-ready','changed-play','manager-wait','changed-possession']:

  practice_scope();i(states+0xa0,5);i(initial_state+0x10,2);i(match+0x1c8,1-user)

  i(D+168,1-user);i(D+180,0);i(D+184,0);ready_choice(user,True);i(manager+0x50c,0x101);cpu_calls.clear()

  if fault=='not-ready':i(D+200,0)

  elif fault=='wrong-team':i(D+204,1-user)

  elif fault=='wrong-role':i(D+12008,4)

  elif fault=='both-ready':i(service+0x28,13)

  elif fault=='no-ready':i(service+0x28,1)

  elif fault=='changed-play':i(service+0x70+user*36+12,99)

  elif fault=='manager-wait':c.c_ubyte.from_address(manager+0x50c+user).value=0

  elif fault=='changed-possession':i(match+0x1c8,user)

  put(M,bytes.fromhex('4883ec28')+imm('48b9',initial_state)+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))

  for _ in range(3):c.WINFUNCTYPE(None)(M)()

  assert cpu_calls==([(manager,1-user)] if fault is None else []),(user,fault,cpu_calls)

  assert get(D+180)==int(fault is None) and get(D+184)==int(fault is None)

  if fault is None:assert get(D+168)==user

  checks+=1

 # Flip back to offense, reopen, then flip to defense again. Each native

 # selection initializes its own opponent request; none is consumed early.

 for defending in [False,True,False,True]:

  i(D+180,1);i(D+200,1);cpu_calls.clear()

  for stage in [0,1]:

   i(initial_state+0x10,stage);c.WINFUNCTYPE(None)(M)();assert not cpu_calls and get(D+180)==get(D+200)==0

  i(initial_state+0x10,2);i(match+0x1c8,1-user if defending else user);ready_choice(user,defending);i(manager+0x50c,0x101)

  c.WINFUNCTYPE(None)(M)();c.WINFUNCTYPE(None)(M)()

  assert cpu_calls==[(manager,1-user)] and get(D+168)==user and get(D+180)==1;checks+=1

# Reopening native play calling (timeout or pause Choose New Play) must clear

# the previously consumed choice even when no plugin QueuePlayCall runs.

put(M+0x100,Path('Evidence/InitialPlayCallTick.bin').read_bytes())

put(B+0x7b7e7e0,jump(c.cast(cpu_mock,c.c_void_p).value))

for user in [0,1]:

 for reopen in ['timeout','pause-choose-new-play']:

  practice_scope();i(states+0xa0,5);i(match+0x1c8,user);i(D+4,0);i(D+180,1);i(D+184,0);i(D+200,1);i(D+164,1);cpu_calls.clear()

  put(M,bytes.fromhex('4883ec28')+imm('48b9',initial_state)+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))

  for stage in [0,1]:

   i(initial_state+0x10,stage);c.WINFUNCTYPE(None)(M)()

   assert get(D+180)==get(D+200)==get(D+164)==0 and not cpu_calls

  i(initial_state+0x10,2);i(manager+0x50c,0);c.WINFUNCTYPE(None)(M)();assert not cpu_calls

  ready_choice(user);c.c_ubyte.from_address(manager+0x50c+user).value=1

  c.WINFUNCTYPE(None)(M)();c.WINFUNCTYPE(None)(M)()

  assert cpu_calls==[(manager,1-user)] and get(D+180)==1 and get(D+184)==1;checks+=1

# A retained manager byte is not proof the new offense finished selection.

# The native completion notification must precede CPU registration.

for user in [0,1]:

 for old_flags in [1,17,25]:

  practice_scope();i(states+0xa0,5);i(initial_state+0x10,2);i(match+0x1c8,user);i(manager+0x50c,0x101)

  i(D+180,0);ready_choice(user);i(service+0x28,old_flags);cpu_calls.clear()

  c.WINFUNCTYPE(None)(M)();assert not cpu_calls and get(D+180)==0 and get(D+200)==0

  ready_choice(user);c.WINFUNCTYPE(None)(M)();c.WINFUNCTYPE(None)(M)()

  assert cpu_calls==[(manager,1-user)] and get(D+180)==1;checks+=1

# A menu re-spot is applied once, at unpaused preplay, without changing scores,

# counting a play, consuming the play-call request, or losing lateral position.

put(M+0x100,Path('Evidence/PlayCallTick.bin').read_bytes())

put(B+0x5d59530,bytes.fromhex('8991d0010000c3'))

put(B+0x5d59570,bytes.fromhex('f30f1189c0010000c3'))

put(B+0x5d595c0,bytes.fromhex('488b02488981680100008b4208898170010000c3'))

for disabled in [None,'enabled','mode','kind','match','practice']:

 for phase in [1,3,4,5,6]:

  for paused in [0,1]:

   practice_scope(disabled);i(states+0xa0,phase);c.c_ubyte.from_address(pause).value=paused

   i(D+4,1);i(D+252,1);i(D+24,41);i(D+28,0);i(D+164,0);i(D+72,13)

   f(D+32,9);f(D+36,45);f(D+40,2);i(D+44,1);f(D+48,50)

   i(match+0x1d0,4);f(match+0x16c,-15);f(match+0x1c0,-5);i(match+0x1d8,7);i(match+0x1dc,4)

   put(M,bytes.fromhex('4883ec28')+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'));c.WINFUNCTYPE(None)(M)()

   active=disabled is None and phase==1 and not paused

   assert get(D+28)==(41 if active else 0) and get(D+4)==get(D+252)==int(not active)

   assert get(match+0x1d0)==(1 if active else 4)

   assert c.c_float.from_address(match+0x16c).value==(45 if active else -15)

   assert c.c_float.from_address(match+0x1c0).value==(50 if active else -5)

   if active:assert c.c_float.from_address(match+0x168).value==9 and c.c_float.from_address(match+0x170).value==2

   assert get(D+72)==13 and get(D+164)==0 and get(match+0x1d8)==7 and get(match+0x1dc)==4

   c.WINFUNCTYPE(None)(M)();assert get(D+72)==13;checks+=1

# Scrimmage applies possession, pre-snap baseline, score and field position

# together on the game thread; a second tick cannot apply the handoff again.

put(B+0x5d59f20,bytes.fromhex('8991c8010000')+imm('48b8',telemetry)+bytes.fromhex('ff00c3'))

put(B+0x35e2110,bytes.fromhex('44898491d8010000c3'))

for target in [0,1]:

 for old in [0,1]:

  for phase in [1,3,4,5]:

   for paused in [0,1]:

    practice_scope();i(states+0xa0,phase);c.c_ubyte.from_address(pause).value=paused

    i(D+4,1);i(D+252,1);i(D+24,91);i(D+28,0);i(D+12048,1);i(D+12052,target)

    i(D+84,1);i(D+88,6);i(D+92,2);i(D+164,0);i(telemetry,0)

    i(match+0x1c8,old);i(match+0x1cc,old);i(match+0x1d8,0);i(match+0x1dc,0)

    f(D+36,-25);f(D+48,-15);i(D+44,1)

    put(M,bytes.fromhex('4883ec28')+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))

    c.WINFUNCTYPE(None)(M)();active=phase==1 and not paused

    assert get(match+0x1c8)==get(match+0x1cc)==(target if active else old)

    assert get(D+12048)==get(D+84)==int(not active) and get(D+28)==(91 if active else 0)

    assert get(match+0x1d8)==(6 if active else 0) and get(match+0x1dc)==(2 if active else 0)

    if active:assert c.c_float.from_address(match+0x16c).value==-25

    c.WINFUNCTYPE(None)(M)();assert get(telemetry)==int(active and old!=target);checks+=1

assert len(Path('Evidence/PlayCallTick.bin').read_bytes())<=1024

# Mode changes at an already selected pre-snap play reset the drive/score

# while preserving that selection, including the native cached-ready state

# observed in the hang. Do not request a duplicate UI or fabricate a choice.

put(M+0x100,Path('Evidence/PlayCallTick.bin').read_bytes())

put(B+0x5d0e950,jump(c.cast(transition_mock,c.c_void_p).value))

put(B+0x7babb30,jump(c.cast(ui_mock,c.c_void_p).value))

for team in [0,1]:

 for paused in [0,1]:

  practice_scope();i(states+0xa0,1);i(D+164,0);i(D+4,1);i(D+252,1);i(D+12048,1);i(D+12052,team)

  i(D+84,1);i(D+88,0);i(D+92,0);i(D+24,117);i(D+28,0);i(D+44,1);f(D+36,-15);f(D+48,-5)

  i(match+0x1c8,team);i(match+0x1cc,team);i(match+0x1d8,6);i(match+0x1dc,4)

  i(manager+0x50c,0x101);i(service+0x28,17);i(D+72,13)

  c.c_ubyte.from_address(pause).value=paused;ui_calls.clear();transition_calls.clear()

  put(M,bytes.fromhex('4883ec28')+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))

  c.WINFUNCTYPE(None)(M)()

  assert not ui_calls and not transition_calls and get(states+0xa0)==1

  assert get(manager+0x50c)==0x101 and get(service+0x28)==17 and get(D+72)==13

  assert get(D+28)==(0 if paused else 117) and get(match+0x1c8)==team

  assert get(match+0x1d8)==(6 if paused else 0) and get(match+0x1dc)==(4 if paused else 0)

  if not paused:

   assert get(match+0x1d0)==1 and c.c_float.from_address(match+0x16c).value==-15 and c.c_float.from_address(match+0x1c0).value==-5

  c.WINFUNCTYPE(None)(M)();assert not ui_calls and not transition_calls and get(D+72)==13

  checks+=1

# Regression: a handoff must open the next picker before returning to native

# preplay, where stale offensive plays caused repeated penalties. Validate the

# complete placement -> phase transition -> UI sequence in a single execution.

put(M+0x100,Path('Evidence/PlayCallTick.bin').read_bytes())

put(B+0x5d0e950,jump(c.cast(transition_mock,c.c_void_p).value))

put(B+0x7babb30,jump(c.cast(ui_mock,c.c_void_p).value))

for target in [0,1]:

 for user in [0,1]:

  for phase in [1,3,4,5]:

   for paused in [0,1]:

    practice_scope();i(states+0xa0,phase);c.c_ubyte.from_address(pause).value=paused

    i(D+4,1);i(D+252,1);i(D+24,101);i(D+28,0);i(D+12048,1);i(D+12052,target)

    i(D+164,1);i(D+168,user);i(D+172,0);i(D+84,1);i(D+88,6);i(D+92,2)

    i(match+0x1c8,1-target);i(match+0x1cc,1-target);f(D+36,15);f(D+48,25);i(D+44,1)

    q(B+0xfa582a0,manager);ui_calls.clear();transition_calls.clear()

    put(M,bytes.fromhex('4883ec28')+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))

    c.WINFUNCTYPE(None)(M)();active=phase==1 and not paused

    assert ui_calls==([(B+0xdadd380,0)] if active else [])

    assert transition_calls==([(states,5)] if active else [])

    assert get(D+164)==int(not active) and get(D+172)==int(active)

    assert get(D+28)==(101 if active else 0) and get(match+0x1cc)==(target if active else 1-target)

    assert get(D+168)==user

    c.WINFUNCTYPE(None)(M)();assert len(ui_calls)==int(active)

    checks+=1



# The mode action publishes only in the bound offline practice session. Its

# fallback replays the native RIP-relative prologue correctly under ASLR.

put(M+0x100,Path('Evidence/ModeDispatch.bin').read_bytes())

put(B+0x7c17d20,bytes.fromhex('b86f000000c3'))

put(B+0x7bcad50+19,imm('48b8',telemetry)+bytes.fromhex('488908c700de0000004883c4205bc3'))

for disabled in [None,'enabled','mode','kind','match','practice']:

 for bound_menu in [False,True]:

  practice_scope(disabled);q(D+12040,expression if bound_menu else 0);i(D+12032,0);i(telemetry,0)

  put(M,bytes.fromhex('4883ec28')+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))

  result=c.WINFUNCTYPE(c.c_int)(M)();active=disabled is None and bound_menu

  assert get(D+12032)==int(active)

  assert get(telemetry)==(0 if active else 222)

  if active:assert result==111

  else:assert c.c_uint64.from_address(telemetry).value==((B+0xbae5718)&~0xffffffff)|222

  checks+=1

assert len(Path('Evidence/ModeDispatch.bin').read_bytes())<=1024

# Native binding refreshes the source strings. Substitute the current mode

# only for the two exact bound TextMedia values, leaving other UI untouched.

put(M+0x100,Path('Evidence/ModeText.bin').read_bytes())

put(B+0x25a3060+15,bytes.fromhex('4c89c04883c4405f488b5c2410488b742418c3'))

q(D+12056,M+0xd038);q(D+12064,M+0xd238)

for disabled in [None,'enabled','mode','kind','match','practice']:

 for source in [M+0xd038,M+0xd238,M+0xe038]:

  practice_scope(disabled)

  put(M,bytes.fromhex('4883ec28')+imm('49b8',source)+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))

  result=c.WINFUNCTYPE(c.c_uint64)(M)()

  expected=D+12100 if source==M+0xd038 else D+12164 if source==M+0xd238 else source

  assert result==(expected if disabled is None else source),(disabled,source,hex(result))

  checks+=1

assert len(Path('Evidence/ModeText.bin').read_bytes())<=1024


# Default Practice keeps only the mode selector active. Native Stats and all
# gameplay hooks continue through their original paths while D.enabled is zero.
for enabled in [0,1]:
 for menus in [0,1]:
  for disabled in [None,'mode','kind','match','practice']:
   practice_scope(disabled);i(D,enabled);i(D+12368,menus)
   q(D+12040,expression+32);q(D+120,expression)
   for selected in [expression,expression+32,expression+48]:
    put(M+0x100,Path('Evidence/StatsVisibility.bin').read_bytes())
    put(M,bytes.fromhex('4883ec28')+imm('48b9',selected)+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))
    value=c.WINFUNCTYPE(c.c_int)(M)()
    active=disabled is None and (enabled==1 or menus==1)
    assert value==int(active and (selected==expression+32 or selected==expression and enabled==1))
    checks+=1
   put(M+0x100,Path('Evidence/ModeDispatch.bin').read_bytes());i(D+12032,0)
   put(M,bytes.fromhex('4883ec28')+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))
   c.WINFUNCTYPE(c.c_int)(M)();assert get(D+12032)==int(active);checks+=1
   put(M+0x100,Path('Evidence/ModeText.bin').read_bytes())
   put(M,bytes.fromhex('4883ec28')+imm('49b8',M+0xd038)+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))
   value=c.WINFUNCTYPE(c.c_uint64)(M)()
   assert value==(D+12100 if active else M+0xd038);checks+=1

# Execute both directions through the complete mode-entry thunk. It must run
# once on an unpaused safe phase, restore the spot and clear scores, then choose
# the native two-sided picker or the custom one-sided picker as appropriate.
restore_address=M+0x1000
restore_body=Path('Evidence/DefaultPracticeRestore.bin').read_bytes()
assert len(restore_body)<=2048
put(restore_address,restore_body)
native_ui_calls=[]
native_ui_mock=UiShow(lambda this,delay:native_ui_calls.append((this,delay)))
put(B+0x7babdd0,jump(c.cast(native_ui_mock,c.c_void_p).value))
put(B+0x7babb30,jump(c.cast(ui_mock,c.c_void_p).value))
put(B+0x5d0e950,jump(c.cast(transition_mock,c.c_void_p).value))
put(B+0x5d59530,bytes.fromhex('8991d0010000c3'))
put(B+0x5d59570,bytes.fromhex('f30f1189c0010000c3'))
put(B+0x5d595c0,bytes.fromhex('488b02488981680100008b4208898170010000c3'))
put(B+0x35e2110,bytes.fromhex('44898491d8010000c3'))
put(B+0x369ed10,bytes.fromhex('89512044884124c3'))
q(B+0xf547540,ui);q(B+0xf547a00,pause)
for request in [0,1,2,3]:
 for enabled in [0,1]:
  for disabled in [None,'mode','kind','match','practice','menus']:
   for phase in [1,3,4,5,6]:
    for paused in [0,1]:
     practice_scope(disabled);i(D,enabled);i(D+12368,0 if disabled=='menus' else 1)
     i(D+12372,request);i(D+12384,0);i(D+12448,0);i(initial_state+0x10,0);i(states+0xa0,phase);c.c_ubyte.from_address(pause).value=paused
     i(D+4,1);i(D+252,1);i(D+164,1);i(D+200,1);i(D+24,119);i(D+28,0)
     i(manager+0x50c,0)
     i(D+12048,1 if request==2 else 0);i(D+12052,0);i(match+0x1c8,0);i(match+0x1cc,1)
     i(D+44,1);f(D+32,3);f(D+36,-15);f(D+40,2);f(D+48,-5)
     i(D+84,1);i(D+88,0);i(D+92,0);i(match+0x1d8,6);i(match+0x1dc,4)
     i(match+0x1d0,4);f(match+0x16c,25);f(match+0x1c0,40);i(ui+0x20,-1)
     ui_calls.clear();native_ui_calls.clear();transition_calls.clear();cpu_calls.clear()
     put(M,bytes.fromhex('4883ec28')+imm('48b8',restore_address)+bytes.fromhex('ffd04883c428c3'))
     c.WINFUNCTYPE(None)(M)()
     scope=disabled is None or disabled=='menus' and enabled==1
     active=scope and request in [1,2] and enabled==request-1 and phase in [1,5] and not paused
     assert get(D+12384)==0,(request,enabled,disabled,phase,paused)
     assert get(D+12372)==(0 if active else request)
     assert get(D+4)==get(D+252)==get(D+164)==get(D+200)==int(not active)
     assert get(D+28)==0
     if active:
      assert c.c_uint64.from_address(D+12400).value==match and c.c_uint64.from_address(D+12408).value==states
      assert get(D+12416)==0 and get(D+12420)==1
      assert c.c_float.from_address(D+12424).value==-15 and c.c_float.from_address(D+12428).value==-5
      assert get(D+12432)==get(D+12436)==0 and get(D+12440)==get(D+72) and get(D+12444)==119
     assert get(D+12048)==(0 if active else int(request==2))
     assert get(match+0x1cc)==(0 if active and request==2 else 1)
     assert get(match+0x1d0)==(1 if active else 4)
     assert c.c_float.from_address(match+0x16c).value==(-15 if active else 25)
     assert c.c_float.from_address(match+0x1c0).value==(-5 if active else 40)
     assert get(match+0x1d8)==(0 if active else 6) and get(match+0x1dc)==(0 if active else 4)
     assert get(ui+0x20)==(20 if active else -1)
     assert transition_calls==([(states,5)] if active else [])
     assert not native_ui_calls and not ui_calls
     assert not cpu_calls and get(D)==enabled
     assert get(D+12448)==(request if active else 0)
     if active:
      # Native initialization clears selections in stages 0/1. Showing UI then
      # loses a cached/fast choice, which reproduced the mode-switch hang.
      for stage in [0,1]:
       i(initial_state+0x10,stage);c.WINFUNCTYPE(None)(M)()
       assert not native_ui_calls and not ui_calls and get(D+28)==0 and get(D+12384)==0
      i(initial_state+0x10,2);c.WINFUNCTYPE(None)(M)()
      assert get(D+28)==119 and get(D+12448)==0 and get(D+12384)==1
      assert native_ui_calls==([(B+0xdadd380,0)] if request==1 else [])
      assert ui_calls==([(B+0xdadd380,0)] if request==2 else [])
     c.WINFUNCTYPE(None)(M)();assert get(D+12384)==int(active)
     checks+=1

# Native callers can reach the queued entry from either preplay's early player
# check or an existing play picker. The full registers/SSE ABI survives the call.
for picker in ['EarlyPlayCall','InitialPlayCallTick']:
 practice_scope();i(D,0);i(D+12368,1);i(D+12372,1);i(D+12384,0)
 i(states+0xa0,1 if picker=='EarlyPlayCall' else 5);i(initial_state+0x10,0);i(D+12448,0);c.c_ubyte.from_address(pause).value=0
 i(D+84,1);i(D+88,0);i(D+92,0);q(D+12376,restore_address)
 put(M+0x100,Path('Evidence/'+picker+'.bin').read_bytes())
 # Let each original prologue/epilogue execute after the pending entry is handled.
 if picker=='EarlyPlayCall':
  # RCX is a valid object for the original instruction sequence.
  arg=obj
 else:arg=initial_state
 ui_calls.clear();native_ui_calls.clear();transition_calls.clear()
 put(M,bytes.fromhex('4883ec28')+imm('48b9',arg)+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))
 c.WINFUNCTYPE(None)(M)()
 assert get(D+12384)==0 and get(states+0xa0)==5 and not native_ui_calls and not ui_calls
 i(initial_state+0x10,2);c.WINFUNCTYPE(None)(M)()
 assert get(D+12384)==1 and len(native_ui_calls)==1 and not ui_calls
 c.WINFUNCTYPE(None)(M)();assert get(D+12384)==1 and len(native_ui_calls)==1 and not ui_calls
 checks+=1
q(D+12376,0)

# Existing plays survive every mode switch. Do not ask the UI to reopen a
# one-sided picker from the paused two-sided flow when both plays are ready.
for team in [0,1]:
 for phase in [1,5]:
  practice_scope();i(D,1);i(D+12368,1);i(D+12372,2);i(D+12384,0)
  i(D+12448,0);i(D+24,141);i(D+28,0);i(states+0xa0,phase);c.c_ubyte.from_address(pause).value=0
  i(manager+0x50c,0x101);i(D+12048,1);i(D+12052,team);i(match+0x1c8,team)
  i(initial_state+0x10,0)
  i(D+44,1);f(D+36,-15);f(D+48,-5);i(D+84,0)
  ui_calls.clear();native_ui_calls.clear();transition_calls.clear();cpu_calls.clear()
  put(M,bytes.fromhex('4883ec28')+imm('48b8',restore_address)+bytes.fromhex('ffd04883c428c3'))
  c.WINFUNCTYPE(None)(M)()
  assert get(states+0xa0)==5 and get(D+28)==0 and not ui_calls and not native_ui_calls
  for stage in [0,1]:
   i(initial_state+0x10,stage);c.WINFUNCTYPE(None)(M)()
   assert get(D+28)==0 and get(D+12384)==0
  i(initial_state+0x10,2);c.WINFUNCTYPE(None)(M)()
  assert get(states+0xa0)==1 and get(D+28)==141 and get(D+12384)==1 and get(D+12448)==0
  assert get(manager+0x50c)==0x101 and not ui_calls and not native_ui_calls and not cpu_calls
  assert get(D+12416)==team and get(D+12420)==1
  c.WINFUNCTYPE(None)(M)();assert get(D+12384)==1
  checks+=1
i(manager+0x50c,0)

# Returning to Default Practice always opens the native two-sided picker,
# including when both cached plays are ready. Never silently reuse a custom
# mode's CPU selection (which may be a Hail Mary on offense).
for team in [0,1]:
 for phase in [1,5]:
  for readiness in [0,1,0x100,0x101]:
   practice_scope();i(D,0);i(D+12368,1);i(D+12372,1);i(D+12384,0)
   i(D+12448,0);i(D+24,142);i(D+28,0);i(states+0xa0,phase);c.c_ubyte.from_address(pause).value=0
   i(manager+0x50c,readiness);i(match+0x1c8,team);i(D+12048,0)
   i(initial_state+0x10,0);i(D+44,1);f(D+36,-15);f(D+48,-5);i(D+84,0)
   ui_calls.clear();native_ui_calls.clear();transition_calls.clear();cpu_calls.clear()
   put(M,bytes.fromhex('4883ec28')+imm('48b8',restore_address)+bytes.fromhex('ffd04883c428c3'))
   c.WINFUNCTYPE(None)(M)()
   assert get(states+0xa0)==5 and get(D+28)==0 and not native_ui_calls
   for stage in [0,1]:
    i(initial_state+0x10,stage);c.WINFUNCTYPE(None)(M)()
    assert get(D+28)==0 and not native_ui_calls
   i(initial_state+0x10,2);c.WINFUNCTYPE(None)(M)()
   assert native_ui_calls==[(B+0xdadd380,0)] and not ui_calls and not cpu_calls
   assert transition_calls==[(states,5)] and get(states+0xa0)==5
   assert get(D+28)==142 and get(D+12384)==1 and get(D+12448)==0
   c.WINFUNCTYPE(None)(M)()
   assert native_ui_calls==[(B+0xdadd380,0)] and get(D+12384)==1
   checks+=1
i(manager+0x50c,0)

# Directly check volatile integer and vector preservation around native calls.
practice_scope();i(D,0);i(D+12368,1);i(D+12372,1)
i(states+0xa0,1);c.c_ubyte.from_address(pause).value=0
put(vectors,bytes(range(96)))
abi_entry=bytes.fromhex('4883ec28')+imm('48b8',vectors)
for x in range(6):abi_entry+=bytes.fromhex('f30f6f')+bytes([0x80+x*8])+struct.pack('<i',x*16)
abi_expected=[0x1111222233334444,0x2222333344445555,0x3333444455556666,0x4444555566667777,0x5555666677778888,0x6666777788889999]
for opcode,value in zip(['48b8','48b9','48ba','49b8','49b9','49ba'],abi_expected):abi_entry+=imm(opcode,value)
abi_entry+=bytes.fromhex('ff15')+struct.pack('<i',len(capture))+capture+struct.pack('<Q',restore_address)
put(M,abi_entry);c.WINFUNCTYPE(None)(M)()
assert [c.c_uint64.from_address(saved+x*8).value for x in range(6)]==abi_expected
assert c.string_at(saved+64,96)==bytes(range(96));checks+=1


# A fast first snap may finish before the helper observes the start ACK. Keep
# its preplay counter/spot, then publish the ACK after UI has been entered.
first_snap_entry=M+0xf000;first_snap_gate=M+0xf100
put(first_snap_gate,Path('Evidence/Gate2.bin').read_bytes())
put(first_snap_entry,bytes.fromhex('53564883ec28')+imm('48bb',obj)+bytes.fromhex('33f6')+jump(first_snap_gate))
put(B+skips[2],ending(1))
first_snap_calls=[]
def finish_first_snap(this,delay):
 assert get(D+28)==0 and get(D+12444)==151
 assert get(D+12440)==17 and c.c_float.from_address(D+12424).value==-15
 first_snap_calls.append((this,delay))
 i(states+0xa0,4);f(match+0x16c,-11);i(match+0x1d0,2)
 q(states+0x50,play);i(play+0x250,0);c.c_ubyte.from_address(play+0x3a8).value=0;f(play+0x234,4)
 c.WINFUNCTYPE(None)(first_snap_entry)()
first_snap_mock=UiShow(finish_first_snap)
put(B+0x7babb30,jump(c.cast(first_snap_mock,c.c_void_p).value))
for team in [0,1]:
 practice_scope();i(D+12368,1);i(D+12372,2);i(D+12384,0)
 i(D+24,151);i(D+28,0);i(D+72,17);i(initial_state+0x10,0);i(D+12448,0);i(states+0xa0,1);c.c_ubyte.from_address(pause).value=0
 i(match+0x1c8,team);i(D+12048,1);i(D+12052,team)
 i(D+44,1);f(D+32,0);f(D+36,-15);f(D+40,0);f(D+48,-5)
 i(D+84,1);i(D+88,0);i(D+92,0);first_snap_calls.clear();cpu_calls.clear()
 put(M,bytes.fromhex('4883ec28')+imm('48b8',restore_address)+bytes.fromhex('ffd04883c428c3'))
 c.WINFUNCTYPE(None)(M)()
 assert not first_snap_calls and get(D+28)==0
 i(initial_state+0x10,2);c.WINFUNCTYPE(None)(M)()
 assert first_snap_calls==[(B+0xdadd380,0)] and get(D+28)==151
 assert get(D+12440)==17 and get(D+72)==18 and c.c_float.from_address(D+76).value==4
 assert get(D+12416)==team and get(D+12420)==1 and get(match+0x1d0)==2
 assert c.c_float.from_address(D+12424).value==-15 and c.c_float.from_address(match+0x16c).value==-11
 c.WINFUNCTYPE(None)(M)()
 assert get(D+72)==18 and get(match+0x1d0)==2 and len(first_snap_calls)==1 and not cpu_calls
 checks+=1


# The same fast first snap must count when mode entry keeps the current plays
# and returns directly to preplay, with no UI notification at all.
def finish_cached_snap(this,phase):
 transition_calls.append((this,phase));i(this+0xa0,phase)
 if phase==1:finish_first_snap(B+0xdadd380,0)
cached_snap_mock=StateTransition(finish_cached_snap)
put(B+0x5d0e950,jump(c.cast(cached_snap_mock,c.c_void_p).value))
for team in [0,1]:
 practice_scope();i(D+12368,1);i(D+12372,2);i(D+12384,0)
 i(D+24,151);i(D+28,0);i(D+72,17);i(D+12448,0);i(states+0xa0,1);c.c_ubyte.from_address(pause).value=0
 i(manager+0x50c,0x101);i(match+0x1c8,team);i(D+12048,1);i(D+12052,team)
 i(initial_state+0x10,0)
 i(D+44,1);f(D+32,0);f(D+36,-15);f(D+40,0);f(D+48,-5)
 i(D+84,1);i(D+88,0);i(D+92,0);first_snap_calls.clear();cpu_calls.clear()
 put(M,bytes.fromhex('4883ec28')+imm('48b8',restore_address)+bytes.fromhex('ffd04883c428c3'))
 c.WINFUNCTYPE(None)(M)()
 assert not first_snap_calls and get(D+28)==0 and get(states+0xa0)==5
 i(initial_state+0x10,2);c.WINFUNCTYPE(None)(M)()
 assert first_snap_calls==[(B+0xdadd380,0)] and get(D+28)==151 and get(D+12448)==0
 assert get(D+12440)==17 and get(D+72)==18 and c.c_float.from_address(D+76).value==4
 assert get(D+12416)==team and get(D+12420)==1 and get(match+0x1d0)==2
 assert c.c_float.from_address(D+12424).value==-15 and c.c_float.from_address(match+0x16c).value==-11
 assert get(manager+0x50c)==0x101 and not cpu_calls
 checks+=1
put(B+0x5d0e950,jump(c.cast(transition_mock,c.c_void_p).value))

# CPU possession is a 32-bit field. It must never overwrite the high half of
# the 64-bit refresh-helper pointer, including after both teams have called plays.
refresh_high=D+0x5000
put(refresh_high,Path('Evidence/PlayCallRefresh.bin').read_bytes())
put(B+0x7babb30,jump(c.cast(ui_mock,c.c_void_p).value))
put(M+0x100,Path('Evidence/InitialPlayCallTick.bin').read_bytes())
for team in [0,1,0,1]:
 practice_scope();i(states+0xa0,5);i(initial_state+0x10,2)
 q(D+12392,refresh_high);i(D+12028,team);i(D+12016,1);i(D+180,0)
 q(D+12376,0);ui_calls.clear();cpu_calls.clear()
 put(M,bytes.fromhex('4883ec28')+imm('48b9',initial_state)+imm('48b8',M+0x100)+bytes.fromhex('ffd04883c428c3'))
 c.WINFUNCTYPE(None)(M)()
 assert c.c_uint64.from_address(D+12392).value==refresh_high
 assert get(D+12016)==0 and ui_calls==[(B+0xdadd380,0)] and not cpu_calls
 checks+=1

print('PASS: %d native hook execution checks (no game access)'%checks)

