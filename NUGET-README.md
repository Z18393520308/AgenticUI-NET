# AgenticUI.NET

AgenticUI.NET 为 WPF 与 Windows Forms 控件提供稳定语义 ID、事件广播、可视化引导、
本地审计和经过令牌认证的本机语义命令。

## 安装

```powershell
# WPF
dotnet add package AgenticUI.Wpf --version 0.3.0

# WinForms
dotnet add package AgenticUI.WinForms --version 0.3.0

# 可选：本机 Named Pipe 网关
dotnet add package AgenticUI.Remote --version 0.3.0
```

支持 .NET 8 和 .NET Framework 4.8。完整示例、快速开始、安全边界和授权说明请访问：

`0.3.0` 新增 DataGrid 行列分页读取、增删行、排序过滤、滚动定位、
单元格选择和高亮等语义动作。

- [GitHub 仓库](https://github.com/Z18393520308/AgenticUI-NET)
- [快速开始](https://github.com/Z18393520308/AgenticUI-NET/blob/main/docs/quickstart.zh-CN.md)
- [安全策略](https://github.com/Z18393520308/AgenticUI-NET/blob/main/SECURITY.md)
- [开源与商业授权](https://github.com/Z18393520308/AgenticUI-NET/blob/main/LICENSING.md)

本包依据 `AGPL-3.0-only` 提供。需要闭源集成且不采用 AGPL 路径时，请联系项目维护者讨论
单独的商业许可证。
