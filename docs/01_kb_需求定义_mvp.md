# 通用Word智能填充Agent - MVP需求文档

**文档版本**：V1.1-MVP  
**撰写日期**：2026年05月14日  
**文档目的**：明确MVP阶段核心功能，采用前端+FrameAgent后端架构，实现「单底座+双入口」最小可用产品。

---

## 一、MVP范围界定

### 1.1 包含功能
- 单文档生成（最多同时处理5条）
- 对话引导填充模式（支持高级用户快捷指令）
- 外部数据源填充模式（Excel/JSON/Word）
- 后台模板上传与字段配置（支持表格字段）
- 模板下载功能
- 基础用户体验优化

### 1.2 明确排除
- 批量处理（>5条）
- 模板对比功能
- 运营统计模块
- ✅ **已改为SQLite轻量级数据库存储**
- 复杂权限系统（仅区分管理员/普通用户）

---

## 二、技术架构

### 2.1 架构设计

```
┌─────────────────┐     ┌─────────────────────────┐
│   前端 (Vue3)   │────▶│  后端 (Microsoft        │
│                 │     │   FrameAgent框架)       │
│ - 用户界面      │◀────│ - Agent编排             │
│ - 对话交互      │     │ - 工具调用              │
│ - 文件上传      │     │ - 文档生成              │
└─────────────────┘     │ - 模板解析              │
         │              │ - 内容填充              │
         │              └─────────────────────────┘
         │                       │
         ▼                       ▼
┌─────────────────┐     ┌─────────────────┐
│   本地存储      │     │   SQLite 数据库 │
│ (IndexedDB)     │     │ - 模板配置      │
│ - 临时缓存      │     │ - 字段定义      │
│                 │     │ - 会话数据      │
└─────────────────┘     └─────────────────┘
                                 │
                                 ▼
                        ┌─────────────────┐
                        │   本地文件系统  │
                        │ - 模板文件      │
                        │ - 生成文档      │
                        │ - 上传文件      │
                        └─────────────────┘
```

### 2.2 技术栈

| 层级 | 技术选型 | 说明 |
|------|----------|------|
| 前端 | Vue3 + Element Plus | 管理后台界面 |
| 前端 | Vue3 + 自定义对话组件 | 用户填充界面 |
| 后端 | Microsoft FrameAgent | Agent框架，支持工具编排、对话管理 |
| 文档引擎 | FrameAgent Tools | 通过工具调用实现Word文档生成、表格填充 |
| 存储 | **SQLite** | **轻量级本地数据库，替代JSON文件存储，保证数据一致性** |
| 存储 | IndexedDB | 前端临时数据缓存 |

### 2.3 存储方案（SQLite + 本地文件混合）

```
/storage
├── /data                      # SQLite数据库文件
│   └── frameagent.db         # 主数据库（模板、字段、会话等结构化数据）
├── /templates                 # 模板文件存储（二进制文件）
│   ├── template_001.docx
│   ├── template_002.docx
│   └── ...
├── /output                    # 生成文档存储
│   ├── output_20250514_001.docx
│   └── ...
└── /uploads                   # 用户上传临时文件
    ├── import_001.xlsx
    └── ...
```

**存储策略说明**：
- **SQLite数据库**：存储所有结构化数据（模板配置、字段定义、对话会话、字段匹配映射等）
- **本地文件系统**：存储二进制文件（Word模板、生成文档、上传的临时文件）
- **优势**：事务支持、并发安全、数据一致性、查询效率

---

## 三、功能模块

### 3.1 前端模块

#### 3.1.1 管理后台（管理员）

**模板管理页面**
- 模板列表：展示已上传模板（名称、上传时间、状态、操作）
- 上传模板：
  - 支持上传.docx文件
  - 自动解析{字段名}占位符
  - 自动识别表格结构（{表格名.字段名}格式）
- 字段配置：
  - 普通字段：编辑名称、类型、必填、同义词、话术
  - **表格字段**：
    - 识别表格结构：{表格名.字段名}
    - 配置表格行数限制（固定/动态）
    - 配置表格内各字段属性
  - 设置字段顺序（拖拽排序）
