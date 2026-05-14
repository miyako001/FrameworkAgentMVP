<template>
  <div class="template-upload-page">
    <div class="page-header">
      <div>
        <h1>上传模板</h1>
        <p class="subtitle">上传 Word 模板并自动提取字段，完成后返回模板管理页。</p>
      </div>
      <div class="header-actions">
        <el-button @click="goBack">返回模板列表</el-button>
      </div>
    </div>

    <div class="content-grid">
      <el-card class="upload-card" shadow="never">
        <template #header>
          <div class="card-title">文件上传</div>
        </template>

        <el-upload
          ref="uploadRef"
          action="/api/templates"
          :auto-upload="false"
          :on-change="handleFileChange"
          :on-exceed="handleExceed"
          :show-file-list="true"
          :limit="1"
          accept=".docx"
          drag
          style="width: 100%;"
        >
          <el-icon class="el-icon--upload"><UploadFilled /></el-icon>
          <div class="el-upload__text">
            将 <strong>.docx</strong> 文件拖拽到此处，或<em>点击选择文件</em>
          </div>
          <template #tip>
            <div class="el-upload__tip">
              仅支持 .docx（Word 2007+），每次上传一个文件
            </div>
          </template>
        </el-upload>

        <div class="cta-strip" :class="{ 'is-ready': !!selectedFile }">
          <div class="cta-text">
            <strong>下一步：</strong>
            <span v-if="selectedFile">已选文件：{{ selectedFile.name }}，可直接开始上传解析</span>
            <span v-else>请选择 .docx 模板文件后，点击“上传并解析”</span>
          </div>
          <el-button type="primary" size="large" @click="submitUpload" :loading="uploading">
            上传并解析
          </el-button>
        </div>

        <el-form :model="uploadForm" label-width="86px" style="margin-top: 12px;">
          <el-form-item label="模板名称" required>
            <el-input v-model="uploadForm.name" placeholder="例如：项目立项申请书"></el-input>
          </el-form-item>
          <el-form-item label="模板描述">
            <el-input
              v-model="uploadForm.description"
              type="textarea"
              :rows="4"
              placeholder="可选：例如适用部门、版本、用途说明"
            ></el-input>
          </el-form-item>
        </el-form>

        <div class="actions">
          <el-button @click="resetForm">清空</el-button>
        </div>
      </el-card>

      <el-card class="guide-card" shadow="never">
        <template #header>
          <div class="card-title">模板编写指南</div>
        </template>

        <el-alert type="info" :closable="false" show-icon>
          系统通过占位符自动提取字段。建议先按下面规范编写模板，再上传。
        </el-alert>

        <div class="guide-section">
          <h3>1. 普通字段格式</h3>
          <p>
            在正文中使用
            <el-tag size="small" type="primary" style="font-family: monospace; margin: 0 4px;">{字段名}</el-tag>
            ，例如 <code>{项目名称}</code>、<code>{负责人}</code>。
          </p>
        </div>

        <div class="guide-section">
          <h3>2. 表格字段格式</h3>
          <p>
            在表格列标题中使用
            <el-tag size="small" type="warning" style="font-family: monospace; margin: 0 4px;">{表格名.列名}</el-tag>
            ，例如 <code>{成员列表.姓名}</code>、<code>{成员列表.职务}</code>。
          </p>
        </div>

        <div v-if="guideExpanded" class="guide-fold-content">
          <div class="guide-section">
            <h3>3. 提取与解析机制</h3>
            <p>上传后系统会自动执行以下步骤：</p>
            <ul>
              <li>解析段落中的普通占位符</li>
              <li>解析表头中的表格占位符</li>
              <li>规范化容错格式（如全角括号、占位符空格）</li>
              <li>生成字段与表格清单，供后续 AI 对话和导入填充使用</li>
            </ul>
          </div>
        </div>

        <div class="guide-toggle-row">
          <el-button text type="primary" @click="guideExpanded = !guideExpanded">
            {{ guideExpanded ? '收起规范' : '展开完整规范' }}
          </el-button>
        </div>
      </el-card>
    </div>

    <el-card v-if="parseSummary" class="result-card" shadow="never">
      <template #header>
        <div class="card-title">解析结果</div>
      </template>
      <div class="result-row">
        <el-tag type="success">普通字段 {{ parseSummary.fieldCount }} 个</el-tag>
        <el-tag type="warning">表格 {{ parseSummary.tableCount }} 个</el-tag>
        <el-tag :type="parseSummary.warningCount > 0 ? 'danger' : 'info'">
          警告 {{ parseSummary.warningCount }} 条
        </el-tag>
      </div>
      <div class="result-actions">
        <el-button type="primary" @click="goBack">返回模板列表</el-button>
        <el-button @click="resetForm">继续上传新模板</el-button>
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import axios from 'axios'
import { ElMessage, ElMessageBox } from 'element-plus'
import { UploadFilled } from '@element-plus/icons-vue'

const router = useRouter()
const uploading = ref(false)
const uploadRef = ref()
const selectedFile = ref<File | null>(null)
const parseSummary = ref<null | { fieldCount: number; tableCount: number; warningCount: number }>(null)
const guideExpanded = ref(false)

const uploadForm = ref({
  name: '',
  description: ''
})

const goBack = () => {
  router.push('/admin/templates')
}

const resetForm = () => {
  uploadForm.value = { name: '', description: '' }
  selectedFile.value = null
  parseSummary.value = null
  uploadRef.value?.clearFiles()
}

const handleFileChange = (file: any) => {
  selectedFile.value = file.raw
  parseSummary.value = null

  if (!uploadForm.value.name) {
    uploadForm.value.name = file.name.replace(/\.docx$/i, '')
  }
}

const handleExceed = () => {
  ElMessage.warning('每次只能上传一个文件，请先移除当前文件')
}

