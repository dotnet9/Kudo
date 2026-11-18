<p align="center">
  <img src="assets/logo.svg" width="120" alt="Kudo logo"/>
</p>

<h1 align="center">Kudo · 构度</h1>

<p align="center">
  <em>Shape Ideas into Form — 让创意成形</em>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-11.0-512BD4" alt=".NET 11"/>
  <img src="https://img.shields.io/badge/Avalonia-12.x-8B44AC" alt="Avalonia 12"/>
  <img src="https://img.shields.io/badge/UI-TKCode.Arc-blueviolet" alt="TKCode.Arc"/>
  <img src="https://img.shields.io/badge/license-MIT-green" alt="License"/>
</p>

---

**Kudo（构度）** 是一款基于 [Avalonia](https://avaloniaui.net/) 构建的开源三维建模软件，
致力于提供跨平台、轻量、可扩展的建模体验。

> 构你所想，度你所形。

## ✨ 特性
- 🧊 跨平台（Windows / macOS / Linux）
- ⚡ 高性能渲染管线（基于 Avalonia 12 优化的合成引擎）
- 🎛 现代化 UI（基于 [TKCode.Arc](https://github.com/AIDotNet/TKCode.Arc) 组件库，Arc 风格 + 弹簧动效）
- 🧩 插件化架构

## 🛠 技术栈
- **运行时**：.NET 11（STS，支持至 2028 年 11 月）
- **UI 框架**：Avalonia 12（原生 Wayland 支持、开源 WebView、跨平台无障碍）
- **UI 组件库**：TKCode.Arc（Arc 风格 Avalonia 12 原生控件，112 个组件，MIT 协议）[citation:1][citation:2]
- **渲染**：SkiaSharp 3.0（Avalonia 12 默认后端）

## 🚀 快速开始

### 环境要求
- [.NET 11 SDK](https://dotnet.microsoft.com/download/dotnet/11.0)
- 对应平台依赖（Linux 需安装 X11 或 Wayland 相关库）

### 构建与运行
```bash
git clone https://github.com/yourname/kudo.git
cd kudo
dotnet build
dotnet run --project Kudo.Desktop
