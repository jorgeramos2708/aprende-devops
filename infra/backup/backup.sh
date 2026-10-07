#!/bin/sh
# Backup diario: pg_dump (formato custom comprimido) + volumen MinIO -> restic -> Cloudflare R2
# Requiere env: PGPASSWORD PGHOST PGUSER PGDATABASE RESTIC_REPOSITORY RESTIC_PASSWORD
#               AWS_ACCESS_KEY_ID AWS_SECRET_ACCESS_KEY (credenciales S3 de R2)
set -eu

STAMP="$(date +%Y%m%d-%H%M%S)"
DUMP="/tmp/devops_platform-${STAMP}.dump"

echo "[backup] $(date -Iseconds) iniciando pg_dump..."
pg_dump -Fc -f "$DUMP"

# Inicializa el repositorio restic solo la primera vez
restic snapshots >/dev/null 2>&1 || restic init

echo "[backup] subiendo a R2..."
restic backup "$DUMP" /data/minio --host vps-aprende-devops

# Retencion: 7 diarios + 4 semanales
restic forget --keep-daily 7 --keep-weekly 4 --prune

rm -f "$DUMP"
echo "[backup] $(date -Iseconds) completado OK"
