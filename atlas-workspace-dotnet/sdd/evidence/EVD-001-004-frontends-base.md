# EVD-001-004 — Apps base Next.js e Angular (TASK-001-011)

**Data UTC:** 2026-10-28 · **Resultado:** PASS · **Playwright:** pendente (requer browsers download — próximo ciclo ou CI)

## Comandos executados e saídas

```text
# Storefront (apps/storefront-next, gerado por create-next-app com TS/App Router/src-dir/alias @atlas)
$ pnpm install          → exit 0
$ pnpm build            → exit 0
  Route (app)  ○ /  ○ /_not-found   (Static — prerender OK)

# Control (apps/control-angular, gerado por ng new --defaults --strict --ssr=false)
$ npm install           → exit 0   (CLI declara packageManager npm)
$ npm run build         → exit 0
  Application bundle generation complete. [1.758 seconds]
  Output: dist/control-angular
```

## Notas

- Strict TS em ambos (flags do CLI); nenhum código de negócio ainda — base para M2 (Next) e M4/M5 (Angular).
- `create-next-app` gerou `AGENTS.md` próprio dentro do app (padrão do template; mantido).
- Angular em npm (packageManager do template); avaliar padronização para pnpm via corepack em ADR de tooling se o proprietário preferir (decisão menor, registrada aqui).
