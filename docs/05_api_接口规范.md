# 接口规范（05_api）

> 项目：通用 Word 智能填充 Agent  
> 基础地址：`http://localhost:5000`  
> 版本：MVP / W8  
> 更新时间：2026-05-14

## 1. 通用约定

- 协议：HTTP/1.1
- 数据格式：`application/json`（文件上传除外）
- 文件上传：`multipart/form-data`
- 流式返回：SSE（`text/event-stream`）
- 字符编码：UTF-8

### 1.1 状态码约定

- `200 OK`：请求成功
- `400 BadRequest`：参数错误、业务校验失败
- `404 NotFound`：资源不存在
- `500 InternalServerError`：服务端异常（主要见测试接口）

### 1.2 统一错误响应模型（验收要求）

建议所有业务错误统一为如下结构：

```json
{
  "success": false,
  "code": "IMPORT_PARSE_FAILED",
  "message": "文件解析失败",
  "details": [
    "Sheet1 第3行字段格式不正确"
  ],
  "requestId": "trace-id-or-correlation-id",
  "timestamp": "2026-05-14T10:00:00Z"
}
```

字段说明：

- `code`：稳定机器码，用于前端分支处理和日志聚合。
- `message`：用户可读摘要。
- `details`：可选，错误明细列表。
- `requestId`：可选，问题追踪 ID。

### 1.3 错误码表（验收版）

| 错误码 | HTTP | 场景 | 建议处理 |
| --- | --- | --- | --- |
| `COMMON_BAD_REQUEST` | 400 | 参数缺失/格式错误 | 检查请求体和参数 |
| `COMMON_NOT_FOUND` | 404 | 资源不存在 | 检查资源 ID 是否有效 |
| `COMMON_INTERNAL_ERROR` | 500 | 未处理异常 | 记录 requestId 并重试 |
| `TEMPLATE_UPLOAD_FAILED` | 400 | 模板上传失败 | 检查文件类型和模板内容 |
| `TEMPLATE_NOT_FOUND` | 404 | 模板不存在 | 刷新模板列表后重试 |
| `TEMPLATE_FIELD_ID_MISMATCH` | 400 | 字段 ID 不匹配 | 修正 path/body 中字段 ID |
| `GENERATE_TEMPLATE_MISSING` | 400 | 生成时模板不存在 | 重新选择模板 |
| `GENERATE_VALIDATION_FAILED` | 400 | 字段校验失败 | 根据 validationErrors 修正 |
| `GENERATE_FILE_NOT_FOUND` | 404 | 下载文件不存在 | 重新生成文档 |
| `CHAT_SESSION_NOT_FOUND` | 404 | 会话不存在或过期 | 重新创建会话 |
| `CHAT_MESSAGE_INVALID` | 400 | 会话ID或消息为空 | 补充 message/sessionId |
| `IMPORT_FILE_EMPTY` | 400 | 上传文件为空 | 重新选择文件 |
| `IMPORT_TEMPLATE_ID_EMPTY` | 400 | templateId 为空 | 补充 templateId |
| `IMPORT_FILE_TYPE_UNSUPPORTED` | 400 | 文件类型不支持 | 使用受支持格式 |
| `IMPORT_PARSE_FAILED` | 400 | 导入解析失败 | 查看错误详情后重试 |
| `IMPORT_SESSION_NOT_FOUND` | 404 | 导入会话不存在 | 重新发起上传 |
| `IMPORT_MAPPING_INVALID` | 400 | 映射更新参数无效 | 校验 templateFieldName |

## 2. 健康与测试接口

### 2.1 健康检查

- 方法与路径：`GET /health`
- 描述：服务存活探测
- 成功响应示例：

```json
{
  "status": "ok",
  "timestamp": "2026-05-14T10:00:00Z"
}
```

### 2.2 LLM 连通性测试

- 方法与路径：`GET /test/llm`
- 描述：测试 AIService 的模型连通性
- 成功响应示例：

```json
{
  "success": true,
  "message": "LLM connection is healthy"
}
```

### 2.3 数据库测试

- 方法与路径：`GET /test/db`
- 描述：检查 SQLite 文件是否存在

### 2.4 数据库表结构测试

- 方法与路径：`GET /test/db/tables`
- 描述：返回所有表结构信息

## 3. 模板管理 API（/api/templates）

### 3.1 上传模板

- 方法与路径：`POST /api/templates`
- Content-Type：`multipart/form-data`
- 表单参数：
  - `file`（必填，doc/docx）
  - `name`（必填，模板名称）
  - `description`（可选）
  - `aiVerify`（可选，bool，默认 `false`；开启后执行 AI 双轨解析比对）
- 成功响应示例：

```json
{
  "success": true,
  "message": "模板上传成功",
  "templateId": "f8b3b6df-3a2f-4f1e-9d52-8ca2d64c0f1a",
  "parseResult": {
    "success": true,
    "fields": [],
    "tables": [],
    "warnings": [
      "AI 比对提示：字段‘项目名称’仅在本地解析中识别到",
      "AI 比对提示：表格‘成员清单’的列数量不一致（本地=5，AI=4）"
    ],
    "errors": []
  },
  "aiVerification": {
    "enabled": true,
    "aiAvailable": true,
    "comparisonLevel": "warning",
    "fieldDiffCount": 1,
    "tableDiffCount": 1
  }
}
```

