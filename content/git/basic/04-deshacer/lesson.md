---
title: "Deshacer errores sin miedo"
order: 4
topic: deshacer
module: colaboracion
estimated_minutes: 30
objective: "Distinguir restore, revert y reset; usar reflog como red de seguridad"
tech_version: "git 2.43+"
sources:
  - https://git-scm.com/docs/git-restore
  - https://git-scm.com/docs/git-revert
  - https://git-scm.com/docs/git-reset
---

# Deshacer errores sin miedo

Todo el que usa Git borra algo por accidente algun dia. La buena noticia: con los
comandos correctos, casi nada se pierde de verdad. Primero entiende QUE quieres
deshacer y HASTA DONDE volver.

## Mapa de "deshacer"

| Situacion | Comando | Toca historia publica? |
|---|---|---|
| Modificaste archivo, no quieres los cambios | `git restore archivo` | no |
| Hiciste `add` pero no quieres stagearlo | `git restore --staged archivo` | no |
| Quieres "des-hacer" un commit YA compartido | `git revert <sha>` | no (crea commit inverso) |
| Quieres borrar commits NO compartidos | `git reset --soft/--mixed/--hard` | SI, cuidado |

## restore y revert: los seguros

```bash
git restore archivo.py            # descarta cambios del working dir
git restore --staged archivo.py   # quita del staging (archivo intacto)
git revert HEAD                   # nuevo commit que DEShace el ultimo
git revert HEAD~2                 # deshace uno de hace dos commits
```

`revert` es seguro para historia ya publicada porque no reescribe el pasado.

## reset: el que reescribe (solo en local)

```bash
git reset --soft HEAD~1     # quita el commit, deja todo en staging
git reset --mixed HEAD~1    # quita el commit, deja cambios sin stagear (default)
git reset --hard HEAD~1     # quita commit Y cambios en disco. IRREVERSIBLE en el disco
```

## reflog: el salvavidas final

Aunque hagas `reset --hard` y "pierdas" commits, Git los recuerda un tiempo:

```bash
git reflog                    # cada movimiento de HEAD, con su sha
git switch -c rescate <sha>   # crea rama en ese punto y recupera tu trabajo
```

## Practica guiada

1. Haz un commit, deshazlo con `--soft`, recomitea distinto.
2. Haz commit, `revert` y mira como la historia crece hacia adelante.
3. Rompe algo a proposito con `--hard` y recuperalo con `reflog`.

## Fuentes oficiales

- git-restore(1), git-revert(1), git-reset(1), git-reflog(1): https://git-scm.com/docs
