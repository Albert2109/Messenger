# 📬 Messenger

**Messenger** — реальнодіючий чат-додаток на ASP.NET Core MVC з підтримкою:

- приватних і групових чатів  
- обміну текстовими та файловими повідомленнями  
- онлайн/офлайн статусів  
- редагування й видалення власних повідомлень  
- профілю користувача з аватаром  

---

## 🚀 Функціонал

### 1. Аутентифікація & Реєстрація
- **Реєстрація** з валідацією через **ланцюжок відповідальності**:
  1. **ModelStateHandler** — перевіряє коректність моделі  
  2. **DuplicateEmailHandler** — унікальність email  
  3. **PasswordMatchHandler** — співпадіння паролів  
  4. **AvatarSizeHandler** — обмеження розміру аватару (≤ 2 MB)  
- **Вхід** з cookie-сесіями (`AccountController`)

### 2. Приватні чати
- Список чатів із прев’ю останнього повідомлення та часом  
- Відправка й отримання тексту та файлів  
- Редагування/видалення власних повідомлень  
- Онлайн/офлайн індикація через **SignalR**  
  - `BaseHub` → `ChatHub`  
  - **Adapter**: `IChatNotifier` + `SignalRChatNotifier`  

### 3. Групові чати
- Створення, перейменування, видалення груп (`GroupController`)
- Зміни автарки групи, 
- Додавання/видалення учасників,  
- Обмін повідомленнями й файлами  
- Синхронізація через **SignalR**  
  - `BaseHub` → `GroupHub`  
  - Повідомлення групі через `IChatNotifier`  

### 4. Профіль користувача
- Перегляд й оновлення логіна, email, аватару (`MessangerHomeController.UpdateProfile`)  
- Розсилка змін через `ProfileHub`

### 5. Пошук користувачів
- AJAX-пошук за логіном чи email (`ChatController.userSearch.js`)

---

# 📦 Локальна установка Messenger

## Вимоги:
# - .NET SDK 7.0+              (`dotnet --version`) [посилання](https://dotnet.microsoft.com/ru-ru/download)
# - Node.js & npm             (`node -v && npm -v`) [посилання](https://nodejs.org/uk/download)
# - EF Core CLI               (`dotnet tool install --global dotnet-ef`)
# - SQL Server
# - Git                       (`git --version`)

# 1. Клонуємо репозиторій:
git clone https://github.com/Albert2109/Messenger.git
cd Messenger/Messanger

# 2. Встановлюємо залежності:
dotnet restore
npm install

# 3. Оновлюємо базу даних:
dotnet ef database update

# 4. Налаштовуємо appsettings.json у корені проекту:
```
 appsettings.json '
{
  "ConnectionStrings": {
    "MessangerConnection": "Server=YOUR_SERVER;Database=Messenger;User Id=USER;Password=PWD;TrustServerCertificate=True;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "AllowedHosts": "*"
}
EOF
```

# 5. Запускаємо додаток:
dotnet run

# 6. Відкриваємо в браузері:
# Перейдіть за URL, що з’явиться в консолі (наприклад, https://localhost:5001)








### 🧩 Використані патерни

