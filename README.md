# 通用Word智能填充Agent

项目路径：C:/gitrepos/FrameworkAgentMVP  
创建日期：2026-05-14  
最后更新：2026-05-14  
当前状态：W8 进行中（验收与收尾）

---

## 项目定位

本项目是一个基于 .NET + Microsoft Agent Framework + GitHub Copilot SDK 的实验性系统，目标是实现 Word 模板智能填充与文档生成。

README 仅保留以下内容：

- 项目概览
- 快速启动
- 文档导航

实现细节、功能说明、阶段复盘与变更记录已迁移到独立文档。

---

## 技术栈

- 后端：C# + .NET 8 + Microsoft Agent Framework + GitHub Copilot SDK
- 前端：Vue3 + TypeScript + Element Plus
- 文档处理：OpenXML SDK（主）/ DocX / NPOI
- 数据存储：SQLite + 本地文件系统

---

## 快速开始

### 1. 环境准备

```bash
node -v
dotnet --version
```

可选（用于 Copilot 能力）：

```bash
winget install GitHub.cli
gh extension install github/gh-copilot
gh auth login
```

### 2. 启动后端

```bash
cd backend/FrameAgentWordFill
dotnet run
```

默认地址：

- http://localhost:5000
- http://localhost:5000/swagger

### 3. 启动前端

```bash
cd frontend
npm install
npm run dev
```

默认地址：

- http://localhost:5173

### 4. 常用检查接口

```powershell
Invoke-RestMethod -Uri "http://localhost:5000/health" -Method Get
Invoke-RestMethod -Uri "http://localhost:5000/test/db/tables" -Method Get
Invoke-RestMethod -Uri "http://localhost:5000/test/llm" -Method Get
Invoke-RestMethod -Uri "http://localhost:5000/api/templates" -Method Get
```

---

## 文档导航

- 总索引：docs/00_知识索引.md

### 分类命名规则

- `01_kb_*`：知识库文档
- `02_ref_*`：参考记录文档
- `03_guide_*`：操作指南文档
- `04_arch_*`：架构设计文档
- `05_api_*`：接口规范文档
- `11_module_*`：模块总览文档
- `12_review_*`：知识回顾文档

补充规范（建议统一遵循）：

- 命名格式：`NN_类型_主题.md`
- `NN`：两位数字前缀（用于排序与扩展）
- `类型`：固定短标识（如 `kb`、`ref`、`guide`）
- `主题`：简洁中文语义，避免空格和特殊符号
- 周报/阶段文档建议保留 `wN` 结构，例如：`02_ref_w4_实现流程.md`

预留扩展类型（05 之后可按需启用）：

- `04_arch_*`：架构设计（系统结构、模块边界、时序/部署）
- `05_api_*`：接口规范（OpenAPI、请求响应契约、错误码）
- `06_data_*`：数据设计（ER、表结构、迁移与字典）
- `07_test_*`：测试资产（测试计划、用例、回归报告）
- `08_ops_*`：运维部署（环境配置、发布流程、监控告警）
- `09_sec_*`：安全合规（权限模型、审计、合规检查）
- `10_product_*`：产品设计（PRD、交互说明、业务规则）
- `11_module_*`：模块总览（核心模块、职责边界、阅读入口）
- `12_review_*`：知识回顾（阶段总结、经验复盘、行动项）

说明：当前仓库已启用 `01`-`05`、`11`、`12`，其余为预留槽位，后续启用时保持同一命名结构即可。

### 核心入口

- 需求定义：docs/01_kb_需求定义_mvp.md
- 技术可研：docs/01_kb_技术可行性研究.md
- MAF 研究：docs/01_kb_maf技术研究.md
- 执行计划：docs/03_guide_项目执行计划.md
- 文件规划：docs/03_guide_项目文件规划.md
- 系统架构设计：docs/04_arch_系统架构设计.md
- 接口规范：docs/05_api_接口规范.md
- 主要模块总览：docs/11_module_主要模块总览.md
- 功能问题排查手册：docs/11_module_功能问题排查手册.md
- 知识回顾与总结：docs/12_review_知识回顾与总结.md

### 新增拆分文档

