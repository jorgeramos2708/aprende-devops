---
title: "Redes en Docker"
order: 4
topic: redes
module: datos
estimated_minutes: 30
objective: "Publicar puertos, conectar contenedores por red y resolver por nombre"
tech_version: "Docker Engine 27+"
sources:
  - https://docs.docker.com/network/
  - https://docs.docker.com/network/drivers/bridge/
---

# Redes en Docker

Los contenedores no comparten la red del host por defecto: cada uno tiene su propia
IP interna. Las redes docker aislan el trafico y te dan **DNS incorporado**: en una
red definida por ti, los contenedores se resuelven por NOMBRE. Eso cambia como
escribes tus servicios.

## Publicar hacia el host

```bash
docker run -d -p 8080:80 nginx          # host:8080 -> contenedor:80
docker run -d -p 127.0.0.1:8080:80 nginx  # solo desde el propio host (mas seguro)
docker port web                          # ver mapeos de un contenedor
```

## Redes definibles

```bash
docker network create backend
docker run -d --name db --network backend postgres:16-alpine
docker run -d --name api --network backend -e DB_HOST=db mi-app
# dentro de api: "db" RESUELVE a la IP del contenedor db (DNS interno)

docker network ls
docker network inspect backend
docker network connect backend otro-contenedor
```

La red por defecto `bridge` NO da DNS por nombre (solo por IP). Una red creada
por ti, si. Este es EL motivo de usar redes propias: nombres estables.

## Diagnostico clasico

"En Docker Desktop funciona, en el server no": casi siempre es que la app escucha
en `127.0.0.1` (solo dentro del contenedor) en vez de `0.0.0.0`.

```
docker exec api ss -tlnp      # en que interfaz escucha dentro? (ver leccion de redes Linux)
```

## Practica guiada

1. Levanta `db` y un `alpine` en red propia; desde alpine haz `ping db`.
2. Repite en la red por defecto y contrasta (falla el nombre, funciona la IP).
3. Mapea un puerto solo a localhost y verifica que no responde desde fuera.

## Fuentes oficiales

- Networking overview: https://docs.docker.com/network/
- Bridge driver: https://docs.docker.com/network/drivers/bridge/
