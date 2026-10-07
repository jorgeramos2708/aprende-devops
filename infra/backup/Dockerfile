FROM alpine:3.20

# postgresql16-client: pg_dump | restic: backup cifrado a S3-compatible (Cloudflare R2)
RUN apk add --no-cache restic postgresql16-client dcron

COPY backup.sh /usr/local/bin/backup.sh
RUN chmod +x /usr/local/bin/backup.sh \
    # 04:00 UTC todos los dias; salida al log del contenedor (docker logs aprende-backup)
    && echo "0 4 * * * /usr/local/bin/backup.sh >> /proc/1/fd/1 2>&1" > /etc/crontabs/root

CMD ["crond", "-f", "-l", "2"]
