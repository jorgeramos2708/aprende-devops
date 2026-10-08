---
title: "Usuarios, grupos y sudo"
order: 5
topic: usuarios
module: seguridad-basica
estimated_minutes: 30
objective: "LPIC-1 107.1 - Gestionar cuentas de usuario y grupo"
tech_version: "shadow 4.x / sudo 1.9"
sources:
  - https://man7.org/linux/man-pages/man8/useradd.8.html
  - https://man7.org/linux/man-pages/man5/passwd.5.html
  - https://www.sudo.ws/docs/man/sudoers.man/
---

# Usuarios, grupos y sudo

Linux es multiusuario desde su diseno: cada proceso corre con la identidad de un
**usuario** (UID) y uno o varios **grupos** (GID). En servidores DevOps la
higiene aqui es critica: cada servicio con su usuario, permisos minimos siempre.

## Donde vive la informacion

```bash
cat /etc/passwd        # cuentas: nombre:shell:home (sin contrasenas!)
cat /etc/shadow        # hashes de contrasena (solo root puede leerlo)
cat /etc/group         # grupos y membresias
id jorge               # uid, gid y grupos de un usuario
```

Formato de una linea de `/etc/passwd`: `usuario:x:uid:gid:comentario:home:shell`.
La `x` indica que el hash vive en `/etc/shadow` (separacion por seguridad).

## Ciclo de vida de cuentas

```bash
sudo useradd -m -s /bin/bash agente   # crea usuario con home y shell
sudo passwd agente                    # asigna contrasena
sudo usermod -aG docker agente        # anexa al grupo docker (-aG = agregar, nunca -G solo)
sudo userdel -r agente                # elimina usuario y su home

sudo groupadd sre
sudo gpasswd -a jorge sre             # otra forma de agregar a grupo
```

Para cuentas de servicio (sin login, sin home): `sudo useradd -r -s /usr/sbin/nologin app`.

## sudo: delegacion controlada

`sudo` ejecuta un comando como root (u otro usuario) registrando cada uso. La
configuracion vive en `/etc/sudoers` y `/etc/sudoers.d/` — editala **siempre**
con `visudo` (valida sintaxis antes de guardar; un error ahi te deja sin sudo).

```bash
sudo -l                # que puedo ejecutar yo con sudo?
sudo -u www-data whoami   # ejecutar como OTRO usuario (no solo root)
sudo su -              # shell de root (solo si tu politica lo permite)
```

Regla comun en DevOps: usuarios humanos en grupo `sudo` (o `wheel`), cuentas de
servicio sin shell y sin sudo jamas.

## Practica guiada

1. Crea un usuario temporal, asignale contrasena y verifica con `id`.
2. Crealo un grupo, agregalo, y comprueba en `/etc/group`.
3. Borra el usuario limpiamente y verifica que su home tambien desaparecio.

## Fuentes oficiales

- useradd(8), passwd(5), group(5): https://man7.org/linux/man-pages/
- Manual de sudoers: https://www.sudo.ws/docs/man/sudoers.man/
