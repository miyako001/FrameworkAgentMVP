import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true
      },
      '/health': {
        target: 'http://localhost:5000',
        changeOrigin: true
      },
      // 只代理后端测试接口，不代理前端路由
      '^/test/(db|llm)': {
        target: 'http://localhost:5000',
        changeOrigin: true
      }
    }
  }
})
