# 项目文件规划 - 完整目录结构

**文档版本**：V1.0  
**更新日期**：2026年05月14日  
**目的**：统一规划后端和前端的文件结构，避免开发过程中的混乱

---

## 📁 项目根目录结构

```
FrameworkAgentMVP/
├── README.md                          # 项目索引文档
├── 01_kb_需求定义_mvp.md                        # MVP需求文档
├── 01_kb_技术可行性研究.md                  # 技术方案文档
├── 03_guide_项目执行计划.md                    # 8周执行计划
├── 03_guide_项目文件规划.md                        # 本文档
├── 01_kb_maf技术研究.md  # MAF技术研究
├── 02_ref_w1_实现流程.md                     # 第1周实现流程
├── 02_ref_w2_实现流程.md                     # 第2周实现流程
├── 02_ref_w3_实现流程.md                     # 第3周实现流程
├── 02_ref_w4_实现流程.md                     # 第4周实现流程
├── 需求                               # 原始需求文档
│
├── /backend                           # 后端项目目录
│   └── /FrameAgentWordFill            # .NET 8 Web API 项目
│       ├── FrameAgentWordFill.csproj  # 项目文件
│       ├── Program.cs                 # 入口文件
│       ├── appsettings.json           # 配置文件
│       ├── appsettings.Development.json
│       │
│       ├── /Data                      # 数据访问基础层
│       │   ├── DatabaseInitializer.cs # 数据库初始化（W1）
│       │   └── DatabaseSchema.sql     # 数据库表结构参考（W1）
│       │
│       ├── /Models                    # 数据模型（按功能分类）
│       │   ├── /Templates             # 模板相关模型
│       │   │   ├── Template.cs        # 模板实体（W2）
│       │   │   ├── Field.cs           # 字段实体（W2）
│       │   │   ├── TableDefinition.cs # 表格定义实体（W2）
│       │   │   └── TableColumn.cs     # 表格列实体（W2）
│       │   ├── /Parsing               # 解析相关模型
│       │   │   ├── TemplateParseResult.cs # 模板解析结果（W2）
│       │   │   ├── FieldInfo.cs       # 字段信息（W2）
│       │   │   └── TableInfo.cs       # 表格信息（W2）
│       │   ├── /Chat                  # 对话相关模型
│       │   │   ├── ChatSession.cs     # 对话会话（W4）
│       │   │   ├── SessionField.cs    # 会话字段（W4）
│       │   │   └── ChatMessage.cs     # 对话消息（W4）
│       │   └── /Generation            # 生成相关模型
│       │       ├── GenerateRequest.cs # 生成请求（W3）
│       │       └── GenerateResult.cs  # 生成结果（W3）
│       │
│       ├── /Repositories              # 数据访问层（Repository 模式）
│       │   ├── TemplateRepository.cs  # 模板数据访问（W2）
│       │   └── ChatSessionRepository.cs # 会话数据访问（W4）
│       │
│       ├── /Services                  # 业务逻辑层
│       │   ├── FileStorageService.cs  # 文件存储服务（W1）
│       │   ├── AIService.cs           # AI服务（MAF + Copilot）（W1）
│       │   ├── TemplateService.cs     # 模板管理服务（W2）
│       │   ├── GenerateService.cs     # 文档生成服务（W3）
│       │   └── ChatService.cs         # 对话服务（W4）
│       │
│       ├── /Tools                     # 工具类（MAF Tools）
│       │   ├── TemplateParser.cs      # 模板解析工具（W2）
│       │   ├── DocGenerator.cs        # 文档生成工具（W3）
│       │   ├── DataValidator.cs       # 数据验证工具（W3）
│       │   └── AIFieldExtractor.cs    # AI字段提取工具（W4）
│       │
│       ├── /Agents                    # Agent 配置和定义
│       │   └── WordFillAgentConfig.cs # WordFillAgent 配置（W4）
│       │
│       ├── /Middleware                # 中间件
│       │   ├── ExceptionMiddleware.cs # 全局异常处理（W1）
│       │   └── LoggingMiddleware.cs   # 请求日志记录（W1）
│       │
│       ├── /Controllers               # API 控制器
│       │   ├── TemplateController.cs  # 模板管理API（W2）
│       │   ├── GenerateController.cs  # 文档生成API（W3）
│       │   └── ChatController.cs      # 对话API（W4）
│       │
│       └── /logs                      # 日志目录（自动生成）
│           └── frameagent-*.log
│
├── /frontend                          # 前端项目目录
│   ├── package.json                   # NPM 配置
│   ├── vite.config.ts                 # Vite 配置
│   ├── tsconfig.json                  # TypeScript 配置
│   ├── index.html                     # 入口 HTML
│   │
│   └── /src
│       ├── main.ts                    # 入口文件（W1）
│       ├── App.vue                    # 根组件（W1）
│       │
│       ├── /api                       # API 封装层
│       │   ├── index.ts               # Axios 配置（W1）
│       │   ├── template.ts            # 模板 API（W2）
│       │   ├── generate.ts            # 生成 API（W3）
│       │   └── chat.ts                # 对话 API（W4）
│       │
│       ├── /router                    # 路由配置
│       │   └── index.ts               # 路由定义（W2）
│       │
│       ├── /stores                    # 状态管理（Pinia）
│       │   ├── templateStore.ts       # 模板状态（W2）
│       │   └── chatStore.ts           # 对话状态（W4）
│       │
│       ├── /views                     # 页面组件
│       │   ├── /admin                 # 管理后台页面
│       │   │   ├── TemplateList.vue   # 模板列表页（W2）
│       │   │   └── TemplateConfig.vue # 字段配置页（W2）
│       │   │
│       │   ├── /user                  # 用户界面页面
│       │   │   ├── Home.vue           # 首页/状态页（W1）
│       │   │   ├── TestGenerate.vue   # 文档生成测试页（W3）
│       │   │   └── ChatFill.vue       # 对话填写页（W4）
│       │   │
│       │   └── /test                  # 测试页面（开发用）
│       │
│       ├── /components                # 公共组件
│       │   ├── ChatWindow.vue         # 对话窗口组件（W4）
│       │   ├── FieldMatcher.vue       # 字段匹配组件（W5）
│       │   ├── FileUploader.vue       # 文件上传组件（W3）
│       │   └── ProgressBar.vue        # 进度条组件（W4）
│       │
│       ├── /composables               # 组合式函数（Vue3 Composition API）
│       │   ├── useTemplate.ts         # 模板逻辑（W2）
│       │   ├── useChat.ts             # 对话逻辑（W4）
│       │   └── useGenerate.ts         # 生成逻辑（W3）
│       │
│       ├── /utils                     # 工具函数
│       │   ├── formatter.ts           # 格式化函数（W2）
│       │   └── storage.ts             # IndexedDB 操作（W4）
│       │
│       ├── /types                     # TypeScript 类型定义
│       │   ├── template.d.ts          # 模板类型（W2）
│       │   ├── chat.d.ts              # 对话类型（W4）
│       │   ├── generate.d.ts          # 生成类型（W3）
│       │   └── common.d.ts            # 通用类型（W1）
│       │
│       └── /assets                    # 静态资源
│           ├── /images
│           └── /styles
│               └── global.css
│
└── /storage                           # 本地存储目录（自动创建）
    ├── /data                          # SQLite 数据库
    │   └── frameagent.db              # 主数据库文件
    │
    ├── /templates                     # 模板文件存储
    │   ├── template_001_xxx.docx
    │   └── ...
    │
    ├── /output                        # 生成的文档
    │   ├── 项目申请表_20260514_001.docx
    │   └── ...
    │
    └── /uploads                       # 用户上传的临时文件
        ├── import_001.xlsx
        └── ...
```

