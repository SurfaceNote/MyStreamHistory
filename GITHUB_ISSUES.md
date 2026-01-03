# GitHub Issues для MyStreamHistory Backend

## Labels для создания в GitHub:
- `backend`
- `infrastructure`
- `auth-service`
- `stream-service`
- `game-service`
- `playthrough-service`
- `analytics-service`
- `api-gateway`
- `testing`
- `documentation`
- `enhancement`
- `bug`

---

## Phase 1: Foundation

### Issue #1: Setup infrastructure and enhance shared libraries
**Labels:** `infrastructure`, `backend`

**Description:**
Setup complete development infrastructure with Docker Compose and enhance existing shared libraries with common patterns and utilities needed across all microservices.

**Tasks:**
- [ ] **Docker Compose Configuration**
  - [ ] PostgreSQL containers for each microservice database
  - [ ] RabbitMQ with management UI
  - [ ] Environment variables and secrets management
  - [ ] Network configuration between services
  - [ ] Volume persistence for databases
  - [ ] Update README with setup instructions
  
- [ ] **Shared.Application Enhancements**
  - [ ] BaseEntity abstract class with Id, CreatedAt, UpdatedAt
  - [ ] Result<T> pattern for error handling
  - [ ] PagedResult<T> for pagination
  - [ ] Common exception classes
  - [ ] IDateTime abstraction for testability
  - [ ] Common validation rules
  
- [ ] **Shared.Infrastructure Enhancements**
  - [ ] Generic repository base class
  - [ ] EF Core base DbContext with audit fields
  - [ ] Serilog configuration and structured logging
  - [ ] Health check utilities and extensions
  - [ ] Correlation ID middleware
  - [ ] Service registration extension methods
  - [ ] Integration with existing MassTransit setup

---

## Phase 2: Auth Service

### Issue #2: Implement complete Auth Service
**Labels:** `auth-service`, `backend`

**Description:**
Build complete authentication and user management microservice with Twitch OAuth integration, JWT token management, and user profile/settings functionality using Clean Architecture.

**Tasks:**
- [ ] **Project Setup**
  - [ ] Create Domain, Application, Infrastructure, API projects
  - [ ] Configure NuGet packages (MediatR, FluentValidation, AutoMapper, EF Core)
  - [ ] Setup project references
  - [ ] Add projects to solution
  
- [ ] **Domain Layer**
  - [ ] User entity with business logic
  - [ ] TwitchToken entity for OAuth management
  - [ ] UserSettings entity for privacy preferences
  - [ ] UserRole enum (Viewer/Streamer)
  - [ ] IUserRepository interface
  - [ ] Domain exceptions (UserNotFoundException, DuplicateUserException)
  - [ ] Domain events (UserRegistered, UserUpdated)
  
- [ ] **Application Layer**
  - [ ] AuthenticateWithTwitchCommand and handler
  - [ ] RefreshTokenCommand and handler
  - [ ] GetUserByIdQuery and handler
  - [ ] GetUserProfileQuery and handler
  - [ ] UpdateUserProfileCommand and handler
  - [ ] UpdateUserSettingsCommand and handler
  - [ ] All DTOs (requests/responses)
  - [ ] FluentValidation validators
  - [ ] AutoMapper profiles
  - [ ] Integration event contracts (UserRegisteredEvent, UserUpdatedEvent)
  
- [ ] **Infrastructure Layer**
  - [ ] AuthDbContext with entity configurations
  - [ ] UserRepository implementation
  - [ ] Database migrations
  - [ ] ITwitchOAuthService interface and implementation
  - [ ] OAuth flow (authorization, token exchange, refresh)
  - [ ] Get Twitch user info API call
  - [ ] IJwtTokenService interface and implementation
  - [ ] JWT generation with claims
  - [ ] JWT validation
  - [ ] MassTransit configuration and event publishing
  
- [ ] **API Layer**
  - [ ] AuthController (login, callback, refresh, logout)
  - [ ] UserController (profile, settings)
  - [ ] Request/response models
  - [ ] Global exception handling middleware
  - [ ] Program.cs configuration (DI, MediatR, EF Core, JWT, CORS, Swagger)
  - [ ] Health check endpoints
  - [ ] Swagger/OpenAPI documentation
  - [ ] appsettings.json configuration

---

## Phase 3: Game Service

### Issue #3: Implement complete Game Service with IGDB integration
**Labels:** `game-service`, `backend`

**Description:**
Build game information microservice with IGDB API integration, caching, and event-driven architecture for automatic game data population.

