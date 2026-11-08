# AdminService: запуск и переключение

Панель управления отделена от публичного сайта: Angular CSR и ASP.NET Core доставляются одним образом. Вход выполняется по отдельной учётной записи. Существующие редактор, диагностика Twitch и административное чтение Community используют same-origin `/api`.

Целевое размещение — существующий Kubernetes-кластер Timeweb. Примеры Compose ниже остаются альтернативой для локального/отдельного серверного запуска; они не являются Kubernetes-деплоем. Манифесты AdminService для Kubernetes ещё нужно подготовить с учётом выбранной группы нод, ingress и постоянного хранилища ключей.

## Что изменилось в доступе

- AuthService больше не выдаёт роль администратора по `Jwt:AdminTwitchIds`.
- Диагностика Gateway и редакционные endpoints ContentService принимают отдельные подписанные серверные токены AdminService, а не старые Twitch JWT.
- ContentService больше не требует `ContentAuthor:TwitchId`; доступ к закрытым медиа проверяет новое право `content`.
- Обычный `/analytics/community` работает только для владельца канала. Административное чтение находится под `/admin/community` и не допускает изменений настроек/исключений.
- Старые URL `/admin/...` на основном сайте открывают соответствующую страницу поддомена. В публичном bundle больше нет маршрутов редактора и диагностики.

**Это согласованный релиз нескольких сервисов.** До обновления Gateway и ContentService новая панель не сможет работать; после их обновления прежний вход по Twitch уже не даёт административных прав. Сначала подготовьте конфигурацию, образ, сеть и сертификат, затем переключайте сервисы и публичный frontend в одном окне. Не включайте изменения production по частям без проверки доступного административного пути.

## Конфигурация

| Переменная | Где | Назначение |
|---|---|---|
| `AdminAuth__Username` | AdminService | Логин первоначального администратора |
| `AdminAuth__PasswordHash` | AdminService | Хеш PBKDF2-SHA256, не исходный пароль |
| `AdminAuth__DataProtectionKeysPath` | AdminService | Постоянный каталог ключей cookies; в образе уже настроен |
| `AdminServiceAuth__Key` | AdminService, Gateway, ContentService | Одинаковый отдельный случайный секрет минимум 64 байта; не `Jwt:Key` |
| `Services__Gateway` | AdminService | HTTP(S) origin Gateway |
| `Services__Content` | AdminService | Origin ContentService либо Gateway с content proxy |
| `Diagnostics__EnableInProduction` | Gateway | Существующий явный флаг разрешения диагностики |
| `Metrics__Token` | .NET сервисы | Секрет доступа к `/internal/metrics` |
| `METRICS_TOKEN` | Node SSR | Тот же scrape-секрет для метрик SSR |
| `Monitoring__PrometheusUrl` | AdminService | В compose — `http://prometheus:9090` |
| `Monitoring__SentryUrl` | AdminService | Необязательная HTTPS-ссылка на представление проекта Sentry |
| `ReverseProxy__KnownProxies__0` | AdminService | Доверенный IP nginx для корректного IP-based ограничения входа |
| `ReverseProxy__KnownNetworks__0` | AdminService | Доверенная сеть proxy; в compose согласована с `ADMIN_NETWORK_SUBNET` |

Хеш создаётся локально с невидимым вводом пароля:

```powershell
dotnet run --project Backend/AdminService/MyStreamHistory.AdminService.Api -- --hash-password
```

Минимальная длина пароля — 16 символов. CLI выводит только готовый хеш. Не передавайте исходный пароль аргументом командной строки, в чат или в Git. Настройки credentials и общих ключей отсутствуют в Angular bundle. При изменении username/hash старые cookies отклоняются после перезапуска с новой конфигурацией.

Сессия действует до 8 часов без автоматического продления. Logout очищает cookie браузера; индивидуального серверного реестра отзыва украденных cookies в этой версии нет. При необходимости отозвать все сессии измените конфигурацию credentials. Начальная конфигурация рассчитана на один экземпляр AdminService; при масштабировании нужны общие ключи Data Protection и распределённое ограничение входа.

## Локальная разработка

Передайте перечисленные настройки через переменные окружения или локальный secret provider. Для двух upstream-сервисов используйте только локальные/тестовые адреса, если проверяете изменяющие операции.

