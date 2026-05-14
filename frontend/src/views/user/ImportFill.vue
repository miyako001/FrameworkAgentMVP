<template>
  <div class="import-fill-container">
    <el-card class="header-card">
      <h2>导入填充</h2>
      <p>上传 Excel、JSON 或 Word 文件自动填充模板</p>
    </el-card>

    <!-- 步骤指示器 -->
    <el-card style="margin-bottom: 20px;">
      <el-steps :active="currentStep - 1" finish-status="success">
        <el-step title="选择模板" />
        <el-step title="上传文件" />
        <el-step title="字段匹配" />
        <el-step title="生成文档" />
      </el-steps>
    </el-card>

    <!-- 步骤1: 选择模板 -->
    <el-card v-if="currentStep === 1">
      <h3>步骤 1：选择模板</h3>
      <el-select v-model="selectedTemplateId" placeholder="请选择模板" style="width: 100%; margin-top: 16px;">
        <el-option
          v-for="template in templates"
          :key="template.id"
          :label="template.name"
          :value="template.id"
        />
      </el-select>
      <div class="button-group">
        <el-button type="primary" :disabled="!selectedTemplateId" @click="nextStep">下一步</el-button>
      </div>
    </el-card>

    <!-- 步骤2: 上传文件 -->
    <el-card v-if="currentStep === 2">
      <h3>步骤 2：上传文件</h3>
      <el-form label-width="120px" style="margin-top: 12px; margin-bottom: 12px;">
        <el-form-item label="提取模式">
          <el-radio-group v-model="extractionMode">
            <el-radio-button label="ai">AI智能提取（W7）</el-radio-button>
            <el-radio-button label="rule">规则提取（W5）</el-radio-button>
          </el-radio-group>
        </el-form-item>
      </el-form>
      <el-upload
        class="upload-area"
        drag
        :auto-upload="false"
        :limit="1"
        :on-change="handleFileChange"
        :on-remove="handleFileRemove"
        :file-list="fileList"
        accept=".xlsx,.xls,.json,.docx,.doc,.pdf,.txt,.csv,.md,.png,.jpg,.jpeg,.bmp,.webp"
      >
        <el-icon class="el-icon--upload"><upload-filled /></el-icon>
        <div class="el-upload__text">拖拽文件到此处，或 <em>点击上传</em></div>
        <template #tip>
          <div class="el-upload__tip">支持 Excel/JSON/Word/PDF/图片/TXT/CSV/Markdown</div>
        </template>
      </el-upload>
      <div class="button-group">
        <el-button @click="prevStep">上一步</el-button>
        <el-button type="primary" :disabled="!uploadFile" :loading="uploading" @click="uploadAndParse">
          上传并解析
        </el-button>
      </div>
    </el-card>

    <!-- 步骤3: 字段匹配 -->
    <el-card v-if="currentStep === 3">
      <h3>步骤 3：字段匹配</h3>
      <el-alert
        v-if="session"
        :title="`匹配结果：${session.matchedFieldCount} 个字段已自动匹配，${session.unmatchedFieldCount} 个字段需手动调整`"
        type="info"
        :closable="false"
        style="margin-bottom: 20px;"
      />

      <el-table :data="fieldMappings" stripe style="width: 100%">
        <el-table-column prop="sourceFieldName" label="源字段名" width="180" />
        <el-table-column label="模板字段" width="260">
          <template #default="{ row }">
            <el-select
              v-model="row.templateFieldName"
              placeholder="选择模板字段"
              clearable
              @change="handleMappingChange(row)"
            >
              <el-option
                v-for="field in templateFields"
                :key="field.name"
                :label="field.name"
                :value="field.name"
              />
            </el-select>
          </template>
        </el-table-column>
        <el-table-column prop="fieldValue" label="字段值" show-overflow-tooltip />
        <el-table-column label="置信度" width="140">
          <template #default="{ row }">
            <el-tag :type="getConfidenceType(row.matchConfidence)">{{ row.matchConfidence }}%</el-tag>
            <el-tag v-if="row.isUserConfirmed" type="success" size="small" style="margin-left: 4px;">已确认</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="匹配方式" width="120">
          <template #default="{ row }">
            <el-tag size="small">{{ getMatchMethodText(row.matchMethod) }}</el-tag>
          </template>
        </el-table-column>
      </el-table>

      <div class="button-group">
        <el-button @click="prevStep">上一步</el-button>
        <el-button type="primary" @click="nextStep">下一步</el-button>
      </div>
    </el-card>

    <!-- 步骤4: 生成文档 -->
    <el-card v-if="currentStep === 4">
      <h3>步骤 4：生成文档</h3>
      <el-result
        v-if="!generateResult"
        icon="success"
        title="准备就绪"
        sub-title="字段匹配完成，点击下方按钮生成文档"
      >
        <template #extra>
          <el-button type="primary" size="large" :loading="generating" @click="generateDocument">
            生成文档
          </el-button>
          <el-button @click="prevStep">返回调整</el-button>
        </template>
      </el-result>

      <el-result
        v-else
        icon="success"
        title="文档生成成功！"
        :sub-title="generateResult.outputFileName"
      >
        <template #extra>
          <el-button type="primary" :href="'http://localhost:5000' + generateResult.downloadUrl" tag="a" target="_blank">
            下载文档
          </el-button>
          <el-button @click="resetAll">重新开始</el-button>
        </template>
      </el-result>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { UploadFilled } from '@element-plus/icons-vue'
