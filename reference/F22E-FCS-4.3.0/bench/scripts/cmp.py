import pandas as pd, sys
S="/tmp/claude-0/-home-claude/91b81c1e-955b-555c-b741-a538fa553687/scratchpad/runs"
tags=sys.argv[1:]
m=pd.read_csv(f"{S}/{tags[0]}/summary.csv")
for t in tags[1:]:
    b=pd.read_csv(f"{S}/{t}/summary.csv")[["ias","sp","rs","ev","bStop","phi1","pS"]]
    b.columns=["ias","sp","rs","ev"]+[c+"_"+t for c in ["bStop","phi1","pS"]]
    m=m.merge(b,on=["ias","sp","rs","ev"])
m=m[m.alpha<45]
pd.set_option("display.width",300); pd.set_option("display.max_rows",300)
cols=["ias","sp","rs","ev","alpha","bStop"]+[f"bStop_{t}" for t in tags[1:]]+["pS"]+[f"pS_{t}" for t in tags[1:]]
print(m[cols].to_string(index=False,float_format=lambda x:f"{x:.1f}"))
for t in [""]+["_"+t for t in tags[1:]]:
    st=m[m.ev=="stop"]["bStop"+t]; rv=m[m.ev=="rev"]["bStop"+t]
    print(f"{t or tags[0]:10s} stop worst {st.max():.1f} mean {st.mean():.2f} n>3 {int((st>3).sum())} | rev worst {rv.max():.1f} mean {rv.mean():.2f} n>3 {int((rv>3).sum())}")
