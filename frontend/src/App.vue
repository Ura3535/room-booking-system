<script setup>
import { ref, computed, onMounted, watch } from "vue";
import {
  API_URL,
  currentUser,
  isAuthenticated,
  authHeaders,
  login,
  logout,
} from "./auth";

// --- Стан авторизації ---
const username = ref("student1");
const password = ref("1234");
const authError = ref("");
const authLoading = ref(false);

async function handleLogin() {
  authError.value = "";
  authLoading.value = true;
  try {
    await login(username.value, password.value);
    await initDashboard();
  } catch (err) {
    authError.value = err.message;
  } finally {
    authLoading.value = false;
  }
}

function quickFill(u, p) {
  username.value = u;
  password.value = p;
}

// --- Стан дашборду ---
const activeTab = ref("rooms"); // 'rooms' | 'my'
const rooms = ref([]);
const myBookings = ref([]);
const loadingRooms = ref(false);
const loadingBookings = ref(false);

// За замовчуванням завтрашній день 10:00 - 11:30 (Europe/Kyiv +02:00)
const getTomorrowStr = () => {
  const d = new Date();
  d.setDate(d.getDate() + 1);
  return d.toISOString().split("T")[0];
};

const selectedDate = ref(getTomorrowStr());
const startHour = ref("10:00");
const endHour = ref("11:30");
const searchQuery = ref("");
const onlyAvailable = ref(false);

// Модальне вікно бронювання
const modalRoom = ref(null);
const bookingPurpose = ref("Практичне заняття");
const submittingBooking = ref(false);

// Сповіщення (Toast)
const toast = ref({ type: "", text: "" });
let toastTimer = null;

function showToast(type, text) {
  toast.value = { type, text };
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => {
    toast.value = { type: "", text: "" };
  }, 4500);
}

const startIso = computed(
  () => `${selectedDate.value}T${startHour.value}:00+02:00`,
);
const endIso = computed(
  () => `${selectedDate.value}T${endHour.value}:00+02:00`,
);

const filteredRooms = computed(() => {
  return rooms.value.filter((r) => {
    const matchesSearch =
      r.name.toLowerCase().includes(searchQuery.value.toLowerCase()) ||
      r.building.toLowerCase().includes(searchQuery.value.toLowerCase()) ||
      r.equipment.toLowerCase().includes(searchQuery.value.toLowerCase());
    const matchesAvail = !onlyAvailable.value || r.isAvailable;
    return matchesSearch && matchesAvail;
  });
});

async function fetchRooms() {
  loadingRooms.value = true;
  try {
    const s = encodeURIComponent(startIso.value);
    const e = encodeURIComponent(endIso.value);
    const res = await fetch(`${API_URL}/rooms?start=${s}&end=${e}`, {
      headers: authHeaders(),
    });
    const data = await res.json();
    if (!res.ok) {
      showToast("error", data.error || "Помилка перевірки інтервалу");
      rooms.value = [];
      return;
    }
    rooms.value = data;
  } catch {
    showToast("error", "Не вдалося зʼєднатися з сервером API");
  } finally {
    loadingRooms.value = false;
  }
}

async function fetchMyBookings() {
  loadingBookings.value = true;
  try {
    const res = await fetch(`${API_URL}/bookings/my`, {
      headers: authHeaders(),
    });
    if (res.status === 401) {
      logout();
      return;
    }
    myBookings.value = await res.json();
  } finally {
    loadingBookings.value = false;
  }
}

function openBookingModal(room) {
  modalRoom.value = room;
  bookingPurpose.value = "Семінар / консультація";
}

async function confirmBooking() {
  if (!modalRoom.value) return;
  submittingBooking.value = true;
  try {
    const res = await fetch(`${API_URL}/bookings`, {
      method: "POST",
      headers: authHeaders(),
      body: JSON.stringify({
        roomId: modalRoom.value.id,
        startTime: startIso.value,
        endTime: endIso.value,
        purpose: bookingPurpose.value,
      }),
    });
    const data = await res.json();
    if (!res.ok) {
      showToast("error", `Помилка [${res.status}]: ${data.error}`);
      return;
    }
    showToast(
      "success",
      `Аудиторію ${modalRoom.value.name} успішно заброньовано (#${data.id})`,
    );
    modalRoom.value = null;
    await Promise.all([fetchRooms(), fetchMyBookings()]);
  } finally {
    submittingBooking.value = false;
  }
}

