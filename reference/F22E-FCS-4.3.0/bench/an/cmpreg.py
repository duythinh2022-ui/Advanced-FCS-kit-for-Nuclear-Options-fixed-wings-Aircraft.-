# compare reg.py summaries: cmpreg.py tag1 tag2 ... -> betaPk / pMax / rMax per case, side by side
import sys, os, pandas as pd
S=os.environ.get('REG_S','/tmp/claude-0/-home-claude/c3114abd-20fc-5984-be11-c25d56f03f2d/scratchpad')
tags=sys.argv[1:]; ds=[pd.read_csv(f'{S}/reg/{t}/summary.csv') for t in tags]
print(f"{'case':38s} " + ' | '.join(f'{t[:16]:>16s}' for t in tags)); print(f"{'':38s} " + ' | '.join(f"{'beta  pMax rMax':>16s}" for t in tags))
for i,c in enumerate(ds[0].case):
    print(f'{c[:38]:38s} ' + ' | '.join(f'{d.betaPk[i]:5.1f} {d.pMax[i]:4.0f} {d.rMax[i]:4.0f}' for d in ds))
print(f"{'worst beta / mean pMax / mean rMax':38s} " + ' | '.join(f'{d.betaPk.max():5.1f} {d.pMax.mean():4.0f} {d.rMax.mean():4.0f}' for d in ds))
b=ds[0].betaPk
for t,d in zip(tags[1:],ds[1:]):
    print(f'{t}: vs {tags[0]}  better {(d.betaPk<b-0.2).sum()}  worse {(d.betaPk>b+0.2).sum()}')
