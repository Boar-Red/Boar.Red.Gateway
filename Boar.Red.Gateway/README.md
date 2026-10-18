# Boar.Red.Gateway

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)

Boar.Red 微服务框架（微服务架构），网关服务。按规则将请求路由到后端微服务节点，支持限流与流量切换、IP 封禁、响应缓存、HTTP 302 透传与路由统计。

Boar.Red Service framework — the gateway service. Routes requests to backend microservice nodes by configurable rules, with rate limiting & traffic switching, IP blocking, response caching, HTTP 302 pass-through and route statistics.

仓库文档（快速开始、路由 URL 前缀、配置说明）见仓库根目录 [README.md](https://github.com/Boar-Red/Boar.Red.Gateway#readme)，更新日志见 [CHANGELOG.md](https://github.com/Boar-Red/Boar.Red.Gateway/blob/main/CHANGELOG.md)。

For the full documentation (quick start, route URL prefix, configuration), see the repository [README](https://github.com/Boar-Red/Boar.Red.Gateway#readme); see [CHANGELOG.md](https://github.com/Boar-Red/Boar.Red.Gateway/blob/main/CHANGELOG.md) for release notes.

本包（`Boar.Red.Gateway`）为网关服务端宿主；如需在客户端调用网关相关接口，请使用 `Boar.Red.Gateway.Client` 包，共享定义请使用 `Boar.Red.Gateway.Defines` 包。

This package (`Boar.Red.Gateway`) is the gateway host. Use `Boar.Red.Gateway.Client` for calling gateway APIs from clients, and `Boar.Red.Gateway.Defines` for shared definitions.
