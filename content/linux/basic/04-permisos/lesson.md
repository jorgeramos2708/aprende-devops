---
title: "Permisos y propiedad (rwx)"
order: 4
topic: permisos
module: seguridad-basica
estimated_minutes: 35
objective: "LPIC-1 104.5 - Gestionar permisos y propiedad de archivos"
tech_version: "coreutils 9"
sources:
  - https://man7.org/linux/man-pages/man1/chmod.1.html
  - https://man7.org/linux/man-pages/man1/chown.1.html
  - https://man7.org/linux/man-pages/man2/umask.2.html
---

# Permisos y propiedad (rwx)

El modelo de permisos de Linux es la primera capa de seguridad de tu servidor.
Un error aqui (permisos de una llave privada, por ejemplo) tiene consecuencias reales:
sshd rechaza trabajar con llaves demasiado abiertas.

## Leer el listado

```bash
ls -l archivo
# -rwxr-xr-- 1 jorge devops 4096 nov 10 10:00 archivo
#  ^^^^^^^^^
#  | ||| `--- otros:  r--
#  | ||`----- grupo:  r-x
#  | `|`---- dueno:  rwx
#  tipo: - archivo, d directorio, l enlace
```

- **r** (read=4): leer contenido (en directorio: listar)
- **w** (write=2): modificar (en directorio: crear/borrar dentro)
- **x** (execute=1): ejecutar (en directorio: entrar con cd)

## chmod: octal y simbolico

```bash
chmod 755 script.sh        # rwx r-x r-x  (dueno todo, resto lee/ejecuta)
chmod 644 config.ini       # rw- r-- r--  (archivo de datos tipico)
chmod 600 ~/.ssh/id_ed25519 # rw- --- ---  (llaves privadas: SOLO el dueno)
chmod 700 ~/.ssh           # directorio de ssh privadisimo

chmod u+x script.sh        # simbolico: agrega ejecucion al dueno
chmod go-w config.ini      # quita escritura a grupo y otros
chmod o= publico.txt       # otros: ningun permiso
```

## chown y chgrp

```bash
sudo chown jorge archivo          # cambiar dueno
sudo chown jorge:devops archivo   # dueno y grupo
sudo chown -R www-data:www-data /var/www/app   # recursivo
```

## umask: permisos por omision

```bash
umask          # tipico 022: nuevos archivos 644 y directorios 755
umask 077      # paranoico: todo nuevo accesible solo para ti
```

## Practica guiada

1. Crea un script con `#!/bin/bash` y `echo hola`; dale 755 y ejecutalo.
2. Quitale x y observa el "Permission denied".
3. Juega con un directorio: quita `x` e intenta entrar y listar (diferencia!).

## Fuentes oficiales

- chmod(1): https://man7.org/linux/man-pages/man1/chmod.1.html
- chown(1): https://man7.org/linux/man-pages/man1/chown.1.html
- umask(2): https://man7.org/linux/man-pages/man2/umask.2.html
