---
title: "Gestion de paquetes con apt"
order: 7
topic: paquetes
module: operacion
estimated_minutes: 25
objective: "LPIC-1 102.4 - Gestion de paquetes Debian"
tech_version: "apt 2.6 (Debian 12) / apt 2.7 (Ubuntu 24.04)"
sources:
  - https://man7.org/linux/man-pages/man8/apt.8.html
  - https://www.debian.org/doc/manuals/debian-reference/ch02.en.html
  - https://man7.org/linux/man-pages/man5/sources.list.5.html
---

# Gestion de paquetes con apt

En Debian/Ubuntu el software se instala como **paquetes .deb** desde repositorios
firmados. La herramienta moderna y recomendada es `apt` (envuelve a `dpkg`,
que instala el archivo .deb en si). Flujo mental: `apt` resuelve dependencias
y descarga; `dpkg` instala.

## El flujo diario

```bash
sudo apt update                 # refresca el INDICE de paquetes (no instala nada)
apt list --upgradable           # que hay pendiente de actualizar
sudo apt upgrade -y             # actualiza lo instalado
sudo apt install htop curl      # instalar
sudo apt remove htop            # quitar (deja config)
sudo apt purge htop             # quitar TODO (incluye config)
sudo apt autoremove             # limpia dependencias huerfanas
```

> En servidores: `apt update && apt upgrade` periodico es higiene basica de
> seguridad (los parches llegan como actualizaciones de paquetes).

## Explorar sin instalar

```bash
apt search redis            # buscar por nombre/descripcion
apt show redis-server       # ficha completa: version, dependencias, tamano
apt policy docker.io        # versiones candidatas y repositorios de donde salen
dpkg -l | grep nginx        # que hay instalado
dpkg -L nginx-common        # archivos que instalo este paquete
```

## De donde sale el software

Los origenes se declaran en `/etc/apt/sources.list` y `/etc/apt/sources.list.d/`.
Anadir un repo de terceros moderno (formato deb822, ejemplo generico):

```bash
curl -fsSL https://ejemplo.com/llave.gpg | sudo gpg --dearmor -o /usr/share/keyrings/ejemplo.gpg
echo "deb [signed-by=/usr/share/keyrings/ejemplo.gpg] https://ejemplo.com/apt stable main" \
  | sudo tee /etc/apt/sources.list.d/ejemplo.list
sudo apt update
```

Siempre con llave dedicada (`signed-by`), nunca `apt-key` (obsoleto) ni
`trusted=yes` (apaga la verificacion: es abrir tu puerta sin mirar).

## Practica guiada

1. `apt update` y cuenta cuantos paquetes estan por actualizar.
2. Instala `tree` y usa `dpkg -L tree` para ver donde aterrizo.
3. `apt show` sobre el paquete `bash` y localiza su mantenedor y tamanio.

## Fuentes oficiales

- apt(8): https://man7.org/linux/man-pages/man8/apt.8.html
- Debian Reference, gestion de paquetes: https://www.debian.org/doc/manuals/debian-reference/ch02.en.html
- sources.list(5): https://man7.org/linux/man-pages/man5/sources.list.5.html
