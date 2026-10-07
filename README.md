# Aprende DevOps

Plataforma de aprendizaje de DevOps estilo KodeKloud: roadmap por tecnologías
(Linux → Git → Docker → Kubernetes → Terraform → CI/CD → Ansible → Observabilidad),
cada una con niveles **básico / intermedio / experto**, laboratorios con terminal
interactiva en el navegador, exámenes por nivel, examen final por tecnología,
simulacros de certificación oficial y un examen general que certifica el nivel
alcanzado. Todo basado en documentación oficial y con detección automática de
nuevas versiones de cada tecnología (TechWatcher).

- **Producción**: https://aprende-devops.edrs.xyz
- **Registry**: https://registry.edrs.xyz (Zot propio)

## Stack

| Capa | Tecnología |
|---|---|
| Frontend | React 18 + Vite + Tailwind (`web/`) |
| Backend | .NET 9 minimal APIs (`src/`) — Api, Core, Infrastructure, LabEngine, Workers (Hangfire), TechWatcher |
| Datos | PostgreSQL 16, Redis 7, MinIO |
| Labs | Contenedores efímeros por usuario vía Docker API + terminal xterm.js ↔ SignalR |
| Infra | Docker Compose en VPS Contabo; Caddy (externo, red `caddy_net`) como edge; Cloudflare DNS/proxy |
| CI/CD | GitHub Actions → build/push a Zot → deploy por SSH |

## Requisitos

- .NET SDK 9, Node 20+, Docker
- Solo para desarrollo local: PostgreSQL y Redis accesibles (ver `src/DevOpsPlatform.Api/appsettings.json`)

## Desarrollo local

```bash
# Backend
dotnet build src/DevOpsPlatform.sln
dotnet test  src/DevOpsPlatform.sln
dotnet run --project src/DevOpsPlatform.Api     # crea/esquema via migraciones EF y siembra demo

# Frontend
cd web && npm ci && npm run dev
```

La API aplica **migraciones EF Core al arrancar** y siembra contenido mínimo
(Linux/Git/Docker básicos) solo si la base está vacía.

## Despliegue (producción)

1. VPS: clonar el repo en `/opt/aprende-devops` y crear `/opt/aprende-devops/.env`
   (raíz del repo; el compose lo toma vía `--env-file`) a partir de
   `infra/docker-compose/.env.example`.
2. Push a `main`: CI compila, prueba, construye imágenes, hace push a Zot y
   despliega por SSH (`docker compose pull && up -d`), con health check final.

Secrets del repo (GitHub → Settings → Secrets):
`ZOT_CI_USER`, `ZOT_CI_PASSWORD`, `SSH_PRIVATE_KEY`, `SSH_KNOWN_HOSTS`,
`VM_USER`, `VM_HOST`.
Nota: el SSH del VPS escucha en el puerto **2222**; genera `SSH_KNOWN_HOSTS` con
`ssh-keyscan -p 2222 <ip-del-vps>`.

## Estructura

```
src/                  Solución .NET 9
web/                  Frontend React + Vite
infra/docker-compose/ Compose de producción, .env.example, Caddyfile de referencia
infra/backup/         Contenedor de backups (pg_dump + restic → Cloudflare R2)
infra/observability/  Configs del perfil `obs` (cadvisor/prometheus/grafana)
infra/terraform/      DNS en Cloudflare como código
tests/e2e/            Smoke tests
.github/workflows/    CI/CD + ejecución manual del TechWatcher
```

## Notas

- Caddy, Zot, Uptime Kuma y Vaultwarden ya operan en el VPS **fuera** de este
  stack; el compose se une a su red externa `caddy_net`.
- `infra/docker-compose/Caddyfile` es la **referencia versionada** del archivo
  vivo en el VPS (`~/caddy/Caddyfile`); aplicar manualmente si cambia.
- Observabilidad opcional: `docker compose -f infra/docker-compose/docker-compose.prod.yml --profile obs up -d`.