const submitUpload = async () => {
  if (!selectedFile.value) {
    ElMessage.warning('请选择 .docx 文件')
    return
  }

  if (!uploadForm.value.name) {
    ElMessage.warning('请输入模板名称')
    return
  }

  uploading.value = true

  try {
    const formData = new FormData()
    formData.append('file', selectedFile.value)
    formData.append('name', uploadForm.value.name)

    if (uploadForm.value.description) {
      formData.append('description', uploadForm.value.description)
    }

    const response = await axios.post('/api/templates', formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    })

    if (!response.data.success) {
      ElMessage.error(response.data.message || '上传失败')
      return
    }

    const parseResult = response.data.parseResult || { fields: [], tables: [], warnings: [] }
    parseSummary.value = {
      fieldCount: parseResult.fields?.length ?? 0,
      tableCount: parseResult.tables?.length ?? 0,
      warningCount: parseResult.warnings?.length ?? 0
    }

    ElMessage.success('上传成功，模板已解析')

    if (parseResult.warnings?.length > 0) {
      ElMessageBox.alert(parseResult.warnings.join('\n'), '解析警告', { type: 'warning' })
    }
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '上传失败')
  } finally {
    uploading.value = false
  }
}
</script>

<style scoped>
.template-upload-page {
  background: linear-gradient(180deg, #f7faff 0%, #ffffff 56%);
  min-height: 100%;
  padding: 14px 16px;
  width: 100%;
  overflow-x: hidden;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 12px;
  margin-bottom: 10px;
  padding: 10px 12px;
  border: 1px solid #e7edf7;
  border-radius: 10px;
  background: linear-gradient(135deg, #ffffff 0%, #f8fbff 100%);
}

.page-header h1 {
  margin: 0;
  color: #1f2a3d;
  font-size: 22px;
}

.subtitle {
  margin-top: 4px;
  color: #6b7380;
  font-size: 12px;
}

.header-actions {
  display: flex;
  gap: 10px;
}

.content-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 14px;
  align-items: start;
}

.upload-card,
.guide-card,
.result-card {
  width: 100%;
  border-color: #e9eef6;
}

.card-title {
  font-weight: 600;
  color: #1f2a3d;
}

.guide-section {
  margin-top: 12px;
}

.guide-fold-content {
  margin-top: 2px;
}

.guide-section h3 {
  margin: 0 0 6px;
  font-size: 14px;
  color: #303133;
}

.guide-section p {
  margin: 0;
  color: #606266;
  line-height: 1.6;
}

.guide-section ul {
  margin: 6px 0 0;
  padding-left: 18px;
  color: #606266;
  line-height: 1.6;
}

.guide-toggle-row {
  margin-top: 6px;
  padding-top: 6px;
  border-top: 1px dashed #e6ebf5;
  display: flex;
  justify-content: flex-start;
}

.actions {
  margin-top: 8px;
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

.cta-strip {
  margin-top: 8px;
  padding: 9px 10px;
  border: 1px solid #d6e6ff;
  border-left: 4px solid #409eff;
  background: linear-gradient(90deg, #f1f7ff 0%, #f8fbff 100%);
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.cta-strip.is-ready {
  border-color: #c7e7d2;
  border-left-color: #36b36f;
  background: linear-gradient(90deg, #eefcf4 0%, #f8fffb 100%);
}

.cta-text {
  color: #35506f;
  font-size: 12px;
  line-height: 1.4;
}

.cta-text strong {
  color: #1f2a3d;
}

.cta-strip :deep(.el-button--primary) {
  min-width: 140px;
  box-shadow: 0 6px 18px rgba(64, 158, 255, 0.3);
}

.result-card {
  margin-top: 12px;
}

.result-row {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.result-actions {
  margin-top: 10px;
  display: flex;
  gap: 8px;
}

.el-icon--upload {
  font-size: 46px;
  color: #c0c4cc;
  margin: 8px 0 6px;
}

.el-upload__text {
  color: #606266;
  font-size: 12px;
}

.el-upload__text em {
  color: #409eff;
  font-style: normal;
}

.el-upload__tip {
  margin-top: 6px;
  color: #909399;
}

.upload-card :deep(.el-card__body),
.guide-card :deep(.el-card__body),
.result-card :deep(.el-card__body) {
  padding: 14px;
}

.upload-card :deep(.el-upload-dragger) {
  min-height: 132px;
  padding: 8px;
}

.upload-card :deep(.el-card__header),
.guide-card :deep(.el-card__header),
.result-card :deep(.el-card__header) {
  padding: 10px 14px;
  background: #fcfdff;
}

.upload-card :deep(.el-form-item) {
  margin-bottom: 10px;
}

.actions :deep(.el-button),
.result-actions :deep(.el-button) {
  min-width: 116px;
}

@media (max-width: 1366px) {
  .content-grid {
    grid-template-columns: 1.15fr 0.85fr;
  }
}

@media (max-width: 1100px) {
  .content-grid {
    grid-template-columns: 1fr;
  }

  .page-header {
    flex-direction: column;
    gap: 10px;
  }
}

@media (max-width: 768px) {
  .template-upload-page {
    padding: 12px;
  }

  .page-header {
    padding: 8px 10px;
  }

  .page-header h1 {
    font-size: 20px;
  }

  .upload-card :deep(.el-card__body),
  .guide-card :deep(.el-card__body),
  .result-card :deep(.el-card__body) {
    padding: 12px;
  }

  .actions,
  .result-actions {
    width: 100%;
    flex-direction: column;
  }

  .actions .el-button,
  .result-actions .el-button,
  .cta-strip .el-button {
    width: 100%;
  }

  .cta-strip {
    flex-direction: column;
    align-items: stretch;
  }
}
</style>
