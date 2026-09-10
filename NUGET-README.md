# AgenticUI.NET

AgenticUI.NET 为 WPF 与 Windows Forms 控件提供稳定语义 ID、事件广播、可视化引导、
本地审计和经过令牌认证的本机语义命令。

## 安装

```powershell
# WPF
dotnet add package AgenticUI.Wpf --version 0.6.1

# WinForms
dotnet add package AgenticUI.WinForms --version 0.6.1

# 可选：本机 Named Pipe 网关
dotnet add package AgenticUI.Remote --version 0.6.1
```

支持 .NET 8 和 .NET Framework 4.8。完整示例、快速开始、安全边界和授权说明请访问：

`0.6.1` 修复 WPF 高亮布局循环，并增加高亮后的界面响应与命名管道读写回归测试。
`0.6.0` 新增动态描边、编号和气泡引导，统一只读与树节点状态协议，
并强化原生编辑校验、敏感数据脱敏、连接清理和长连接稳定性。

- [GitHub 仓库](https://github.com/Z18393520308/AgenticUI-NET)
- [快速开始](https://github.com/Z18393520308/AgenticUI-NET/blob/main/docs/quickstart.zh-CN.md)
- [安全策略](https://github.com/Z18393520308/AgenticUI-NET/blob/main/SECURITY.md)
- [开源与商业授权](https://github.com/Z18393520308/AgenticUI-NET/blob/main/LICENSING.md)

本包依据 `AGPL-3.0-only` 提供。需要闭源集成且不采用 AGPL 路径时，请联系项目维护者讨论
单独的商业许可证。
