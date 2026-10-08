FROM alpine:3.20

# postgresql16-client: pg_dump | restic: backup cifrado a S3-compatible (Cloudflare R2)
# Sin dcron: en contenedor restricted (no-new-privileges + PID1) crond falla con
# "setpgid: Operation not permitted". Un loop con sleep hace lo mismo con menos piezas.
RUN apk add --no-cache restic postgresql16-client

COPY backup.sh /usr/local/bin/backup.sh
RUN chmod +x /usr/local/bin/backup.sh

# Primer backup 10 min tras el arranque, luego 1/dia (stdout -> docker logs)
CMD ["sh", "-c", "sleep 600; while :; do /usr/local/bin/backup.sh; sleep 86400; done"]
