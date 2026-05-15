# W9 实现流程 - 模板上传 AI 双轨解析比对

**周期**：第 9 周  
**里程碑**：M9 - 模板解析稳定性增强  
**目标**：在保持上传主链路稳定的前提下，落地“本地规则 + AI 语义”双轨解析与差异告警

**⚠️ 核心功能说明**：
- 本周聚焦模板上传解析质量，不改动文档生成主流程
- 本地解析结果仍作为唯一入库依据，AI 仅用于比对和提示
- 上传接口支持 aiVerify 开关，默认关闭以保持兼容
- 扩展占位符语法和点号字段识别，解决复杂模板漏识别问题

---

## 📋 实施步骤总览

```
步骤1: 冻结范围与兼容目标
    ↓
步骤2: 升级本地解析规则（语法兼容 + 点号层级）
    ↓
步骤3: 接入 AI 双轨解析与差异比较
    ↓
步骤4: 升级上传接口契约（aiVerify + aiVerification）
    ↓
步骤5: 完成前端开关与 warning 展示
    ↓
步骤6: 执行回归与降级验证
    ↓
步骤7: 文档归档与发布说明
```

---

## 步骤 1：冻结范围与兼容目标

### 1.1 范围冻结

本周仅处理以下改造：

1. 模板上传解析链路（/api/templates）
2. 解析规则增强（占位符与点号字段）
3. AI 差异比对与 warnings 合并
4. 前端上传页参数透传和提示展示

不在本周范围：

1. AI 结果直接覆盖数据库字段
2. 模板自动修复回写文件
3. 新增复杂表达式语法

### 1.2 兼容目标

必须兼容：

1. `aiVerify=false` 与历史行为一致
2. 占位符语法：`{}`、`{{}}`、`[]`、`()`
3. 点号字段：`{A.B}`、`{A.B.C}`、`{Acronym.WordAcronym}`
4. AI 异常时上传不失败，仅追加降级 warning

---

## 步骤 2：升级本地解析规则

### 2.1 解析器增强

目标文件：

1. `backend/FrameAgentWordFill/Tools/TemplateParser.cs`

改造点：

1. 占位符匹配支持多包裹语法
2. 解析后统一规范化为内部标准 token
3. 点号字段从“仅两段”放宽为“至少两段”
4. 无法归类 token 写入 warnings，避免静默忽略

### 2.2 规范化策略

建议新增工具：

1. `backend/FrameAgentWordFill/Tools/TemplatePlaceholderNormalizer.cs`

职责：

1. 识别原始语法类型
2. 输出规范化字段名
3. 返回是否规范化及提示文案

---

## 步骤 3：接入 AI 双轨解析与差异比较

### 3.1 服务编排

目标文件：

1. `backend/FrameAgentWordFill/Services/TemplateService.cs`

编排流程：

1. 执行本地解析，得到 localResult
2. 当 aiVerify=true 时执行 AI 解析，得到 aiResult
3. 执行差异比较，得到 diffWarnings
4. 将 diffWarnings 合并进 localResult.Warnings
5. 继续以 localResult 入库保存

### 3.2 新增工具建议

1. `backend/FrameAgentWordFill/Tools/TemplateAiParser.cs`
2. `backend/FrameAgentWordFill/Tools/TemplateParseComparer.cs`

职责：

1. TemplateAiParser：模板文本抽取、提示词构建、JSON 反序列化、容错降级
2. TemplateParseComparer：字段/表格差异计算、warning 生成

---

## 步骤 4：升级上传接口契约

### 4.1 控制器改造

目标文件：

1. `backend/FrameAgentWordFill/Controllers/TemplatesController.cs`

改造内容：

1. 上传接口新增参数 `aiVerify`（默认 false）
2. 响应体增加 `aiVerification` 摘要

### 4.2 响应结构建议

在原 `parseResult` 基础上追加：

1. enabled
2. aiAvailable
3. fieldDiffCount
4. tableDiffCount
5. comparisonLevel

---

## 步骤 5：前端开关与 warning 展示

### 5.1 上传页改造

目标文件：

1. `frontend/src/views/TemplateUpload.vue`

改造内容：

1. 增加“启用 AI 比对”开关（默认关闭）
2. FormData 透传 aiVerify
3. 展示 parseResult.warnings 与 aiVerification 摘要

### 5.2 交互要求

1. AI 不可用时明确提示“已降级为本地解析”
2. 规范化提示可读性优先，避免技术术语堆叠

---

## 步骤 6：回归与降级验证

### 6.1 功能回归用例

1. 标准模板：`{项目名称}` + `{成员表.姓名}`
2. 语法混用：`{{项目名称}}` + `[负责人]` + `(联系电话)`
3. 点号字段：`{Acronym.WordAcronym}`
4. 多段点号：`{A.B.C}`

### 6.2 异常降级用例

1. AI 服务不可用
2. AI 返回非 JSON
3. AI 调用超时

预期：

1. 接口仍成功返回
2. parseResult.warnings 含降级提示
3. 模板成功入库

---

## 步骤 7：文档归档与发布说明

### 7.1 需同步文档

1. `docs/05_api_接口规范.md`
2. `docs/06_issue_w8_模板上传AI双轨解析比对.md`
3. `docs/02_ref_w9_模板上传AI双轨解析比对_方法论.md`
4. `docs/11_module_功能问题排查手册.md`

### 7.2 发布说明建议

1. 新增 aiVerify 参数（默认关闭）
2. 增强占位符兼容与点号字段解析
3. AI 比对仅提示，不影响上传成功

---

## 验收标准

1. `aiVerify=false` 时行为与历史一致
2. `aiVerify=true` 时可返回差异 warnings
3. AI 异常时上传不失败且有降级警告
4. `{}`、`{{}}`、`[]`、`()` 四种写法可识别
5. `{Acronym.WordAcronym}` 与 `{A.B.C}` 可进入有效候选

---

## 风险与应对

1. AI 输出波动：严格 JSON 解析 + 容错降级
2. 接口时延增加：默认关闭 aiVerify，按需开启
3. 误识别增加：增加黑名单和最小长度约束
4. 前后端版本不一致：通过响应字段向后兼容

---

## 工时预估

1. 后端解析规则增强：1.0~1.5 人日
2. AI 比对编排与容错：1.0~1.5 人日
3. 前端开关与展示：0.5~1.0 人日
4. 联调与回归：0.5~1.0 人日

预计总量：3~5 人日。