- 话术配置：
  - 引导话术、缺失追问、无效提示
  - **表格填充话术**："请提供{表格名}数据，格式：字段1,字段2..."
- 启用/禁用模板
- **模板下载**：管理员可下载原始模板文件

#### 3.1.2 用户界面（普通用户）

**首页**
- 展示所有启用模板（卡片形式）
- 每个模板显示：名称、描述、操作按钮
- 操作按钮：「对话填写」「导入数据」「下载模板」

**对话引导模式**
- 对话界面：消息列表形式，支持文本输入
- 字段收集进度条
- 实时预览已收集字段
- 支持修改已填字段
- **高级用户快捷指令**：
  - "我要填{模板名}的所有字段"→一次性列出所有字段要求
  - "下载这个模板"→提供模板下载链接
  - "直接给我模板"→返回模板下载链接
- 生成结果预览与下载

**导入数据模式**
- 文件上传：支持.xlsx / .json / .docx
- **Word文件导入**：解析已有Word中的表格数据
- 字段匹配展示：已匹配 / 未匹配 / 格式错误
- 缺失字段补全输入框
- 生成结果预览与下载

### 3.2 后端模块（FrameAgent）

#### 3.2.1 Agent设计

**主Agent：WordFillAgent**
- 职责：协调各工具，完成文档填充全流程
- 输入：用户意图、模板ID、数据
- 输出：生成文档路径或引导话术

**工具集（Tools）**

| 工具名 | 功能 | 输入 | 输出 |
|--------|------|------|------|
| TemplateParser | 解析模板文件（含容错处理） | .docx文件路径 | 字段列表（含表格结构）+ 解析报告 |
| FieldExtractor | 从对话中提取字段 | 用户消息、字段配置 | 提取的字段键值对 |
| TableProcessor | 处理表格数据 | 表格配置、数据源 | 格式化表格数据 |
| DocGenerator | 生成Word文档（含复杂元素处理） | 模板路径、字段数据 | 生成文档路径 |
| FileUploader | 处理文件上传 | 上传文件 | 存储路径 |
| FileDownloader | 提供文件下载 | 文件路径 | 下载链接 |
| DataImporter | 解析导入文件 | .xlsx/.json/.docx | 结构化数据 |
| **TemplateValidator** | **模板预检与验证** | **模板文件** | **验证报告（错误/警告列表）** |
| **ContentControlHandler** | **内容控件处理** | **控件类型、填充值** | **填充结果** |
| **PlaceholderNormalizer** | **占位符规范化** | **原始文本** | **标准化占位符** |
| **AIDataExtractor** | **AI数据提取（多引擎）** | **上传文件、模板字段** | **提取结果+置信度+人工确认** |
| **FieldMatcher** | **智能字段匹配** | **提取数据、模板字段** | **匹配映射+置信度评分** |

#### 3.2.2 API接口

**模板管理接口**
```
POST   /api/templates                 # 上传模板
GET    /api/templates                 # 获取模板列表
GET    /api/templates/:id             # 获取模板详情
PUT    /api/templates/:id             # 更新模板配置
DELETE /api/templates/:id             # 删除模板
GET    /api/templates/:id/download    # 下载模板文件
```

**用户接口**
```
GET    /api/templates/enabled         # 获取启用模板列表
POST   /api/chat                      # 对话消息（FrameAgent）
POST   /api/generate                  # 生成文档（对话/导入模式）
GET    /api/documents/:id/download    # 下载生成文档
```

---

## 四、数据格式

### 4.1 数据库表结构（SQLite）

#### 4.1.1 核心表结构

