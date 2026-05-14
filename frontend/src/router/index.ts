import { createRouter, createWebHistory } from 'vue-router'
import Home from '../views/Home.vue'
import TemplateManager from '../views/TemplateManager.vue'
import TemplateUpload from '../views/TemplateUpload.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'Home',
      component: Home,
      meta: { title: '首页' }
    },
    {
      path: '/admin',
      name: 'Admin',
      redirect: '/admin/templates'
    },
    {
      path: '/admin/templates',
      name: 'TemplateManager',
      component: TemplateManager,
      meta: { title: '模板管理' }
    },
    {
      path: '/admin/templates/upload',
      name: 'TemplateUpload',
      component: TemplateUpload,
      meta: { title: '上传模板' }
    },
    {
      path: '/test',
      name: 'Test',
      redirect: '/test/generate'
    },
    {
      path: '/test/generate',
      name: 'TestGenerate',
      component: () => import('../views/user/TestGenerate.vue'),
      meta: { title: '文档生成测试' }
    },
    {
      path: '/chat',
      name: 'ChatFillEntry',
      component: () => import('../views/user/ChatFill.vue'),
      meta: { title: 'AI对话填写' }
    },
    {
      path: '/chat/:templateId',
      name: 'ChatFill',
      component: () => import('../views/user/ChatFill.vue'),
      meta: { title: 'AI对话填写' }
    },
    {
      path: '/import-fill',
      name: 'ImportFill',
      component: () => import('../views/user/ImportFill.vue'),
      meta: { title: '导入填充' }
    }
  ]
})

// 路由守卫：更新页面标题
router.beforeEach((to, _from, next) => {
  if (to.meta.title) {
    document.title = `${to.meta.title} - FrameAgent`
  }
  next()
})

export default router
