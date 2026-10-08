---
title: "El shell y la terminal"
order: 1
topic: cli
module: fundamentos
estimated_minutes: 25
objective: "LPIC-1 103.1 - Trabajar en la linea de comandos"
tech_version: "bash 5.x / coreutils 9"
sources:
  - https://man7.org/linux/man-pages/man1/bash.1.html
  - https://www.gnu.org/software/bash/manual/
---

# El shell y la terminal

Todo en DevOps empieza aqui: la terminal. El **shell** es el programa que interpreta tus
comandos; el mas comun en Linux es **Bash** (Bourne Again Shell). Cuando abres una terminal,
Bash te presenta un *prompt* y espera instrucciones.

## Anatomia de un comando

```bash
comando  [opciones]  [argumentos]
ls       -la         /etc
```

- **comando**: el programa a ejecutar (`ls`)
- **opciones** (flags): modifican el comportamiento (`-la` = lista larga + ocultos)
- **argumentos**: sobre que actua (`/etc`)

Las opciones casi siempre tienen forma corta (`-a`) y larga (`--all`). Consulta siempre
la pagina de manual de cada comando con `man`:

```bash
man ls          # documentacion oficial local de ls
ls --help       # resumen rapido de opciones
```

## Tus primeros comandos

```bash
whoami          # quien eres (usuario actual)
hostname        # nombre del equipo
pwd             # directorio actual  (print working directory)
ls              # lista el contenido del directorio
ls -la /etc     # lista detallada (-l) incluyendo ocultos (-a)
cd /var/log     # cambia de directorio (change directory)
cd ..           # subir un nivel
cd ~            # tu directorio home
cd -            # volver al directorio anterior
```

## Ayudas de bash que te salvaran

| Atajo / comando | Que hace |
|---|---|
| `Tab` | autocompleta comandos y rutas |
| `Tab Tab` (doble) | muestra todas las opciones posibles |
| `Ctrl + R` | busca en tu historial de comandos |
| `Ctrl + C` | interrumpe el proceso en primer plano |
| `history` | historial de comandos ejecutados |
| `!42` | reejecuta el comando numero 42 del historial |

## Tipos de comandos

Bash distingue entre comandos **internos** (builtin, como `cd`, `echo`) y **externos**
(programas en disco, como `ls` en `/usr/bin/ls`). Descubre cual es cual con:

```bash
type cd        # -> cd is a shell builtin
type ls        # -> ls is /usr/bin/ls
which python3  # ruta del ejecutable que se ejecutaria
```

## Practica guiada (en papel mental, el lab llega pronto)

1. Abre una terminal y ejecuta `pwd` - donde caes?
2. Lista `/etc` en formato largo y localiza `passwd`.
3. Usa `man ls`, entra y sal con `q`.
4. Repite tu ultimo comando con `!!`.

## Fuentes oficiales

- bash(1) - GNU Bash manual: https://man7.org/linux/man-pages/man1/bash.1.html
- GNU Bash Reference Manual: https://www.gnu.org/software/bash/manual/
