---
title: "Procesos: verlos, controlarlos, matarlos"
order: 6
topic: procesos
module: operacion
estimated_minutes: 30
objective: "LPIC-1 103.5 - Crear, monitorizar y matar procesos"
tech_version: "procps-ng 4.x"
sources:
  - https://man7.org/linux/man-pages/man1/ps.1.html
  - https://man7.org/linux/man-pages/man7/signal.7.html
  - https://man7.org/linux/man-pages/man1/kill.1.html
---

# Procesos: verlos, controlarlos, matarlos

Cada programa en ejecucion es un **proceso** con su PID (Process ID). Diagnosticar
"que esta corriendo y por que se come la CPU/RAM" es probablemente el 30% del
trabajo operativo en DevOps.

## Ver procesos

```bash
ps aux                 # fotografia completa: usuario, %CPU, %MEM, comando
ps aux --sort=-%mem | head    # los que mas RAM consumen
ps -ef --forest        # arbol padre-hijo (quien lanzo a quien)
pgrep -a nginx         # PID(s) por nombre, sin ruido

top                    # monitor interactivo clasico (q para salir)
htop                   # version moderna (F9 mata, F6 ordena) si esta instalado
```

Columnas clave de `ps aux`: `%CPU`, `%MEM`, `STAT` (R corriendo, S durmiendo,
Z zombie, T detenido) y `TIME` (CPU acumulada).

## Senales: hablar con procesos

```bash
kill -TERM 1234        # o `kill 1234`: "por favor termina" (limpio)
kill -INT  1234        # como Ctrl+C
kill -HUP  1234        # muchos daemons releen su configuracion
kill -KILL 1234        # kill -9: ejecucion inmediata, SIN limpieza (ultimo recurso)
pkill -f "python app"  # matar por patron de comando
```

Orden profesional: TERM primero, espera, y solo entonces KILL. Un `-9` puede
dejar archivos corruptos o bloqueos sin liberar.

## Primer plano, segundo plano y trabajos

```bash
sleep 300 &            # & lo manda a background
jobs                   # trabajos de esta shell
fg %1                  # traer de vuelta al frente
bg %1                  # reanudar en segundo plano (tras Ctrl+Z)
nohup ./worker.sh &    # inmune a cierre de terminal
```

## Donde miran los procesos "desde dentro"

```bash
cat /proc/1234/status    # estado, memoria, padre...
ls -l /proc/1234/fd      # archivos que tiene abiertos (maravilla forense)
```

## Practica guiada

1. Lanza `sleep 600 &`, localizalo con `ps` y `pgrep`, terminalo con TERM.
2. Repite y usa `-9` (observa que desaparece al instante).
3. Abre `top` y observa el proceso con mas CPU mientras ejecutas `sha256sum /dev/urandom` en otra terminal.

## Fuentes oficiales

- ps(1), kill(1): https://man7.org/linux/man-pages/
- signal(7) (lista completa de senales): https://man7.org/linux/man-pages/man7/signal.7.html
- proc(5): https://man7.org/linux/man-pages/man5/proc.5.html