**fa_templates 表** - 模板主表
```sql
CREATE TABLE fa_templates (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    filename TEXT NOT NULL,
    description TEXT,
    status TEXT DEFAULT 'enabled', -- enabled/disabled
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

**fa_fields 表** - 字段定义表
```sql
CREATE TABLE fa_fields (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    template_id TEXT NOT NULL,
    name TEXT NOT NULL,
    type TEXT DEFAULT 'text', -- text/phone/email/date/number
    required BOOLEAN DEFAULT 0,
    field_order INTEGER DEFAULT 0,
    guide_prompt TEXT,
    missing_prompt TEXT,
    invalid_prompt TEXT,
    FOREIGN KEY (template_id) REFERENCES fa_templates(id) ON DELETE CASCADE
);
```

**fa_field_synonyms 表** - 字段同义词表
```sql
CREATE TABLE fa_field_synonyms (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    field_id INTEGER NOT NULL,
    synonym TEXT NOT NULL,
    FOREIGN KEY (field_id) REFERENCES fa_fields(id) ON DELETE CASCADE
);
```

**fa_tables 表** - 表格定义表
```sql
CREATE TABLE fa_tables (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    template_id TEXT NOT NULL,
    name TEXT NOT NULL,
    row_type TEXT DEFAULT 'dynamic', -- fixed/dynamic
    max_rows INTEGER DEFAULT 10,
    guide_prompt TEXT,
    missing_prompt TEXT,
    invalid_prompt TEXT,
    FOREIGN KEY (template_id) REFERENCES fa_templates(id) ON DELETE CASCADE
);
```

**fa_table_columns 表** - 表格列定义表
```sql
CREATE TABLE fa_table_columns (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    table_id INTEGER NOT NULL,
    name TEXT NOT NULL,
    type TEXT DEFAULT 'text',
    required BOOLEAN DEFAULT 0,
    column_order INTEGER DEFAULT 0,
    FOREIGN KEY (table_id) REFERENCES fa_tables(id) ON DELETE CASCADE
);
```

**fa_chat_sessions 表** - 对话会话表
```sql
CREATE TABLE fa_chat_sessions (
    id TEXT PRIMARY KEY,
    template_id TEXT NOT NULL,
    mode TEXT DEFAULT 'dialog', -- dialog/import/ai
    status TEXT DEFAULT 'active', -- active/completed/expired
    collected_data TEXT, -- JSON格式存储已收集字段
    current_field_index INTEGER DEFAULT 0,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (template_id) REFERENCES fa_templates(id)
);
```

**fa_ai_extractions 表** - AI提取记录表
```sql
CREATE TABLE fa_ai_extractions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id TEXT NOT NULL,
    file_path TEXT NOT NULL,
    engine TEXT, -- copilot/azure/openai
    extraction_result TEXT, -- JSON格式原始结果
    match_result TEXT, -- JSON格式匹配结果
    confidence_avg REAL,
    status TEXT DEFAULT 'pending', -- pending/confirmed/rejected
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (session_id) REFERENCES fa_chat_sessions(id) ON DELETE CASCADE
);
```

**fa_generated_documents 表** - 生成文档记录表
```sql
CREATE TABLE fa_generated_documents (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id TEXT NOT NULL,
    template_id TEXT NOT NULL,
    output_path TEXT NOT NULL,
    fill_data TEXT, -- JSON格式填充数据
    status TEXT DEFAULT 'success', -- success/failed
    error_message TEXT,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (session_id) REFERENCES fa_chat_sessions(id),
    FOREIGN KEY (template_id) REFERENCES fa_templates(id)
);
```

#### 4.1.2 数据关系图

```
fa_templates (1)
    ├── fa_fields (N)
    │       └── fa_field_synonyms (N)
    ├── fa_tables (N)
    │       └── fa_table_columns (N)
    └── fa_chat_sessions (N)
            ├── fa_ai_extractions (N)
            └── fa_generated_documents (N)
