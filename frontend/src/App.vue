<script setup>
import { ref, onMounted } from "vue";

const API_URL = "http://localhost:5154";

const users = ref([]);
const currentUserId = ref(1);
const rooms = ref([]);
const myBookings = ref([]);

const startTime = ref("2026-11-20T10:00");
const endTime = ref("2026-11-20T11:30");
const purpose = ref("Практичне заняття");
const message = ref("");
const errorMsg = ref("");

const headers = () => ({
  "Content-Type": "application/json",
  "X-User-Id": String(currentUserId.value),
});

const toIsoWithOffset = (localDt) => `${localDt}:00+02:00`;

async function loadUsers() {
  const res = await fetch(`${API_URL}/users`);
  users.value = await res.json();
}

async function searchRooms() {
  errorMsg.value = "";
  const s = encodeURIComponent(toIsoWithOffset(startTime.value));
  const e = encodeURIComponent(toIsoWithOffset(endTime.value));
  const res = await fetch(`${API_URL}/rooms?start=${s}&end=${e}`, {
    headers: headers(),
  });
  const data = await res.json();
  if (!res.ok) {
    errorMsg.value = data.error || "Помилка перевірки доступності";
    return;
  }
  rooms.value = data;
}

async function loadMyBookings() {
  const res = await fetch(`${API_URL}/bookings/my`, { headers: headers() });
  myBookings.value = await res.json();
}

async function bookRoom(roomId) {
  message.value = "";
  errorMsg.value = "";
  const res = await fetch(`${API_URL}/bookings`, {
    method: "POST",
    headers: headers(),
    body: JSON.stringify({
      roomId,
      startTime: toIsoWithOffset(startTime.value),
      endTime: toIsoWithOffset(endTime.value),
      purpose: purpose.value,
    }),
  });
  const data = await res.json();
  if (!res.ok) {
    errorMsg.value = `[${res.status}] ${data.error}`;
    return;
  }
  message.value = `Бронювання #${data.id} успішно створено (${data.status})!`;
  await searchRooms();
  await loadMyBookings();
}

async function cancelBooking(id) {
  message.value = "";
  errorMsg.value = "";
  const res = await fetch(`${API_URL}/bookings/${id}/cancel`, {
    method: "POST",
    headers: headers(),
  });
  const data = await res.json();
  if (!res.ok) {
    errorMsg.value = `[${res.status}] ${data.error}`;
    return;
  }
  message.value = `Бронювання #${id} скасовано.`;
  await searchRooms();
  await loadMyBookings();
}

async function onUserChange() {
  message.value = "";
  errorMsg.value = "";
  await loadMyBookings();
}

onMounted(async () => {
  await loadUsers();
  await searchRooms();
  await loadMyBookings();
});
</script>

<template>
  <div
    style="
      max-width: 900px;
      margin: 20px auto;
      font-family: sans-serif;
      padding: 0 16px;
    "
  >
    <header
      style="
        display: flex;
        justify-content: space-between;
        align-items: center;
        border-bottom: 1px solid #ddd;
        padding-bottom: 12px;
      "
    >
      <h2>Сервіс бронювання аудиторій (v1.0)</h2>
      <div>
        <label>Користувач: </label>
        <select
          v-model="currentUserId"
          @change="onUserChange"
          style="padding: 6px"
        >
          <option v-for="u in users" :key="u.id" :value="u.id">
            {{ u.fullName }} ({{ u.role }})
          </option>
        </select>
      </div>
    </header>

    <div
      v-if="message"
      style="
        background: #e6ffed;
        border: 1px solid #34d058;
        padding: 10px;
        margin-top: 12px;
      "
    >
      {{ message }}
    </div>
    <div
      v-if="errorMsg"
      style="
        background: #ffeef0;
        border: 1px solid #d73a49;
        padding: 10px;
        margin-top: 12px;
      "
    >
      {{ errorMsg }}
    </div>

    <section
      style="
        margin-top: 20px;
        background: #f6f8fa;
        padding: 16px;
        border-radius: 6px;
      "
    >
      <h3>1. Пошук доступних аудиторій</h3>
      <div style="display: flex; gap: 12px; flex-wrap: wrap; align-items: end">
        <div>
          <label>Початок (Kyiv):</label><br />
          <input
            type="datetime-local"
            v-model="startTime"
            style="padding: 6px"
          />
        </div>
        <div>
          <label>Завершення (Kyiv):</label><br />
          <input type="datetime-local" v-model="endTime" style="padding: 6px" />
        </div>
        <div>
          <label>Мета заходу:</label><br />
          <input
            type="text"
            v-model="purpose"
            style="padding: 6px; width: 200px"
          />
        </div>
        <button @click="searchRooms" style="padding: 7px 14px; cursor: pointer">
          Перевірити
        </button>
      </div>

      <table
        border="1"
        cellpadding="8"
        cellspacing="0"
        style="
          width: 100%;
          margin-top: 16px;
          border-collapse: collapse;
          background: #fff;
        "
      >
        <thead>
          <tr>
            <th>Аудиторія</th>
            <th>Корпус</th>
            <th>Місткість</th>
            <th>Обладнання</th>
            <th>Статус</th>
            <th>Дія</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="r in rooms" :key="r.id">
            <td>
              <strong>{{ r.name }}</strong>
            </td>
            <td>{{ r.building }}</td>
            <td>{{ r.capacity }} місць</td>
            <td>{{ r.equipment }}</td>
            <td>
              <span
                :style="{
                  color: r.isAvailable ? 'green' : 'red',
                  fontWeight: 'bold',
                }"
              >
                {{ r.isAvailable ? "Вільна" : "Зайнята" }}
              </span>
            </td>
            <td>
              <button
                :disabled="!r.isAvailable"
                @click="bookRoom(r.id)"
                style="padding: 5px 10px; cursor: pointer"
              >
                Забронювати
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </section>

    <section style="margin-top: 24px">
      <h3>2. Мої бронювання</h3>
      <table
        border="1"
        cellpadding="8"
        cellspacing="0"
        style="width: 100%; border-collapse: collapse"
      >
        <thead>
          <tr>
            <th>ID</th>
            <th>Аудиторія</th>
            <th>Інтервал (UTC)</th>
            <th>Мета</th>
            <th>Статус</th>
            <th>Дія</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="b in myBookings" :key="b.id">
            <td>#{{ b.id }}</td>
            <td>{{ b.roomName }} ({{ b.capacity }} м.)</td>
            <td>
              {{ new Date(b.startTime).toLocaleString("uk-UA") }} —
              {{ new Date(b.endTime).toLocaleString("uk-UA") }}
            </td>
            <td>{{ b.purpose }}</td>
            <td>
              <strong>{{ b.status }}</strong>
            </td>
            <td>
              <button
                v-if="b.status === 'Active'"
                @click="cancelBooking(b.id)"
                style="padding: 5px 10px; cursor: pointer"
              >
                Скасувати
              </button>
            </td>
          </tr>
          <tr v-if="myBookings.length === 0">
            <td colspan="6" style="text-align: center">Бронювань поки немає</td>
          </tr>
        </tbody>
      </table>
    </section>
  </div>
</template>
