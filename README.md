[English](./README.en.md)

# 神麤詭末的复古物语启动器（MVL）

<p align="center">
  <a href="https://github.com/scgm0/MVL">
    <img src="./MVL/Assets/Icon/icon.svg" width="200" alt="MVL Icon">
  </a>
</p>

## 跨平台复古物语启动器

[![Weblate](https://hosted.weblate.org/widget/mvl/mvl/svg-badge.svg)](https://hosted.weblate.org/engage/mvl/)
[![Github release](https://img.shields.io/github/v/tag/scgm0/MVL)](https://github.com/scgm0/MVL/releases)
[![GitHub](https://img.shields.io/github/license/scgm0/MVL)](https://github.com/scgm0/MVL/blob/main/LICENSE)
[![GitHub last commit](https://img.shields.io/github/last-commit/scgm0/MVL)](https://github.com/scgm0/MVL/commits/main)
[![GitHub issues](https://img.shields.io/github/issues/scgm0/MVL)](https://github.com/scgm0/MVL/issues)
[![GitHub Test Actions](https://github.com/scgm0/MVL/actions/workflows/runner.yml/badge.svg)](https://github.com/scgm0/MVL/actions/workflows/runner.yml)

神麤詭末的复古物语启动器（`MVL`）是一个免费、开源、由社区驱动的复古物语启动器，支持`Windows`与`Linux`系统

项目使用[Godot4](https://godotengine.org)与[C#](https://dotnet.microsoft.com)，围绕多版本管理 + 整合包管理 + 模组浏览下载 + 启动封装提供一体化桌面体验

## 项目简介

`MVL`面向两类主要用户：

1. 玩家：快速导入或下载游戏版本、创建整合包、管理账号并启动游戏
2. 开发者：可在本地构建完整工程，或按 CI 工作流进行可复现打包

## 快速开始

1. 在`版本`页面导入已有游戏目录，或在线下载并安装游戏版本
2. 在`整合`页面创建整合包，并为整合包选择目标游戏版本
3. 在`主页`确认当前整合包与对应版本信息
4. 点击右上角账号入口，选择在线登录或离线账号
5. 回到`主页`点击`启动游戏`。需要结束时可再次点击停止
6. 需要管理模组时，可在`浏览`页面搜索下载，或在整合包模组管理窗口执行更新与依赖处理

## 当前功能

| 模块 | 主要功能 |
| --- | --- |
| 主页 | 选择当前整合包，启动/停止游戏，显示当前整合包绑定的版本信息 |
| 版本 | 导入本地已安装游戏；从版本列表下载并解压游戏；维护本地版本列表 |
| 整合 | 创建整合包、绑定游戏版本、调整启动命令与主程序集、管理图标与本地化字段 |
| 模组管理 | 扫描`Mods`目录（zip/dll/目录），比对`ModDB`版本，批量更新并处理依赖提示 |
| 浏览 | 对接`ModDB API`，按作者/标签/版本/安装状态筛选，查看并下载模组发布版本 |
| 账号 | 官方账号登录（含 TOTP 流程）与离线账号模式，切换当前启动账号 |
| 设置 | 语言、缩放、渲染驱动、代理、下载线程、最新版本、第三方游戏源等 |
| 关于 | 展示版本号、贡献者、赞助者、第三方许可证与项目外链 |
| 启动封装 | 使用`VSRun`传递运行参数、注入账号信息、处理离线补丁与模组路径修正 |

## 配置与数据目录

MVL 使用 Godot 用户目录（`OS.GetUserDataDir()`），并开启了`use_custom_user_dir`，目录名为`MVL`

常见路径示例：`Windows`通常在`%APPDATA%\MVL`，`Linux`通常在`~/.local/share/MVL`

| 路径 | 类型 | 用途 |
| --- | --- | --- |
| `data.json` | 文件 | 启动器主配置（语言、缩放、代理、账号、版本与整合包索引等） |
| `Modpack/` | 目录 | 默认整合包目录 |
| `Release/` | 目录 | 默认游戏版本目录 |
| `logs/` | 目录 | 日志目录，`MVL.log`自动轮转保留最近文件 |
| `.Cache/` | 目录 | 图标等缓存内容 |
| `Translation/` | 目录 | 本地翻译目录，可放置自定义`.po`并在设置中重载 |
| `override.cfg` | 文件 | 渲染驱动等覆盖配置 |

## 开发与构建

1. 克隆仓库：`git clone https://github.com/scgm0/MVL.git`
2. 安装依赖（`Python/SCons`、平台工具链、`.NET 8`+`.NET 10`）
3. 准备`VINTAGE_STORY`目录（工作流中会下载并解压客户端后写入环境变量）
4. 克隆自定义`Godot`分支：`https://github.com/scgm0/godot.git`的`MVL`分支
5. 用`SCons`构建`Godot C#编辑器`（参考[官方文档](https://docs.godotengine.org/en/latest/engine_details/development/compiling/index.html)），构建`template_release`时可选应用`MVL/GodotBuildProfile/custom.py`与`custom.build`
6. 将`mvl-repo/MVL/project.godot`导入自构建的编辑器中

CI 参考文件：[windows.yml](.github/workflows/windows.yml)、[linux.yml](.github/workflows/linux.yml)

## 网络服务

| 服务 | 用途 | 触发场景 | 可配置性 |
| --- | --- | --- | --- |
| `auth3.vintagestory.at` | 账号登录、会话校验 | 在线账号登录与启动前校验 | 否 |
| `api.vintagestory.at` | 获取游戏版本信息（如版本列表/最新信息） | 版本下载与登录流程 | 版本列表可改为第三方地址 |
| `mods.vintagestory.at` | 模组列表、详情、发布版本下载 | 浏览与更新模组 | 否 |
| `api.github.com`（`scgm0/MVL`） | 检查启动器最新发布版本 | 设置页版本检查与下载窗口 | 可选 GitHub 代理 |
| `gh-proxy.*` | GitHub API/下载代理 | 启用 GitHub 代理时 | 可在设置中切换 |

## 许可证与致谢

| 项目 | 链接 |
| --- | --- |
| 项目许可证 | [MIT License](./LICENSE) |
| 贡献者名单 | [AUTHORS.md](./AUTHORS.md) |
| 赞助者名单 | [DONORS.md](./DONORS.md) |

项目内第三方组件许可证文件位于`MVL/License/`，并在应用`关于`页内动态展示。

## 相关项目

| 名称 | 介绍 |
| --- | --- |
| [VS Launcher](https://vsldocs.xurxomf.xyz) | 另一个开源复古物语启动器 |
| [复古物语中文社区](https://vintagestory.top) | 非官方的复古物语中文论坛 |

## 相关链接

| 名称 | 地址 |
| --- | --- |
| GitHub（上游） | https://github.com/scgm0/MVL |
| ModDB | https://mods.vintagestory.at/mvl |
| Weblate | https://hosted.weblate.org/engage/mvl |
| 复古物语 | https://www.vintagestory.at/ |
| QQ频道 | https://pd.qq.com/s/cgzpnm0lh |
| 赞助 | https://afdian.com/a/MystiVaid |
