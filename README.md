# AgenticUI.NET

[![CI](https://github.com/Z18393520308/AgenticUI-NET/actions/workflows/ci.yml/badge.svg)](https://github.com/Z18393520308/AgenticUI-NET/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/AgenticUI.Core.svg)](https://www.nuget.org/packages/AgenticUI.Core)
[![License: AGPL-3.0](https://img.shields.io/badge/license-AGPL--3.0--only-blue.svg)](LICENSE)

让专业桌面软件的真实控件可被外部程序发现、读取、操作、引导和记录。
支持 WPF 与 WinForms、.NET 8 与 .NET Framework 4.8；适用于工业上位机、仪器、设计工具和企业客户端。

**控件本身没有 AI。** 人和 AI 控制端使用同一套界面、原生事件与业务逻辑；不接入模型或网络时，
软件仍正常人工使用。既可使用 Agentic 控件，也可为已有原生控件附加能力，无需另做 AI 专用界面。

![语义控件与事件时间线演示](docs/images/agenticui-overview.png)

## 只需阅读两份文档

| 读者 | 唯一入口 | 内容 |
| --- | --- | --- |
| 第一次接入的应用开发者 | [使用指南](docs/quickstart.zh-CN.md) | 安装、完整 WPF/WinForms 示例、连接、控件操作、配对、日志与排错 |
| 控件库维护者、控制端开发者、接手 AI | [开发文档](docs/development.zh-CN.md) | 架构、全部动作与状态协议、配置边界、扩展、测试和发布 |

README 和 NuGet 说明仅作入口，不再维护重复教程。原快速开始链接仍有效；旧交接及专项文档
已合并，版本差异统一查 [CHANGELOG](CHANGELOG.md)。

## 版本与能力

当前正式版为 [0.7.0](https://github.com/Z18393520308/AgenticUI-NET/releases/tag/v0.7.0)，
包含应用内网关/首次配对、items.v1 下拉选项读取与选择修复。
从 0.6.1 升级时请统一更新四个包；版本分界和安装方式见[版本范围](docs/quickstart.zh-CN.md#start)。

- 稳定 ID、状态发现、语义动作与事件，本地审计和默认敏感脱敏。
- 动态描边、编号、纯文本气泡；按连接隔离并清理，不等于点击或业务确认。
- 按钮、输入、勾选、下拉、列表、日期/数值、树、菜单和 DataGrid。
- 表格行列读取、编辑、增删、排序过滤与定位；应用界面内鼠标动作。
- 默认本机 Named Pipe；可配置随应用启动的 WSS/TLS 和 UDP 发现，保留首次核验。
- 原生外观/可选主题，两套 Workbench 和 RemoteConsole。Web 控件尚未实现。

## 授权与参与

授权以 [LICENSE](LICENSE) 和 [LICENSING.md](LICENSING.md) 为准；开源路径为 AGPL-3.0-only，
另提供独立商业许可路径。企业身份本身不自动产生费用。社区能力与未来服务见 [EDITIONS.md](EDITIONS.md)。

- [贡献指南](CONTRIBUTING.md) / [行为准则](CODE_OF_CONDUCT.md)
- [安全策略](SECURITY.md) / [修改记录](CHANGELOG.md)
- [商业许可模板](COMMERCIAL-LICENSE-TEMPLATE.md) / [CLA 模板](CLA-TEMPLATE.md)

模板不是已生效合同，正式使用须完成主体信息和审查。请勿在公开问题、日志或截图里提交令牌与业务秘密。
