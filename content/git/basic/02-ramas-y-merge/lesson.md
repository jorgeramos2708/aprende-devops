---
title: "Ramas y merge"
order: 2
topic: ramas
module: fundamentos
estimated_minutes: 30
objective: "Crear ramas, cambiar entre ellas y fusionar entendiendo fast-forward"
tech_version: "git 2.43+"
sources:
  - https://git-scm.com/docs/git-branch
  - https://git-scm.com/docs/git-switch
  - https://git-scm.com/docs/git-merge
---

# Ramas y merge

Una rama es un **puntero movil** a un commit. Nada mas. Por eso crear una rama
es baratisimo: no copia archivos, solo anota "de aqui en adelante, sigo por este lado".

## Trabajar con ramas

```bash
git branch                    # lista ramas
git branch feature/login      # crea el puntero (aun no te mueves)
git switch feature/login      # te mueves a la rama   (o: git checkout)
git switch -c hotfix/api      # crear + moverse en un paso
git branch -d feature/login   # borra la rama ya fusionada
git branch -D rama-rota       # borra a la fuerza (pierde commits no fusionados)
```

HEAD es "donde estas parado": un puntero a la rama actual.

## Merge: dos destinos distintos

```bash
git switch main
git merge feature/login
```

- **fast-forward**: main simplemente avanza al mismo commit (no hubo divergencia).
- **merge commit**: si ambas ramas avanzaron, git crea un commit de union
  (tiene DOS padres; el historial conserva la forma real del trabajo).

## Cuando hay conflicto

```
<<<<<<< HEAD
contenido en main
=======
contenido en la rama
>>>>>>> feature/login
```

Resuelves a mano entre esos marcadores, luego:

```bash
git add archivo-en-conflicto        # marca resuelto
git merge --continue                # o termina el merge con commit
git merge --abort                   # cancelar y volver atras
```

## Practica guiada

1. Crea rama `demo`, un commit ahi, vuelve a main, otro commit, y fusiona
   (tendras que resolver o no, segun edites la misma linea).
2. Repite editando la MISMA linea en ambas ramas para forzar conflicto y resuelvelo.

## Fuentes oficiales

- git-branch(1), git-switch(1), git-merge(1): https://git-scm.com/docs
