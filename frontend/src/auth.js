import { ref, computed } from "vue";

export const API_URL = "http://localhost:5154/api";

const storedToken = localStorage.getItem("rb_token");
const storedUser = localStorage.getItem("rb_user");

export const token = ref(storedToken || "");
export const currentUser = ref(storedUser ? JSON.parse(storedUser) : null);
export const isAuthenticated = computed(() =>
  Boolean(token.value && currentUser.value),
);

export function authHeaders() {
  return {
    "Content-Type": "application/json",
    ...(token.value ? { Authorization: `Bearer ${token.value}` } : {}),
  };
}

export async function login(username, password) {
  const res = await fetch(`${API_URL}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ username, password }),
  });

  const data = await res.json();
  if (!res.ok) {
    throw new Error(data.error || "Помилка авторизації");
  }

  token.value = data.token;
  currentUser.value = data.user;
  localStorage.setItem("rb_token", data.token);
  localStorage.setItem("rb_user", JSON.stringify(data.user));
}

export function logout() {
  token.value = "";
  currentUser.value = null;
  localStorage.removeItem("rb_token");
  localStorage.removeItem("rb_user");
}
