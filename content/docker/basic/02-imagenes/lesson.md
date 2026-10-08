---
title: "Imágenes y Dockerfiles"
order: 2
topic: imagenes
module: fundamentos
estimated_minutes: 35
objective: "Entender capas, etiquetas y construir una imagen con Dockerfile"
tech_version: "Docker Engine 27+, BuildKit"
sources:
  - https://docs.docker.com/reference/dockerfile/
  - https://docs.docker.com/build/concepts/dockerfile/
---

# Imágenes y Dockerfiles

Una imagen es una plantilla de SO solo-lectura hecha de **capas apiladas**.
Cada instruccion del Dockerfile anade (o reutiliza) una capa. Entender capas =
entender builds rapidos e imagenes ligeras.

## Imagenes de otros

```bash
docker pull postgres:16-alpine      # descarga imagen:tag
docker images                       # las que tienes en local
docker tag postgres:16-alpine registry.local/pg:16   # re-etiqueta
docker push registry.local/pg:16    # subir a un registry (el tuyo: Zot!)
docker inspect postgres:16-alpine   # metadata: env, puertos, capas
docker history postgres:16-alpine   # como se construyo, capa a capa
docker rmi vieja:tag                # borrar referencia local
docker system df                    # cuanto disco usa Docker
docker system prune                 # limpieza (con cuidado)
```

`imagen:tag` — la etiqueta es un puntero mutable (latest no es especial: es
solo el nombre por defecto). **Nunca produccion con `:latest`** sin fijar digest.

## Tu primer Dockerfile

```dockerfile
FROM python:3.12-slim
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY . .
EXPOSE 8000
CMD ["python", "app.py"]
```

```bash
docker build -t mi-app:1.0 .        # construir
docker run -d -p 8000:8000 mi-app:1.0
```

## Reglas que separan a los novatos de los operadores

- **ORDEN de instrucciones = cache**: lo que cambia menos va arriba
  (dependencias antes que tu codigo). Si editas `app.py`, pip NO se reejecuta.
- `--no-cache-dir`, limpiar listas de apt, y bases `slim`/`alpine` = imagenes pequenas.
- Un `.dockerignore` (node_modules, .git) = builds rapidos y contexto chico.

## Practica guiada

1. Construye la imagen del ejemplo contra cualquier app minima.
2. Edita `app.py` y reconstruye: mira como las capas de deps se reusan (cache HIT).
3. Sube cualquier imagen a tu registry Zot: `docker tag x registry.edrs.xyz/labs/x && docker push`.

## Fuentes oficiales

- Dockerfile reference: https://docs.docker.com/reference/dockerfile/
- Build concepts: https://docs.docker.com/build/concepts/dockerfile/