```powershell
dotnet run --project Backend/AdminService/MyStreamHistory.AdminService.Api --urls https://localhost:7443
```

Для работы secure cookies нужен локальный HTTPS-сертификат. Затем из `Frontend`:

```powershell
npm run start:admin
```

Dev server использует HTTPS и проксирует `/api` на `https://localhost:7443`. Откройте URL, который он напечатает. Production-сборка: `npm run build:admin`. Публичный проект по-прежнему запускается через `npm start` и собирается через `npm run build`.

## Docker и первый запуск

Сборка из корня репозитория:

```sh
docker build -f Backend/AdminService/MyStreamHistory.AdminService.Api/Dockerfile -t mystreamhistoryadmin:local .
```

Используется отдельный Docker ignore-файл: сертификаты, локальные env-файлы и build outputs не входят в контекст.

На frontend-хосте:

1. Скопируйте `.env.example` в `infra/admin/.env` для параметров Compose, а `admin.env.example` — в `infra/admin/admin.env` для credentials и адресов. Используется совместимый с Docker Compose 2.23 формат `env_file`. Хеш в `admin.env` задаётся **в одинарных кавычках**, чтобы сохранить `$` без интерполяции.
2. Создайте `infra/admin/secrets/metrics-token` с одним scrape-секретом. Значение должно совпадать с `Metrics__Token` и `METRICS_TOKEN`. Файл должен читаться пользователем контейнера Prometheus; ограничьте доступ на хосте соответствующими правами. Не включайте каталог secrets в Git или CI artifacts.
3. Узнайте имя сети существующего frontend stack и задайте `FRONTEND_NETWORK`, если оно отличается от `frontend_internalnet`.
4. Задайте `AdminServiceAuth__Key` в Gateway и ContentService через их production secrets. Настройте `Metrics__Token`, а для SSR — `METRICS_TOKEN` в `.env` frontend stack. Секреты не выводите через `docker compose config` в общедоступные логи.
5. Подготовьте DNS `admin.mystreamhistory.com` и TLS-сертификат. Портами 80/443 уже владеет web nginx, поэтому второй nginx с теми же портами не запускается.
6. Запустите stack из `infra/admin`: `docker compose up -d`. AdminService доступен хосту только на loopback `127.0.0.1:4080`; Prometheus наружу не опубликован. Том `admin-keys` сохраняет ключи сессий; `prometheus-data` — историю, ограниченную 14 днями и 2 GB.
7. Для текущего Docker nginx подключите overlay из каталога `Frontend`:

```sh
docker compose -f docker-compose.yml -f ../infra/admin/compose.frontend.yml up -d
docker compose -f docker-compose.yml -f ../infra/admin/compose.frontend.yml exec mystreamhistoryweb nginx -t
```

Overlay подключает nginx к сети `mystreamhistory-admin` и монтирует сертификат и `nginx.docker.conf`. Применяйте его только после создания сертификата и запуска AdminService, иначе nginx не стартует. Сохраните использование overlay в своей процедуре последующих frontend-деплоев. Для nginx на хосте используйте отдельный пример `nginx.conf` вместо overlay.

Compose создаёт выделенную сеть `ADMIN_NETWORK_SUBNET` и доверяет forwarded-заголовкам только из неё (а также стандартного loopback). Перед запуском проверьте, что выбранная подсеть не пересекается с существующими Docker/VPN-сетями; при необходимости измените её в `.env`. Не подключайте к этой сети произвольные приложения. Для другого размещения настройте точный IP или CIDR nginx: это необходимо и для распознавания исходного HTTPS при TLS termination, и для ограничения входа по реальному IP. Без доверенного proxy secure antiforgery cookie не сможет корректно работать за HTTP upstream. `/internal/metrics` закрыт на публичном nginx; Prometheus обращается к сервисам по внутренней сети. Для gateway/content добавьте внутренние scrape targets в `prometheus.yml` после организации сетевого доступа к их окружению; в стандартном compose уже подключены admin и SSR.

Проверка readiness на хосте:

```sh
curl -fsS -H 'Host: admin.mystreamhistory.com' http://127.0.0.1:4080/health/ready
```

