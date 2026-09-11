# AgenticUI.NET

为 WPF / WinForms 的真实控件提供稳定 ID、状态发现、语义操作、动态引导和本地审计。
控件不含 AI，人和外部控制端共用原界面与业务逻辑，支持 .NET 8 / .NET Framework 4.8。

WPF 应用安装 AgenticUI.Wpf，WinForms 应用安装 AgenticUI.WinForms；需要进程外通信时添加
同版本 AgenticUI.Remote，Core 会作为依赖引入。默认本机 Named Pipe，不自动开放网络。

文档只维护两个入口：

- [使用指南：安装、完整示例、操作与排错](https://github.com/Z18393520308/AgenticUI-NET/blob/main/docs/quickstart.zh-CN.md)
- [开发文档：架构、协议、测试与扩展](https://github.com/Z18393520308/AgenticUI-NET/blob/main/docs/development.zh-CN.md)

main 文档会明确区分正式包与未发布源码能力；不能仅凭同名 API 出现在 main 就认为当前安装包支持。
版本变化见 [CHANGELOG](https://github.com/Z18393520308/AgenticUI-NET/blob/main/CHANGELOG.md)。
授权和安全边界见 [LICENSING](https://github.com/Z18393520308/AgenticUI-NET/blob/main/LICENSING.md)
与 [SECURITY](https://github.com/Z18393520308/AgenticUI-NET/blob/main/SECURITY.md)。

本包按 AGPL-3.0-only 提供，另有独立商业许可路径，具体以授权文件和有效协议为准。