- [**Chain of Responsibility**](https://github.com/Albert2109/Messenger/pull/7) — обробка реєстрації через `HandlerBase<T>`
- [**Adapter**](https://github.com/Albert2109/Messenger/pull/8) — `IChatNotifier` уніфікує виклики SignalR
- [**Template Method**](https://github.com/Albert2109/Messenger/pull/6) — базовий хаб `BaseHub`

---

### 🏗️ Архітектура & Принципи

- **SRP (Single Responsibility)** — кожен клас виконує одну функцію  
- **OCP (Open/Closed)** — розширення через DI, без зміни існуючого коду  
- **DIP (Dependency Inversion)** — залежність від абстракцій (`IChatNotifier`, `IHandler<T>`)  
- **ISP (Interface Segregation)** — дрібні, вузькоспеціалізовані інтерфейси  
- **LSP (Liskov Substitution)** — підкласи можна заміняти без порушення поведінки  
- **DRY (Don’t Repeat Yourself)** — спільна логіка винесена у базові класи та сервіси  
- **KISS (Keep It Simple & Stupid)** — прості й зрозумілі рішення


### [🛠️ методи рефакторингу](https://github.com/Albert2109/Messenger/pull/10)

####  1.Extract Method
####  2.Extract Constant
####  3.Rename Variable
####  4.Extract Class
####  5.Introduce Parameter Object


### Кількість рядків коду


![Кількість коду](https://github.com/Albert2109/Messenger/blob/main/images/%D0%A1%D0%BD%D0%B8%D0%BC%D0%BE%D0%BA%20%D1%8D%D0%BA%D1%80%D0%B0%D0%BD%D0%B0%202025-05-20%20111255.png)








### Функціонал
![Головна сторінка](https://github.com/Albert2109/Messenger/blob/main/images/%D0%A1%D0%BD%D0%B8%D0%BC%D0%BE%D0%BA%20%D1%8D%D0%BA%D1%80%D0%B0%D0%BD%D0%B0%202025-05-20%20102146.png)
![Реєстрація](https://github.com/Albert2109/Messenger/blob/main/images/%D0%A1%D0%BD%D0%B8%D0%BC%D0%BE%D0%BA%20%D1%8D%D0%BA%D1%80%D0%B0%D0%BD%D0%B0%202025-05-20%20102158.png)
![Авторизація](https://github.com/Albert2109/Messenger/blob/main/images/%D0%A1%D0%BD%D0%B8%D0%BC%D0%BE%D0%BA%20%D1%8D%D0%BA%D1%80%D0%B0%D0%BD%D0%B0%202025-05-20%20102209.png)
![Чат 1 на 1](https://github.com/Albert2109/Messenger/blob/main/images/%D0%A1%D0%BD%D0%B8%D0%BC%D0%BE%D0%BA%20%D1%8D%D0%BA%D1%80%D0%B0%D0%BD%D0%B0%202025-05-20%20103507.png)
![Контекст меню для повідомлень](https://github.com/Albert2109/Messenger/blob/main/images/%D0%A1%D0%BD%D0%B8%D0%BC%D0%BE%D0%BA%20%D1%8D%D0%BA%D1%80%D0%B0%D0%BD%D0%B0%202025-05-20%20103519.png)
![Форма для створення групи](https://github.com/Albert2109/Messenger/blob/main/images/%D0%A1%D0%BD%D0%B8%D0%BC%D0%BE%D0%BA%20%D1%8D%D0%BA%D1%80%D0%B0%D0%BD%D0%B0%202025-05-20%20103606.png)
![Груповий чат](https://github.com/Albert2109/Messenger/blob/main/images/%D0%A1%D0%BD%D0%B8%D0%BC%D0%BE%D0%BA%20%D1%8D%D0%BA%D1%80%D0%B0%D0%BD%D0%B0%202025-05-20%20105524.png)
![Управління групою](https://github.com/Albert2109/Messenger/blob/main/images/%D0%A1%D0%BD%D0%B8%D0%BC%D0%BE%D0%BA%20%D1%8D%D0%BA%D1%80%D0%B0%D0%BD%D0%B0%202025-05-20%20105723.png)
![Контекст меню групи для звичайного користувача](https://github.com/Albert2109/Messenger/blob/main/images/%D0%A1%D0%BD%D0%B8%D0%BC%D0%BE%D0%BA%20%D1%8D%D0%BA%D1%80%D0%B0%D0%BD%D0%B0%202025-05-20%20105736.png)
![Контекст меню для адміна групи](https://github.com/Albert2109/Messenger/blob/main/images/%D0%A1%D0%BD%D0%B8%D0%BC%D0%BE%D0%BA%20%D1%8D%D0%BA%D1%80%D0%B0%D0%BD%D0%B0%202025-05-20%20105747.png)
![Пошук співрозмовника](https://github.com/Albert2109/Messenger/blob/main/images/%D0%A1%D0%BD%D0%B8%D0%BC%D0%BE%D0%BA%20%D1%8D%D0%BA%D1%80%D0%B0%D0%BD%D0%B0%202025-05-20%20105800.png)



