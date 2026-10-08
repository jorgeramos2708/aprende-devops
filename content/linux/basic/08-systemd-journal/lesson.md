---
title: "systemd y journal: servicios y logs"
order: 8
topic: systemd
module: operacion
estimated_minutes: 35
objective: "LPIC-1 101.2 / 101.3 - Arranque del sistema y gestion de servicios"
tech_version: "systemd 252+"
sources:
  - https://www.freedesktop.org/software/systemd/man/systemctl.html
  - https://www.freedesktop.org/software/systemd/man/systemd.unit.html
  - https://www.freedesktop.org/software/systemd/man/journalctl.html
---

# systemd y journal: servicios y logs

**systemd** es el PID 1 y gestor del sistema en las distros que usaras (Debian,
Ubuntu, RHEL...). Todo lo que corre "como servicio" (tu web, tu API, tu base de
datos) casi siempre es una **unit** de systemd. Sus gemelos de trabajo:
`systemctl` (controla servicios) y `journalctl` (lee sus logs).

## systemctl: operar servicios

```bash
systemctl status nginx        # estado, PID, memoria, ultimos logs
sudo systemctl start nginx
sudo systemctl stop nginx
sudo systemctl restart nginx
sudo systemctl reload nginx   # relee config sin tumbar (si lo soporta)
sudo systemctl enable nginx   # arrancar con el sistema
sudo systemctl enable --now nginx  # enable + start en un paso
systemctl list-units --type=service --state=running
systemctl is-active nginx ; systemctl is-enabled nginx
```

Concepto de **estado**: `active (running)`, `inactive (dead)`, `failed`.
Un servicio `failed` con `journalctl -u` te dice el porque en segundos.

## journalctl: el diario del sistema

```bash
journalctl -u nginx              # logs de un servicio (unit)
journalctl -u nginx -f           # en vivo (tail -f del journal)
journalctl -u nginx --since "-1h"     # ultima hora
journalctl -p err -b             # errores desde este arranque (boot)
journalctl -b -1                 # logs del arranque anterior (caidas!)
journalctl --disk-usage          # cuanto ocupan los journals
```

Los journals son binarios indexados (no texto), con metadatos por campo. Bonus:
`journalctl -u sshd` es tu primera parada ante "no puedo entrar por SSH".

## Anatomia de una unit (adelanto que quitaras al dente en intermedio)

`/etc/systemd/system/miapp.service`:

```ini
[Unit]
Description=Mi aplicacion
After=network.target

[Service]
ExecStart=/opt/miapp/miapp
Restart=on-failure
User=miapp

[Install]
WantedBy=multi-user.target
```

Tras crearla o editarla: `sudo systemctl daemon-reload`.

## Practica guiada

1. `systemctl status` de un servicio activo (`cron`, `ssh`).
2. `journalctl -u` sobre ese mismo servicio; despues `-f` y genera eventos.
3. Localiza tus boots recientes con `journalctl --list-boots`.

## Fuentes oficiales

- systemctl(1), systemd.unit(5), journalctl(1): https://www.freedesktop.org/software/systemd/man/
