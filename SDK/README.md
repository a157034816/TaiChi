# SDK 目录说明

`SDK/` 用于存放面向外部接入方的客户端封装、契约说明和使用示例。

## 当前结构

- `SDK/NodeGraph`
  - `README.md`：NodeGraph SDK 总览、统一 HTTP 契约与多语言说明
  - `javascript`：JavaScript SDK，适用于浏览器或 Node.js 环境
  - `dotnet`：.NET SDK，适用于 C# 业务系统接入
  - `rust`：Rust SDK，适用于 Rust 服务端或工具链接入

## 设计原则

- 所有 SDK 都围绕 `Service/NodeGraph` 暴露的 HTTP API 进行封装。
- SDK 不负责替代业务系统保存节点图结果，而是帮助业务侧快速创建编辑会话并读取结果状态。
- 编辑完成后的最终节点图数据由 NodeGraph 回调业务方提供的 webhook，业务方按自身领域逻辑处理。