async function cancelBooking(id) {
  const res = await fetch(`${API_URL}/bookings/${id}/cancel`, {
    method: "POST",
    headers: authHeaders(),
  });
  const data = await res.json();
  if (!res.ok) {
    showToast("error", `Помилка [${res.status}]: ${data.error}`);
    return;
  }
  showToast("success", `Бронювання #${id} скасовано, слот звільнено`);
  await Promise.all([fetchRooms(), fetchMyBookings()]);
}

function formatKyivTime(isoStr) {
  return new Date(isoStr).toLocaleString("uk-UA", {
    timeZone: "Europe/Kyiv",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

async function initDashboard() {
  if (!isAuthenticated.value) return;
  await Promise.all([fetchRooms(), fetchMyBookings()]);
}

watch([selectedDate, startHour, endHour], () => {
  if (isAuthenticated.value) fetchRooms();
});

onMounted(initDashboard);
</script>

<template>
  <!-- ЕКРАН ВХОДУ -->
  <div
    v-if="!isAuthenticated"
    class="min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-900 via-indigo-950 to-slate-900 p-4"
  >
    <div
      class="w-full max-w-md bg-white rounded-2xl shadow-2xl p-8 border border-slate-100"
    >
      <div class="text-center mb-6">
        <div
          class="inline-flex items-center justify-center w-12 h-12 rounded-xl bg-indigo-600 text-white font-bold text-xl mb-3 shadow-md"
        >
          RB
        </div>
        <h1 class="text-2xl font-bold text-slate-900">RoomBooking</h1>
        <p class="text-sm text-slate-500 mt-1">
          Університетський сервіс бронювання аудиторій
        </p>
      </div>

      <div class="mb-5">
        <p
          class="text-xs font-medium text-slate-400 uppercase tracking-wider mb-2"
        >
          Швидкий вибір навчального акаунту
        </p>
        <div class="grid grid-cols-3 gap-2">
          <button
            type="button"
            @click="quickFill('student1', '1234')"
            class="px-3 py-2 text-xs font-medium rounded-lg border transition"
            :class="
              username === 'student1'
                ? 'bg-indigo-50 border-indigo-500 text-indigo-700'
                : 'border-slate-200 text-slate-600 hover:bg-slate-50'
            "
          >
            student1
          </button>
          <button
            type="button"
            @click="quickFill('student2', '1234')"
            class="px-3 py-2 text-xs font-medium rounded-lg border transition"
            :class="
              username === 'student2'
                ? 'bg-indigo-50 border-indigo-500 text-indigo-700'
                : 'border-slate-200 text-slate-600 hover:bg-slate-50'
            "
          >
            student2
          </button>
          <button
            type="button"
            @click="quickFill('admin', 'admin')"
            class="px-3 py-2 text-xs font-medium rounded-lg border transition"
            :class="
              username === 'admin'
                ? 'bg-indigo-50 border-indigo-500 text-indigo-700'
                : 'border-slate-200 text-slate-600 hover:bg-slate-50'
            "
          >
            admin
          </button>
        </div>
      </div>

      <form @submit.prevent="handleLogin" class="space-y-4">
        <div>
          <label class="block text-sm font-medium text-slate-700 mb-1"
            >Логін</label
          >
          <input
            v-model="username"
            type="text"
            required
            class="w-full px-3.5 py-2.5 rounded-lg border border-slate-300 focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition bg-white"
          />
        </div>
        <div>
          <label class="block text-sm font-medium text-slate-700 mb-1"
            >Пароль</label
          >
          <input
            v-model="password"
            type="password"
            required
            class="w-full px-3.5 py-2.5 rounded-lg border border-slate-300 focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition bg-white"
          />
        </div>

        <div
          v-if="authError"
          class="p-3 rounded-lg bg-rose-50 border border-rose-200 text-rose-700 text-sm"
        >
          {{ authError }}
        </div>

        <button
          type="submit"
          :disabled="authLoading"
          class="w-full py-2.5 px-4 bg-indigo-600 hover:bg-indigo-700 text-white font-semibold rounded-lg shadow-sm transition disabled:opacity-50"
        >
          {{ authLoading ? "Перевірка..." : "Увійти в систему" }}
        </button>
      </form>
    </div>
  </div>

  <!-- ОСНОВНИЙ ІНТЕРФЕЙС -->
  <div v-else class="min-h-screen flex flex-col">
    <!-- Навігаційна панель -->
    <header class="bg-white border-b border-slate-200 sticky top-0 z-30">
      <div
        class="max-w-6xl mx-auto px-4 sm:px-6 h-16 flex items-center justify-between"
      >
        <div class="flex items-center gap-3">
          <div
            class="w-9 h-9 rounded-lg bg-indigo-600 text-white font-bold flex items-center justify-center shadow-sm"
          >
            RB
          </div>
          <div>
            <span class="font-bold text-slate-900 text-lg">RoomBooking</span>
            <span
              class="ml-2 text-xs font-semibold px-2 py-0.5 rounded-full bg-indigo-50 text-indigo-700 border border-indigo-200"
            >
              v1.0
            </span>
          </div>
        </div>

        <!-- Вкладки -->
        <nav class="flex gap-1 bg-slate-100 p-1 rounded-xl">
          <button
            @click="activeTab = 'rooms'"
            class="px-4 py-1.5 rounded-lg text-sm font-medium transition"
            :class="
              activeTab === 'rooms'
                ? 'bg-white text-slate-900 shadow-sm'
                : 'text-slate-600 hover:text-slate-900'
            "
          >
            Аудиторії
          </button>
          <button
            @click="activeTab = 'my'"
            class="px-4 py-1.5 rounded-lg text-sm font-medium transition flex items-center gap-2"
            :class="
              activeTab === 'my'
                ? 'bg-white text-slate-900 shadow-sm'
                : 'text-slate-600 hover:text-slate-900'
            "
          >
            Мої бронювання
            <span
              class="px-1.5 py-0.2 text-xs rounded-full bg-indigo-100 text-indigo-700 font-semibold"
            >
              {{ myBookings.filter((b) => b.status === "Active").length }}
            </span>
          </button>
        </nav>

        <!-- Профіль -->
        <div class="flex items-center gap-3">
          <div class="text-right hidden sm:block">
            <div class="text-sm font-semibold text-slate-800">
              {{ currentUser.fullName }}
            </div>
            <div class="text-xs text-slate-500">
              {{ currentUser.username }} • {{ currentUser.role }}
            </div>
          </div>
          <button
            @click="logout"
            class="px-3 py-1.5 text-sm font-medium text-slate-600 hover:text-rose-600 hover:bg-rose-50 rounded-lg border border-slate-200 transition"
          >
            Вийти
          </button>
        </div>
      </div>
    </header>

    <!-- Сповіщення (Toast) -->
    <div v-if="toast.text" class="fixed bottom-5 right-5 z-50 max-w-md">
      <div
        class="px-4 py-3 rounded-xl shadow-lg border text-sm font-medium flex items-center gap-3"
        :class="
          toast.type === 'error'
            ? 'bg-rose-600 text-white border-rose-700'
            : 'bg-emerald-600 text-white border-emerald-700'
        "
      >
        <span>{{ toast.text }}</span>
      </div>
    </div>

    <!-- Контент -->
    <main class="max-w-6xl w-full mx-auto px-4 sm:px-6 py-8 flex-1">
      <!-- ВКЛАДКА 1: ПОШУК АУДИТОРІЙ -->
      <div v-if="activeTab === 'rooms'" class="space-y-6">
        <!-- Панель вибору інтервалу -->
        <div class="bg-white p-5 rounded-2xl shadow-sm border border-slate-200">
          <div
            class="flex flex-col md:flex-row md:items-end justify-between gap-4"
          >
            <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 flex-1">
              <div>
                <label
                  class="block text-xs font-semibold text-slate-500 uppercase mb-1"
                  >Дата</label
                >
                <input
                  type="date"
                  v-model="selectedDate"
                  class="w-full px-3 py-2 rounded-lg border border-slate-300 text-sm focus:ring-2 focus:ring-indigo-500 outline-none bg-white"
                />
              </div>
              <div>
                <label
                  class="block text-xs font-semibold text-slate-500 uppercase mb-1"
                  >Початок</label
                >
                <input
                  type="time"
                  v-model="startHour"
                  class="w-full px-3 py-2 rounded-lg border border-slate-300 text-sm focus:ring-2 focus:ring-indigo-500 outline-none bg-white"
                />
              </div>
              <div>
                <label
                  class="block text-xs font-semibold text-slate-500 uppercase mb-1"
                  >Завершення</label
                >
                <input
                  type="time"
                  v-model="endHour"
                  class="w-full px-3 py-2 rounded-lg border border-slate-300 text-sm focus:ring-2 focus:ring-indigo-500 outline-none bg-white"
                />
              </div>
            </div>

            <div class="flex flex-wrap items-center gap-3">
              <input
                type="text"
                v-model="searchQuery"
                placeholder="Пошук за назвою, корпусом..."
                class="px-3.5 py-2 rounded-lg border border-slate-300 text-sm w-56 focus:ring-2 focus:ring-indigo-500 outline-none bg-white"
              />
              <label
                class="inline-flex items-center gap-2 text-sm text-slate-700 cursor-pointer select-none"
              >
                <input
                  type="checkbox"
                  v-model="onlyAvailable"
                  class="rounded text-indigo-600 focus:ring-indigo-500"
                />
                Тільки вільні
              </label>
            </div>
          </div>
        </div>

        <!-- Сітка карток аудиторій -->
        <div v-if="loadingRooms" class="text-center py-12 text-slate-500">
          Завантаження аудиторій...
        </div>
        <div
          v-else-if="filteredRooms.length === 0"
          class="text-center py-12 bg-white rounded-2xl border border-slate-200 text-slate-500"
        >
          Аудиторій за вказаними параметрами не знайдено.
        </div>
        <div v-else class="grid grid-cols-1 md:grid-cols-2 gap-5">
          <div
            v-for="room in filteredRooms"
            :key="room.id"
            class="bg-white rounded-2xl p-6 border transition shadow-sm flex flex-col justify-between"
            :class="
              room.isAvailable
                ? 'border-slate-200 hover:border-indigo-300'
                : 'border-slate-200 bg-slate-50/70 opacity-85'
            "
          >
            <div>
              <div class="flex items-start justify-between gap-2">
                <div>
                  <span class="text-xs font-medium text-slate-400">{{
                    room.building
                  }}</span>
                  <h3 class="text-lg font-bold text-slate-900 mt-0.5">
                    {{ room.name }}
                  </h3>
                </div>
                <span
                  class="px-2.5 py-1 rounded-full text-xs font-semibold"
                  :class="
                    room.isAvailable
                      ? 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                      : 'bg-rose-50 text-rose-700 border border-rose-200'
                  "
                >
                  {{ room.isAvailable ? "Вільна" : "Зайнята" }}
                </span>
              </div>

              <div class="mt-4 space-y-2 text-sm text-slate-600">
                <div
                  class="flex items-center justify-between py-1.5 border-b border-slate-100"
                >
                  <span class="text-slate-400">Місткість:</span>
                  <span class="font-semibold text-slate-800"
                    >{{ room.capacity }} місць</span
                  >
                </div>
                <div class="flex items-center justify-between py-1.5">
                  <span class="text-slate-400">Обладнання:</span>
                  <span class="font-medium text-slate-700 text-right">{{
                    room.equipment
                  }}</span>
                </div>
              </div>
            </div>

            <div class="mt-6 pt-4 border-t border-slate-100 flex justify-end">
              <button
                @click="openBookingModal(room)"
                :disabled="!room.isAvailable"
                class="px-4 py-2 rounded-lg text-sm font-semibold transition"
                :class="
                  room.isAvailable
                    ? 'bg-indigo-600 hover:bg-indigo-700 text-white shadow-sm'
                    : 'bg-slate-200 text-slate-400 cursor-not-allowed'
                "
              >
                {{ room.isAvailable ? "Забронювати слот" : "Слот зайнятий" }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- ВКЛАДКА 2: МОЇ БРОНЮВАННЯ -->
      <div
        v-if="activeTab === 'my'"
        class="bg-white rounded-2xl shadow-sm border border-slate-200 overflow-hidden"
      >
        <div
          class="px-6 py-4 border-b border-slate-200 flex items-center justify-between"
        >
          <h2 class="font-bold text-slate-900">
            Історія та активні бронювання
          </h2>
          <button
            @click="fetchMyBookings"
            class="text-sm text-indigo-600 hover:underline"
          >
            Оновити
          </button>
        </div>

        <div v-if="loadingBookings" class="p-8 text-center text-slate-500">
          Завантаження...
        </div>
        <div
          v-else-if="myBookings.length === 0"
          class="p-12 text-center text-slate-500"
        >
          У вас ще немає жодного бронювання. Оберіть аудиторію на першій
          вкладці.
        </div>
        <div v-else class="overflow-x-auto">
          <table class="w-full text-left border-collapse">
            <thead>
              <tr
                class="bg-slate-50 text-xs font-semibold text-slate-500 uppercase border-b border-slate-200"
              >
                <th class="py-3.5 px-6">ID</th>
                <th class="py-3.5 px-6">Аудиторія</th>
                <th class="py-3.5 px-6">Часовий інтервал (Kyiv)</th>
                <th class="py-3.5 px-6">Мета</th>
                <th class="py-3.5 px-6">Статус</th>
                <th class="py-3.5 px-6 text-right">Дія</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 text-sm">
              <tr
                v-for="b in myBookings"
                :key="b.id"
                class="hover:bg-slate-50/60"
              >
                <td class="py-4 px-6 font-mono text-slate-400">#{{ b.id }}</td>
                <td class="py-4 px-6">
                  <div class="font-semibold text-slate-900">
                    {{ b.roomName }}
                  </div>
                  <div class="text-xs text-slate-400">
                    {{ b.building }} • {{ b.capacity }} м.
                  </div>
                </td>
                <td class="py-4 px-6 text-slate-700">
                  <div>{{ formatKyivTime(b.startTime) }}</div>
                  <div class="text-xs text-slate-400">
                    до {{ formatKyivTime(b.endTime) }}
                  </div>
                </td>
                <td class="py-4 px-6 text-slate-600">{{ b.purpose }}</td>
                <td class="py-4 px-6">
                  <span
                    class="px-2.5 py-1 rounded-full text-xs font-semibold"
                    :class="{
                      'bg-emerald-50 text-emerald-700 border border-emerald-200':
                        b.status === 'Active',
                      'bg-slate-100 text-slate-600 border border-slate-200':
                        b.status === 'Cancelled',
                      'bg-amber-50 text-amber-700 border border-amber-200':
                        b.status === 'Pending',
                      'bg-rose-50 text-rose-700 border border-rose-200':
                        b.status === 'Rejected',
                    }"
                  >
                    {{ b.status }}
                  </span>
                </td>
                <td class="py-4 px-6 text-right">
                  <button
                    v-if="b.status === 'Active'"
                    @click="cancelBooking(b.id)"
                    class="px-3 py-1.5 text-xs font-semibold text-rose-600 hover:bg-rose-50 border border-rose-200 rounded-lg transition"
                  >
                    Скасувати
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </main>

    <!-- МОДАЛЬНЕ ВІКНО ПІДТВЕРДЖЕННЯ БРОНЮВАННЯ -->
    <div
      v-if="modalRoom"
      class="fixed inset-0 z-40 bg-slate-900/50 backdrop-blur-xs flex items-center justify-center p-4"
    >
      <div
        class="bg-white rounded-2xl max-w-md w-full p-6 shadow-xl border border-slate-100"
      >
        <h3 class="text-lg font-bold text-slate-900">
          Бронювання: {{ modalRoom.name }}
        </h3>
        <p class="text-sm text-slate-500 mt-1">
          {{ modalRoom.building }} • Місткість: {{ modalRoom.capacity }} місць
        </p>

        <div
          class="mt-4 p-3.5 rounded-xl bg-slate-50 border border-slate-200 text-sm space-y-1"
        >
          <div>
            <span class="text-slate-400">Дата:</span>
            <strong>{{ selectedDate }}</strong>
          </div>
          <div>
            <span class="text-slate-400">Інтервал:</span>
            <strong>{{ startHour }} — {{ endHour }}</strong>
          </div>
        </div>

        <div class="mt-4">
          <label class="block text-sm font-medium text-slate-700 mb-1"
            >Мета бронювання</label
          >
          <input
            v-model="bookingPurpose"
            type="text"
            placeholder="Наприклад: Консультація з КРПП"
            class="w-full px-3.5 py-2 rounded-lg border border-slate-300 text-sm focus:ring-2 focus:ring-indigo-500 outline-none bg-white"
          />
        </div>

        <div class="mt-6 flex justify-end gap-2">
          <button
            @click="modalRoom = null"
            class="px-4 py-2 text-sm font-medium text-slate-600 hover:bg-slate-100 rounded-lg transition"
          >
            Скасувати
          </button>
          <button
            @click="confirmBooking"
            :disabled="submittingBooking"
            class="px-4 py-2 text-sm font-semibold bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg shadow-sm transition disabled:opacity-50"
          >
            {{ submittingBooking ? "Збереження..." : "Підтвердити" }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