说明：

- 当 `aiVerify=false` 时，`aiVerification` 字段可省略，行为与当前接口一致。
- 当 `aiVerify=true` 且 AI 服务不可用时，接口不应失败，建议仅追加警告：`AI 比对未执行，已降级为本地解析`。
- 差异结果统一写入 `parseResult.warnings`，前端可直接复用现有警告展示区。
- 占位符语法兼容建议：上传解析应支持 `{}`、`{{}}`、`[]`、`()`, 并统一规范化到内部标准格式。
- 点号字段兼容建议：应支持 `{Acronym.WordAcronym}`，并兼容 `{A.B.C}` 等扩展段数写法（至少 2 段）。

### 3.2 查询全部模板（管理）

- 方法与路径：`GET /api/templates`
- 响应：`{ success, data[] }`

### 3.3 查询启用模板（用户）

- 方法与路径：`GET /api/templates/enabled`
- 响应：`{ success, data[] }`

### 3.4 查询模板详情

- 方法与路径：`GET /api/templates/{id}`
- 失败：`404`（模板不存在）

### 3.5 更新模板状态

- 方法与路径：`PUT /api/templates/{id}/status`
- 请求体：

```json
{
  "status": "enabled"
}
```

### 3.6 更新字段配置

- 方法与路径：`PUT /api/templates/fields/{fieldId}`
- 请求体（示例）：

```json
{
  "id": 12,
  "templateId": "template-id",
  "name": "公司名称",
  "fieldType": "text",
  "required": true,
  "fieldOrder": 1,
  "guidePrompt": "请输入公司名称",
  "missingPrompt": null,
  "invalidPrompt": null
}
```

- 约束：请求体中的 `id` 必须与路径 `fieldId` 一致

### 3.7 删除模板

- 方法与路径：`DELETE /api/templates/{id}`

### 3.8 下载模板文件

- 方法与路径：`GET /api/templates/{id}/download`
- 成功返回：Word 文件二进制流

## 4. 文档生成 API（/api/generate）

### 4.1 生成文档

- 方法与路径：`POST /api/generate`
- 请求体：

```json
{
  "templateId": "template-id",
  "fields": [
    { "name": "公司名称", "value": "示例科技有限公司" },
    { "name": "联系人", "value": "张三" }
  ],
  "tables": [
    {
      "name": "产品清单",
      "rows": [
        { "产品": "A", "数量": "10", "单价": "100" },
        { "产品": "B", "数量": "5", "单价": "200" }
      ]
    }
  ]
}
```

- 成功响应：

```json
{
  "success": true,
  "fileName": "合同模板_20260514_101530.docx",
  "downloadUrl": "/api/generate/download/合同模板_20260514_101530.docx"
}
```

- 失败响应（示例）：

```json
{
  "error": "数据验证失败",
  "validationErrors": [
    "身份证号: 格式不正确"
  ]
}
```

### 4.2 下载生成文档

- 方法与路径：`GET /api/generate/download/{fileName}`
- 成功返回：Word 文件二进制流

## 5. 对话填充 API（/api/chat）

### 5.1 开始会话

- 方法与路径：`POST /api/chat/start`
- 请求体：

```json
{
  "templateId": "template-id"
}
```

- 成功响应：

```json
{
  "success": true,
  "sessionId": "chat-session-id",
  "message": "欢迎使用..."
}
```

### 5.2 发送消息（普通 JSON）

- 方法与路径：`POST /api/chat/message`
- 请求体：

```json
{
  "sessionId": "chat-session-id",
  "message": "我们公司叫示例科技，联系人张三"
}
```

- 成功响应字段（核心）：
  - `success`：是否成功
  - `message`：AI 回复文本
  - `extractedFields`：本轮抽取字段
  - `validationErrors`：校验错误列表（可空）
  - `isCompleted`：是否已完成
  - `progress`：进度（0~1）

### 5.3 发送消息（SSE 流式）

- 方法与路径：`POST /api/chat/message/stream`
- 请求体同 5.2
- Content-Type：`text/event-stream`
- 事件类型：
  - `message`：分片文本（`{ chunk }`）
  - `metadata`：结构化元数据
  - `error`：错误信息
  - `done`：结束信号

### 5.4 获取会话状态

- 方法与路径：`GET /api/chat/session/{sessionId}`
- 响应字段（核心）：
  - `sessionId`
  - `templateId`
  - `templateName`
  - `status`
  - `collectedFields`
  - `totalFields`
  - `progress`

## 6. 导入填充 API（/api/import）

### 6.1 上传文件并创建导入会话

- 方法与路径：`POST /api/import/upload`
- Content-Type：`multipart/form-data`
- 表单参数：
  - `templateId`（必填）
  - `file`（必填）