**Tasks:**
- [ ] **Project Setup**
  - [ ] Create all four Clean Architecture projects
  - [ ] Install NuGet packages
  - [ ] Configure project references
  
- [ ] **Domain Layer**
  - [ ] Game entity with IGDB data
  - [ ] GameGenre entity
  - [ ] GamePlatform entity (optional)
  - [ ] IGameRepository interface
  - [ ] Domain exceptions
  - [ ] Domain events (GameAdded, GameUpdated)
  
- [ ] **Application Layer**
  - [ ] SearchGamesQuery with pagination
  - [ ] GetGameByIdQuery
  - [ ] GetGameByIgdbIdQuery
  - [ ] AddGameCommand (manual/auto)
  - [ ] UpdateGameCommand
  - [ ] SyncGameFromIgdbCommand
  - [ ] DTOs and validators
  - [ ] AutoMapper profiles
  - [ ] Integration event contracts (GameAddedEvent)
  - [ ] StreamStartedEvent consumer (for auto-adding games)
  
- [ ] **Infrastructure Layer**
  - [ ] GameDbContext with entity configurations
  - [ ] GameRepository implementation
  - [ ] Database migrations
  - [ ] IIgdbService interface and implementation
  - [ ] IGDB API client (search, get by id)
  - [ ] IGDB authentication with Twitch credentials
  - [ ] Rate limiting and retry policies
  - [ ] Response caching strategy (memory/distributed)
  - [ ] MassTransit configuration
  
- [ ] **API Layer**
  - [ ] GamesController (search, get, CRUD)
  - [ ] Admin endpoints for sync
  - [ ] Program.cs configuration
  - [ ] Swagger documentation
  - [ ] Health checks

---

## Phase 4: Stream Service

### Issue #4: Implement complete Stream Service with Twitch tracking
**Labels:** `stream-service`, `backend`

**Description:**
Build stream tracking microservice with Twitch API integration, EventSub webhooks, viewer tracking, and background services for real-time monitoring.

**Tasks:**
- [ ] **Project Setup**
  - [ ] Create all four Clean Architecture projects
  - [ ] Install NuGet packages
  - [ ] Configure project references
  
- [ ] **Domain Layer**
  - [ ] Stream entity with metrics
  - [ ] ViewerSession entity
  - [ ] StreamMetrics value object
  - [ ] StreamStatus enum
  - [ ] IStreamRepository and IViewerSessionRepository interfaces
  - [ ] Domain exceptions
  - [ ] Domain events (StreamStarted, StreamEnded, ViewerJoined, ViewerLeft)
  
- [ ] **Application Layer**
  - [ ] StartStreamTrackingCommand
  - [ ] EndStreamTrackingCommand
  - [ ] GetStreamDetailsQuery
  - [ ] GetStreamerHistoryQuery with filters
  - [ ] GetStreamViewersQuery
  - [ ] GetActiveStreamsQuery
  - [ ] TrackViewerJoinCommand
  - [ ] TrackViewerLeaveCommand
  - [ ] DTOs and validators
  - [ ] AutoMapper profiles
  - [ ] Integration event contracts
  - [ ] UserRegisteredEvent consumer
  
- [ ] **Infrastructure Layer**
  - [ ] StreamDbContext with entity configurations
  - [ ] Repository implementations
  - [ ] Database migrations with indexes
  - [ ] ITwitchStreamService interface and implementation
  - [ ] Get active streams API call
  - [ ] Get stream details API call
  - [ ] Get chatters/viewers list API call
  - [ ] EventSub webhook subscription management
  - [ ] Webhook signature verification
  - [ ] Rate limiting and error handling
  - [ ] StreamMonitoringService (background service for active streams)
  - [ ] ViewerTrackingService (background service for polling viewers)
  - [ ] WebhookProcessingService (background service for events)
  - [ ] MassTransit configuration and event publishing
  
- [ ] **API Layer**
  - [ ] StreamsController (CRUD, history, active)
  - [ ] ViewersController (list, stats)
  - [ ] WebhooksController (Twitch EventSub callback)
  - [ ] Program.cs with background services registration
  - [ ] Swagger documentation
  - [ ] Health checks

---

## Phase 5: Playthrough Service

### Issue #5: Implement complete Playthrough Service
**Labels:** `playthrough-service`, `backend`

**Description:**
Build playthrough management microservice for creating game playthrough "folders" and linking streams to them with statistics aggregation.

**Tasks:**
- [ ] **Project Setup**
  - [ ] Create all four Clean Architecture projects
  - [ ] Install NuGet packages
  - [ ] Configure project references
  