- 系统功能详解：docs/01_kb_系统功能详解.md
- 项目进度与变更记录：docs/02_ref_项目进度与变更记录.md
- AI 项目理解指南：docs/03_guide_ai协作与项目理解指南.md
- 系统架构设计：docs/04_arch_系统架构设计.md
- 接口规范：docs/05_api_接口规范.md
- 主要模块总览：docs/11_module_主要模块总览.md
- 功能问题排查手册：docs/11_module_功能问题排查手册.md
- 知识回顾与总结：docs/12_review_知识回顾与总结.md

### 参考记录

- 实现流程：docs/02_ref_w1_实现流程.md 至 docs/02_ref_w8_实现流程.md
- 验收报告：docs/02_ref_w2_验收报告.md、docs/02_ref_w3_验收报告.md、docs/02_ref_w4_验收报告.md、docs/02_ref_w6_验收报告.md、docs/02_ref_w7_验收报告.md
- 修复日志：docs/02_ref_w8_修复日志.md

---

## 建议阅读路径

```text
首次了解项目 -> README.md
AI 快速入门与协作规则 -> docs/03_guide_ai协作与项目理解指南.md
了解功能范围 -> docs/01_kb_系统功能详解.md
查看当前进度/历史变更 -> docs/02_ref_项目进度与变更记录.md
理解需求与计划 -> docs/01_kb_需求定义_mvp.md + docs/03_guide_项目执行计划.md
进入开发实现 -> docs/02_ref_w1_实现流程.md 到 docs/02_ref_w8_实现流程.md
查看验收结果 -> docs/02_ref_w2_验收报告.md + docs/02_ref_w3_验收报告.md + docs/02_ref_w4_验收报告.md + docs/02_ref_w6_验收报告.md + docs/02_ref_w7_验收报告.md
```

---

## 给 AI 助手的说明

**如果你是 AI 助手（GitHub Copilot、Claude、ChatGPT 等）**，请按以下顺序快速上手：

### AI 第一次进入项目（15 分钟）

1. **读这个文件** - AI_QUICK_START.md（5 分钟）
   - 快速了解如何处理问题
   - 学会查文档的顺序
   - 知道输出的格式规范

2. **读协作指南** - docs/03_guide_ai协作与项目理解指南.md（5 分钟）
   - 理解项目业务链路
   - 学会系统化分析问题

3. **读工作流指令** - .instructions.md 或 .copilot-instructions.md（5 分钟）
   - 掌握问题处理的完整流程
   - 记住快速决策树

**完成后，你就可以独立处理用户的问题了。**

### AI 处理问题时的标准流程

当用户报告问题时，按这个流程：

```
用户报告问题
  ↓
[Step 1] 收集信息（追问 5 个问题）
  ↓
[Step 2] 查文档（按优先级查：01_kb → 02_ref → W*_验收 → 11_module → 06_issue）
  ↓
[Step 3] 诊断分类（已知限制 / 已报告 / 使用错误 / 新Bug）
  ↓
[Step 4] 输出报告（【诊断结果】【根本原因】【建议行动】【相关文档】）
```

### AI 相关的指令文件

| 文件 | 用途 | 阅读时间 |
|------|------|---------|
| `.instructions.md` | 完整的问题处理流程 | 20 分钟 |
| `.copilot-instructions.md` | 快速参考版本 | 10 分钟 |
| `AI_QUICK_START.md` | 新手入门指南 | 5 分钟 |
| `docs/03_guide_ai协作与项目理解指南.md` | 项目协作规范 | 10 分钟 |

**推荐顺序**：AI_QUICK_START.md → .copilot-instructions.md → .instructions.md

### AI 快速查询表

想快速找到答案？用这个表：

| 想了解... | 查这个文件 |
|---------|----------|
| 功能是什么 | docs/01_kb_系统功能详解.md |
| 功能是否已完成 | docs/02_ref_项目进度与变更记录.md |
| 功能的限制是什么 | docs/02_ref_w{N}_验收报告.md |
| 怎样排查问题 | docs/11_module_功能问题排查手册.md |
| 问题是否已报告 | docs/06_issue_*.md |

---

## 说明

如需了解具体功能点、AI 策略、置信度机制、阶段完成细节，请优先查看：

1. docs/01_kb_系统功能详解.md
2. docs/02_ref_项目进度与变更记录.md
3. docs/03_guide_ai协作与项目理解指南.md


