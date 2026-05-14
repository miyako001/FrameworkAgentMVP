# W8 修复日志

生成时间：2026-05-14  
修复类型：存储路径解析修复  
优先级：P1（环境一致性与数据落盘路径风险）

---

## 1. 问题描述

在本地运行后端时，发现 `storage` 目录被创建在 `backend/FrameAgentWordFill/bin/...` 下，而不是项目预期的根级 `storage` 目录。

### 现象

1. 启动后端后，`bin` 目录下出现 `storage/data`、`storage/templates`、`storage/output`、`storage/uploads`。
2. SQLite 数据库 `frameagent.db` 可能落到 `bin` 下的临时目录。
3. 开发、调试、发布环境的路径行为不一致，容易造成数据混乱与排查困难。

---

## 2. 根因分析

配置项 `Storage:RootPath` 使用相对路径 `..\\..\\storage`，但代码使用 `AppContext.BaseDirectory` 作为相对路径锚点。

`AppContext.BaseDirectory` 在运行时通常指向可执行输出目录（如 `bin/Debug/net8.0`），导致相对路径被解析到 `bin` 附近，而非项目内容根目录。

---

## 3. 修复方案

统一将相对路径解析锚点从 `AppContext.BaseDirectory` 调整为 `IHostEnvironment.ContentRootPath`。

### 修复原则

1. 保持现有配置值不变（兼容 `Storage:RootPath`）。
2. 仅修正路径解析方式，不改业务逻辑。
3. 文件存储层与数据库连接层使用一致的根路径策略。

---

## 4. 代码变更清单

1. `backend/FrameAgentWordFill/Services/FileStorageService.cs`
2. `backend/FrameAgentWordFill/Data/DatabaseInitializer.cs`
3. `backend/FrameAgentWordFill/Repositories/TemplateRepository.cs`
4. `backend/FrameAgentWordFill/Repositories/ImportSessionRepository.cs`
5. `backend/FrameAgentWordFill/Repositories/ChatSessionRepository.cs`

### 关键变更点

1. 构造函数新增 `IHostEnvironment hostEnvironment` 注入。
2. 相对路径解析改为：
   - `Path.GetFullPath(Path.Combine(hostEnvironment.ContentRootPath, rootPath))`
3. 文件存储目录与数据库路径统一按同一规则计算。

---

## 5. 影响范围评估

### 正向影响

1. `storage` 目录位置稳定，符合项目文档预期。
2. 多运行方式（IDE 启动、命令行启动）路径行为一致。
3. 降低数据落盘路径漂移导致的问题风险。

### 注意事项

1. 旧的 `bin/.../storage` 历史数据不会自动迁移。
2. 如需保留历史数据，请手动迁移至目标 `storage` 目录。

---

## 6. 验证结果

### 已完成

1. 变更文件静态错误检查通过（无语法/分析错误）。
2. 路径逻辑审阅通过，解析锚点已统一。

### 受环境影响未完成

1. `dotnet build` 在本机验证阶段被运行中进程锁定输出文件导致失败（`FrameAgentWordFill.exe/.dll` 被占用）。
2. 失败原因为文件锁，不是本次修复引入的编译错误。

---

## 7. 回归建议

1. 停止正在运行的后端进程后执行：
   - `dotnet build`
   - `dotnet run`
2. 启动后确认 `storage` 创建位置是否为项目预期目录。
3. 通过以下接口做快速健康检查：
   - `/test/db`
   - `/test/db/tables`
4. 如确认无误，可清理旧的 `bin/.../storage`。

---

## 8. 结论

本次修复已完成路径解析策略统一，能够从根本上解决 `storage` 落入 `bin` 目录的异常行为。修复范围小、兼容性好、风险可控，建议纳入 W8 验收收尾内容。