---

## 📋 文件创建时间线（按周划分）

### W1 - 基础设施搭建

**目标**：搭建可运行的前后端骨架，配置数据库、中间件、API层

**后端文件**：
```
backend/FrameAgentWordFill/
├── FrameAgentWordFill.csproj          # 项目文件
├── Program.cs                         # 入口和配置
├── appsettings.json                   # 配置文件
├── appsettings.Development.json       # 开发配置
├── Data/
│   └── DatabaseInitializer.cs         # 数据库初始化
├── Middleware/
│   ├── ExceptionMiddleware.cs         # 全局异常处理
│   └── LoggingMiddleware.cs           # 请求日志记录
├── Services/
│   ├── FileStorageService.cs          # 文件存储
│   └── AIService.cs                   # AI服务（MAF + Copilot）
└── logs/                              # 日志目录（自动生成）
```

**前端文件**：
```
frontend/
├── package.json                       # 依赖配置
├── vite.config.ts                     # Vite配置
├── src/
│   ├── main.ts                        # 入口
│   ├── App.vue                        # 根组件
│   ├── api/
│   │   └── index.ts                   # Axios配置（统一请求封装）
│   ├── types/
│   │   └── common.d.ts                # 通用类型定义
│   └── views/
│       └── user/
│           └── Home.vue               # 状态测试页
└── node_modules/                      # 依赖（自动生成）
```

