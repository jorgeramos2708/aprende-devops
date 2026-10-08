---
title: "Repositorio, staging y commits"
order: 1
topic: fundamentos
module: fundamentos
estimated_minutes: 30
objective: "Dominar el ciclo basico add -> commit -> log"
tech_version: "git 2.43+"
sources:
  - https://git-scm.com/docs/git-init
  - https://git-scm.com/docs/git-commit
  - https://git-scm.com/docs/git-status
---

# Repositorio, staging y commits

Git es una maquina de **fotografias del proyecto**: cada commit es una foto inmutable
de tu arbol de archivos, enlazada a la foto anterior. Entender esto (contenido,
no "diferencias") aclara todo lo demas.

## Los tres pisos

```
directorio de trabajo  ->  staging (index)  ->  repositorio (commits)
        editas                preparas             guardas la foto
```

- **Working directory**: tus archivos reales.
- **Staging (index)**: la zona de preparacion del proximo commit (eliges QUE entra).
- **Repositorio**: la historia inmutable en `.git/`.

## Ciclo de trabajo minimo

```bash
git init                      # nace el repo en este directorio (una sola vez)
git status                    # SIEMPRE: que esta modificado / staged / sin seguimiento
git add app.py                # manda app.py al staging
git add -p                    # interactivo: eliges trozos concretos (quirurgico)
git commit -m "mensaje"       # crea la foto
git log --oneline --graph     # la historia
git diff                      # cambios no-staged
git diff --staged             # lo que ya esta en staging
```

## Un buen commit

- Mensaje en imperativo: "Agrega validacion de correo" no "agregue validacion".
- Un commit = una idea completa y autocontenida.
- Mensajes tipo `fix`, `wip`, `asd` dificultan la revision y los revert.

## .gitignore desde el dia uno

```
node_modules/
.env
dist/
*.log
```
Lo que nunca debe entrar: secretos, dependencias, artefactos de build.

## Practica guiada

1. Crea una carpeta, `git init`, dos archivos, y haz dos commits separados.
2. Modifica uno, observa `git status` y `git diff` antes y despues de `git add`.
3. Mira `git log --oneline` y explica en voz alta la cadena de fotos.

## Fuentes oficiales

- git-init(1), git-add(1), git-commit(1), git-status(1): https://git-scm.com/docs
