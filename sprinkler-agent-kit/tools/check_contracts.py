"""Standard-library package checks. NOT a complete OpenAPI/JSON Schema validator."""
import json, math, re
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
spec = json.loads((ROOT/'contracts/openapi.json').read_text(encoding='utf-8'))
examples = json.loads((ROOT/'contracts/examples.json').read_text(encoding='utf-8'))
counts = {}
def count(kind): counts[kind] = counts.get(kind,0)+1
def resolve(ref):
    assert ref.startswith('#/'), 'Only local references checked'
    value=spec
    for token in ref[2:].split('/'): value=value[token.replace('~1','/').replace('~0','~')]
    return value
def walk(value):
    if isinstance(value,dict):
        if '$ref' in value: resolve(value['$ref']); count('resolved schema/parameter references')
        for v in value.values(): walk(v)
    elif isinstance(value,list):
        for v in value: walk(v)
walk(spec)
def valid(v,s):
    # Limited evaluator only for the keywords used by this package's examples.
    if '$ref' in s: return valid(v,resolve(s['$ref']))
    if 'allOf' in s and not all(valid(v,x) for x in s['allOf']): return False
    if 'oneOf' in s and sum(valid(v,x) for x in s['oneOf'])!=1: return False
    if 'const' in s and v!=s['const']: return False
    if 'enum' in s and v not in s['enum']: return False
    if 'type' in s:
        types=s['type'] if isinstance(s['type'],list) else [s['type']]
        checks={'null':v is None,'object':isinstance(v,dict),'array':isinstance(v,list),'string':isinstance(v,str),'number':type(v) in (int,float) and math.isfinite(v),'integer':type(v) is int,'boolean':type(v) is bool}
        if not any(checks[t] for t in types): return False
    if isinstance(v,dict):
        if not all(k in v for k in s.get('required',[])): return False
        if s.get('additionalProperties') is False and any(k not in s.get('properties',{}) for k in v): return False
        if not all(valid(v[k],sub) for k,sub in s.get('properties',{}).items() if k in v): return False
    if isinstance(v,list):
        if len(v)<s.get('minItems',0): return False
        if 'items' in s and not all(valid(x,s['items']) for x in v): return False
    if isinstance(v,str):
        if len(v)<s.get('minLength',0): return False
        if 'pattern' in s and not re.search(s['pattern'],v): return False
    if type(v) in (int,float) and 'minimum' in s and v<s['minimum']: return False
    return True
for expected,key in [(True,'valid'),(False,'invalid')]:
    for case in examples[key]:
        assert valid(case['value'],spec['components']['schemas'][case['schema']])==expected,case['name']
        count(key+' structural examples')
seen=set()
for path,methods in spec['paths'].items():
    for method,op in methods.items():
        assert op['operationId'] not in seen
        seen.add(op['operationId'])
        ps=[resolve(p['$ref']) if '$ref' in p else p for p in op['parameters']]
        assert set(re.findall(r'{([^}]+)}',path))=={p['name'] for p in ps if p['in']=='path'}
        assert all(p.get('required') for p in ps if p['in']=='path')
        if method=='post':
            assert any(p['name']=='X-Operation-Id' and p['required'] for p in ps)
            if '{projectId}' in path: assert any(p['name']=='If-Match' and p['required'] for p in ps)
        assert any(k.startswith('2') for k in op['responses'])
        count('operation shape/replay/precondition checks')
for path in ROOT.rglob('*.md'):
    for target in re.findall(r'\[[^\]]*\]\(([^)]+)\)',path.read_text(encoding='utf-8')):
        if '://' in target or target.startswith('#') or target.startswith('mailto:'): continue
        target=target.split('#')[0]
        assert (path.parent/target).exists(),f'{path}: missing {target}'
        count('existing Markdown link targets')
fixture=json.loads((ROOT/'fixtures/reference-network.json').read_text(encoding='utf-8'))
route=cut=0
for p in fixture['pieces']:
    length=math.dist(p['start_m'],p['end_m'])
    piece=length-p['start_net_offset_m']-p['end_net_offset_m']
    assert piece>0
    route+=length; cut+=piece
assert math.isclose(route,21,abs_tol=1e-10)
assert math.isclose(cut,19.77,abs_tol=1e-10)
count('synthetic route/cut baseline checks')
cases=[(math.hypot(3,4,12),13),(2*math.pi/2,math.pi),(12/2400*730,3.65),(6-.15-.1-.003-.003,5.744),(1+.025+.025,1.05),(math.ceil(13/5),3)]
for actual,expected in cases:
    assert math.isclose(actual,expected,abs_tol=1e-10)
    count('independent formula arithmetic checks')
assert set(spec['components']['schemas']['Ledger']['enum'])=={'route','fabrication','hydraulic','procurement'}
assert 'hydraulic' not in spec['components']['schemas']['TakeoffLedger']['enum']
count('ledger boundary checks')
print(json.dumps({'status':'PASS for listed limited checks','counts':counts,'limitations':['Not complete OpenAPI or JSON Schema conformance validation','No production implementation, real catalogue, UI, auth, concurrency or recovery tests','Synthetic baseline arithmetic is not real-project accuracy evidence']},indent=2))
