---
title: "Remotos: clone, push, pull"
order: 3
topic: remotos
module: colaboracion
estimated_minutes: 30
objective: "Sincronizar con repositorios remotos y entender fetch vs pull"
tech_version: "git 2.43+"
sources:
  - https://git-scm.com/docs/git-clone
  - https://git-scm.com/docs/git-push
  - https://git-scm.com/docs/git-pull
---

# Remotos: clone, push, pull

Git es distribuido: tu repo lo tiene TODO (historia incluida). El remoto (GitHub,
tu VPS, otro disco) es otra copia con la que sincronizas *cuando tu quieres*.
Incluso sin internet sigues commiteando en local.

## Conectar con un remoto

```bash
git clone https://github.com/org/repo.git       # descarga repo ENT + historia
git clone https://github.com/org/repo.git destino
git remote -v                                    # remotos configurados
git remote add upstream https://github.com/otro/repo.git
```

El remoto por defecto al clonar se llama **`origin`**.

## Enviar y traer

```bash
git push origin main          # sube TU rama main al remoto origin
git push -u origin main       # ademas la vincula (siguientes: solo git push)
git fetch origin              # TRAE referencias, no toca tu trabajo (seguro)
git pull                      # = git fetch + git merge (o rebase si configurado)
git status                    # te dice si vas por delante/detras del remoto
```

## fetch vs pull: la diferencia que evita sustos

- `git fetch` actualiza tu Conocimiento del remoto (`origin/main`) sin mover tu rama.
- `git pull` = fetch + fusiona (puede crear merge commit inesperado).

Contrato sano: `fetch`, miras `git log --oneline main..origin/main`, decides
merge o rebase consciente.

## Practica guiada

1. Clona cualquier repo publico y explora `git log --oneline` offline.
2. Crea un repo vacio en tu proveedor y subele tu proyecto local.

## Fuentes oficiales

- git-clone(1), git-push(1), git-pull(1), git-remote(1): https://git-scm.com/docs
