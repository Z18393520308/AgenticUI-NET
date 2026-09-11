# Gateway 安全部署指南

当前源码已移除独立 Gateway 程序，统一使用 AgenticUI.Remote 中的应用内宿主。
请阅读[内嵌网关接入与部署文档](embedded-host.zh-CN.md)，在应用入口初始化一次，
通过 agenticui.json 控制 WSS 和 UDP 发现。网络与发现默认关闭，配置修改后重启生效。

此变更尚未发布，0.6.1 NuGet 不包含新宿主 API。
