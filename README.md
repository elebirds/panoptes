# panoptes

A Unity 2D game project with a Go backend server.

## Repository Structure

```
panoptes/
├── frontend/          # Unity 2D project
│   ├── Assets/        # Game assets (scripts, scenes, sprites, …)
│   ├── Packages/      # Unity package manifest
│   └── ProjectSettings/
├── backend/           # Go HTTP server
│   ├── main.go
│   └── go.mod
└── configs/           # Designer-editable configuration files
    ├── game_config.json
    └── levels.json
```

## Prerequisites

| Tool | Version |
|------|---------|
| Unity | 2022.3 LTS (or newer) |
| Go | 1.22+ |

## Getting Started

### Backend

```bash
cd backend
go run .
```

The server listens on port `8080` by default. Set the `PORT` environment variable to override.

### Frontend

Open the `frontend/` folder in Unity Hub as an existing project.

## Configs

JSON files inside `configs/` are loaded at runtime and can be edited by
designers without modifying source code:

- **`game_config.json`** — global game settings (frame rate, player stats, audio volumes, …)
- **`levels.json`** — per-level definitions (scene path, enemies, unlock state, …)

## Submodules

If the Unity frontend or other large assets are maintained in a separate
repository, add them as a Git submodule:

```bash
# Example: add the frontend as a submodule
git submodule add https://github.com/elebirds/panoptes-frontend frontend
git submodule update --init --recursive
```

After cloning this repository with submodules:

```bash
git clone --recurse-submodules https://github.com/elebirds/panoptes
```
