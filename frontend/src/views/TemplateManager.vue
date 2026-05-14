<template>
  <div class="template-manager">
    <div class="page-header">
      <h1>模板管理</h1>
      <div class="header-actions">
        <el-button type="primary" @click="goToUploadPage" :icon="Plus">上传新模板</el-button>
        <el-button @click="loadTemplates" :icon="Refresh">刷新</el-button>
      </div>
    </div>

    <!-- 模板列表 -->
    <div class="template-list">
      <el-table :data="templates" style="width: 100%" v-loading="loading" stripe>
        <el-table-column prop="name" label="模板名称" width="200" />
        <el-table-column prop="originalFileName" label="文件名" width="180" />
        <el-table-column prop="description" label="描述" />
        <el-table-column prop="status" label="状态" width="100">
          <template #default="scope">
            <el-tag :type="scope.row.status === 'enabled' ? 'success' : 'info'">
              {{ scope.row.status === 'enabled' ? '启用' : '禁用' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="createdAt" label="创建时间" width="180">
          <template #default="scope">
            {{ formatDate(scope.row.createdAt) }}
          </template>
        </el-table-column>
        <el-table-column label="操作" min-width="420">
          <template #default="scope">
            <el-button size="small" type="primary" @click="goToChatFill(scope.row.id)" :disabled="scope.row.status !== 'enabled'">💬 AI对话填写</el-button>
            <el-button size="small" @click="viewTemplate(scope.row.id)">详情</el-button>
            <el-button size="small" @click="downloadTemplate(scope.row.id)">下载</el-button>
            <el-button 
              size="small" 
              :type="scope.row.status === 'enabled' ? 'warning' : 'success'"
              @click="toggleStatus(scope.row)"
            >
              {{ scope.row.status === 'enabled' ? '禁用' : '启用' }}
            </el-button>
            <el-button size="small" type="danger" @click="deleteTemplate(scope.row.id)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- 模板详情对话框 -->
    <el-dialog v-model="detailDialogVisible" title="模板详情" width="70%">
      <div v-if="currentTemplate">
        <el-descriptions :column="2" border>
          <el-descriptions-item label="名称">{{ currentTemplate.name }}</el-descriptions-item>
          <el-descriptions-item label="状态">
            <el-tag :type="currentTemplate.status === 'enabled' ? 'success' : 'info'">
              {{ currentTemplate.status === 'enabled' ? '启用' : '禁用' }}
            </el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="文件名">{{ currentTemplate.originalFileName }}</el-descriptions-item>
          <el-descriptions-item label="创建时间">{{ formatDate(currentTemplate.createdAt) }}</el-descriptions-item>
          <el-descriptions-item label="描述" :span="2">{{ currentTemplate.description || '无' }}</el-descriptions-item>
        </el-descriptions>
        
        <h3 style="margin-top: 20px;">字段列表 ({{ currentTemplate.fields.length }}个)</h3>
        <el-table :data="currentTemplate.fields" size="small" border>
          <el-table-column prop="name" label="字段名" />
          <el-table-column prop="fieldType" label="类型" width="100" />
          <el-table-column prop="required" label="必填" width="80">
            <template #default="scope">
              <el-tag :type="scope.row.required ? 'danger' : 'info'" size="small">
                {{ scope.row.required ? '是' : '否' }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="guidePrompt" label="引导话术" />
        </el-table>

        <h3 style="margin-top: 20px;">表格列表 ({{ currentTemplate.tables.length }}个)</h3>
        <div v-for="table in currentTemplate.tables" :key="table.id" style="margin-bottom: 15px;">
          <h4>{{ table.name }}</h4>
          <el-table :data="table.columns" size="small" border>
            <el-table-column prop="name" label="列名" />
            <el-table-column prop="columnOrder" label="顺序" width="80" />
          </el-table>
        </div>
      </div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import axios from 'axios'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Refresh } from '@element-plus/icons-vue'

const router = useRouter()
const templates = ref<any[]>([])
const loading = ref(false)

const detailDialogVisible = ref(false)
const currentTemplate = ref<any>(null)

const loadTemplates = async () => {
  loading.value = true
  try {
    const response = await axios.get('/api/templates')
    templates.value = response.data.data
    ElMessage.success('加载成功')
  } catch (error) {
    ElMessage.error('加载失败')
  } finally {
    loading.value = false
  }
}

const goToUploadPage = () => {
  router.push('/admin/templates/upload')
}

const viewTemplate = async (id: string) => {
  try {
    const response = await axios.get(`/api/templates/${id}`)
    currentTemplate.value = response.data.data
    detailDialogVisible.value = true
  } catch (error) {
    ElMessage.error('加载详情失败')
  }
}

const downloadTemplate = async (id: string) => {
  try {
    const response = await axios.get(`/api/templates/${id}/download`, {
      responseType: 'blob'
    })
    const url = window.URL.createObjectURL(new Blob([response.data]))
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', response.headers['content-disposition']?.split('filename=')[1] || 'template.docx')
    document.body.appendChild(link)
    link.click()
    link.remove()
    ElMessage.success('下载成功')
  } catch (error) {
    ElMessage.error('下载失败')
  }
}

const toggleStatus = async (template: any) => {
  const newStatus = template.status === 'enabled' ? 'disabled' : 'enabled'
  try {
    await axios.put(`/api/templates/${template.id}/status`, { status: newStatus })
    ElMessage.success('状态更新成功')
    loadTemplates()
  } catch (error) {
    ElMessage.error('更新失败')
  }
}

const deleteTemplate = async (id: string) => {
  try {
    await ElMessageBox.confirm('确定要删除该模板吗？', '确认删除', {
      type: 'warning'
    })
    await axios.delete(`/api/templates/${id}`)
    ElMessage.success('删除成功')
    loadTemplates()
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error('删除失败')
    }
  }
}

const goToChatFill = (templateId: string) => {
  router.push(`/chat/${templateId}`)
}

const formatDate = (dateStr: string) => {
  return new Date(dateStr).toLocaleString('zh-CN')
}

onMounted(() => {
  loadTemplates()
})
</script>

<style scoped>
.template-manager {
  background: white;
  padding: 24px;
  height: 100%;
  display: flex;
  flex-direction: column;
  width: 100%;
  min-width: 0;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
  padding-bottom: 16px;
  border-bottom: 1px solid #e4e7ed;
}

.page-header h1 {
  margin: 0;
  font-size: 24px;
  color: #303133;
}

.header-actions {
  display: flex;
  gap: 10px;
}

.template-list {
  margin-top: 20px;
  flex: 1;
  overflow: auto;
}

@media (max-width: 768px) {
  .template-manager {
    padding: 16px;
  }

  .page-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 10px;
  }

  .header-actions {
    width: 100%;
  }

  .header-actions .el-button {
    flex: 1;
  }
}

h3, h4 {
  color: #303133;
}
</style>