- 支持类型：Excel、JSON、Word、PDF、图片、TXT、CSV、Markdown
- 成功响应：

```json
{
  "sessionId": 1001,
  "message": "文件上传成功"
}
```

### 6.2 解析并匹配字段

- 方法与路径：`POST /api/import/parse/{sessionId}`
- Query 参数：`useAI`（可选，默认 `false`）
- 成功响应（示例）：

```json
{
  "message": "AI 提取与解析完成",
  "mode": "AI"
}
```

### 6.3 获取字段映射结果

- 方法与路径：`GET /api/import/mappings/{sessionId}`
- 响应包含：
  - `session`：会话状态与匹配统计
  - `mappings`：映射列表（置信度、匹配方式、是否人工确认）

### 6.4 更新字段映射

- 方法与路径：`PUT /api/import/mappings/{mappingId}`
- 请求体：

```json
{
  "templateFieldName": "公司名称"
}
```

### 6.5 根据导入结果生成文档

- 方法与路径：`POST /api/import/generate/{sessionId}`
- 成功响应：

```json
{
  "outputFileName": "xxx.docx",
  "downloadUrl": "/api/generate/download/xxx.docx",
  "message": "文档生成成功"
}
```

## 7. 数据模型摘要

### 7.1 GenerateRequest

```json
{
  "templateId": "string",
  "fields": [
    { "name": "string", "value": "string" }
  ],
  "tables": [
    {
      "name": "string",
      "rows": [
        { "列名": "值" }
      ]
    }
  ]
}
```

### 7.2 StartSessionRequest

```json
{
  "templateId": "string"
}
```

### 7.3 ChatMessageRequest

```json
{
  "sessionId": "string",
  "message": "string"
}
```

### 7.4 UpdateStatusRequest

```json
{
  "status": "enabled | disabled"
}
```

### 7.5 UpdateMappingRequest

```json
{
  "templateFieldName": "string"
}
```

## 8. 兼容与演进建议

- 建议增加 API 版本前缀（如 `/api/v1/...`）。
- 建议统一错误模型（`code/message/details/requestId`）。
- 建议在 Swagger 中补齐请求/响应示例与字段注释。
- 建议补充鉴权与访问控制后，再对外开放管理接口。

## 9. 验收测试用例（API 维度）

### 9.1 验收通过标准

- 核心接口 200 路径可稳定返回。
- 异常路径返回统一错误模型（含 `code`）。
- 对话流式接口可完整输出 `message -> metadata -> done`。
- 导入链路可在 `useAI=true/false` 两种模式下完成解析并进入生成。

### 9.2 核心用例清单

| 用例ID | 接口 | 场景 | 期望 |
| --- | --- | --- | --- |
| `API-HEALTH-001` | `GET /health` | 健康探测 | 返回 `status=ok` |
| `API-TPL-001` | `POST /api/templates` | 上传合法 docx 模板 | 返回 `success=true` 和 `templateId` |
| `API-TPL-002` | `GET /api/templates/{id}` | 查询不存在模板 | 返回 404 + `TEMPLATE_NOT_FOUND` |
| `API-GEN-001` | `POST /api/generate` | 合法数据生成文档 | 返回 `downloadUrl` |
| `API-GEN-002` | `POST /api/generate` | 缺少必填字段 | 返回 400 + `GENERATE_VALIDATION_FAILED` |
| `API-CHAT-001` | `POST /api/chat/start` | 启动会话 | 返回 `sessionId` |
| `API-CHAT-002` | `POST /api/chat/message/stream` | 流式对话 | 收到 `message/metadata/done` |
| `API-IMP-001` | `POST /api/import/upload` | 上传支持类型文件 | 返回 `sessionId` |
| `API-IMP-002` | `POST /api/import/parse/{sessionId}?useAI=false` | 规则解析 | 返回 `mode=Rule` |
| `API-IMP-003` | `POST /api/import/parse/{sessionId}?useAI=true` | AI解析 | 返回 `mode=AI` |
| `API-IMP-004` | `PUT /api/import/mappings/{mappingId}` | 手工修正映射 | 返回更新成功 |
| `API-IMP-005` | `POST /api/import/generate/{sessionId}` | 基于导入生成 | 返回 `downloadUrl` |

### 9.3 建议验收命令（PowerShell）

```powershell
# 1) 健康检查
Invoke-RestMethod -Uri "http://localhost:5000/health" -Method Get

# 2) 启动聊天会话（将 templateId 替换为实际值）
$chat = Invoke-RestMethod -Uri "http://localhost:5000/api/chat/start" -Method Post -ContentType "application/json" -Body '{"templateId":"template-id"}'
$chat

# 3) 规则模式导入解析（将 sessionId 替换为实际值）
Invoke-RestMethod -Uri "http://localhost:5000/api/import/parse/1001?useAI=false" -Method Post

# 4) AI 模式导入解析（将 sessionId 替换为实际值）
Invoke-RestMethod -Uri "http://localhost:5000/api/import/parse/1001?useAI=true" -Method Post
```
