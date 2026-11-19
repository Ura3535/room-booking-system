<script setup>
import { ref } from 'vue'
import { currentUser, isAuthenticated, login, logout } from './auth'

const username = ref('student1')
const password = ref('1234')
const errorMsg = ref('')
const loading = ref(false)

async function handleLogin() {
  errorMsg.value = ''
  loading.value = true
  try {
    await login(username.value, password.value)
  } catch (err) {
    errorMsg.value = err.message
  } finally {
    loading.value = false
  }
}

function fillAccount(u, p) {
  username.value = u
  password.value = p
}
</script>

<template>
  <main style="max-width: 420px; margin: 60px auto; font-family: system-ui, sans-serif;">
    <section v-if="!isAuthenticated" style="border: 1px solid #ddd; padding: 24px; border-radius: 8px;">
      <h2>Вхід у систему бронювання</h2>

      <div style="margin-bottom: 12px; display: flex; gap: 6px; flex-wrap: wrap;">
        <button type="button" @click="fillAccount('student1', '1234')">student1</button>
        <button type="button" @click="fillAccount('student2', '1234')">student2</button>
        <button type="button" @click="fillAccount('admin', 'admin')">admin</button>
      </div>

      <form @submit.prevent="handleLogin" style="display: flex; flex-direction: column; gap: 12px;">
        <input v-model="username" placeholder="Логін" required style="padding: 8px;" />
        <input v-model="password" type="password" placeholder="Пароль" required style="padding: 8px;" />
        <button type="submit" :disabled="loading" style="padding: 10px; cursor: pointer;">
          {{ loading ? 'Вхід...' : 'Увійти' }}
        </button>
      </form>

      <p v-if="errorMsg" style="color: #d32f2f; margin-top: 12px;">{{ errorMsg }}</p>
    </section>

    <section v-else style="border: 1px solid #ddd; padding: 24px; border-radius: 8px;">
      <h3>Авторизацію пройдено</h3>
      <p>Користувач: <strong>{{ currentUser.fullName }}</strong> ({{ currentUser.username }})</p>
      <p>Роль: <code>{{ currentUser.role }}</code></p>
      <button @click="logout" style="padding: 8px 14px; cursor: pointer;">Вийти</button>
    </section>
  </main>
</template>