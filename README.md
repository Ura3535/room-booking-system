# RoomBooking — Сервіс бронювання університетських аудиторій

Навчальний проєкт із командної розробки ПЗ (ASP.NET Core .NET 10 + PostgreSQL 17 + Vue 3).  
Дозволяє студентам і викладачам перевіряти зайнятість аудиторій у часовому інтервалі `[start, end)`, створювати та скасовувати бронювання із захистом від паралельних запитів, а адміністраторам (починаючи з `v2.0`) — погоджувати заявки на великі аудиторії місткістю понад `100` місць.

---

## Навігація по документації та артефактах проєкту

- **Головний звіт-навігатор проєкту:** [REPORT.md](./REPORT.md) _(командна угода, вимоги, C4-діаграми, ADR-001/002, розбір навчального інциденту, аналіз міграції `v2.0` та таблиця особистого внеску)_
- **Трекер задач (Jira):** Проєкт [`KAN`](https://krpp.atlassian.net/jira/software/projects/KAN/boards/1?filter=&groupBy=none) («Лаба з КРПП») — задачі `KAN-4` – `KAN-14`
- **Випуски (Releases):**
  - [`v1.0`](https://github.com/Ura3535/room-booking-system/releases/tag/v1.0) — базовий функціонал (`FR-01`–`FR-04`), автентифікація, захист від конкурентного бронювання
  - [`v2.0`](https://github.com/Ura3535/room-booking-system/releases/tag/v2.0) — погодження великих аудиторій (`> 100` місць) адміністратором, міграція даних, `/health` моніторинг
- **CI/CD пайплайн:** [.github/workflows/ci.yml](./.github/workflows/ci.yml)

---

## Структура репозиторію

```text
RoomBookingSystem/
├── backend/
│   ├── RoomBooking.Api/          # REST API на .NET 10 (EF Core + Npgsql)
│   │   └── migrations/           # SQL-скрипти міграції схеми v1.0 -> v2.0
│   └── RoomBooking.Tests/        # Модульні, конкурентні та рольові тести (xUnit)
├── frontend/                     # Вебклієнт на Vue 3 + Vite + Tailwind CSS
├── migrations/                   # Резервні копії даних (backup_v1.sql)
├── .github/workflows/ci.yml      # GitHub Actions (автозбірка та тести з PostgreSQL)
├── docker-compose.yml            # Конфігурація контейнера PostgreSQL 17
├── RoomBooking.slnx              # Файл рішення .NET 10
├── REPORT.md                     # Повний звіт-навігатор по лабораторній роботі
└── README.md                     # Поточна інструкція запуску та супроводу
```

---

## Вимоги до середовища (Prerequisites)

Для локального запуску та перевірки проєкту в чистому середовищі потрібні:

1. **.NET SDK 10.0** (`dotnet --version` >= `10.0.100`)
2. **Node.js 20+** та **npm** (`node -v` >= `20.0.0`)
3. **Docker Engine** або **Docker Desktop** (для запуску бази даних PostgreSQL 17)

---

## Покрокова інструкція запуску (версія `v2.0`)

### Крок 1. Клонування репозиторію

```bash
git clone [https://github.com/Ura3535/room-booking-system.git](https://github.com/Ura3535/room-booking-system.git)
cd room-booking-system
```

### Крок 2. Запуск бази даних PostgreSQL у Docker

Контейнер запускається на порту **`5433`** хоста (щоб уникнути конфлікту з системним PostgreSQL на `5432`):

```bash
docker run -d \
  --name roombooking_db \
  --restart unless-stopped \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=postgres_dev_password \
  -e POSTGRES_DB=roombooking \
  -p 5433:5432 \
  -v pgdata:/var/lib/postgresql/data \
  postgres:17-alpine
```

_(Альтернативно, якщо встановлено плагін Compose: `docker compose up -d`)._

Переконайтеся, що контейнер працює:

```bash
docker ps
```

### Крок 3. Запуск автоматичних тестів (xUnit)

Перевірте проходження модульних, інтеграційних (конкурентне бронювання) та регресійних тестів `v2.0`:

```bash
dotnet test RoomBooking.slnx
```

Очікуваний результат: усі тести проходять без помилок (`Passed`).

### Крок 4. Запуск Backend API (.NET 10)

```bash
dotnet run --project backend/RoomBooking.Api
```

Після запуску в консолі відобразиться адреса сервера (за замовчуванням `http://localhost:5000` або `http://localhost:5142`):

- **Swagger UI (документація та тестування API):** `http://localhost:<PORT>/swagger`
- **Health-check (перевірка стану API та БД):** `http://localhost:<PORT>/health`

_(При першому запуску на порожній БД автоматично створюються таблиці та початковий набір даних: 3 користувачі та 4 аудиторії)._

### Крок 5. Запуск Frontend-інтерфейсу (Vue 3)

Відкрийте новий термінал, перевірте в `frontend/src/auth.js` значення `API_URL` (має відповідати порту запущеного Backend API з Кроку 4) та запустіть клієнт:

```bash
cd frontend
npm install
npm run dev
```

Відкрийте у браузері адресу, вказану Vite (зазвичай `http://localhost:5173`).

---

## Тестові облікові записи та сценарії перевірки

У системі попередньо створено 3 навчальні облікові записи (на екрані входу є кнопки швидкого заповнення):

| Логін      | Пароль  | Роль      | Доступні сценарії для перевірки                                                                                                                                        |
| :--------- | :------ | :-------- | :--------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `student1` | `1234`  | `Student` | Перегляд аудиторій, миттєве бронювання залів `<= 100` місць (`Active`), подання заявки на зали `> 100` місць (`Pending`), скасування власних записів                   |
| `student2` | `1234`  | `Student` | Перевірка захисту від бронювання зайнятого/очікуючого слоту (`409 Conflict`), перевірка заборони скасування чужого запису або погодження (`403 Forbidden`)             |
| `admin`    | `admin` | `Admin`   | Доступ до вкладки **«Погодження заявок»** (`v2.0`): підтвердження (`Active`) або відхилення (`Rejected`) бронювань великих аудиторій (`Велика фізична`, `Актова зала`) |

### Початковий довідник аудиторій (Seed Data)

1. **Ауд. 101** (Головний корпус) — `30 місць` → бронюється одразу (`Active`)
2. **Ауд. 205** (Головний корпус) — `80 місць` → бронюється одразу (`Active`)
3. **Велика фізична** (Корпус №2) — `150 місць` → потребує погодження адміна (`Pending`)
4. **Актова зала** (Головний корпус) — `250 місць` → потребує погодження адміна (`Pending`)

---

## Міграція даних та відновлення з резервної копії (Супровід)

### Накат міграції `v1.0 → v2.0` на існуючу базу даних

Якщо база даних була розгорнута на версії `v1.0`, перед запуском `v2.0` виконайте ідемпотентний SQL-скрипт (усі існуючі бронювання та їх статуси зберігаються):

```bash
docker exec -i roombooking_db psql -U postgres -d roombooking < backend/RoomBooking.Api/migrations/v1_to_v2_migration.sql
```

### Створення та відновлення резервної копії БД

- **Створення резервної копії:**
  ```bash
  docker exec roombooking_db pg_dump -U postgres -d roombooking > migrations/backup_v1.sql
  ```
- **Відновлення даних з резервної копії (перевірка передавання):**
  ```bash
  docker exec -i roombooking_db psql -U postgres -d roombooking -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"
  docker exec -i roombooking_db psql -U postgres -d roombooking < migrations/backup_v1.sql
  ```
  Після виконання команди перевірте наявність відновлених записів через вкладку **«Мої бронювання»** у вебінтерфейсі або запитом `SELECT * FROM "Bookings";`.
