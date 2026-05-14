<template>
  <div class="chat-fill">
    <el-page-header title="返回" @back="goBack" :content="`对话填写${templateName ? ' - ' + templateName : ''}`" />

    <div class="chat-layout">
      <el-card class="chat-container">
        <div class="progress-bar">
          <el-progress :percentage="Math.round(progress * 100)" :color="progressColor" />
          <span class="progress-text">已收集 {{ collectedFieldCount }} / {{ totalFieldCount }} 个字段</span>
        </div>

        <div class="message-list" ref="messageListRef">
          <template v-if="messages.length > 0">
            <div
              v-for="(msg, index) in messages"
              :key="index"
              :class="['message', msg.role === 'user' ? 'message-user' : 'message-assistant']"
            >
              <div class="message-avatar">{{ msg.role === 'user' ? '我' : 'AI' }}</div>
              <div class="message-content">
                <div class="message-text">{{ msg.content }}</div>
                <div class="message-time">{{ formatTime(msg.timestamp) }}</div>
              </div>
            </div>
          </template>
          <el-empty v-else description="请在右侧选择模板并开始会话" />

          <div v-if="isTyping" class="message message-assistant">
            <div class="message-avatar">AI</div>
            <div class="message-content">
              <div class="typing-indicator"><span></span><span></span><span></span></div>
            </div>
          </div>
        </div>

        <div class="input-area">
          <el-input
            v-model="userInput"
            type="textarea"
            :rows="3"
            placeholder="请输入您的回答..."
            @keydown.ctrl.enter="sendMessage"
            :disabled="!sessionId || isCompleted || isSending"
          />
          <div class="input-actions">
            <el-button type="primary" @click="sendMessage" :loading="isSending" :disabled="!sessionId || isCompleted">
              发送 (Ctrl+Enter)
            </el-button>
            <el-button v-if="isCompleted" type="success" @click="generateDocument">生成文档</el-button>
          </div>
          <div class="shortcut-hint">提示：你可以说"我要一次性填完"、"下载模板"等快捷指令</div>
        </div>
      </el-card>

      <el-card class="side-panel">
        <template #header><span>模板与会话</span></template>

        <div class="side-section">
          <div class="side-title">选择模板</div>
          <el-select v-model="selectedTemplateId" placeholder="请选择模板" style="width: 100%" :loading="templatesLoading">
            <el-option v-for="template in templates" :key="template.id" :label="template.name" :value="template.id" />
          </el-select>
          <el-button class="start-btn" type="primary" :disabled="!selectedTemplateId || isSending" @click="startSession">
            开始对话
          </el-button>
        </div>

        <div class="side-section" v-if="Object.keys(collectedFields).length > 0">
          <div class="side-title">已收集字段</div>
          <el-descriptions :column="1" border size="small">
            <el-descriptions-item v-for="(value, key) in collectedFields" :key="key" :label="key">
              {{ value }}
            </el-descriptions-item>
          </el-descriptions>
        </div>
      </el-card>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import axios from 'axios'

const router = useRouter()
const route = useRoute()

const routeTemplateId = route.params.templateId as string | undefined
const selectedTemplateId = ref<string>(routeTemplateId ?? '')
const templates = ref<any[]>([])
const templatesLoading = ref(false)

const templateName = ref('')
const sessionId = ref('')
const messages = ref<any[]>([])
const userInput = ref('')
const isSending = ref(false)
const isTyping = ref(false)
const isCompleted = ref(false)
const progress = ref(0)
const collectedFields = ref<Record<string, string>>({})
const collectedFieldCount = ref(0)
const totalFieldCount = ref(0)
const messageListRef = ref<HTMLElement>()

const progressColor = computed(() => {
  if (progress.value < 0.3) return '#f56c6c'
  if (progress.value < 0.7) return '#e6a23c'
  return '#67c23a'
})