- [ ] **Domain Layer**
  - [ ] GamePlaythrough entity
  - [ ] PlaythroughStream entity (many-to-many with ordering)
  - [ ] PlaythroughStats value object
  - [ ] PlaythroughStatus enum
  - [ ] IPlaythroughRepository interface
  - [ ] Domain exceptions
  - [ ] Domain events (PlaythroughCreated, PlaythroughCompleted, PlaythroughUpdated)
  
- [ ] **Application Layer**
  - [ ] CreatePlaythroughCommand
  - [ ] UpdatePlaythroughCommand
  - [ ] DeletePlaythroughCommand
  - [ ] CompletePlaythroughCommand
  - [ ] AddStreamToPlaythroughCommand
  - [ ] RemoveStreamFromPlaythroughCommand
  - [ ] ReorderStreamsCommand
  - [ ] GetPlaythroughsByStreamerQuery
  - [ ] GetPlaythroughDetailsQuery
  - [ ] GetPlaythroughStatsQuery
  - [ ] DTOs and validators
  - [ ] AutoMapper profiles
  - [ ] Integration event contracts
  - [ ] StreamEndedEvent consumer (for auto-update stats)
  
- [ ] **Infrastructure Layer**
  - [ ] PlaythroughDbContext with entity configurations
  - [ ] PlaythroughRepository implementation
  - [ ] Database migrations
  - [ ] MassTransit configuration
  - [ ] Event publishing and consuming
  
- [ ] **API Layer**
  - [ ] PlaythroughsController (full CRUD)
  - [ ] Endpoints for stream management
  - [ ] Stats endpoints
  - [ ] Program.cs configuration
  - [ ] Swagger documentation
  - [ ] Health checks

---

## Phase 6: Analytics Service

### Issue #6: Implement complete Analytics Service
**Labels:** `analytics-service`, `backend`

**Description:**
Build analytics and reporting microservice that aggregates data from all other services to provide comprehensive statistics and insights.

**Tasks:**
- [ ] **Project Setup**
  - [ ] Create all four Clean Architecture projects
  - [ ] Install NuGet packages
  - [ ] Configure project references
  
- [ ] **Domain Layer**
  - [ ] ViewerStatistics entity
  - [ ] GameStatistics entity
  - [ ] ChannelMetrics entity
  - [ ] GrowthMetrics value object
  - [ ] Repository interfaces
  
- [ ] **Application Layer**
  - [ ] GetDashboardQuery (overall channel metrics)
  - [ ] GetTopViewersQuery with filters
  - [ ] GetViewerDetailsQuery
  - [ ] GetGameStatisticsQuery
  - [ ] GetGrowthMetricsQuery (daily/weekly/monthly)
  - [ ] GetStreamComparisonQuery
  - [ ] ExportDataQuery (CSV/JSON)
  - [ ] DTOs for analytics data
  - [ ] AutoMapper profiles
  - [ ] Event consumers (StreamStarted, StreamEnded, ViewerJoined, ViewerLeft, PlaythroughCompleted)
  
- [ ] **Infrastructure Layer**
  - [ ] AnalyticsDbContext with entity configurations
  - [ ] Repository implementations
  - [ ] Database migrations
  - [ ] StatisticsAggregationService (background service)
  - [ ] Scheduled aggregation jobs
  - [ ] Event-based real-time aggregation
  - [ ] CSV/JSON export implementations
  - [ ] MassTransit configuration
  - [ ] Strategy for reading from other databases (read replicas or event sourcing)
  
- [ ] **API Layer**
  - [ ] AnalyticsController (dashboard, viewers, games, growth)
  - [ ] ExportController (data export endpoints)
  - [ ] Program.cs with background services
  - [ ] Swagger documentation
  - [ ] Health checks

---

## Phase 7: API Gateway

### Issue #7: Configure and implement API Gateway
**Labels:** `api-gateway`, `backend`

**Description:**
Configure API Gateway as a single entry point for all microservices with routing, authentication, rate limiting, CORS, and monitoring.

**Tasks:**
- [ ] **Gateway Selection and Setup**
  - [ ] Choose between Ocelot or YARP
  - [ ] Install necessary NuGet packages
  - [ ] Basic configuration setup
  
