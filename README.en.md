[简体中文](./README.md)

# MystiVaid's VintageStory Launcher (MVL)

<p align="center">
  <img src="./MVL/Assets/Icon/icon.svg" width="200" alt="MVL Icon">
</p>

## Cross-Platform Retro Tale Launcher

[![Weblate](https://hosted.weblate.org/widget/mvl/mvl/svg-badge.svg)](https://hosted.weblate.org/engage/mvl/)
[![Github release](https://img.shields.io/github/v/tag/scgm0/MVL)](https://github.com/scgm0/MVL/releases)
[![GitHub](https://img.shields.io/github/license/scgm0/MVL)](https://github.com/scgm0/MVL/blob/main/LICENSE)
[![GitHub last commit](https://img.shields.io/github/last-commit/scgm0/MVL)](https://github.com/scgm0/MVL/commits/main)
[![GitHub issues](https://img.shields.io/github/issues/scgm0/MVL)](https://github.com/scgm0/MVL/issues)
[![GitHub Test Actions](https://github.com/scgm0/MVL/actions/workflows/runner.yml/badge.svg)](https://github.com/scgm0/MVL/actions/workflows/runner.yml)

MystiVaid's VintageStory Launcher (`MVL`) is a free, open-source, community-driven launcher for Vintage Story, supports both `Windows` and `Linux` systems.

The project utilizes `Godot4` and `C#` to deliver an integrated desktop experience centered around multi-version management, modpack management, mod browsing and downloading, and launch encapsulation

## Overview

MVL serves both players and contributors:

1. Players can import/download game releases, manage modpacks and accounts, then launch quickly
2. Contributors can build locally or follow CI-aligned reproducible packaging steps

## Quick Start

1. Open `Releases` and import an existing game directory, or download one online
2. Open `Modpacks` and create a modpack
3. Bind the modpack to a game release version
4. Pick an account (online or offline) from the top-right account menu
5. Return to `Home` and click launch
6. Use `Browse` or mod management windows to install/update mods

## Capabilities

| Area | What it does |
| --- | --- |
| Home | Select current modpack, launch/stop game, view bound release info |
| Releases | Import local installs, download and extract official releases |
| Modpacks | Create/manage modpacks, set startup command and main assembly |
| Mod Management | Scan the `Mods` directory (zip/dll/directories), compare versions with `ModDB`, batch update, and handle dependency prompts |
| Browser | Query `ModDB API` with filters, inspect releases, download mods |
| Account | Online login (with TOTP flow) and offline mode |
| Settings | Language, scale, renderer, proxy, download threads, latest version, custom game source |
| Info | Build info, contributors, donors, third-party license entries |
| Launch Runtime | `VSRun` wrapper injects run config, account/session data, and Harmony patches |

## Configuration and Data Directory

MVL uses the Godot user directory (`OS.GetUserDataDir()`) with `use_custom_user_dir` enabled, and the directory is named `MVL`

Common path examples: On `Windows`, it is typically located at `%APPDATA%\MVL`, and on `Linux`, it is usually at `~/.local/share/MVL`

| Path | Type | Purpose |
| --- | --- | --- |
| `data.json` | File | Launcher main configuration (language, scaling, proxy, accounts, version and modpack indexes, etc.) |
| `Modpack/` | Directory | Default modpack directory |
| `Release/` | Directory | Default game version directory |
| `logs/` | Directory | Log directory, `MVL.log` automatically rotates to retain recent files |
| `.Cache/` | Directory | Cache content such as icons |
| `Translation/` | Directory | Local translation directory, where custom `.po` files can be placed and reloaded in settings |
| `override.cfg` | File | Override configurations such as rendering drivers |

## Develop and Build

1. Clone the repository: `git clone https://github.com/scgm0/MVL.git`  
2. Install dependencies (`Python/SCons`, platform toolchain, `.NET 8` + `.NET 10`)  
3. Prepare the `VINTAGE_STORY` directory (the workflow will download and extract the client, then write it to environment variables)  
4. Clone the custom `Godot` branch: the `MVL` branch of `https://github.com/scgm0/godot.git`  
5. Build the `Godot C# Editor` using `SCons` (refer to the [official documentation](https://docs.godotengine.org/en/latest/engine_details/development/compiling/index.html)). When building `template_release`, optionally apply `MVL/GodotBuildProfile/custom.py` and `custom.build`  
6. Import `mvl-repo/MVL/project.godot` into the self-built editor  

CI reference files: `.github/workflows/windows.yml`, `.github/workflows/linux.yml`

## External Services

| Service | Purpose |
| --- | --- |
| `auth3.vintagestory.at` | Login and session validation |
| `api.vintagestory.at` | Release/version metadata used by launcher flows |
| `mods.vintagestory.at` | Mod listing, mod metadata, and mod file downloads |
| `api.github.com/repos/scgm0/MVL/releases/latest` | Launcher update checks |
| `gh-proxy.*` (optional) | GitHub proxy endpoints |

## License and Credits

| Item | Link |
| --- | --- |
| License | [MIT](./LICENSE) |
| Contributors | [AUTHORS.md](./AUTHORS.md) |
| Donors | [DONORS.md](./DONORS.md) |

Third-party component license files within the project are located in `MVL/License/` and are dynamically displayed on the app's `Info` page.

## Links

| Name | URL |
| --- | --- |
| GitHub (upstream) | https://github.com/scgm0/MVL |
| ModDB page | https://mods.vintagestory.at/mvl |
| Weblate | https://hosted.weblate.org/engage/mvl |
| Vintage Story | https://www.vintagestory.at/ |
| QQ Channel | https://pd.qq.com/s/cgzpnm0lh |
| Sponsorship | https://afdian.com/a/MystiVaid |
