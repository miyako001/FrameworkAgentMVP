<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { HomeFilled, Document, EditPen, ChatDotRound, Upload, Setting } from '@element-plus/icons-vue'

const route = useRoute()

const activeMenu = computed(() => {
  if (route.path === '/') return 'home'
  if (route.path.startsWith('/admin/templates')) return route.path.includes('/upload') ? 'template-upload' : 'templates'
  if (route.path.startsWith('/test/generate')) return 'test-generate'
  if (route.path.startsWith('/chat')) return 'chat-fill'
  if (route.path.startsWith('/import-fill')) return 'import-fill'
  return 'home'
})
</script>

<template>
  <div id="app">
    <el-container style="height: 100vh;">
      <!-- 左侧菜单 -->
      <el-aside width="118px" class="app-sidebar">
        <div class="logo">
          <el-icon><ChatDotRound /></el-icon>
        </div>

        <div class="menu-block">
          <div class="menu-title">User</div>
          <el-menu
            :default-active="activeMenu"
            class="el-menu-vertical"
            background-color="#545c64"
            text-color="#fff"
            active-text-color="#ffd04b"
            router
          >
            <el-menu-item index="home" route="/">
              <el-icon><HomeFilled /></el-icon>
              <span>Home</span>
            </el-menu-item>
            <el-menu-item index="test-generate" route="/test/generate">
              <el-icon><EditPen /></el-icon>
              <span>Form</span>
            </el-menu-item>
            <el-menu-item index="chat-fill" route="/chat">
              <el-icon><ChatDotRound /></el-icon>
              <span>Ask</span>
            </el-menu-item>
            <el-menu-item index="import-fill" route="/import-fill">
              <el-icon><Upload /></el-icon>
              <span>Import</span>
            </el-menu-item>
          </el-menu>
        </div>

        <div class="menu-divider"></div>

        <div class="menu-block">
          <div class="menu-title">
            <el-icon><Setting /></el-icon>
            <span>Setting</span>
          </div>
          <el-menu
            :default-active="activeMenu"
            class="el-menu-vertical"
            background-color="#545c64"
            text-color="#fff"
            active-text-color="#ffd04b"
            router
          >
            <el-menu-item index="templates" route="/admin/templates">
              <el-icon><Document /></el-icon>
              <span>Tpl</span>
            </el-menu-item>
            <el-menu-item index="template-upload" route="/admin/templates/upload">
              <el-icon><Upload /></el-icon>
              <span>Upload</span>
            </el-menu-item>
          </el-menu>
        </div>
      </el-aside>

      <!-- 右侧内容区 -->
      <el-container>
        <el-main style="background-color: #f0f2f5;">
          <router-view />
        </el-main>
      </el-container>
    </el-container>
  </div>
</template>

<style>
* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

html, body {
  height: 100%;
  overflow: hidden;
}

#app {
  font-family: Avenir, Helvetica, Arial, sans-serif;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
  color: #2c3e50;
  height: 100%;
}

.el-menu-vertical {
  border-right: none;
}

.app-sidebar {
  background-color: #545c64;
  display: flex;
  flex-direction: column;
}

.menu-block {
  padding: 8px 0;
}

.menu-title {
  color: #cfd6de;
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.4px;
  padding: 0 10px 6px;
  display: flex;
  align-items: center;
  gap: 4px;
}

.menu-divider {
  height: 1px;
  background: #434a50;
  margin: 2px 8px;
}

.logo {
  padding: 10px 0;
  color: white;
  font-size: 18px;
  font-weight: 600;
  text-align: center;
  border-bottom: 1px solid #434a50;
}

.el-menu-vertical .el-menu-item {
  height: 36px;
  line-height: 36px;
  font-size: 11px;
  padding-left: 8px !important;
}

.el-menu-vertical .el-icon {
  margin-right: 4px;
}

.el-main {
  padding: 0 !important;
  overflow-y: auto;
}
</style>

