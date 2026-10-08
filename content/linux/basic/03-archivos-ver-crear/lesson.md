---
title: "Ver, crear y manipular archivos"
order: 3
topic: archivos
module: fundamentos
estimated_minutes: 30
objective: "LPIC-1 103.2 - Procesar flujos de texto con filtros"
tech_version: "coreutils 9"
sources:
  - https://man7.org/linux/man-pages/man1/cat.1.html
  - https://man7.org/linux/man-pages/man1/cp.1.html
  - https://man7.org/linux/man-pages/man1/mv.1.html
  - https://man7.org/linux/man-pages/man1/less.1.html
---

# Ver, crear y manipular archivos

El trabajo diario con Linux es, en buena parte, mover texto de un lado a otro.
Estas herramientas son tu caja basica; todas forman parte de **GNU coreutils**.

## Leer contenido

```bash
cat /etc/os-release       # vuelca el archivo completo a pantalla
less /var/log/syslog      # paginador: navega con flechas, busca con /, sal con q
head -n 20 app.log        # primeras 20 lineas
tail -n 50 app.log        # ultimas 50
tail -f app.log           # "sigue" el archivo en vivo (logs en tiempo real)
wc -l app.log             # cuenta lineas (tambien -w palabras, -c bytes)
```

`tail -f` es tu mejor amigo observando despliegues; en systemd luego veras
`journalctl -f`, su equivalente para servicios.

## Crear y copiar

```bash
touch notas.txt                 # crea vacio (o refresca fecha de modificacion)
mkdir -p proyectos/demo/src     # crea toda la cadena de directorios
cp notas.txt notas.bak          # copia archivo
cp -r proyectos/demo /tmp/      # copia recursiva de directorio
mv notas.bak notas-renombradas.txt   # mover/renombrar (mismo comando)
rm notas.txt
rm -r /tmp/demo                 # borrado recursivo; ojo: NO hay papelera
```

> Regla de oro: duda antes de `rm -rf`. En scripts automatizados usa rutas
> absolutas e imprime antes con `echo` lo que borrarias.

## Editores en terminal

```bash
nano notas.txt       # amigable: atajos visibles abajo (Ctrl+O guarda, Ctrl+X sale)
vi notas.txt         # omnipresente: vale la pena saber entrar (i), guardar y salir (:wq)
```

No necesitas dominar `vi` hoy; necesitas sobrevivir a el: `i` inserta,
`Esc` vuelve a modo comando, `:wq` guarda y sale, `:q!` sale sin guardar.

## Trucos de coreutils que usaras en pipelines

```bash
basename /a/b/archivo.txt     # -> archivo.txt
dirname  /a/b/archivo.txt     # -> /a/b
sort nombres.txt | uniq       # ordenar + deduplicar
tee salida.log                # escribe a archivo Y a pantalla a la vez
```

## Practica guiada

1. Crea `~/practicas/`, dentro un archivo con 5 lineas, y explora con
   `cat/head/tail/less`.
2. Copia el archivo, renombralo, y borra la copia.
3. `tail -f` sobre un log mientras generas eventos en otra terminal.

## Fuentes oficiales

- cat(1), cp(1), mv(1): https://man7.org/linux/man-pages/
- less(1): https://man7.org/linux/man-pages/man1/less.1.html
- GNU coreutils: https://www.gnu.org/software/coreutils/manual/