const resetSessionState = () => {
  sessionId.value = ''
  messages.value = []
  userInput.value = ''
  isSending.value = false
  isTyping.value = false
  isCompleted.value = false
  progress.value = 0
  collectedFields.value = {}
  collectedFieldCount.value = 0
  totalFieldCount.value = 0
  templateName.value = ''
}

const loadTemplates = async () => {
  templatesLoading.value = true
  try {
    const response = await axios.get('/api/templates')
    const payload = response.data?.data ?? response.data
    const allTemplates = Array.isArray(payload) ? payload : []
    templates.value = allTemplates.filter((t: any) => t.status === 'enabled')
  } catch {
    ElMessage.error('加载模板列表失败')
  } finally {
    templatesLoading.value = false
  }
}

const startSession = async () => {
  if (!selectedTemplateId.value) {
    ElMessage.warning('请先选择模板')
    return
  }

  resetSessionState()

  try {
    const response = await axios.post('/api/chat/start', { templateId: selectedTemplateId.value })
    sessionId.value = response.data.sessionId

    messages.value.push({
      role: 'assistant',
      content: response.data.message,
      timestamp: new Date()
    })

    const templateResponse = await axios.get(`/api/templates/${selectedTemplateId.value}`)
    const template = templateResponse.data?.data ?? templateResponse.data
    templateName.value = template.name
    totalFieldCount.value = Array.isArray(template.fields) ? template.fields.length : 0

    if (route.path !== '/chat') {
      router.replace('/chat')
    }

    await scrollToBottom()
  } catch (error: any) {
    ElMessage.error(error.response?.data?.error || '启动会话失败')
  }
}

const sendMessage = async () => {
  if (!sessionId.value) {
    ElMessage.warning('请先选择模板并开始会话')
    return
  }
  if (!userInput.value.trim() || isSending.value) return

  const message = userInput.value.trim()
  userInput.value = ''

  messages.value.push({ role: 'user', content: message, timestamp: new Date() })
  await scrollToBottom()

  isSending.value = true
  isTyping.value = true
  try {
    await sendMessageWithStream(message)
  } catch {
    ElMessage.error('发送失败')
    isTyping.value = false
  } finally {
    isSending.value = false
  }
}

const sendMessageWithStream = async (message: string) => {
  const response = await fetch('/api/chat/message/stream', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ sessionId: sessionId.value, message })
  })

  if (!response.body) throw new Error('No response body')

  const reader = response.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''
  let currentMessage = ''

  messages.value.push({ role: 'assistant', content: '', timestamp: new Date() })
  const messageIndex = messages.value.length - 1

  while (true) {
    const { done, value } = await reader.read()
    if (done) break

    buffer += decoder.decode(value, { stream: true })
    const lines = buffer.split('\n\n')
    buffer = lines.pop() || ''

    for (const line of lines) {
      if (!line.trim()) continue

      const eventMatch = line.match(/^event: (.+)$/m)
      const dataMatch = line.match(/^data: (.+)$/m)
      if (!eventMatch || !dataMatch) continue

      const eventType = eventMatch[1]
      const data = JSON.parse(dataMatch[1])

      if (eventType === 'message') {
        currentMessage += data.chunk
        messages.value[messageIndex].content = currentMessage
        await scrollToBottom()
      } else if (eventType === 'metadata') {
        if (data.extractedFields) {
          Object.assign(collectedFields.value, data.extractedFields)
          collectedFieldCount.value = Object.keys(collectedFields.value).length
        }
        if (data.progress !== undefined) progress.value = data.progress
        if (data.isCompleted) isCompleted.value = true
      } else if (eventType === 'done') {
        isTyping.value = false
      } else if (eventType === 'error') {
        ElMessage.error(data.message)
        isTyping.value = false
      }
    }
  }
}

