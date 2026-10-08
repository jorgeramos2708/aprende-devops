---
title: "Volúmenes y persistencia"
order: 3
topic: volumenes
module: datos
estimated_minutes: 30
objective: "Elegir entre volumen nombrado, bind mount y tmpfs; persistir datos"
tech_version: "Docker Engine 27+"
sources:
  - https://docs.docker.com/storage/volumes/
  - https://docs.docker.com/storage/bind-mounts/
---

# Volúmenes y persistencia

La capa escribible del contenedor es **efimera**: borras el contenedor, se va.
Los datos que importan (DBs, uploads, configs generadas) viven FUERA de esa capa.

## Las tres opciones

| Tipo | Uso | Dato vive en |
|---|---|---|
| Volumen nombrado | **datos en produccion** (dbs) | gestionado por docker (`/var/lib/docker/volumes`) |
| Bind mount | codigo/config desde el host (desarrollo) | carpeta del host |
| tmpfs | secretos/datos temporales en RAM | memoria (muere al parar) |

## Volumen nombrado (el default correcto)

```bash
docker volume create pgdata
docker run -d --name db -v pgdata:/var/lib/postgresql/data postgres:16-alpine
docker volume ls
docker volume inspect pgdata
docker rm -f db                        # el volumen SIGUE ahi
docker run -d --name db2 -v pgdata:/var/lib/postgresql/data postgres:16-alpine   # datos sobrevivieron
```

## Bind mount (desarrollo)

```bash
docker run -d --name web -p 8080:80 -v /home/jorge/sitio:/usr/share/nginx/html nginx
```

Editas `sitio/index.html` en el host → el contenedor lo ve al instante.
Cuidado con permisos: el UID dentro y fuera puede no coincidir.

## tmpfs

```bash
docker run -d --tmpfs /run/cache:rw,size=64m nginx
```

## Practica guiada

1. Crea el contenedor de base de datos con volumen, mete una tabla, borra el
   contenedor, recrea con el mismo volumen y verifica que la tabla sigue.
2. Repite el mismo ejercicio con bind mount y explica cuando preferir cada uno.

## Fuentes oficiales

- Volumes: https://docs.docker.com/storage/volumes/
- Bind mounts: https://docs.docker.com/storage/bind-mounts/