```

#### 4.1.3 查询示例

**获取完整模板配置（含字段、表格）**：
```sql
SELECT 
    t.*,
    json_group_array(
        DISTINCT json_object(
            'id', f.id,
            'name', f.name,
            'type', f.type,
            'required', f.required,
            'order', f.field_order,
            'synonyms', (
                SELECT json_group_array(s.synonym) 
                FROM fa_field_synonyms s 
                WHERE s.field_id = f.id
            ),
            'prompts', json_object(
                'guide', f.guide_prompt,
                'missing', f.missing_prompt,
                'invalid', f.invalid_prompt
            )
        )
    ) as fields,
    json_group_array(
        DISTINCT json_object(
            'id', tbl.id,
            'name', tbl.name,
            'rowType', tbl.row_type,
            'maxRows', tbl.max_rows,
            'columns', (
                SELECT json_group_array(
                    json_object(
                        'name', tc.name,
                        'type', tc.type,
                        'required', tc.required
                    )
                )
                FROM fa_table_columns tc
                WHERE tc.table_id = tbl.id
            )
        )
    ) as tables
FROM fa_templates t
LEFT JOIN fa_fields f ON f.template_id = t.id
LEFT JOIN fa_tables tbl ON tbl.template_id = t.id
WHERE t.id = 'template_001'
GROUP BY t.id;
```

### 4.2 生成请求格式

**普通字段填充**
```json
{
  "templateId": "template_001",
  "mode": "dialog",
  "fields": [
    {
      "name": "项目名称",
      "value": "智能办公系统"
    },
    {
      "name": "负责人电话",
      "value": "13800138000"
    }
  ],
  "tables": [
    {
      "name": "成员列表",
      "rows": [
        {"姓名": "张三", "职务": "项目经理", "联系方式": "13800138001"},
        {"姓名": "李四", "职务": "技术负责人", "联系方式": "13800138002"}
      ]
    }
  ]
}
```

### 4.3 模板占位符格式

**普通字段**
```
{字段名}
例如：{项目名称}、{负责人电话}
```

**表格字段**
```
{表格名.字段名}
例如：{成员列表.姓名}、{成员列表.职务}
```

**表格填充规则**
- 模板中表格第一行为表头，使用{表格名.字段名}作为占位符
- 生成时根据数据行数动态扩展表格行数
- 保留表格原有样式（边框、背景色、字体）

### 4.4 Word文档解析复杂性处理方案

#### 4.4.1 占位符规范化策略

**支持的占位符格式**：
```
标准格式: {字段名}
表格格式: {表格名.字段名}
全角支持: 【字段名】、［字段名］（自动转换为半角）
容错格式: { 字段名 }、{字段名 }（自动去除空格）
```

**占位符规范化流程**：
1. **预处理**：去除多余空格、转换全角符号为半角
2. **验证**：检查占位符命名规范（只允许中文、字母、数字、下划线）
3. **冲突检测**：检测重复定义的占位符
4. **报告生成**：输出解析报告（成功/警告/错误）

#### 4.4.2 模板预检机制

**上传时自动检测**：
| 检测项 | 处理方式 | 级别 |
|--------|----------|------|
| 占位符格式错误 | 提示具体位置和错误原因 | 错误 |
| 占位符命名不规范 | 建议修改，但允许继续 | 警告 |
| 表格结构不完整 | 提示缺失的表头字段 | 错误 |
| 嵌套占位符 | 不支持，提示简化 | 错误 |
| 跨段落占位符 | 尝试合并，失败则报错 | 警告 |
| 特殊字符 | 自动转义或提示修改 | 警告 |

**预检报告示例**：
```json
{
  "status": "warning",
  "summary": "发现2个警告",
  "fields": [
    {"name": "项目名称", "type": "text", "status": "ok"},
    {"name": "负责人电话", "type": "text", "status": "ok"}
  ],
  "tables": [
    {
      "name": "成员列表",
      "columns": ["姓名", "职务"],
      "status": "ok"
    }
  ],
  "warnings": [
    {
      "type": "format",
      "message": "发现全角占位符【项目名称】，已自动转换为{项目名称}",
      "location": "第3段"
    },
    {
      "type": "naming",
      "message": "占位符{项目 名称}包含空格，建议改为{项目名称}",
      "location": "第5段"
    }
  ],
  "errors": []
}
```

#### 4.4.3 复杂元素处理策略

**内容控件（Content Controls）支持**：
| 控件类型 | 支持情况 | 处理方式 |
|----------|----------|----------|
| 纯文本内容控件 | ✅ 完全支持 | 直接填充文本 |
| 富文本内容控件 | ✅ 完全支持 | 保留格式填充 |
| 下拉列表控件 | ✅ 支持 | 验证选项合法性后填充 |
| 复选框控件 | ✅ 支持 | 布尔值转换填充 |
| 日期选择器 | ✅ 支持 | 格式化日期后填充 |
| 图片内容控件 | ✅ 支持 | 替换图片数据 |

**处理优先级**：
1. 优先匹配内容控件（Tag属性匹配字段名）
2. 其次匹配占位符文本 `{字段名}`
3. 最后匹配旧版表单域

#### 4.4.4 容错与降级策略

| 异常情况 | 处理策略 |
|----------|----------|
| 占位符解析失败 | 记录错误，跳过该字段，继续处理其他字段 |
| 表格行数超过限制 | 截断并提示用户，或分页生成 |
| 样式继承失败 | 使用默认样式，记录警告 |
| 字体缺失 | 自动替换为系统默认中文字体（微软雅黑/宋体）|
| 图片替换失败 | 保留原图片，记录错误 |

#### 4.4.5 中文兼容性保障

| 问题场景 | 解决方案 |
|----------|----------|
| 字体显示异常 | 模板预设中文字体，生成时检测并替换 |
| 东亚换行规则 | 启用OpenXML的兼容性设置 |
| 字符编码 | 统一使用UTF-8编码处理 |
| 表格列宽变形 | 使用固定列宽，避免自动调整 |

### 4.5 AI数据提取准确性保障方案

#### 4.5.1 多引擎策略（主备方案）

**主引擎：GitHub Copilot SDK**
- 优先使用官方SDK进行数据提取
- 支持文件类型：PDF、Word、Excel、图片、文本

**备用引擎：Azure OpenAI / OpenAI API**
- 当Copilot SDK不可用时自动切换
- 使用GPT-4o模型，支持多模态（文本+图片）

**降级策略**：
```
Copilot SDK → Azure OpenAI → OpenAI API → 本地规则提取 → 失败提示
```

#### 4.5.2 数据提取流程（含人工确认）

```
┌─────────────────┐
│  用户上传文件   │
└────────┬────────┘
         ▼