const generateDocument = async () => {
  if (!selectedTemplateId.value) {
    ElMessage.warning('请先选择模板')
    return
  }

  try {
    const request = {
      templateId: selectedTemplateId.value,
      fields: Object.keys(collectedFields.value).map((key) => ({ name: key, value: collectedFields.value[key] })),
      tables: []
    }

    const response = await axios.post('/api/generate', request)
    if (response.data.success) {
      ElMessage.success('文档生成成功')
      const downloadResponse = await axios.get(response.data.downloadUrl, { responseType: 'blob' })
      const url = window.URL.createObjectURL(new Blob([downloadResponse.data]))
      const link = document.createElement('a')
      link.href = url
      link.setAttribute('download', response.data.fileName)
      document.body.appendChild(link)
      link.click()
      link.remove()
    }
  } catch (error: any) {
    ElMessage.error(error.response?.data?.error || '生成文档失败')
  }
}

const scrollToBottom = async () => {
  await nextTick()
  if (messageListRef.value) messageListRef.value.scrollTop = messageListRef.value.scrollHeight
}

const formatTime = (date: Date) => {
  return new Date(date).toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' })
}

const goBack = () => {
  router.push('/')
}

onMounted(async () => {
  await loadTemplates()
  if (selectedTemplateId.value) {
    await startSession()
  }
})
</script>

<style scoped>
.chat-fill {
  padding: 12px 16px;
  height: 100%;
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.chat-layout {
  flex: 1;
  min-height: 0;
  display: grid;
  grid-template-columns: minmax(0, 1fr) 248px;
  gap: 12px;
}

.chat-container {
  min-height: 0;
  display: flex;
  flex-direction: column;
}

.side-panel {
  min-height: 0;
  overflow: auto;
}

.side-section {
  margin-bottom: 14px;
}

.side-title {
  font-size: 13px;
  color: #666;
  margin-bottom: 8px;
}

.start-btn {
  margin-top: 10px;
  width: 100%;
}

.progress-bar {
  margin-bottom: 15px;
}

.progress-text {
  font-size: 12px;
  color: #666;
  margin-top: 5px;
  display: block;
}

.message-list {
  flex: 1;
  min-height: 320px;
  overflow-y: auto;
  padding: 15px;
  background: #f5f5f5;
  border-radius: 4px;
  margin-bottom: 15px;
}

@media (max-width: 1080px) {
  .chat-layout {
    grid-template-columns: 1fr;
  }

  .side-panel {
    order: -1;
  }
}

@media (max-width: 768px) {
  .chat-fill {
    padding: 8px;
    gap: 8px;
  }

  .message-list {
    min-height: 240px;
    padding: 10px;
  }
}

.message {
  display: flex;
  margin-bottom: 20px;
  animation: fadeIn 0.3s;
}

@keyframes fadeIn {
  from {
    opacity: 0;
    transform: translateY(10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.message-user {
  flex-direction: row-reverse;
}

.message-avatar {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: #409eff;
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  flex-shrink: 0;
}

.message-user .message-avatar {
  background: #67c23a;
}

.message-content {
  max-width: 70%;
  margin: 0 10px;
}

.message-user .message-content {
  text-align: right;
}

.message-text {
  background: white;
  padding: 10px 15px;
  border-radius: 8px;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.1);
  word-wrap: break-word;
  white-space: pre-wrap;
}

.message-user .message-text {
  background: #409eff;
  color: white;
}

.message-time {
  font-size: 11px;
  color: #999;
  margin-top: 5px;
}

.typing-indicator {
  display: flex;
  gap: 4px;
  padding: 10px 15px;
}

.typing-indicator span {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #ccc;
  animation: typing 1.4s infinite;
}

.typing-indicator span:nth-child(2) {
  animation-delay: 0.2s;
}

.typing-indicator span:nth-child(3) {
  animation-delay: 0.4s;
}

@keyframes typing {
  0%, 60%, 100% {
    opacity: 0.4;
  }
  30% {
    opacity: 1;
  }
}

.input-area {
  margin-top: 10px;
}

.input-actions {
  margin-top: 10px;
  display: flex;
  gap: 10px;
}

.shortcut-hint {
  margin-top: 10px;
  font-size: 12px;
  color: #999;
}
</style>
