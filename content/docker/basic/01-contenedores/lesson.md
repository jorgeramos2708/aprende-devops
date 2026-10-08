---
title: "Contenedores: correr y operar"
order: 1
topic: contenedores
module: fundamentos
estimated_minutes: 30
objective: "Ejecutar, inspeccionar y detener contenedores; entrar con exec"
tech_version: "Docker Engine 27+"
sources:
  - https://docs.docker.com/reference/cli/docker/container/run/
  - https://docs.docker.com/reference/cli/docker/container/exec/
---

# Contenedores: correr y operar

Un contenedor es un proceso (o arbol de procesos) aislado: tiene su propio filesystem
(de la imagen), su propia red y sus limites, pero comparte el kernel del host.
No es una maquina virtual completa: por eso arrancan en milisegundos.

## El ciclo de vida en 8 comandos

```bash
docker run nginx                       # primer plano (te ocupa la terminal)
docker run -d --name web nginx         # -d: detach (segundo plano)
docker ps                              # corriendo ahora
docker ps -a                           # todos, incluidos parados
docker logs -f web                     # stdout del proceso principal, en vivo
docker exec -it web bash               # shell DENTRO del contenedor vivo
docker stop web                        # SIGTERM (amable)
docker start web                       # arranca de nuevo
docker rm -f web                       # elimina (forzado si sigue vivo)
```

## run: las opciones que mas usaras

```bash
docker run -d --name api \
  -p 8080:80 \                 # puerto host 8080 -> contenedor 80
  -e "MODO=produccion" \        # variable de entorno
  --restart unless-stopped \    # politica de re-arranque
  nginx
```

Claves: `-d` (fondo), `--name` (nombre fijo), `-p` (mapeo de puertos),
`-e` (env), `--rm` (autodestruye al terminar, ideal para pruebas),
`-it` (interactivo + pseudo-TTY, para shells).

## El modelo mental

```
imagen  = plantilla inmutable (solo lectura)
contenedor = plantilla + capa escribible efimera + proceso(s)
```

Borrar un contenedor NO borra su imagen. Parar un contenedor conserva su capa
(el contenedor existe aunque no corra; `ps -a` lo muestra).

## Practica guiada

1. `docker run -d --name web -p 8080:80 nginx`, abre `localhost:8080`.
2. Entra con `docker exec -it web sh` y modifica `/usr/share/nginx/html/index.html`.
3. Para, borra, recrea: observa que el cambio moria con el contenedor.

## Fuentes oficiales

- docker run, exec, logs, ps: https://docs.docker.com/reference/cli/docker/
