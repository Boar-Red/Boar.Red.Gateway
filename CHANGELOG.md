# 更新日志 / Changelog — Boar.Red.Gateway

## 10.0.0 — 首个开源版本 / First Open-Source Release

Boar.Red 微服务框架网关以 MIT 协议开源，目标框架 .NET 10。包含三个项目：`Boar.Red.Gateway`（网关服务）、`Boar.Red.Gateway.Defines`（共享定义）、`Boar.Red.Gateway.Client`（客户端封装）。

The Boar.Red gateway is open-sourced under the MIT license, targeting .NET 10. It contains three projects: `Boar.Red.Gateway` (the service), `Boar.Red.Gateway.Defines` (shared definitions) and `Boar.Red.Gateway.Client` (client wrapper).

核心能力 / Highlights：

- 规则路由与数学表达式匹配转发规则 / rule-based routing with math-expression matching
- RPS 限流与终点超限流量自动切换 / rate limiting with automatic traffic switching on endpoint overflow
- IP 封禁、响应缓存、HTTP 302 透传、路由统计 / IP blocking, response caching, HTTP 302 pass-through, route statistics
- 路由 URL 前缀可配置（`@` / `~` / 空前缀）/ configurable route URL prefix (`@` / `~` / empty)

---

以下为历史版本的手工更新记录（迁移自旧版 README，原文保留）。

The following are historical manual release notes (migrated from the legacy README; original text preserved).

---

## 1.0.2

- 发布：1.0.2 版本。
- 支持新功能：当终点流量超过指定的最大值时，自动将超过的流量切换至其他节点。
- 服务配置文件相关字段更改：
	- "EndPointRequestsCountPerSecondMax" 字段更改为："EndPointRPSMax"；
	- "RequestsCountPerSecondMax" 字段更改为："RPSMax"；
	- 新增字段："IsDisableWhenRPSOverMax"。

Released: 1.0.2.

- New feature: when an endpoint's traffic exceeds its configured maximum, the excess traffic is automatically switched to other nodes.
- Config field changes:
	- `EndPointRequestsCountPerSecondMax` renamed to `EndPointRPSMax`;
	- `RequestsCountPerSecondMax` renamed to `RPSMax`;
	- new field: `IsDisableWhenRPSOverMax`.

---

## 1.0.1（2022-09-22）

- 发布：1.0.1 版本。
- 更新了日志组件。
- 服务配置文件新增"日志组件"的相关字段：
	- AutoFlushLogBufferIntervalSeconds；
	- MaxBytesCountPerLogFile；
	- LogRecordsCountPerFileWrite；
	- TimeoutSecondsToStorageLogRecords；
	- ExceptionLogIgnoreKeys。

Released: 1.0.1.

- Updated the logging component.
- New logging-related config fields:
	- `AutoFlushLogBufferIntervalSeconds`;
	- `MaxBytesCountPerLogFile`;
	- `LogRecordsCountPerFileWrite`;
	- `TimeoutSecondsToStorageLogRecords`;
	- `ExceptionLogIgnoreKeys`.

---

## 2022-09-15

- 服务配置文件新增字段：HttpIPAddress，HttpPort，HttpIsPAddress，HttpsPort。
- EndPoint.IsDisable 升级至 EndPoint.State。
- RouteRate 升级至 DefaultRouteRate。

- New config fields: `HttpIPAddress`, `HttpPort`, `HttpIsPAddress`, `HttpsPort`.
- `EndPoint.IsDisable` upgraded to `EndPoint.State`.
- `RouteRate` upgraded to `DefaultRouteRate`.

---

## 2022-07-30

- 项目开启使用"Null 指针检查"选项。
- 支持 HTTP 的 302 跳转功能，将 EndPoint 的 302 跳转转发给客户端，而不是在路由环节跳转。
- 完善了缓存刷新接口。
- 验证了"响应同步转发"功能，在 API、普通文件上下传的条件下：
	- 不能大幅提高转发性能，因此暂时搁置。
- 使用数学表达式匹配转发规则。

- Enabled null-checking across the project.
- HTTP 302 redirects from endpoints are now forwarded to the client instead of being followed during routing.
- Improved the cache refresh API.
- Validated "synchronous response forwarding": under API and regular file upload/download scenarios it does not significantly improve forwarding performance, so it is put on hold.
- Forwarding rules can now be matched with math expressions.