import type { UploadFile, UploadUserFile } from 'element-plus'
import axios from 'axios'

const API_BASE_URL = 'http://localhost:5000/api'

const currentStep = ref(1)
const selectedTemplateId = ref<string | null>(null)
const templates = ref<any[]>([])
const templateFields = ref<any[]>([])
const extractionMode = ref<'ai' | 'rule'>('ai')
const uploadFile = ref<File | null>(null)
const fileList = ref<UploadUserFile[]>([])
const uploading = ref(false)
const sessionId = ref<number | null>(null)
const session = ref<any>(null)
const fieldMappings = ref<any[]>([])
const generating = ref(false)
const generateResult = ref<any>(null)

const loadTemplates = async () => {
  try {
    const response = await axios.get(`${API_BASE_URL}/templates`)
    const payload = response.data?.data ?? response.data
    templates.value = Array.isArray(payload) ? payload : []
  } catch {
    ElMessage.error('加载模板列表失败')
  }
}

const loadTemplateFields = async () => {
  if (!selectedTemplateId.value) return
  try {
    const response = await axios.get(`${API_BASE_URL}/templates/${selectedTemplateId.value}`)
    const payload = response.data?.data ?? response.data
    templateFields.value = Array.isArray(payload?.fields) ? payload.fields : []
  } catch {
    ElMessage.error('加载模板字段失败')
  }
}

const handleFileChange = (file: UploadFile) => {
  uploadFile.value = file.raw ?? null
}

const handleFileRemove = () => {
  uploadFile.value = null
}

const uploadAndParse = async () => {
  if (!uploadFile.value || !selectedTemplateId.value) return
  uploading.value = true

  try {
    const formData = new FormData()
    formData.append('file', uploadFile.value)
    formData.append('templateId', selectedTemplateId.value)

    const uploadResponse = await axios.post(`${API_BASE_URL}/import/upload`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })
    sessionId.value = uploadResponse.data.sessionId

    await axios.post(
      `${API_BASE_URL}/import/parse/${sessionId.value}`,
      null,
      { params: { useAI: extractionMode.value === 'ai' } }
    )

    const mappingsResponse = await axios.get(`${API_BASE_URL}/import/mappings/${sessionId.value}`)
    session.value = mappingsResponse.data.session
    fieldMappings.value = mappingsResponse.data.mappings

    await loadTemplateFields()

    ElMessage.success(extractionMode.value === 'ai' ? 'AI提取完成' : '规则解析完成')
    currentStep.value = 3
  } catch (error: any) {
    ElMessage.error(error.response?.data?.error ?? '上传失败')
  } finally {
    uploading.value = false
  }
}

const handleMappingChange = async (row: any) => {
  try {
    await axios.put(`${API_BASE_URL}/import/mappings/${row.mappingId}`, {
      templateFieldName: row.templateFieldName
    })
    row.isUserConfirmed = true
    ElMessage.success('字段映射已更新')
  } catch {
    ElMessage.error('更新失败')
  }
}

const generateDocument = async () => {
  if (!sessionId.value) return
  generating.value = true

  try {
    const response = await axios.post(`${API_BASE_URL}/import/generate/${sessionId.value}`)
    generateResult.value = response.data
    ElMessage.success('文档生成成功')
  } catch (error: any) {
    ElMessage.error(error.response?.data?.error ?? '生成失败')
  } finally {
    generating.value = false
  }
}

const nextStep = () => { if (currentStep.value < 4) currentStep.value++ }
const prevStep = () => { if (currentStep.value > 1) currentStep.value-- }

const resetAll = () => {
  currentStep.value = 1
  selectedTemplateId.value = null
  extractionMode.value = 'ai'
  uploadFile.value = null
  fileList.value = []
  sessionId.value = null
  session.value = null
  fieldMappings.value = []
  generateResult.value = null
}

const getConfidenceType = (confidence: number) => {
  if (confidence >= 90) return 'success'
  if (confidence >= 70) return ''
  return 'danger'
}

const getMatchMethodText = (method: string): string => {
  const map: Record<string, string> = {
    Exact: '精确匹配',
    Fuzzy: '模糊匹配',
    Semantic: '语义匹配',
    NoMatch: '未匹配',
    Manual: '手动调整'
  }
  return map[method] ?? method
}

onMounted(() => {
  loadTemplates()
})
</script>

<style scoped>
.import-fill-container {
  padding: 20px;
}

.header-card {
  margin-bottom: 20px;
}

.upload-area {
  margin-top: 16px;
  width: 100%;
}

.button-group {
  margin-top: 20px;
  display: flex;
  gap: 10px;
  justify-content: flex-end;
}
</style>