**存储目录**：
```
storage/
├── data/                              # 数据库目录
├── templates/                         # 模板目录
├── output/                            # 输出目录
└── uploads/                           # 上传目录
```

---

### W2 - 模板管理与解析

**目标**：实现模板上传、解析、字段配置功能，建立完整的模板管理体系

**后端新增文件**：
```
backend/FrameAgentWordFill/
├── Models/
│   ├── /Templates                     # 模板相关模型
│   │   ├── Template.cs                # 模板实体
│   │   ├── Field.cs                   # 字段实体
│   │   ├── TableDefinition.cs         # 表格定义
│   │   └── TableColumn.cs             # 表格列
│   └── /Parsing                       # 解析相关模型
│       ├── TemplateParseResult.cs     # 解析结果
│       ├── FieldInfo.cs               # 字段信息
│       └── TableInfo.cs               # 表格信息
├── Repositories/
│   └── TemplateRepository.cs          # 模板数据访问
├── Services/
│   └── TemplateService.cs             # 模板管理服务
├── Tools/
│   └── TemplateParser.cs              # 模板解析工具
└── Controllers/
    └── TemplateController.cs          # 模板管理API
```

**前端新增文件**：
```
frontend/src/
├── api/
│   └── template.ts                    # 模板API（封装后端接口）
├── composables/
│   └── useTemplate.ts                 # 模板逻辑（组合式函数）
├── stores/
│   └── templateStore.ts               # 模板状态（Pinia）
├── types/
│   └── template.d.ts                  # 模板类型定义
├── utils/
│   └── formatter.ts                   # 格式化函数（日期、文件大小等）
├── router/
│   └── index.ts                       # 路由配置（所有页面路由）
└── views/
    └── admin/
        ├── TemplateList.vue           # 模板列表页
        └── TemplateConfig.vue         # 字段配置页
```
└── views/
    └── admin/
        ├── TemplateList.vue           # 模板列表
        └── TemplateConfig.vue         # 字段配置
```

---

### W3 - 文档生成核心闭环

**目标**：实现文档生成核心功能，完成从数据到文档的完整链路

**后端新增文件**：
```
backend/FrameAgentWordFill/
├── Models/
│   └── /Generation
│       ├── GenerateRequest.cs         # 生成请求（API请求模型）
│       └── GenerateResult.cs          # 生成结果（API响应模型）
├── Tools/
│   ├── DocGenerator.cs                # 文档生成工具（Word填充）
│   └── DataValidator.cs               # 数据验证工具（字段校验）
├── Services/
│   └── GenerateService.cs             # 文档生成服务（业务编排）
└── Controllers/
    └── GenerateController.cs          # 文档生成API
