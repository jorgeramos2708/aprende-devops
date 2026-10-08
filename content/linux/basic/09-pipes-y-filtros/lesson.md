---
title: "Pipes, redirecciones y filtros de texto"
order: 9
topic: texto
module: fundamentos
estimated_minutes: 35
objective: "LPIC-1 103.2 / 103.4 - Filtros de texto y uso de streams y pipes"
tech_version: "coreutils 9 / grep 3.11"
sources:
  - https://man7.org/linux/man-pages/man1/grep.1.html
  - https://www.gnu.org/software/bash/manual/html_node/Redirections.html
  - https://man7.org/linux/man-pages/man1/find.1.html
---

# Pipes, redirecciones y filtros de texto

La filosofia Unix: programas pequenos que hacen una cosa bien, conectados por
tuberias. La salida de uno es la entrada del siguiente. Esto convierte tu
terminal en una fabrica de consultas ad-hoc sobre texto (logs, configs, CSVs).

## Los tres canales

Cada proceso abre: **stdin (0)**, **stdout (1)**, **stderr (2)**.

```bash
comando > salida.txt        # stdout a archivo (sobreescribe)
comando >> salida.txt       # idem, agregando al final
comando 2> errores.log      # stderr aparte
comando > todo.log 2>&1     # junta stderr en stdout (orden importa)
comando < entrada.txt       # stdin desde archivo
comando 2>/dev/null         # silencia errores
```

## El pipe `|`

```bash
cat /var/log/syslog | grep -i error | sort | uniq -c | sort -nr | head
```

Lee la cadena asi: "contenido del log → filtra lineas con 'error' → ordena →
cuenta repetidas → ordena por cuenta descendente → top 10". Acabas de construir
un mini-panel de errores sin levantar nada.

## grep y amigos

```bash
grep -Rni "password" /etc/      # recursivo, insensitive, numero de linea
grep -v "DEBUG" app.log         # invertir: todo menos DEBUG
grep -E '^[0-9]{4}-' app.log    # regex extendida (-E)
grep -oE "user=[a-z]+" app.log  # solo la parte que coincide

awk '{print $1, $4}' accesos.txt       # columnas por nombre
sed 's/http:/https:/g' urls.txt        # buscar-reemplazar en flujo
cut -d: -f1 /etc/passwd                # trocear por delimitador
```

## find: el buscador del sistema

```bash
find /var/log -name "*.log" -mtime -1      # logs modificados ultimas 24h
find /etc -type f -size +1M                # archivos de mas de 1MB
find . -name "*.tmp" -delete               # limpieza (prueba antes sin -delete!)
sudo find / -perm -4000                     # binarios setuid (auditoria)
```

## Practica guiada

1. De `/etc/passwd`, extrae solo los usuarios con shell bash usando pipes.
2. Encuentra los 5 archivos mas grandes de `/var`.
3. Cuenta cuantas lineas tienen "error" en alguno de tus logs.

## Fuentes oficiales

- grep(1), find(1): https://man7.org/linux/man-pages/
- Bash Redirections: https://www.gnu.org/software/bash/manual/html_node/Redirections.html
