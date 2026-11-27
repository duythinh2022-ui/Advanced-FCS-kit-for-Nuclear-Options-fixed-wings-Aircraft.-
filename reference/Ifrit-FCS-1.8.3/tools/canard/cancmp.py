import sys,re,pandas as pd
tags=sys.argv[1:]; T={}
for t in tags:
    L=[l for l in open(f'/tmp/can_{t}.txt') if l.strip() and not l.startswith('/') and 'FAILED' not in l]
    hdr=L[0].split(); rows=[l.split() for l in L[1:]]
    df=pd.DataFrame(rows,columns=hdr).set_index('case'); T[t]=df
cases=T[tags[0]].index
for c in cases:
    line=f'{c:14s}'
    for t in tags:
        r=T[t].loc[c]
        st=r['settle']; cs=[x for x in st.split(';') if x[3]=='c']; cs=cs[0].split(':')[1] if cs else '-'
        line+=f' | {t}: b{float(r.bPk):4.1f} pS{float(r.pSmean) if r.pSmean!="NaN" else float("nan"):5.0f} cSt{float(r.canStall):.2f} set{cs}'
    print(line)