```

**前端新增文件**：
```
frontend/src/
├── api/
│   └── generate.ts                    # 生成API（封装后端接口）
├── composables/
│   └── useGenerate.ts                 # 生成逻辑（组合式函数）
├── types/
│   └── generate.d.ts                  # 生成类型定义
├── components/
│   └── FileUploader.vue               # 文件上传组件（拖拽上传）
└── views/
    └── user/
        └── TestGenerate.vue           # 文档生成测试页
```

---

### W4 - AI 智能对话填充

**目标**：集成Agent框架，实现AI对话引导式字段填充功能

**后端新增文件**：
```
backend/FrameAgentWordFill/
├── Models/
│   └── /Chat                          # 对话相关模型
│       ├── ChatSession.cs             # 对话会话（会话管理）
│       ├── SessionField.cs            # 会话字段（已填字段）
│       └── ChatMessage.cs             # 对话消息（历史记录）
├── Repositories/
│   └── ChatSessionRepository.cs       # 会话数据访问
├── Services/
│   └── ChatService.cs                 # 对话服务（业务编排）
├── Tools/
│   └── AIFieldExtractor.cs            # AI字段提取（NLU）
├── Agents/
│   └── WordFillAgentConfig.cs         # Agent配置（MAF）
└── Controllers/
    └── ChatController.cs              # 对话API
```

**前端新增文件**：
```
frontend/src/
├── api/
│   └── chat.ts                        # 对话API（封装后端接口）
├── composables/
│   └── useChat.ts                     # 对话逻辑（组合式函数）
├── stores/
│   └── chatStore.ts                   # 对话状态（Pinia）
├── types/
│   └── chat.d.ts                      # 对话类型定义
├── utils/
│   └── storage.ts                     # IndexedDB操作（会话缓存）
├── components/
│   ├── ChatWindow.vue                 # 对话窗口组件
│   └── ProgressBar.vue                # 进度条组件（字段收集进度）
└── views/
    └── user/
        └── ChatFill.vue               # 对话填写页
```

---

### W5 - 导入填充链路（待实现）

**后端新增文件（预计）**：
```
backend/FrameAgentWordFill/
├── Tools/
│   ├── ExcelParser.cs                 # Excel解析
│   ├── JsonParser.cs                  # JSON解析
│   ├── WordParser.cs                  # Word解析
│   └── FieldMatcher.cs                # 字段匹配算法
├── Services/
│   └── ImportService.cs               # 导入服务
└── Controllers/
    └── ImportController.cs            # 导入API
```

**前端新增文件（预计）**：
```
frontend/src/
├── views/
│   └── user/
│       └── ImportFill.vue             # 导入填写页
└── components/
    └── FieldMatcher.vue               # 字段匹配组件
```

---

### W6 - 复杂模板能力增强（待实现）

**后端新增文件（预计）**：
```
backend/FrameAgentWordFill/
├── Tools/
│   ├── TemplateValidator.cs           # 模板验证
│   ├── PlaceholderNormalizer.cs       # 占位符规范化
│   ├── ContentControlFiller.cs        # 内容控件填充
│   └── PictureReplacer.cs             # 图片替换
└── Models/
    └── ValidationReport.cs            # 验证报告
```

---

### W7 - AI 文件智能提取（待实现）

**后端新增文件（预计）**：
```
backend/FrameAgentWordFill/
├── Tools/
│   ├── FileParser.cs                  # 通用文件解析
│   ├── PdfParser.cs                   # PDF解析
│   ├── ImageOCR.cs                    # 图片OCR
│   ├── AIDataExtractor.cs             # AI数据提取（多引擎）
│   └── ConfidenceCalculator.cs        # 置信度计算
├── Services/
│   └── AIFillService.cs               # AI填充服务
└── Controllers/
    └── AIFillController.cs            # AI填充API
```

**前端新增文件（预计）**：
```
frontend/src/
├── views/
│   └── user/
│       └── AIFill.vue                 # AI智能填充页
└── components/
    ├── FileUploader.vue               # 文件上传组件
    └── ConfidenceEditor.vue           # 置信度编辑器
