# game's PlayerThrottleAxis1Controls (relative, digital) + the Vortex STOVL detent postfix, 60 fps
dt=1/60; B=0.25; INW=1e-4; RE=0.02; HOLD=200; W=0.04
def run(seq, neg, start):
    sim = start if not neg else 2*start-1
    prev=0; st=dict(hasLast=False,last=0,holding=False,dir=0,t=0,unl=False)
    log=[]; t=0
    for (dur,key) in seq:
        n=int(round(dur/dt))
        for i in range(n):
            num=key; num4=abs(num-prev)
            if 0<num4<0.5: sim=num
            elif abs(num)>0.5: sim+=max(-dt,min(dt,num-sim))
            if not neg: sim=min(max(sim,0),1)
            else: sim=min(max(sim,-1),1)
            pub=0.5*(sim+1) if neg else sim; pub=min(max(pub,0),1)
            prev=num
            # postfix
            cmd=1 if key>0.1 else (-1 if key<-0.1 else 0); tag=''
            def pin(p):
                nonlocal sim,pub
                pub=p; sim=2*p-1 if neg else p
            if not st['hasLast']: st['hasLast']=True; st['last']=pub
            else:
                if st['unl'] and abs(pub-B)>RE: st['unl']=False
                done=False
                if st['holding']:
                    if cmd!=st['dir']:
                        st['holding']=False; st['t']=0; st['last']=pub; done=True
                    else:
                        st['t']+=dt
                        if st['t']*1000<HOLD:
                            pin(B-st['dir']*INW); st['last']=pub; tag='HOLD %d%%'%round(min(1,st['t']*1000/HOLD)*100); done=True
                        else:
                            st['holding']=False; st['t']=0; st['unl']=True
                if not done:
                    if (not st['unl']) and cmd!=0 and ((cmd>0 and st['last']<=B and pub>B) or (cmd<0 and st['last']>=B and pub<B)):
                        st['holding']=True; st['dir']=cmd; st['t']=0; pin(B-cmd*INW); st['last']=pub; tag='HOLD 0%'
                    else:
                        st['last']=pub
                        if abs(pub-B)<=W: tag='LOCK'
            t+=dt; log.append((t,key,pub,tag))
    return log
def show(name,log,every=6):
    print('==',name)
    for (t,k,p,tag) in log[::every]: print(f"  t {t:5.2f} key {k:+.0f} thr {p:.4f} {tag}")
    print(f"  final thr {log[-1][2]:.4f}")
for neg in (False,True):
    print('##### throttleUseNegative', neg)
    L=run([(1.0,1)],neg,0.0); 
    first=[x for x in L if x[3].startswith('HOLD')]; passed=[x for x in L if x[2]>B+1e-6]
    print(f"hold-through from 0: held from t {first[0][0]:.3f} to {first[-1][0]:.3f} ({(first[-1][0]-first[0][0])*1000:.0f} ms) at {first[0][2]:.4f}; past the detent at t {passed[0][0]:.3f}; final {L[-1][2]:.3f}")
    L=run([(0.65,1),(0.3,0),(0.15,1),(0.3,0)],neg,0.0)
    print(f"tap (stop) then 150 ms re-press: final thr {L[-1][2]:.4f} (should stay 0.2499)")
    L=run([(1.2,-1)],neg,0.6); first=[x for x in L if x[3].startswith('HOLD')]
    print(f"down from 0.6: held at {first[0][2]:.4f} for {(first[-1][0]-first[0][0])*1000:.0f} ms, final {L[-1][2]:.3f}")
    L=run([(1.0,1),(0.3,0),(0.04,-1),(0.5,0),(0.2,-1)],neg,0.0); 
    print(f"through, back down 40 ms (< 0.02 off, not re-armed), then down again: thr after {L[-1][2]:.4f}")
    L=run([(1.0,1),(0.3,0),(0.4,1),(0.3,0),(1.0,-1)],neg,0.0); first=[x for x in L if x[3].startswith('HOLD') and x[1]<0]
    print(f"through, well past, then down: re-armed hold on the way down at {first[0][2] if first else None}")
print('##### event-timed checks')
for neg,reach in ((False,0.25),(True,0.5)):
    L=run([(reach+0.1,1),(0.3,0),(reach*0+0.15,1),(0.3,0)],neg,0.0)
    print(f"neg {neg}: release 100 ms into the hold, re-press 150 ms: final {L[-1][2]:.4f} (stays 0.2499)")
    L=run([(reach+0.1,1),(0.3,0),(0.25,1),(0.3,0)],neg,0.0)
    print(f"neg {neg}: release 100 ms into the hold, re-press 250 ms: final {L[-1][2]:.4f} (goes through)")
    L=run([(reach+0.3,1),(0.2,0),(0.01,-1),(0.3,0),(0.4,-1)],neg,0.0)
    print(f"neg {neg}: through, nudge back < 0.02, then down 0.4 s: final {L[-1][2]:.4f} (no re-arm: passes below 0.25)")
