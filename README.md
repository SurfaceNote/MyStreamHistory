# MyStreamHistory

A web service for tracking Twitch streamer activity with detailed analytics on games, viewers, and playthroughs.

> **This is a learning project.** I'm using it to study microservices architecture, .NET, Angular, and software development best practices. The project is actively evolving and will be continuously improved with new features.

## About

MyStreamHistory helps streamers:
- Track their streaming history
- See who visited their channel and for how long
- Manage game playthroughs (create "folders" with multiple streams of the same game)
- Analyze viewer and game statistics
- Get game information from IGDB database

## Architecture

The project is built on **microservices architecture**, where each microservice uses **Clean Architecture** internally.

### Microservices

- **API Gateway** - Single entry point
- **Auth Service** - Authentication and user management
- **Stream Service** - Stream and viewer tracking
- **Game Service** - IGDB integration, game information
- **Playthrough Service** - Game playthrough management
- **Analytics Service** - Data aggregation and statistics

### Tech Stack

**Backend:**
- .NET 8.0 (C#)
- Clean Architecture (Domain, Application, Infrastructure, API)
- CQRS pattern (MediatR)
- PostgreSQL
- RabbitMQ (MassTransit) for inter-service communication
- JWT Authentication
- Docker & Docker Compose

**Frontend:**
- Angular 17+
- SCSS
- SignalR for real-time updates

**External APIs:**
- Twitch API (OAuth, Streams, EventSub)
- IGDB API (game information)

## Current Status

### Implemented
- Basic project structure
- Shared libraries (Common, Application, Infrastructure)
- API Gateway (skeleton)
- Frontend (Angular skeleton)
- Transport Bus with MassTransit

### In Development
- Auth Service with Clean Architecture
- Database schema and migrations
- Twitch OAuth flow

### Planned
- Stream tracking and viewer monitoring
- IGDB integration
- Playthrough management
- Analytics dashboard
- Real-time updates via SignalR
- Comprehensive testing
- CI/CD pipeline
- Kubernetes deployment

## Learning Goals

Through this project, I'm learning:
- Microservices architecture patterns
- Clean Architecture and Domain-Driven Design
- CQRS and Event-Driven Architecture
- .NET 8.0 and ASP.NET Core
- Entity Framework Core
- Angular and TypeScript
- Docker and containerization
- Message queuing with RabbitMQ
- OAuth 2.0 and JWT
- Best practices for testing, logging, and monitoring

## License

MIT License

---

**Last updated:** November 2025