```

---

## 🔍 文件规划检查与优化建议

### 当前规划合理性分析

#### ✅ 合理之处

1. **清晰的分层架构**
   - Data 层：数据库初始化
   - Models 层：数据实体
   - Repositories 层：数据访问
   - Services 层：业务逻辑
   - Tools 层：工具类（MAF Tools）
   - Controllers 层：API接口

2. **前后端分离**
   - 后端：FrameAgentWordFill（.NET 8）
   - 前端：Vue3 + TypeScript
   - 存储：独立的 storage 目录

3. **模块化设计**
   - 每个功能模块有独立的 Service 和 Controller
   - 工具类可复用
   - 模型定义清晰

4. **渐进式开发**
   - 按周划分，逐步增加复杂度
   - W1 基础设施 → W2 模板管理 → W3 文档生成 → W4 AI对话

#### ⚠️ 需要注意的地方

1. **Models 层可能过于集中**
   - 建议：将不同功能的 Model 分到子目录
   ```
   Models/
   ├── /Templates         # 模板相关模型
   │   ├── Template.cs
   │   ├── Field.cs
   │   └── TableDefinition.cs
   ├── /Chat              # 对话相关模型
   │   ├── ChatSession.cs
   │   └── ChatMessage.cs
   └── /Generation        # 生成相关模型
       ├── GenerateRequest.cs
       └── GenerateResult.cs
   ```

#### ✅ 合理之处

1. **清晰的分层架构**
   - Data 层：数据库初始化
   - Models 层：数据实体（已按功能分类）
   - Repositories 层：数据访问
   - Services 层：业务逻辑
   - Tools 层：工具类（MAF Tools）
   - Middleware 层：全局中间件
   - Controllers 层：API接口

2. **前后端分离**
   - 后端：FrameAgentWordFill（.NET 8）
   - 前端：Vue3 + TypeScript
   - 存储：独立的 storage 目录

3. **模块化设计**
   - 每个功能模块有独立的 Service 和 Controller
   - 工具类可复用
   - 模型定义清晰且分类明确
   - API 层统一封装
   - Composables 组合式函数复用

4. **渐进式开发**
   - 按周划分，逐步增加复杂度
   - W1 基础设施 → W2 模板管理 → W3 文档生成 → W4 AI对话

#### ⚠️ 可选优化（MVP阶段可延后）

1. **前端路由规划**
   - 建议：在 W2 规划完整路由结构（包含未来功能）
   ```typescript
   routes: [
     { path: '/', redirect: '/admin/templates' },
     { path: '/admin/templates', component: TemplateList },
     { path: '/admin/templates/:id/config', component: TemplateConfig },
     { path: '/user/chat/:templateId', component: ChatFill },
     { path: '/user/import/:templateId', component: ImportFill },  // W5
     { path: '/test/generate', component: TestGenerate }
   ]
   ```

2. **全局配置和常量**（可选）
   - 建议：添加配置类统一管理常量
   ```
   backend/FrameAgentWordFill/
   ├── /Configuration
   │   ├── AppSettings.cs      # 应用配置
   │   └── Constants.cs        # 常量定义
   ```

---

## 📝 当前文件结构总结

### 后端结构（已优化）

```
backend/FrameAgentWordFill/
├── FrameAgentWordFill.csproj
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
│
├── /Data                              # 数据访问基础
│   ├── DatabaseInitializer.cs
│   └── DatabaseSchema.sql
│
├── /Models                            # 数据模型（已分类）
│   ├── /Templates
│   │   ├── Template.cs
│   │   ├── Field.cs
│   │   ├── TableDefinition.cs
│   │   └── TableColumn.cs
│   ├── /Parsing
│   │   ├── TemplateParseResult.cs
│   │   ├── FieldInfo.cs
│   │   └── TableInfo.cs
│   ├── /Chat
│   │   ├── ChatSession.cs
│   │   ├── SessionField.cs
│   │   └── ChatMessage.cs
│   └── /Generation
│       ├── GenerateRequest.cs
│       └── GenerateResult.cs
│
├── /Repositories                      # 数据访问层
│   ├── TemplateRepository.cs
│   └── ChatSessionRepository.cs
│
├── /Services                          # 业务逻辑层
│   ├── FileStorageService.cs
│   ├── AIService.cs
│   ├── TemplateService.cs
│   ├── GenerateService.cs
│   ├── ChatService.cs
│   └── ImportService.cs (W5)
│
├── /Tools                             # 工具类（MAF Tools）
│   ├── TemplateParser.cs
│   ├── DocGenerator.cs
│   ├── DataValidator.cs
│   ├── AIFieldExtractor.cs
│   └── FieldMatcher.cs (W5)
│
├── /Agents                            # Agent 配置
│   └── WordFillAgentConfig.cs
│
├── /Controllers                       # API 控制器
│   ├── TemplateController.cs
│   ├── GenerateController.cs
│   ├── ChatController.cs
│   └── ImportController.cs (W5)
│
├── /Middleware                        # 中间件
│   ├── ExceptionMiddleware.cs
│   └── LoggingMiddleware.cs
│
└── /logs                              # 日志（自动生成）
```

### 前端优化结构

```
frontend/src/
├── main.ts
├── App.vue
│
├── /api                               # API 封装
│   ├── index.ts                       # Axios 配置
│   ├── template.ts                    # 模板API
│   ├── generate.ts                    # 生成API
│   ├── chat.ts                        # 对话API
│   └── import.ts                      # 导入API (W5)
│
├── /router
│   └── index.ts                       # 路由配置
│
├── /stores                            # 状态管理
│   ├── templateStore.ts               # 模板状态
│   └── chatStore.ts                   # 对话状态
│
├── /views
│   ├── /admin
│   │   ├── TemplateList.vue
│   │   └── TemplateConfig.vue
│   ├── /user
│   │   ├── Home.vue
│   │   ├── ChatFill.vue
│   │   ├── ImportFill.vue (W5)
│   │   └── AIFill.vue (W7)
│   └── /test
│       └── TestGenerate.vue
│
├── /components                        # 公共组件
│   ├── ChatWindow.vue
│   ├── FieldMatcher.vue
│   ├── FileUploader.vue
│   └── ProgressBar.vue
│
├── /composables                       # 组合式函数
│   ├── useTemplate.ts                 # 模板逻辑
│   ├── useChat.ts                     # 对话逻辑
│   └── useGenerate.ts                 # 生成逻辑
│
├── /utils                             # 工具函数
│   ├── formatter.ts                   # 格式化函数
│   └── storage.ts                     # 存储操作
│
├── /types                             # TypeScript 类型
│   ├── template.d.ts
│   ├── chat.d.ts
│   ├── generate.d.ts
│   └── common.d.ts
│
└── /assets                            # 静态资源
    ├── /images
    └── /styles
        └── global.css
