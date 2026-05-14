# Issue: 删除模板触发 SQLite 外键约束失败（W8）

创建日期：2026-05-14  
最后更新：2026-05-14  
优先级：P1  
状态：已修复（待回归验证）

---

## 1. 问题描述

用户调用删除模板接口时出现异常：

- 接口：`DELETE /api/templates/{id}`
- 典型报错：`SQLite Error 19: 'FOREIGN KEY constraint failed'`

示例路径：
- `api/templates/7de9e91b-ddef-48ad-b419-ee01e41ae355`

---

## 2. 根因分析

### 2.1 触发链路

`TemplatesController.DeleteTemplate` -> `TemplateService.DeleteTemplateAsync` -> `TemplateRepository.DeleteTemplateAsync`

`TemplateRepository` 之前直接执行：

```sql
DELETE FROM fa_templates WHERE id = @id
```

### 2.2 根因

`fa_chat_sessions.template_id` 引用 `fa_templates.id`，但历史库结构中该外键未配置 `ON DELETE CASCADE`，导致模板仍被会话数据引用时无法删除。

---

## 3. 影响范围

- 删除模板功能失败（管理端）
- 前端提示“删除失败”
- 与该模板关联的会话数据长期保留，无法随模板生命周期清理

---

## 4. 修复方案

### 4.1 代码级修复（兼容历史数据库）

在 `TemplateRepository.DeleteTemplateAsync` 中增加事务化删除顺序：

1. 删除 `fa_session_fields`（通过会话子查询）
2. 删除 `fa_chat_sessions`
3. 删除 `fa_templates`

这样即便历史库未配置级联删除，也能稳定删除模板。

### 4.2 结构级修复（面向新库）

将 `fa_chat_sessions` 外键更新为：

```sql
FOREIGN KEY (template_id) REFERENCES fa_templates(id) ON DELETE CASCADE
```

已同步更新：
- `backend/FrameAgentWordFill/Data/DatabaseSchema.sql`
- `backend/FrameAgentWordFill/Data/DatabaseInitializer.cs`

---

## 5. 变更清单

- `backend/FrameAgentWordFill/Repositories/TemplateRepository.cs`
- `backend/FrameAgentWordFill/Data/DatabaseSchema.sql`
- `backend/FrameAgentWordFill/Data/DatabaseInitializer.cs`

---

## 6. 验收标准

- 调用 `DELETE /api/templates/{id}` 返回成功
- 已有关联会话数据时仍可删除模板
- 删除后不再存在该模板的会话残留
- 无 `SQLite Error 19` 报错

---

## 7. 回归建议

1. 准备一个带聊天会话历史的模板
2. 调用删除模板接口
3. 检查数据库：
   - `fa_templates` 中无目标模板
   - `fa_chat_sessions` 中无目标模板关联会话
   - `fa_session_fields` 中无对应会话字段
4. 验证前端模板列表同步更新
