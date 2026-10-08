---
title: "Redes esenciales: ip, ss y curl"
order: 10
topic: redes
module: fundamentos
estimated_minutes: 30
objective: "LPIC-1 109.1/109.2 - Fundamentos de protocolos de internet y configuracion basica"
tech_version: "iproute2 6.x"
sources:
  - https://man7.org/linux/man-pages/man8/ip.8.html
  - https://man7.org/linux/man-pages/man8/ss.8.html
  - https://curl.se/docs/manpage.html
---

# Redes esenciales: ip, ss y curl

Un servicio que "no responde" se diagnostica siempre por capas: ¿hay IP? ¿hay
ruta? ¿escucha el puerto? ¿responde el protocolo? Estas cuatro preguntas se
responden con cuatro herramientas del paquete **iproute2** y cia.

## Mi direccion y mis enlaces

```bash
ip addr                 # interfaces y direcciones IP (ip addr show)
ip -brief addr          # resumen de una linea por interfaz
ip link                 # estado de interfaces (UP/DOWN, MTU)
ip route                # tabla de rutas y gateway por defecto
```

El atajo mental: `3` te dice la IP (`192.168.x.x/24`), `ip route | grep default`
te dice por donde sales a internet.

## Que puertos estan escuchando

```bash
ss -tlnp                # TCP escuchando (listening), con numeros de puerto y proceso
ss -tlnp | grep 8080    # quien (si alguien) escucha el 8080?
ss -tulpn               # incluye UDP y procesos
ss -tn state established  # conexiones activas ahora mismo
```

Este es EL comando de "no levanta mi servicio": si no aparece con `-tlnp`,
el servicio no esta escuchando (o escucha en 127.0.0.1 y no es alcanzable
desde fuera — clasico).

## Conectividad basica

```bash
ping -c 4 1.1.1.1           # hay internet?  (ICMP)
ping -c 4 google.com        # resuelve DNS?
resolvectl status           # que DNS usa el sistema (systemd-resolved)
ip neigh                    # vecinos ARP en tu red local
```

Diagnostico por orden: IP propia (`ip addr`) → gateway (`ip route`) → internet
(`ping 1.1.1.1`) → DNS (`ping dominio`) → servicio (`ss`/`curl`).

## curl: el cliente universal

```bash
curl -I https://ejemplo.com           # solo cabeceras (HEAD)
curl -v https://api.local:8443/health # verbose: TLS, cabeceras, tiempos
curl -s https://api.github.com        # salida limpia a stdout (pipe-able)
curl -o archivo.tar.gz https://...    # descarga a archivo
```

En DevOps `curl` es el ping de HTTP: si responde 200 de dentro del servidor
pero no de fuera → firewall/red, no la app.

## Practica guiada

1. Localiza tu IP, mascara y gateway de este equipo.
2. Lista puertos escuchando y vincula cada uno con su proceso.
3. Diferencia con curl entre "no responde" (timeout) y "rechaza" (connection refused).

## Fuentes oficiales

- ip(8), ss(8): https://man7.org/linux/man-pages/
- curl(1): https://curl.se/docs/manpage.html
