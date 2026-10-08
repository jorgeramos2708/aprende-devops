---
title: "El sistema de archivos (FHS)"
order: 2
topic: filesystem
module: fundamentos
estimated_minutes: 25
objective: "LPIC-1 104.7 - Encontrar archivos, jerarquia del sistema"
tech_version: "FHS 3.0"
sources:
  - https://refspecs.linuxfoundation.org/FHS_3.0/fhs/index.html
  - https://man7.org/linux/man-pages/man7/hier.7.html
---

# El sistema de archivos y la FHS

En Linux **todo es un archivo**: discos, procesos, sockets, dispositivos. Y todo cuelga
de una unica raiz `/`. No existen letras de unidad (C:, D:); los discos se "montan"
como directorios. La disposicion estandar de directorios se define en la
**Filesystem Hierarchy Standard (FHS)**.

## Las rutas esenciales

| Ruta | Contenido |
|---|---|
| `/` | raiz de todo |
| `/bin`, `/usr/bin` | ejecutables de usuario (coreutils y cia) |
| `/sbin`, `/usr/sbin` | ejecutables de administracion |
| `/etc` | configuracion del sistema (texto plano) |
| `/var` | datos variables: logs (`/var/log`), colas, caches |
| `/home/<user>` | carpeta personal de usuarios |
| `/root` | home del superusuario |
| `/tmp` | temporal: se borra entre reinicios |
| `/proc` | sistema de archivos VIRTUAL: procesos y kernel en vivo |
| `/dev` | dispositivos representados como archivos (`/dev/sda`, `/dev/null`) |
| `/opt` | software de terceros fuera del gestor de paquetes |

La referencia canonica en tu propio sistema: `man hier`.

## Descriptores especiales de /dev

```bash
echo "hola" > /dev/null   # agujero negro: descarta lo que escribas
ls /dev/zero              # ceros infinitos (util para pruebas de disco)
ls /dev/random            # entropia del kernel
```

## Rutas absolutas vs relativas

```bash
cd /var/log          # absoluta: empieza en /
cd log               # relativa: depende de donde estes
./script.sh          # relativa: "en este directorio"
```

Y los ataques de navegacion precisos:

```bash
realpath ../logs/app.log    # resuelve a la ruta absoluta real
ls -l /usr/bin/python*      # comodines (globbing): * ? [...]
```

## Enlaces duros y blandos

```bash
ln archivo.txt enlace-duro.txt        # mismo inode (mismo contenido)
ln -s archivo.txt enlace-blando.txt   # acceso directo (puede romperse)
ls -li                                # -i muestra el inode
```

En DevOps los enlaces simbolicos son pan diario (configs activas, `current ->` releases).

## Practica guiada

1. `man hier` y recorre las secciones clave.
2. Encuentra el inode de tu home con `ls -ld -i ~`.
3. Crea un enlace simbolico a `/etc/os-release` en tu home y lee a traves de el.

## Fuentes oficiales

- FHS 3.0 (Linux Foundation): https://refspecs.linuxfoundation.org/FHS_3.0/fhs/index.html
- hier(7): https://man7.org/linux/man-pages/man7/hier.7.html
