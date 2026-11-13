# extract_damage.py <bundle.bin> <out.json> [rootName]
# Per-part damage data the game reads at run time, from the aircraft's own bundle: hit points, structural/integrity
# thresholds, joints (connected part, listed breakForce/Torque, anchor transform id - the game builds a FixedJoint per
# entry at x10 and rescales by hp on every hit), and each Turbofan's damageThreshold + critical parts (threshold, weight).
import UnityPy, json, sys
bp, out = sys.argv[1], sys.argv[2]; root = sys.argv[3] if len(sys.argv) > 3 else None
env = UnityPy.load(bp)
ms = {o.path_id: o.read_typetree()['m_ClassName'] for o in env.objects if o.type.name == "MonoScript"}
GO = {o.path_id: o.read_typetree()['m_Name'] for o in env.objects if o.type.name == "GameObject"}
tr_of_go = {}
for o in env.objects:
    if o.type.name in ("Transform", "RectTransform"):
        d = o.read_typetree(); tr_of_go[d['m_GameObject']['m_PathID']] = o.path_id
mb = {}
for o in env.objects:
    if o.type.name == "MonoBehaviour":
        d = o.read_typetree(); mb[o.path_id] = (ms.get(d['m_Script']['m_PathID']), GO.get(d['m_GameObject']['m_PathID']), d)
name = {pid: g for pid, (c, g, d) in mb.items()}
parts, engines = {}, {}
for pid, (c, g, d) in mb.items():
    if c in ('AeroPart', 'UnitPart'):
        a = d.get('armorProperties') or {}
        js = []
        for j in d.get('joints') or []:
            anc = j.get('anchor', {}).get('m_PathID', 0)
            js.append(dict(to=name.get(j['connectedPart']['m_PathID']), breakForce=j['breakForce'], breakTorque=j['breakTorque'],
                           anchorTid=str(anc) if anc else None))
        parts[g] = dict(cls=c, tid=str(tr_of_go.get(d['m_GameObject']['m_PathID'])), hp=d.get('hitPoints', 100.0),
                        structural=d.get('structuralThreshold', 0.0), integrity=max(d.get('integrityThreshold', 0.0), -1e30),
                        critical=int(d.get('criticalPart', 0)), joints=js,
                        armor=dict(pierceArmor=a.get('pierceArmor'), pierceTolerance=a.get('pierceTolerance'), blastTolerance=a.get('blastTolerance')))
    elif c == 'Turbofan':
        engines[g] = dict(damageThreshold=d.get('damageThreshold'), hp=d.get('hitPoints', 100.0),
                          critical=[dict(part=name.get(cp['part']['m_PathID']), threshold=cp['threshold'], weight=cp['weight']) for cp in d.get('criticalParts', [])])
json.dump(dict(source=bp, parts=parts, engines=engines), open(out, 'w'), indent=1)
print(len(parts), 'parts,', len(engines), 'engines ->', out)
