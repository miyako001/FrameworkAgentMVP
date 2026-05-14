<template>
  <div class="test-generate">
    <el-page-header title="返回" @back="goBack" content="文档生成测试" />

    <el-card style="margin-top: 20px">
      <template #header>
        <span>选择模板</span>
      </template>

      <el-select v-model="selectedTemplateId" placeholder="请选择模板" @change="loadTemplate">
        <el-option
          v-for="template in templates"
          :key="template.id"
          :label="template.name"
          :value="template.id"
        />
      </el-select>
    </el-card>

    <el-card v-if="currentTemplate" style="margin-top: 20px">
      <template #header>
        <span>填写字段</span>
      </template>

      <el-form :model="formData" label-width="150px">
        <el-form-item
          v-for="field in currentTemplate.fields"
          :key="field.id"
          :label="field.name"
          :required="field.required"
        >
          <el-input
            v-model="formData.fields[field.name]"
            :placeholder="field.guidePrompt || `请输入${field.name}`"
            :type="getInputType(field.fieldType)"
          />
          <div class="field-hint">类型: {{ field.fieldType }}</div>
        </el-form-item>
      </el-form>
    </el-card>

    <el-card v-if="currentTemplate && currentTemplate.tables.length > 0" style="margin-top: 20px">
      <template #header>
        <span>填写表格数据</span>
      </template>

      <div v-for="table in currentTemplate.tables" :key="table.id" style="margin-bottom: 30px">
        <h4>{{ table.name }}</h4>
        
        <el-button type="primary" size="small" @click="addTableRow(table.name)">
          添加行
        </el-button>

        <el-table
          :data="formData.tables[table.name] || []"
          style="width: 100%; margin-top: 10px"
          border
        >
          <el-table-column
            v-for="column in table.columns"
            :key="column.id"
            :label="column.name"
          >
            <template #default="scope">
              <el-input
                v-model="scope.row[column.name]"
                :placeholder="`请输入${column.name}`"
                size="small"
              />
            </template>
          </el-table-column>
          <el-table-column label="操作" width="100">
            <template #default="scope">
              <el-button
                type="danger"
                size="small"
                @click="deleteTableRow(table.name, scope.$index)"
              >
                删除
              </el-button>
            </template>
          </el-table-column>
        </el-table>
      </div>
    </el-card>

    <div style="margin-top: 20px; text-align: center">
      <el-button type="primary" size="large" @click="generateDocument" :loading="generating">
        生成文档
      </el-button>
    </div>

    <!-- 生成结果对话框 -->
    <el-dialog v-model="showResultDialog" title="生成结果" width="500px">
      <div v-if="generateResult.success">
        <el-result icon="success" title="生成成功">
          <template #extra>
            <el-button type="primary" @click="downloadDocument">下载文档</el-button>
          </template>
        </el-result>
      </div>
      <div v-else>
        <el-result icon="error" :title="generateResult.error">
          <template #sub-title>
            <ul v-if="generateResult.validationErrors">
              <li v-for="(err, index) in generateResult.validationErrors" :key="index">
                {{ err }}
              </li>
            </ul>
          </template>
        </el-result>
      </div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import axios from 'axios'

const router = useRouter()
const templates = ref<any[]>([])
const selectedTemplateId = ref('')
const currentTemplate = ref<any>(null)
const generating = ref(false)
const showResultDialog = ref(false)

const formData = reactive<{
  fields: Record<string, string>
  tables: Record<string, any[]>
}>({
  fields: {},
  tables: {}
})

const generateResult = reactive({
  success: false,
  error: '',
  validationErrors: [] as string[],
  downloadUrl: ''
})

const loadTemplates = async () => {
  try {
    const response = await axios.get('/api/templates')
    templates.value = response.data.data.filter((t: any) => t.status === 'enabled')
  } catch (error) {
    ElMessage.error('加载模板列表失败')
  }
}

const loadTemplate = async () => {
  if (!selectedTemplateId.value) return

  try {
    const response = await axios.get(`/api/templates/${selectedTemplateId.value}`)
    currentTemplate.value = response.data.data

    // 初始化表单数据
    formData.fields = {}
    formData.tables = {}

    currentTemplate.value.fields.forEach((field: any) => {
      formData.fields[field.name] = ''
    })

    currentTemplate.value.tables.forEach((table: any) => {
      formData.tables[table.name] = []
    })
  } catch (error) {
    ElMessage.error('加载模板详情失败')
  }
}

const addTableRow = (tableName: string) => {
  if (!formData.tables[tableName]) {
    formData.tables[tableName] = []
  }

  const table = currentTemplate.value.tables.find((t: any) => t.name === tableName)
  const newRow: Record<string, string> = {}
  table.columns.forEach((col: any) => {
    newRow[col.name] = ''
  })

  formData.tables[tableName].push(newRow)
}

const deleteTableRow = (tableName: string, rowIndex: number) => {
  formData.tables[tableName].splice(rowIndex, 1)
}

const generateDocument = async () => {
  if (!selectedTemplateId.value) {
    ElMessage.warning('请选择模板')
    return
  }

  generating.value = true

  try {
    // 构建请求数据
    const request = {
      templateId: selectedTemplateId.value,
      fields: Object.keys(formData.fields).map((key) => ({
        name: key,
        value: formData.fields[key]
      })),
      tables: Object.keys(formData.tables).map((key) => ({
        name: key,
        rows: formData.tables[key]
      }))
    }

    const response = await axios.post('/api/generate', request)

    if (response.data.success) {
      generateResult.success = true
      generateResult.downloadUrl = response.data.downloadUrl
      showResultDialog.value = true
    }
  } catch (error: any) {
    generateResult.success = false
    generateResult.error = error.response?.data?.error || '生成失败'
    generateResult.validationErrors = error.response?.data?.validationErrors || []
    showResultDialog.value = true
  } finally {
    generating.value = false
  }
}

const downloadDocument = async () => {
  try {
    const response = await axios.get(generateResult.downloadUrl, {
      responseType: 'blob'
    })

    const url = window.URL.createObjectURL(new Blob([response.data]))
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', generateResult.downloadUrl.split('/').pop() || 'document.docx')
    document.body.appendChild(link)
    link.click()
    link.remove()

    ElMessage.success('下载成功')
    showResultDialog.value = false
  } catch (error) {
    ElMessage.error('下载失败')
  }
}

const getInputType = (fieldType: string) => {
  switch (fieldType) {
    case 'email':
      return 'email'
    case 'number':
      return 'number'
    case 'date':
      return 'date'
    default:
      return 'text'
  }
}

const goBack = () => {
  router.push('/')
}

onMounted(() => {
  loadTemplates()
})
</script>

<style scoped>
.test-generate {
  padding: 20px;
}

.field-hint {
  font-size: 12px;
  color: #999;
  margin-top: 5px;
}

h4 {
  margin: 15px 0 10px 0;
}
</style>