После входа проверьте чтение диагностики, создание/сохранение тестового черновика, перевод, закрытый preview и медиа, просмотр Community выбранного канала, logout. Проверяйте публикацию/удаление только на выделенных тестовых материалах. Убедитесь, что прямой вызов старых API с ранее выданным admin Twitch JWT больше не даёт доступ.

## CI/CD

Сборка AdminService включена в общую матрицу `.github/workflows/backend-images.yml` рядом с остальными backend-сервисами. Для него используется корневой Docker context (`.`), поскольку образ включает Angular из `Frontend`. Перед сборкой образов выполняются проверки административных API, Community, контента, Angular и SSR-метрик. Изменения `Frontend/**` также запускают workflow из-за общих компонентов админки. Кеш Docker разделён по сервисам. На pull request образы собираются без публикации; на push в main публикуются `latest` и SHA-теги, включая `mystreamhistoryadmin`.

Workflow только собирает и публикует образы. Развёртывание AdminService в Kubernetes выполняется через манифесты и выбранный процесс применения; отдельного SSH/Compose workflow нет. Secrets, DNS/TLS и состояние кластера pipeline не меняет.

Существующий `deploy-frontend.yml` отвечает только за публичный frontend и не подключает Compose overlay админки. При выборе альтернативного Compose-размещения отдельный процесс его применения и последующих обновлений нужно настроить явно.

## Метрики

Сбор не требует открытой панели. SSR пишет структурированный лог `ssr.request`, request ID, длительность, размер HTML и временную шкалу вызовов API без raw URL, ID и query strings. Заголовок `Server-Timing` показывает полное время подготовки HTML. Метрики используют шаблоны маршрутов, гистограммы задержек и отдельные статус-классы. Перцентили вычисляются Prometheus из суммарных гистограмм.

Панель показывает SSR p50/p95, запросы/с, долю 5xx, API-вызовы на страницу, средний размер HTML, RSS/CPU SSR, задержку event loop, таблицы страниц/API и временной график. CPU — процессорное время процесса, не процент квоты контейнера; RSS — память процесса, не вся память cgroup. Для метрик контейнера/хоста понадобится отдельный exporter.

Browser Web Vitals и ошибки доступны по ссылке в Sentry. Собственного импорта LCP/INP/CLS из Sentry в эту версию панели нет; перед использованием проверьте фактический DSN и sampling production. Node SSR пишет ошибки в логи и метрики; отдельная Sentry Node-интеграция ещё не добавлена.

При локальной сборке `npm ci` сообщил об уязвимостях в существующем наборе frontend-зависимостей. Lockfile и версии библиотек в этой работе не обновлялись; перед production требуется отдельный разбор runtime/dev-зависимостей и исправлений. Автоматический `npm audit fix --force` не применялся.

Без Prometheus панель явно сообщает, что источник не подключён. Пустой ряд/NaN отображается как отсутствие данных. При недоступности источника ранее полученные данные помечаются устаревшими. После первого включения нужны минимум два scrape для вычисления rate. Проверяйте свежесть по состоянию targets; в диапазонных показателях могут оставаться исторические наблюдения после остановки сервиса.

Нагрузочный сценарий `Frontend/scripts/load-ssr.k6.js` требует явно заданные `BASE_URL` и `PATHS` тестового окружения. Он увеличивает число виртуальных пользователей до пяти. На production автоматически не запускается. Сначала сохраните исходные показатели, затем оптимизируйте загрузку профиля.

## Откат

Храните согласованный набор SHA-образов AdminService, Gateway, ContentService, AuthService и публичного frontend. Откат только одного backend-сервиса может восстановить старый путь авторизации по Twitch; при аварийном откате ограничьте административные маршруты на ingress и проверьте их напрямую. Не удаляйте тома с ключами и метриками. Если проблема в панели, откатите только её образ к совместимой версии, сохранив новые серверные политики.

## Проверки

```powershell
dotnet test Backend/AdminService/MyStreamHistory.AdminService.Tests -m:1
dotnet test Backend/Community.Tests --filter FullyQualifiedName~CommunityAccessTests -m:1
dotnet test Backend/ContentService/MyStreamHistory.ContentService.Api.Tests -m:1
```

Из `Frontend`: `npm run test:admin`, `npm run test:ssr-metrics`, `npm run test:content`, `npm run build:admin`, `npm run build`, `npm run smoke:localization`. SSR smoke использует локальный API fixture, не production.