┌─────────────────┐
│  文件预处理     │
│  - 格式验证     │
│  - 大小检查     │
│  - 内容提取     │
└────────┬────────┘
         ▼
┌─────────────────┐
│  AI数据提取     │
│  - 多引擎尝试   │
│  - 结构化输出   │
└────────┬────────┘
         ▼
┌─────────────────┐
│  字段智能匹配   │
│  - 精确匹配     │
│  - 语义匹配     │
│  - 置信度评分   │
└────────┬────────┘
         ▼
┌─────────────────┐
│  人工确认界面   │◀──── 低置信度字段需人工确认
│  - 高亮显示     │
│  - 一键修正     │
│  - 批量确认     │
└────────┬────────┘
         ▼
┌─────────────────┐
│  生成Word文档   │
└─────────────────┘
```

#### 4.5.3 置信度评分机制

| 置信度等级 | 分数范围 | 处理方式 |
|------------|----------|----------|
| 高置信度 | 90-100 | 自动填充，无需确认 |
| 中置信度 | 70-89 | 自动填充，但标记提示 |
| 低置信度 | 50-69 | 需人工确认后填充 |
| 无法识别 | <50 | 标记为未匹配，需人工输入 |

**评分维度**：
1. **字段名匹配度**：提取字段与模板字段名称相似度
2. **数据完整性**：该字段在源文件中的信息完整程度
3. **格式正确性**：提取数据是否符合字段类型要求
4. **上下文一致性**：与周围数据的逻辑一致性

#### 4.5.4 Prompt工程优化

**分层Prompt策略**：

**第一层：文件类型识别**
```
请识别上传文件的类型和内容结构。
文件类型：[自动检测]
请描述文件的主要章节和数据组织方式。
```

**第二层：字段提取**
```
请从文件中提取以下字段的信息：
字段列表：
- 项目名称（字符串）
- 负责人电话（11位数字）
- 成员列表（数组，包含姓名、职务）

