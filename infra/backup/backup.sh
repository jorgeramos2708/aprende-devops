#!/bin/sh
# Backup diario: pg_dump (formato custom comprimido) + volumen MinIO/RustFS -> restic -> Cloudflare R2
#
# Proteccion del free tier de R2 (10 GB):
#   - retencion normal: 7 diarios + 4 semanales
#   - guarda de tamano (escalonada, cada corrida):
#       > 7 GiB  -> retencion reducida (5 diarios)
#       > 9 GiB  -> poda agresiva (2 ultimas) y ALERTA en log
# Requiere env: PGPASSWORD PGHOST PGUSER PGDATABASE RESTIC_REPOSITORY RESTIC_PASSWORD
#               AWS_ACCESS_KEY_ID AWS_SECRET_ACCESS_KEY (credenciales S3 de R2)
set -eu

SOFT_BYTES=$((7 * 1024 * 1024 * 1024))   # 7 GiB
HARD_BYTES=$((9 * 1024 * 1024 + 512 * 1024 * 1024))  # 9.5 GiB

STAMP="$(date +%Y%m%d-%H%M%S)"
DUMP="/tmp/devops_platform-${STAMP}.dump"

echo "[backup] $(date -Iseconds) iniciando pg_dump..."
pg_dump -Fc -f "$DUMP"

# Inicializa el repositorio restic solo la primera vez
restic snapshots >/dev/null 2>&1 || restic init

echo "[backup] subiendo a R2..."
restic backup "$DUMP" /data/minio --host vps-aprende-devops

# Retencion normal
restic forget --keep-daily 7 --keep-weekly 4 --prune

# Guarda de tamano del free tier (podas escalonadas)
SIZE="$(restic stats --mode raw-data --json | jq -r '.total_size // 0')"
echo "[backup] tamano del repositorio: ${SIZE} bytes (limite free: 10 GB)"

if [ "$SIZE" -gt "$HARD_BYTES" ]; then
  echo "[backup] ALERTA: repositorio > 9.5 GiB -> poda agresiva, revisar crecimiento anomalo"
  restic forget --keep-last 2 --prune
elif [ "$SIZE" -gt "$SOFT_BYTES" ]; then
  echo "[backup] repositorio > 7 GiB -> retencion reducida"
  restic forget --keep-daily 5 --keep-last 5 --prune
fi

rm -f "$DUMP"
echo "[backup] $(date -Iseconds) completado OK"
