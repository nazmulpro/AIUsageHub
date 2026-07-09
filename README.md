# AIUsageHub

**AIUsageHub is a Windows app which monitors usage, quotas, and spending across popular AI coding services in real time.**

## Supported Providers

- **Claude** (Anthropic) — session limits, weekly usage, spend
- **Codex** (OpenAI) — daily usage, costs, tokens
- **Cursor** — credits, premium requests, daily spend
- **Grok** (xAI) — credits used, pay-as-you-go spend
- **OpenRouter** — credits, balance, daily/weekly/monthly spend
- **Z.ai** — token limits, web search limits, plan info

## Features

- **Manual API key entry** for all providers
- **Visual dashboard** with progress bars color-coded by pace (blue/green = on track, yellow = caution, red = critical)
- **Configurable auto-refresh** (1–15 min intervals)
- **Dark/Light/System theme**
- **Minimal view** — collapse/expand metric details per provider
- **Launch at Windows startup** option

## Tech Stack

- **.NET 10** (WPF, Windows-only)

## Getting Started

1. Download the latest release from [Releases](https://github.com/nazmulpro/AIUsageHub/releases)
2. Run `AIUsageHub.exe` and install it
3. Providers prompt for an API key in Settings

> **Note:** This is a Windows-only application built on .NET 10 with WPF.