```

---

## 🎯 实施建议（MVP版本）

### ✅ 已完成的优化

- [x] 后端创建 `/Middleware` 目录和异常处理中间件
- [x] 前端创建 `/api` 目录，封装 Axios 请求
- [x] 前端创建 `/types` 目录，定义 TypeScript 类型
- [x] 将 Models 按功能分类到子目录
- [x] 添加 Composables（Vue3 组合式函数）
- [x] 统一 API 路径设计
- [x] 添加前端 stores（状态管理）

### 📝 可选优化（MVP阶段可延后）

- [ ] 创建 `/Configuration` 目录和配置类（可用 appsettings.json 代替）
- [ ] 完善路由规划（包含未来W5-W7功能的路由）
- [ ] 添加单元测试结构
- [ ] 添加 API 文档（Swagger）配置

### ✅ 保持现有规划

- ✅ 后端分层架构（Repository、Service、Controller、Middleware）
- ✅ 前端模块化结构（api、stores、composables、views、components）
- ✅ 存储目录结构（data、templates、output、uploads）
- ✅ 按周渐进式开发计划

---

## 📊 文件数量统计

### W1-W4 已规划文件统计

| 周次 | 后端文件 | 前端文件 | 总计 |
|------|---------|---------|------|
| W1   | 7       | 5       | 12   |
| W2   | 16      | 9       | 25   |
| W3   | 6       | 6       | 12   |
| W4   | 10      | 10      | 20   |
| **总计** | **39** | **30** | **69** |

### W5-W8 预计文件统计

| 周次 | 后端文件 | 前端文件 | 总计 |
|------|---------|---------|------|
| W5   | 6       | 3       | 9    |
| W6   | 5       | 2       | 7    |
| W7   | 7       | 4       | 11   |
| W8   | 0       | 0       | 0    |
| **总计** | **18** | **9** | **27** |

### 项目完成后文件总计

- **后端文件**：约 57 个（不含自动生成）
- **前端文件**：约 39 个（不含 node_modules）
- **文档文件**：约 10 个
- **配置文件**：约 5 个

**总计**：约 111 个核心文件

---

## 🔒 文件命名规范

### 后端命名规范（C#）

1. **类文件**：PascalCase，与类名一致
   - 示例：`TemplateService.cs`、`DocGenerator.cs`

2. **接口文件**：以 `I` 开头
   - 示例：`ITemplateService.cs`、`IRepository.cs`

3. **配置文件**：小写，用点分隔
   - 示例：`appsettings.json`、`appsettings.Development.json`

### 前端命名规范（Vue3 + TypeScript）

1. **组件文件**：PascalCase
   - 示例：`ChatWindow.vue`、`FieldMatcher.vue`

2. **页面文件**：PascalCase
   - 示例：`TemplateList.vue`、`ChatFill.vue`

3. **工具文件**：camelCase
   - 示例：`request.ts`、`storage.ts`

4. **类型文件**：camelCase，以 `.d.ts` 结尾
   - 示例：`template.d.ts`、`chat.d.ts`

5. **配置文件**：小写，用点或横线分隔
   - 示例：`vite.config.ts`、`tsconfig.json`

---

## 📌 注意事项

### 1. 数据库表名前缀

⚠️ **所有数据库表必须使用 `fa_` 前缀**（frame agent）

```sql
fa_templates
fa_fields
fa_tables
fa_table_columns
fa_chat_sessions
fa_session_fields
```

### 2. 存储路径配置

确保所有文件路径通过配置文件管理，不要硬编码：

```json
{
  "Storage": {
    "RootPath": "..\\..\\storage",
    "TemplatesPath": "templates",
    "OutputPath": "output",
    "UploadsPath": "uploads"
  }
}
```

### 3. 日志文件命名

日志文件自动按日期滚动：

```
logs/frameagent-20260514.log
logs/frameagent-20260515.log
```

### 4. .gitignore 配置

确保不提交敏感文件和自动生成的文件：

```gitignore
# 后端
backend/**/bin/
backend/**/obj/
backend/**/logs/

# 前端
frontend/node_modules/
frontend/dist/

# 存储
storage/data/*.db
storage/templates/*
storage/output/*
storage/uploads/*

# 配置
appsettings.*.json
!appsettings.json
```

---

## ✅ 总结

本文件规划文档提供了：

1. ✅ **完整的目录结构**：前端、后端、存储的详细组织
2. ✅ **按周划分的文件创建时间线**：W1-W8 清晰规划
3. ✅ **合理性分析**：指出当前规划的优缺点
4. ✅ **优化建议**：提供更好的组织方式
5. ✅ **命名规范**：统一的文件和代码命名标准
6. ✅ **注意事项**：数据库、存储、日志等关键配置

**建议**：
- 在每周开发前，先查看本文档确认文件创建位置
- 如有新增文件，及时更新本文档
- 保持文件结构的一致性和清晰性


