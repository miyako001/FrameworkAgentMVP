<template>
  <div class="home">
    <h1>FrameAgent WordFill - MVP</h1>
    <p>技术栈：Microsoft Agent Framework + GitHub Copilot SDK</p>
    
    <!-- 快速导航 -->
    <div class="quick-nav">
      <h2>快速导航</h2>
      <div class="nav-buttons">
        <el-button type="info" @click="goToSetting" size="large">
          ⚙️ Setting（模板与上传）
        </el-button>
        <el-button type="primary" @click="goToTemplateManager" size="large">
          📑 模板管理
        </el-button>
        <el-button type="success" @click="goToTestGenerate" size="large">
          📝 文档生成测试（手动填写）
        </el-button>
        <el-button type="warning" @click="goToChatFill" size="large">
          💬 AI对话填写（智能填充）
        </el-button>
      </div>
    </div>

    <div class="status-panel">
      <h2>系统状态</h2>
      
      <div class="status-item">
        <span>后端健康检查：</span>
        <el-tag :type="healthStatus ? 'success' : 'danger'">
          {{ healthStatus ? '正常' : '异常' }}
        </el-tag>
        <el-button @click="checkHealth" size="small">刷新</el-button>
      </div>
      
      <div class="status-item">
        <span>数据库连接（fa_ 表）：</span>
        <el-tag :type="dbStatus ? 'success' : 'danger'">
          {{ dbStatus ? '正常' : '异常' }}
        </el-tag>
        <el-button @click="checkDatabase" size="small">刷新</el-button>
      </div>
      
      <div class="status-item">
        <span>MAF + Copilot SDK：</span>
        <el-tag :type="llmStatus ? 'success' : 'danger'">
          {{ llmStatus ? '正常' : '异常' }}
        </el-tag>
        <el-button @click="checkLLM" size="small">测试</el-button>
      </div>
      
      <div v-if="llmMessage" class="llm-response">
        <strong>AI 回复：</strong> {{ llmMessage }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import axios from 'axios'
import { ElMessage } from 'element-plus'

const router = useRouter()
const healthStatus = ref(false)
const dbStatus = ref(false)
const llmStatus = ref(false)
const llmMessage = ref('')

const goToTemplateManager = () => {
  router.push('/admin/templates')
}

const goToSetting = () => {
  router.push('/admin/templates')
}

const goToTestGenerate = () => {
  router.push('/test/generate')
}

const goToChatFill = () => {
  ElMessage.info('请先在模板管理页面选择一个模板进行AI对话填写')
  router.push('/admin/templates')
}

const checkHealth = async () => {
  try {
    const response = await axios.get('/health')
    healthStatus.value = response.data.status === 'ok'
    ElMessage.success('健康检查通过')
  } catch (error) {
    healthStatus.value = false
    ElMessage.error('健康检查失败')
  }
}

const checkDatabase = async () => {
  try {
    const response = await axios.get('/test/db')
    dbStatus.value = response.data.success
    ElMessage.success(`数据库正常 (${response.data.databasePath})`)
  } catch (error) {
    dbStatus.value = false
    ElMessage.error('数据库连接失败')
  }
}

const checkLLM = async () => {
  try {
    const response = await axios.get('/test/llm')
    llmStatus.value = response.data.success
    llmMessage.value = response.data.message
    ElMessage.success('MAF + Copilot SDK 正常工作')
  } catch (error: any) {
    llmStatus.value = false
    llmMessage.value = error.response?.data?.error || '连接失败'
    ElMessage.error('AI 服务异常')
  }
}

onMounted(async () => {
  await checkHealth()
  await checkDatabase()
})
</script>

<style scoped>
.home {
  padding: 24px;
  background: white;
  height: 100%;
}

.quick-nav {
  margin-top: 20px;
  padding: 20px;
  border: 1px solid #ddd;
  border-radius: 4px;
  background: #f9f9f9;
}

.nav-buttons {
  display: flex;
  gap: 15px;
  margin-top: 10px;
  flex-wrap: wrap;
}

.status-panel {
  margin-top: 20px;
  padding: 20px;
  border: 1px solid #ddd;
  border-radius: 4px;
}

.status-item {
  margin: 10px 0;
  display: flex;
  align-items: center;
  gap: 10px;
}

.llm-response {
  margin-top: 15px;
  padding: 10px;
  background: #f5f5f5;
  border-radius: 4px;
}

@media (max-width: 768px) {
  .home {
    padding: 16px;
  }

  .nav-buttons {
    flex-direction: column;
  }

  .nav-buttons .el-button {
    width: 100%;
  }

  .status-item {
    flex-wrap: wrap;
    align-items: flex-start;
  }
}
</style>