要求：
1. 以JSON格式返回
2. 每个字段附带confidence评分(0-100)
3. 如果找不到对应信息，返回null并说明原因
4. 对于表格数据，保留原始结构
```

**第三层：结果校验**
```
请校验以下提取结果的合理性：
[提取结果JSON]

检查项：
1. 数据格式是否符合字段类型要求
2. 数值范围是否在合理区间
3. 日期格式是否标准
4. 标记可疑或不合理的数据
```

#### 4.5.5 字段匹配算法

**多级匹配策略**：

1. **精确匹配**（权重40%）
   - 字段名完全相等
   - 同义词匹配（使用预定义同义词库）

2. **模糊匹配**（权重30%）
   - 编辑距离算法（Levenshtein Distance）
   - 阈值：相似度≥0.8视为匹配

3. **语义匹配**（权重30%）
   - 使用Embedding模型计算语义相似度
   - 适用于同义不同字的字段（如"负责人"vs"经办人"）

**匹配结果示例**：
```json
{
  "matches": [
    {
      "templateField": "项目名称",
      "extractedField": "项目全称",
      "value": "智能办公系统建设",
      "confidence": 95,
      "matchType": "semantic",
      "needConfirm": false
    },
    {
      "templateField": "负责人电话",
      "extractedField": "联系电话",
      "value": "13800138000",
      "confidence": 88,
      "matchType": "fuzzy",
      "needConfirm": false
    },
    {
      "templateField": "成员列表",
      "extractedField": null,
      "value": null,
      "confidence": 0,
      "matchType": "none",
      "needConfirm": true,
      "suggestion": "未在文件中找到成员信息，请手动输入"
    }
  ]
}
```

#### 4.5.6 人工确认界面设计

**确认界面要素**：

| 元素 | 说明 |
|------|------|
| 源文件预览 | 右侧显示原始文件，支持高亮定位 |
| 字段匹配表 | 左侧显示匹配结果，按置信度排序 |
| 置信度标识 | 颜色区分：绿色(高)、黄色(中)、红色(低) |
| 快速编辑 | 点击字段可直接修改值 |
| 批量操作 | 支持"全部接受"/"全部拒绝" |
| 重新提取 | 可更换AI引擎重新提取 |

**交互流程**：
1. 系统展示提取结果和置信度
2. 用户检查并修正低置信度字段
3. 确认无误后点击"生成文档"
4. 系统记录用户修正，用于后续优化

#### 4.5.7 错误处理与降级

| 异常情况 | 处理策略 |
|----------|----------|
| AI服务不可用 | 提示用户，提供手动输入入口 |
| 文件解析失败 | 提示具体错误，建议转换格式后重试 |
| 提取超时(>30s) | 中断并提示，建议分批上传或手动输入 |
| JSON解析失败 | 使用正则表达式兜底提取关键信息 |
| 全部字段低置信度 | 切换到"手动输入模式"，AI结果作为参考 |

#### 4.5.8 持续优化机制

**反馈收集**：
- 记录用户修正行为
- 统计各字段类型的提取准确率
- 收集用户满意度评分

**模型优化**：
- 定期更新Prompt模板
- 根据反馈调整置信度阈值
- 积累领域特定词汇表

---

## 五、用户体验细节

### 5.1 对话模式体验优化

#### 5.1.1 渐进式引导（默认模式）
- 每次只问1-2个字段
- 必填字段优先，选填字段最后
- 表格数据在普通字段收集完成后统一询问

#### 5.1.2 高级用户快捷指令

| 用户输入 | 系统响应 |
|----------|----------|
| "我要填项目申报表的所有字段" | 一次性列出所有字段（含表格）要求，格式：字段1=?,字段2=?... |
| "给我项目申报表的模板" | 提供模板下载链接 |
| "下载这个模板" | 提供当前选中模板的下载链接 |
| "直接开始填" | 跳过欢迎语，直接进入字段收集 |
| "我要导入数据" | 切换到导入模式 |

#### 5.1.3 智能修改
- "改一下项目名称"→识别意图→只修改该字段
- "修改第3个字段"→按顺序定位修改
- "成员列表加一行"→表格数据追加

#### 5.1.4 输入辅助
- 日期字段：显示格式提示（YYYY-MM-DD）
- 手机号：自动校验11位数字
- 表格数据：提供格式示例"姓名,职务,联系方式"
- 输入时实时校验，即时反馈

#### 5.1.5 断点保护
- 刷新页面后自动恢复当前对话进度
- 使用IndexedDB本地存储

### 5.2 导入模式体验优化

#### 5.2.1 支持文件类型
- **Excel(.xlsx)**：自动识别Sheet、表头行
- **JSON**：标准对象数组格式
- **Word(.docx)**：解析已有Word中的表格数据

#### 5.2.2 智能匹配
- 自动识别文件类型并解析
- 自动匹配字段（名称匹配 + 同义词映射）
- 表格数据自动识别表格结构

#### 5.2.3 可视化匹配
- 表格形式展示匹配结果
- 未匹配字段高亮提示
- 支持手动拖拽调整对应关系

#### 5.2.4 单条预览
- 生成前可预览单条数据填充效果
- 确认后再生成全部

### 5.3 模板下载功能

#### 5.3.1 下载入口
- 首页模板卡片上的「下载模板」按钮
- 对话中用户说"下载模板"时的快捷响应
- 管理后台的模板下载功能

#### 5.3.2 下载内容
- 原始模板文件（.docx）
- 附带字段说明文档（可选）

---

## 六、文件存储结构

```
/project-root
├── /frontend                      # 前端代码
│   ├── /admin                    # 管理后台
│   └── /user                     # 用户界面
├── /backend                       # 后端代码（FrameAgent）
│   ├── /agents                   # Agent定义
│   ├── /tools                    # 工具实现
│   ├── /services                 # 业务服务
│   └── /utils                    # 工具函数
├── /storage                       # 本地文件存储
│   ├── /templates               # 模板文件
│   │   ├── template_001.docx
│   │   └── ...
│   ├── /output                  # 生成文档
│   │   ├── output_20250514_001.docx
│   │   └── ...
│   ├── /config                  # 配置JSON
│   │   ├── templates.json       # 模板配置
│   │   └── fields.json          # 字段配置
│   └── /uploads                 # 用户上传临时文件
│       ├── import_001.xlsx
│       └── ...
└── /docs                          # 文档
```

---

## 七、MVP验收标准

### 7.1 功能验收

| 功能 | 验收标准 |
|------|----------|
| 模板上传 | 上传.docx后自动解析{字段}和{表格.字段}占位符 |
| 字段配置 | 可配置普通字段和表格字段的类型、必填、同义词、话术 |
| 表格填充 | 支持动态行数表格的数据填充，保留表格样式 |
| 对话模式 | 完成多轮对话采集（含表格数据）并生成文档 |
| 快捷指令 | 支持"我要填所有字段""下载模板"等高级指令 |
| 导入模式 | 支持Excel/JSON/Word导入并生成文档 |
| 模板下载 | 用户可在首页或对话中下载原始模板 |
| 文档生成 | 生成文档格式与原模板一致，占位符正确替换 |

### 7.2 性能验收

- 单文档生成 ≤ 10秒
- 页面加载 ≤ 3秒
- 文件上传 ≤ 5秒（<1MB文件）
- 对话响应 ≤ 2秒

### 7.3 兼容性验收

- Chrome/Firefox/Edge 最新版本正常访问
- 支持.docx格式模板和生成
- 支持.xlsx/.json/.docx格式导入

---

## 八、后续迭代方向（非MVP）

1. 批量处理（>5条）
2. 运营统计面板
3. 更多数据源支持（API/数据库）
4. 复杂权限管理
5. 文档在线编辑
6. 云端存储同步

---

**本文档为MVP阶段开发依据，聚焦核心功能快速落地。**