- [ ] **Routing Configuration**
  - [ ] Auth Service routes (/api/auth/*)
  - [ ] Stream Service routes (/api/streams/*)
  - [ ] Game Service routes (/api/games/*)
  - [ ] Playthrough Service routes (/api/playthroughs/*)
  - [ ] Analytics Service routes (/api/analytics/*)
  - [ ] Service discovery configuration
  - [ ] Load balancing configuration
  
- [ ] **Authentication & Authorization**
  - [ ] JWT authentication middleware
  - [ ] Token validation from Auth Service
  - [ ] Claims transformation
  - [ ] Public endpoints configuration (login, callback, webhooks)
  - [ ] Protected endpoints require valid JWT
  - [ ] Role-based access control
  
- [ ] **Cross-Cutting Concerns**
  - [ ] Rate limiting per endpoint/user
  - [ ] CORS policy configuration
  - [ ] Request/response logging
  - [ ] Correlation ID generation and propagation
  - [ ] Error handling and transformation
  - [ ] Request/response caching (optional)
  
- [ ] **Monitoring & Health**
  - [ ] Aggregate health checks from all services
  - [ ] Performance metrics collection
  - [ ] Distributed tracing setup (OpenTelemetry)
  - [ ] Circuit breaker pattern
  - [ ] Retry policies
  
- [ ] **Documentation**
  - [ ] API documentation (Swagger aggregation)
  - [ ] Gateway configuration documentation

---

## Phase 8: Testing & Quality

### Issue #8: Implement comprehensive testing
**Labels:** `testing`, `backend`

**Description:**
Setup testing infrastructure and write comprehensive unit and integration tests for all microservices.

**Tasks:**
- [ ] **Testing Infrastructure**
  - [ ] Create test projects for each microservice
  - [ ] Install xUnit, Moq, FluentAssertions, Testcontainers
  - [ ] Test base classes and helpers
  - [ ] In-memory database configuration
  - [ ] Test data builders and factories
  - [ ] Test naming conventions documentation
  
- [ ] **Unit Tests**
  - [ ] Auth Service (domain, handlers, validators, services)
  - [ ] Game Service (domain, handlers, IGDB service)
  - [ ] Stream Service (domain, handlers, Twitch service)
  - [ ] Playthrough Service (domain, handlers)
  - [ ] Analytics Service (domain, handlers, aggregation)
  - [ ] Target: 80%+ code coverage
  
- [ ] **Integration Tests**
  - [ ] WebApplicationFactory setup for each service
  - [ ] Testcontainers for PostgreSQL
  - [ ] Testcontainers for RabbitMQ
  - [ ] API endpoint tests
  - [ ] Database integration tests
  - [ ] Message bus integration tests
  - [ ] End-to-end scenario tests
  
- [ ] **Load Testing** (optional)
  - [ ] JMeter or k6 scripts
  - [ ] Performance benchmarks

---

## Phase 9: Documentation & DevOps

### Issue #9: Complete documentation and deployment setup
**Labels:** `documentation`, `backend`

**Description:**
Create comprehensive documentation for API usage, deployment, and setup CI/CD pipeline.

**Tasks:**
- [ ] **API Documentation**
  - [ ] Enhance Swagger docs with descriptions and examples
  - [ ] Request/response examples for all endpoints
  - [ ] Error response documentation
  - [ ] Authentication flow documentation
  - [ ] API versioning strategy
  - [ ] Postman collection (optional)
  
- [ ] **Deployment Documentation**
  - [ ] Environment variables for each service
  - [ ] Database migration strategy
  - [ ] Docker Compose for production
  - [ ] Kubernetes manifests (optional)
  - [ ] Health check endpoints documentation
  - [ ] Monitoring and logging setup guide
  - [ ] Backup and recovery procedures
  - [ ] Troubleshooting guide
  
- [ ] **Development Documentation**
  - [ ] Architecture overview with diagrams
  - [ ] Service communication patterns
  - [ ] Development environment setup
  - [ ] Coding standards and conventions
  - [ ] How to add a new microservice
  - [ ] How to add a new feature
  
- [ ] **CI/CD Pipeline**
  - [ ] GitHub Actions or Azure DevOps setup
  - [ ] Build pipeline for all services
  - [ ] Test execution in pipeline
  - [ ] Docker image building and pushing
  - [ ] Automated deployment to staging
  - [ ] Deployment to production workflow
  
- [ ] **Monitoring & Observability**
  - [ ] Distributed tracing (OpenTelemetry/Jaeger)
  - [ ] Centralized logging (ELK or Seq)
  - [ ] Metrics collection (Prometheus/Grafana)
  - [ ] Alerting setup
  - [ ] Dashboard creation

---

## Total: 9 Major Issues

**Milestones suggested:**
- **Milestone 1: Foundation** (Issue #1)
- **Milestone 2: Core Services** (Issues #2, #3, #4)
- **Milestone 3: Extended Services** (Issues #5, #6)
- **Milestone 4: Gateway & Quality** (Issues #7, #8, #9)


