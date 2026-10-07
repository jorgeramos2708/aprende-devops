# AGENTS.md

Guía para agentes/contribuidores del proyecto **Aprende DevOps**.

## Qué es esto

Plataforma de aprendizaje DevOps (estilo KodeKloud) con labs CLI en navegador,
exámenes por nivel, simulacros de certificación y vigilancia automática de
nuevas versiones de tecnologías. Producción: `aprende-devops.edrs.xyz`.

## Comandos

```bash
dotnet build src/DevOpsPlatform.sln          # backend
dotnet test  src/DevOpsPlatform.sln          # tests backend (xunit)
dotnet ef migrations add <Nombre> \
  --project src/DevOpsPlatform.Infrastructure \
  --startup-project src/DevOpsPlatform.Api   # nueva migración (requiere dotnet-ef 9)

cd web && npm ci                             # frontend
npm run lint && npm run typecheck && npm run test && npm run build
```

Si el equipo solo tiene runtime .NET 10+: `export DOTNET_ROLL_FORWARD=LatestMajor`.

## Convenciones (no romper)

- **IDs**: `Guid` v7 generados en app (`Guid.CreateVersion7()`). Prohibido volver
  a `Ulid`/`int` identity.
- **Esquema de BD**: la fuente de verdad son las **migraciones EF Core**
  (`src/DevOpsPlatform.Infrastructure/Migrations`). No crear SQL a mano para el
  esquema. La API ejecuta `MigrateAsync()` al arrancar.
- **Enums** se persisten como `text` (`HasConversion<string>`); JSON como `jsonb`.
- **Contenido educativo**: español, basado en documentación oficial (cada nodo
  declara `official_source`/fuentes y versión de la tecnología).
- **DI en ASP.NET**: servicios sin estado con dependencias de infraestructura
  (p. ej. `IContainerRuntime` sobre DockerClient) se registran **singleton**;
  los `BackgroundService` jamás consumen scoped directamente.
- **Minimal APIs**: sin valores por defecto en parámetros de lambda
  (no compila); usar tipos anulables.

## Infraestructura (reglas duras)

- El stack del repo corre en `/opt/aprende-devops` con
  `infra/docker-compose/docker-compose.prod.yml` y se une a la red externa
  **`caddy_net`** (Caddy/Zot/Uptime-Kuma/Vaultwarden viven fuera de este stack).
- **Ningún servicio publica puertos** al host; Caddy enruta por nombre de
  contenedor (`api:8080`, `web:80`).
- Redis es `redis:7-alpine` **sin módulos** (solo caché/tokens/Hangfire).
- Puertos públicos del VPS: **80, 443 (CF + directos grises), 2222 (SSH),
  13095 (aaPanel)**. No abrir más.
- En Cloudflare: `registry.edrs.xyz` y `vps-panel.edrs.xyz` son **grises**
  (DNS only) — nunca naranja (límite 100 MB de subida / puerto no proxiable).
- Labs de usuarios: contenedores **sin puertos publicados**, creados por la API
  vía `/var/run/docker.sock`; la terminal fluye por SignalR (`/hubs/`).

## Secretos

- App: `/opt/aprende-devops/.env` (raíz del repo; plantilla
  `infra/docker-compose/.env.example`; el compose la recibe vía `--env-file`).
- CI: secrets de GitHub listados en README. En código, jamás commitear valores.

## Roadmap de fases (contexto)

0. Saneamiento (este estado) → 1. MVP navegable → 2. Labs reales (Sysbox,
terminal robusta) → 3. Exámenes + certificados PDF (QuestPDF) → 3.5 Donativos
(GH Sponsors/Ko-fi/BMC → entitlements) → 4. Auto-update de versiones →
5. Pulido UI (shadcn) + obs → 6. Contenido continuo.
