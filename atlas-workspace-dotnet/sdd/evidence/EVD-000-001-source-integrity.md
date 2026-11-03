# EVD-000-001 — Integridade das fontes do handoff

**Tipo:** verificação de auditoria (M0) · **Resultado:** PASS · **Data UTC:** 2026-10-27 (primeira execução) e 2026-10-28T02:12:01Z (re-execução ao fechar o M0 — saída integral em `integrity-run-2026-10-27.txt`)

## Comando (reproduzível)

```bash
cd ATLAS_COMMERCE_DOTNET10_MAF_SDD_AGENT_HANDOFF
python3 - <<'EOF'
import json, hashlib, os, datetime
print("UTC:", datetime.datetime.now(datetime.timezone.utc).isoformat(timespec="seconds"))
m = json.load(open('SOURCE_MANIFEST_SHA256.json'))
ok, missing, mismatch = 0, [], []
for f in m['files']:
    p = f['path']
    if not os.path.exists(p):
        missing.append(p); continue
    h = hashlib.sha256(open(p,'rb').read()).hexdigest()
    if h == f['sha256'] and os.path.getsize(p) == f['size_bytes']:
        ok += 1
    else:
        mismatch.append(p)
print(f"files_in_manifest={len(m['files'])} ok={ok} missing={missing} mismatch={mismatch}")
print("RESULT: PASS" if ok == len(m['files']) else "RESULT: FAIL")
EOF
```

## Saída (re-execução)

```text
UTC: 2026-10-28T02:12:01+00:00
files_in_manifest=32 ok=32 missing=[] mismatch=[]
RESULT: PASS
```

## Interpretação

- As 32 fontes do pacote de handoff conferem byte a byte (SHA-256 + tamanho) com `SOURCE_MANIFEST_SHA256.json` **antes e depois** da execução do M0 — o pacote de provenança permaneceu intocado durante a modelagem.
- Arquivos derivados criados pelo M0 (cópias de prompt/diagramas) vivem somente em `atlas-workspace-dotnet/` com diffs registrados em `architecture/diagrams/README.md`.

Ambiente: linux x86_64, python3 3.14.7, git 2.55.0 (ver EVD-000-002).
