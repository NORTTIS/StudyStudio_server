# StudyStudio — Backend API

<p align="center">
    <img src="logo.png" alt="StudyStudio" width="420">
</p>

<p align="center">
    <strong>Không gian học tập dành cho sinh viên</strong>
</p>

<p align="center">
    <a href="https://github.com/your-username/StudyStudio"><img src="https://img.shields.io/badge/ASP.NET_Core-8.0-512BD4?style=for-the-badge&logo=.net" alt="ASP.NET Core"></a>
    <a href="https://github.com/your-username/StudyStudio"><img src="https://img.shields.io/badge/C%23-12-239120?style=for-the-badge&logo=csharp" alt="C#"></a>
    <a href="https://github.com/your-username/StudyStudio"><img src="https://img.shields.io/badge/PostgreSQL-17-336791?style=for-the-badge&logo=postgresql" alt="PostgreSQL"></a>
    <a href="https://github.com/your-username/StudyStudio"><img src="https://img.shields.io/badge/Redis-7-DC382D?style=for-the-badge&logo=redis" alt="Redis"></a>
    <a href="https://github.com/your-username/StudyStudio"><img src="https://img.shields.io/badge/Docker-ready-2496ED?style=for-the-badge&logo=docker" alt="Docker"></a>
    <a href="https://github.com/your-username/StudyStudio"><img src="https://img.shields.io/badge/JWT-Auth-B00000?style=for-the-badge&logo=json-web-tokens" alt="JWT"></a>
    <a href="https://github.com/your-username/StudyStudio"><img src="https://img.shields.io/badge/Gemini-AI-4285F4?style=for-the-badge&logo=google-gemini" alt="Gemini"></a>
    <a href="https://github.com/your-username/StudyStudio"><img src="https://img.shields.io/badge/Qdrant-VectorDB-33B5E5?style=for-the-badge" alt="Qdrant"></a>
    <a href="https://github.com/your-username/StudyStudio"><img src="https://img.shields.io/badge/SignalR-Real--Time-512BD4?style=for-the-badge&logo=.net" alt="SignalR"></a>
    <a href="https://github.com/your-username/StudyStudio"><img src="https://img.shields.io/badge/License-MIT-blue?style=for-the-badge" alt="MIT License"></a>
</p>

---

## 📖 Table of Contents

1. [About the Backend](#about-the-backend)
2. [Tech Stack](#tech-stack)
3. [Architecture](#architecture)
4. [Project Structure](#project-structure)
5. [Getting Started](#getting-started)
6. [Installation](#installation)
7. [API Documentation](#api-documentation)
8. [Authentication](#authentication)
9. [Real-time — SignalR](#real-time--signalr)
10. [AI System](#ai-system)
11. [Database](#database)
12. [Background Jobs](#background-jobs)
13. [Monitoring](#monitoring)
14. [Environment Variables](#environment-variables)
15. [Testing](#testing)
16. [Documentation](#documentation)
17. [Team](#team)
18. [Changelog](#changelog)

---

## 🧠 About the Backend

**StudyStudio Backend** is an ASP.NET Core 8.0 Web API that powers the StudyStudio collaborative workspace platform. It handles all business logic, data persistence, real-time communication, AI processing, and external integrations.

**Core responsibilities:**
- 🛡️ Authentication & authorization (JWT + Google OAuth 2.0)
- 📡 Real-time group chat via SignalR
- 🤖 AI processing with Gemini + Qdrant (ReAct agent)
- 💾 Data persistence with EF Core + PostgreSQL
- ⚡ Caching with Redis
- 💳 Payment processing with PayOS
- ☁️ File storage via Backblaze B2
- 📊 Analytics and reporting
- 🛠️ System administration
- 📈 Prometheus metrics and health checks

---

## 💻 Tech Stack

<p align="center">

| | | |
|:---:|:---:|:---:|
| ![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-8.0-512BD4?style=for-the-badge&logo=.net) | ![C#](https://img.shields.io/badge/C%23-12-239120?style=for-the-badge&logo=csharp) | ![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-336791?style=for-the-badge&logo=postgresql) |
| ![Redis](https://img.shields.io/badge/Redis-7-DC382D?style=for-the-badge&logo=redis) | ![Docker](https://img.shields.io/badge/Docker-ready-2496ED?style=for-the-badge&logo=docker) | ![JWT](https://img.shields.io/badge/JWT-Auth-B00000?style=for-the-badge&logo=json-web-tokens) |
| ![Gemini](https://img.shields.io/badge/Gemini-AI-4285F4?style=for-the-badge&logo=google-gemini) | ![Qdrant](https://img.shields.io/badge/Qdrant-VectorDB-33B5E5?style=for-the-badge) | ![EF Core](https://img.shields.io/badge/EF_Core-8.0.2-512BD4?style=for-the-badge&logo=entity-framework) |
| ![SignalR](https://img.shields.io/badge/SignalR-Real--Time-512BD4?style=for-the-badge&logo=.net) | ![PayOS](https://img.shields.io/badge/PayOS-Payments-00A9A5?style=for-the-badge) | ![Swagger](https://img.shields.io/badge/Swagger-6.5.0-green?style=for-the-badge) |

</p>

---

## 🏗️ Architecture

### Request Flow

```
HTTP Request
    ↓
[Middleware Pipeline]
    ├── ExceptionHandlingMiddleware  → 500 ApiResponse
    ├── AuthenticationMiddleware     → 401 Unauthorized
    ├── AuthorizationMiddleware      → 403 Forbidden
    └── LocalizationMiddleware       → i18n error messages
    ↓
Controller
    ↓ (no try-catch — middleware handles exceptions)
Service (business logic)
    ↓
Repository (data access)
    ↓
Entity Framework Core
    ↓
PostgreSQL
```

### SignalR Hub Flow

```
SignalR Client connects
    ↓
/hubs/group-discuss
    ↓
GroupDiscussHub
    ├── OnConnectedAsync     → increment connection metric
    ├── OnDisconnectedAsync → decrement connection metric
    ├── JoinGroup           → add connection to SignalR group
    ├── LeaveGroup          → remove connection from SignalR group
    ├── SendMessage         → broadcast message + handle @mentions
    ├── ReplyToMessage      → broadcast reply
    └── DeleteMessage       → soft-delete + broadcast to group
```

### AI System Architecture

```
User Question
    ↓
[AI Controller: POST /api/ai/{level}/ask]
    ↓
[AI Service: PersonalAI / GroupAI / MasterAI]
    ↓
[ReAct Agent Loop] — max 5 tool calls
    ├── Gemini API (text generation)
    └── Qdrant (context retrieval)
    ↓
Response (sync) OR SSE stream (text/event-stream)
```

---

## 📁 Project Structure

```
StudyStudio_backend/
├── README.md                    # ← This file
├── docker-compose.yml          # Full stack: API + PostgreSQL + Redis
├── prometheus.yml              # Prometheus scrape config
├── prometheus_data/           # Prometheus data directory
├── .env                        # Local environment variables
├── .env.docker.example       # Environment variables template
└── StudioStudio_Server/
    ├── Program.cs              # Application entry point
    ├── StudioStudio_Server.csproj
    │
    ├── Controllers/             # API controllers
    │   ├── AuthController.cs
    │   ├── GroupController.cs
    │   ├── StudioController.cs
    │   ├── TaskController.cs
    │   ├── DocumentController.cs
    │   ├── AI*.cs              # AI controllers (personal, group, master)
    │   ├── PaymentController.cs
    │   ├── Admin*.cs           # Admin controllers
    │   └── AnalyticsController.cs
    │
    ├── Services/               # Business logic layer
    │   ├── AuthService.cs
    │   ├── GroupService.cs
    │   ├── AI*.cs              # AI service implementations
    │   └── *.cs
    │
    ├── Repositories/           # Data access layer
    │   ├── GenericRepository.cs
    │   ├── GroupRepository.cs
    │   └── *.cs
    │
    ├── Models/                  # DTOs
    │   ├── Requests/           # Request DTOs
    │   └── Responses/          # Response DTOs
    │
    ├── Hubs/                   # SignalR hubs
    │   └── GroupDiscussHub.cs
    │
    ├── Middlewares/            # Custom middleware
    │   └── ExceptionHandlingMiddleware.cs
    │
    ├── Data/                   # EF Core layer
    │   ├── ApplicationDbContext.cs
    │   └── Configurations/     # Entity Fluent API configs
    │
    ├── Migrations/             # EF Core migrations
    │   └── *.cs
    │
    ├── Resources/              # i18n resources
    │   └── Errors/
    │       ├── errors.vi.json  # Vietnamese
    │       └── errors.en.json  # English
    │
    ├── Jobs/                    # Background hosted services
    │   ├── TaskNotificationJob.cs
    │   ├── EmbeddingJob.cs
    │   └── DeleteBackgroundJob.cs
    │
    ├── Exceptions/              # Custom exceptions
    │   └── AppException.cs
    │
    ├── HealthChecks/            # Health check endpoints
    │   ├── DatabaseHealthCheck.cs
    │   ├── RedisHealthCheck.cs
    │   └── ExternalServicesHealthCheck.cs
    │
    ├── Metrics/                 # Prometheus metrics
    │   └── *.cs
    │
    ├── Docs/                   # Documentation
    │   └── AI/
    │       ├── README.md
    │       ├── AI-ARCHITECTURE.md
    │       ├── AI-PERSONAL-WORKFLOW.md
    │       ├── AI-GROUP-WORKFLOW.md
    │       └── AI-MASTER-WORKFLOW.md
    │
    └── CLAUDE.md              # Backend architecture guidance
```

---

## 🚀 Getting Started

### Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | 8.0 | [Download](https://dotnet.microsoft.com/download/dotnet/8.0) |
| Docker | Latest | For full stack |
| PostgreSQL | 17 | Only if not using Docker |
| Redis | 7 | Only if not using Docker |

### 🐳 Quick Start (Docker — Recommended)

```bash
# 1. Navigate to backend directory
cd StudyStudio_backend

# 2. Start all services (API + PostgreSQL + Redis)
docker compose up -d

# 3. Verify the API is running
curl http://localhost:8080/health
```

**Services available:**

| Service | URL |
|---------|-----|
| API | http://localhost:8080 |
| Swagger Docs | http://localhost:8080/swagger |
| Health Check | http://localhost:8080/health |
| Prometheus | http://localhost:9090 |
| Grafana | http://localhost:3001 |

### 💻 Local Development

```bash
# 1. Navigate to backend directory
cd StudyStudio_backend

# 2. Copy environment file
cp .env.docker .env

# 3. Edit .env with your local settings
#   - Set DB_CONNECTION_STRING to your PostgreSQL
#   - Set REDIS_CONNECTION_STRING to your Redis
#   - Add your API keys (Gemini, Google OAuth, PayOS, B2)

# 4. Restore packages
dotnet restore

# 5. Run (Kestrel on http://localhost:5006)
dotnet run

# 6. Or with hot reload
dotnet watch
```

> **Note:** Migrations run automatically on startup via `Database.Migrate()` in `Program.cs`.

### Running Tests

```bash
cd StudioStudio_Server
dotnet test
```

---

## 📥 Installation

This section covers how to set up the entire backend stack for development or deployment.

### Prerequisites

| Requirement | Version | Notes |
|-------------|---------|-------|
| **.NET SDK** | 8.0 | [Download here](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **Docker** | Latest | For containerized deployment |
| **Docker Compose** | Latest | Included with Docker Desktop |
| **PostgreSQL** | 17 | Only if running without Docker |
| **Redis** | 7 | Only if running without Docker |

### Option 1: Docker (Recommended)

> Docker is the recommended way to run the entire backend stack. It starts the API, PostgreSQL, Redis, Prometheus, and Grafana in one command.

#### Step 1: Prepare the environment file

```bash
# Navigate to backend directory
cd StudyStudio_backend

# Copy the example env file (must match the name in docker-compose.yml)
cp .env.docker.example .env.docker

# Open .env.docker and fill in all required values:
# - JWT__Key           → your secret key (min 32 chars)
# - POSTGRES_PASSWORD  → your PostgreSQL password
# - GEMINI__APIKEY     → your Gemini API key
# - Google__ClientId   → your Google OAuth client ID
# - PayOS credentials   → your PayOS keys
# - B2 credentials      → your Backblaze B2 keys
# - Qdrant credentials  → your Qdrant endpoint + API key
```

#### Step 2: Pull and start containers

```bash
# Build + start all services in the background
docker compose up -d

# To see real-time logs (press Ctrl+C to exit)
docker compose logs -f

# To see logs for a specific service
docker compose logs -f api
```

#### Step 3: Verify all services are running

```bash
# Check container status
docker compose ps
```

All containers should show `Up` status:

```
NAME                    STATUS
studiostudio_api        Up
studiostudio_postgres   Up
studiostudio_redis      Up
studiostudio_prometheus Up
studiostudio_grafana    Up
```

#### Step 4: Verify the API is responding

```bash
# Health check
curl http://localhost:8080/health

# Should return: {"status":"Healthy"}
```

#### Services available

| Service | URL | Notes |
|---------|-----|-------|
| API | http://localhost:8080 | Main backend |
| Swagger UI | http://localhost:8080/swagger | API documentation |
| Health Check | http://localhost:8080/health | System health |
| Prometheus | http://localhost:9090 | Metrics |
| Grafana | http://localhost:3001 | Dashboards (admin/admin) |

#### Common Docker commands

```bash
# Stop all services (keeps data volumes)
docker compose down

# Stop and remove all data volumes (CLEAN SLATE — deletes all data!)
docker compose down -v

# Restart a specific service
docker compose restart api

# Rebuild and restart
docker compose up -d --build

# Enter a container shell (for debugging)
docker exec -it studiostudio_api sh
docker exec -it studiostudio_postgres psql -U postgres -d study_studio
```

#### Troubleshooting

| Problem | Solution |
|---------|----------|
| Port 8080 already in use | Change port in docker-compose.yml or stop conflicting service |
| Postgres connection failed | Wait 10s for postgres to init, then retry |
| API returns 500 on first start | Check logs: `docker compose logs api` — usually missing env vars |
| Images fail to pull | Check internet connection; try `docker compose pull` |

### Option 2: Local Development

> Use this if you want to run the API directly on your machine (requires PostgreSQL and Redis installed locally).

#### Step 1: Install dependencies

| Tool | Install | Notes |
|------|---------|-------|
| .NET SDK | [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download/dotnet/8.0) | Version 8.0 required |
| PostgreSQL 17 | [postgresql.org/download](https://www.postgresql.org/download/) | Or use Docker |
| Redis 7 | [redis.io/download](https://redis.io/download/) | Or use Docker |
| Docker (optional) | [docker.com](https://docs.docker.com/desktop/install/windows-install/) | Alternative to native install |

#### Step 2: Set up the environment

```bash
cd StudyStudio_backend

# Copy the example env file
cp .env.docker.example .env

# Edit .env — fill in all your API keys and credentials
nano .env   # or use any text editor
```

**Critical values to configure:**

```env
# Database — change localhost password to match your PostgreSQL setup
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=study_studio;Username=postgres;Password=your-password

# Redis — use localhost for local dev
ConnectionStrings__Redis=localhost:6379,abortConnect=False

# JWT — generate your own secret key (min 32 characters)
JWT__Key=your-own-random-secret-key-min-32-chars

# Gemini AI — get key at https://aistudio.google.com/apikey
GEMINI__APIKEY=your-gemini-api-key

# Google OAuth — create at https://console.cloud.google.com/apis/credentials
Google__ClientId=your-client-id.apps.googleusercontent.com

# PayOS — get at https://payos.vn/
PayOS__ClientId=your-payos-client-id
PayOS__ApiKey=your-payos-api-key
PayOS__ChecksumKey=your-payos-checksum-key
```

#### Step 3: Create the database

**Option A: Using psql (command line)**
```bash
# Create the database
psql -U postgres -c "CREATE DATABASE study_studio;"
```

**Option B: Using pgAdmin or DBeaver**
Create a new database named `study_studio` with user `postgres`.

> **Note:** If `psql` is not found, add PostgreSQL 17's `bin` directory to your PATH, or use the `psql` tool bundled with pgAdmin/DBeaver.

#### Step 4: Run the API

```bash
# Restore NuGet packages
dotnet restore

# Run the application
dotnet run
# API starts on http://localhost:5006

# Or with hot reload (auto-restarts on code changes)
dotnet watch run
```

> **Note:** EF Core migrations run automatically on startup via `Database.Migrate()` in `Program.cs`. You do **not** need to run `dotnet ef` manually unless you're creating new migrations.

#### Step 5: Verify

```bash
# Health check
curl http://localhost:5006/health

# Swagger UI
open http://localhost:5006/swagger
```

#### Troubleshoot local setup

| Problem | Solution |
|---------|----------|
| `dotnet` command not found | Reinstall .NET SDK 8.0 and restart terminal |
| PostgreSQL connection refused | Start PostgreSQL service, verify port 5432 |
| Redis connection refused | Start Redis service, verify port 6379 |
| 401 on all API calls | Check JWT settings in .env; ensure token is sent in header |
| 500 on startup | Check .env for missing values; run `dotnet restore` first |

### Option 3: Linux VPS / Server Deployment

This option is for deploying on a remote Linux server (Ubuntu, Debian, CentOS, etc.).

#### Step 1: Install prerequisites on the server

```bash
# Update system
sudo apt update && sudo apt upgrade -y

# Install Docker and Docker Compose
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER

# Install Git (if not present)
sudo apt install git -y

# Verify Docker is installed
docker --version
docker compose version
```

#### Step 2: Transfer the project to the server

**Option A: Git clone**
```bash
git clone https://github.com/your-username/StudyStudio.git
cd StudyStudio/StudyStudio_backend
```

**Option B: Upload via SCP**
```bash
# From your local machine
scp -r ./StudyStudio_backend user@your-server-ip:/home/user/
```

#### Step 3: Configure environment

```bash
cd StudyStudio_backend

# Copy and configure the env file
cp .env.docker.example .env.docker
nano .env.docker
```

**Critical changes for production:**

```env
# Use strong, unique JWT key (generate with: openssl rand -base64 32)
JWT__Key=your-production-jwt-secret-key-min-32-chars

# Set a strong PostgreSQL password
POSTGRES_PASSWORD=your-strong-postgres-password
ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=study_studio;Username=postgres;Password=your-strong-postgres-password

# Change all API keys to production values
GEMINI__APIKEY=your-production-gemini-key
Google__ClientId=your-production-google-client-id
# ...

# Frontend URL — change to your domain or server IP
Frontend__BaseUrl=http://your-server-ip:3000
Frontend__VerifyURL=http://your-server-ip:3000/verify-email
Frontend__ResetPassURL=http://your-server-ip:3000/verify-reset-token
```

#### Step 4: Update docker-compose for production

Edit `docker-compose.yml` to:

```yaml
services:
  api:
    restart: always        # Auto-restart on crash
    ports:
      - "80:8080"          # Expose on port 80 (HTTP)
    # Remove Development environment if not needed
    environment:
      ASPNETCORE_ENVIRONMENT: Production
```

#### Step 5: Run with firewall

```bash
# Allow Docker through firewall
sudo ufw allow 80/tcp    # HTTP (API)
sudo ufw allow 443/tcp   # HTTPS (if using reverse proxy)
sudo ufw allow 3000/tcp  # Frontend (if running locally)
sudo ufw allow 9090/tcp  # Prometheus
sudo ufw enable

# Start the stack
docker compose up -d

# Check all containers
docker compose ps

# View API logs
docker compose logs -f api
```

#### Step 6: Verify from external access

```bash
# Test API from another machine
curl http://your-server-ip:8080/health

# Swagger docs
http://your-server-ip:8080/swagger
```

#### Step 7: (Recommended) Set up Nginx reverse proxy with SSL

```bash
# Install Nginx and Certbot
sudo apt install nginx -y
sudo apt install certbot python3-certbot-nginx -y

# Create Nginx config
sudo nano /etc/nginx/sites-available/studystudio
```

```nginx
server {
    listen 80;
    server_name your-domain.com;

    location / {
        proxy_pass http://localhost:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

```bash
# Enable site and get SSL
sudo ln -s /etc/nginx/sites-available/studystudio /etc/nginx/sites-enabled/
sudo nginx -t
sudo certbot --nginx -d your-domain.com
```

---

## 📚 API Documentation

Swagger UI is available at **http://localhost:8080/swagger** when running.

### Key API Endpoints

| Group | Endpoints | Description |
|-------|---------|-------------|
| **Auth** | `POST /api/auth/register`, `/login`, `/google`, `/refresh`, `/forgot`, `/reset-password` | Authentication |
| **User Profile** | `GET/PUT /api/user-profile`, `POST /api/change-password` | User management |
| **Groups** | `GET/POST/PUT/DELETE /api/group`, `GET /api/group/{id}/detail` | Group CRUD |
| **Studio** | `GET/POST/PUT/DELETE /api/studio`, `POST /api/studio/{id}/members/batch-assign` | Workspace management |
| **Tasks** | `POST/GET/PUT/DELETE /api/tasks`, `PUT /api/tasks/{id}/restore` | Task management |
| **Documents** | `POST /api/documents/request-upload`, `GET /api/documents/{id}/download` | Document management |
| **AI** | `POST /api/ai/personal/group/master/ask`, `/ask/stream` | AI Q&A (SSE) |
| **Payments** | `POST /api/payment/create`, `GET /api/payment/{id}/status` | PayOS integration |
| **Analytics** | `GET /api/analytics/group/{id}/*`, `/studio/{id}/*`, `/user/{id}/*` | Dashboards |
| **Admin** | `GET/PATCH /api/admin/users`, `/admin/groups`, `/admin/reports` | Administration |
| **Health** | `GET /health` | System health check |
| **Metrics** | `GET /metrics` | Prometheus metrics |

### Request/Response Format

**Request:**
```http
POST /api/auth/login
Content-Type: application/json
Accept-Language: vi

{
  "email": "user@example.com",
  "password": "password123"
}
```

**Response:**
```json
{
  "status": "success",
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "refreshToken": "..."
  }
}
```

**Error Response:**
```json
{
  "status": "error",
  "code": "AUTH001",
  "message": "Invalid email or password"
}
```

---

## 🔐 Authentication

### JWT Bearer Token

1. **Register/Login** → Returns JWT (`token`) + Refresh token (`refreshToken`)
2. Client stores JWT in **localStorage**
3. Every request includes: `Authorization: Bearer <token>`
4. On 401 → Client auto-refreshes token and retries

### Token Structure

| Claim | Description |
|-------|-------------|
| `NameIdentifier` | User ID (GUID) |
| `email` | User email |
| `IsAdmin` | Admin flag |
| `exp` | Expiration time |

### Google OAuth 2.0

```http
POST /api/auth/google
Content-Type: application/json

{ "idToken": "<google-id-token>" }
```

### RBAC Roles

| Role | Description |
|------|-------------|
| **Owner** | Full control — delete group/studio, manage all members |
| **Moderator** | Similar to Owner except ownership-level actions |
| **Member** | Create/edit tasks, participate in discussions |
| **Commenter** | Comment only |
| **Viewer** | Read-only access |

---

## 📡 Real-time — SignalR

### Hub Endpoint

```
/hubs/group-discuss
```

### Connection

```csharp
// Client (C#)
var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:8080/hubs/group-discuss", options =>
    {
        options.AccessTokenProvider = () => Task.FromResult(jwtToken);
    })
    .WithAutomaticReconnect()
    .Build();
```

### Server Events

| Event | Direction | Payload |
|-------|-----------|---------|
| `ReceiveMessage` | Server → Client | Group chat message with sender info |
| `MessageReplied` | Server → Client | Reply to a message |
| `MessageDeleted` | Server → Client | Soft-deleted message + reply count |
| `UserJoined` | Server → Client | User joined group chat |
| `UserLeft` | Server → Client | User left group chat |
| `ReceiveAnnouncement` | Server → Client | @mention notification |

### Client Methods

| Method | Description |
|--------|-------------|
| `JoinGroup(groupId)` | Join a group chat room |
| `LeaveGroup(groupId)` | Leave a group chat room |
| `SendMessage(request)` | Send a message (content + groupId) |
| `ReplyToMessage(request)` | Reply to a message (content + groupId + parentMessageId) |
| `DeleteMessage(messageId)` | Soft-delete own message or group admin deletes any |

---

## 🤖 AI System

### 3-Tier Architecture

```
Personal AI  →  Any authenticated user
Group AI    →  Group members (access to group context)
Master AI   →  Studio Owner only (10 tools)
```

### ReAct Agent Loop

```
User Question
    ↓
[Retrieve] — Fetch relevant context from Qdrant
    ↓
[Reason] — Gemini generates thought
    ↓
[Act] — Call tool (if needed) or generate response
    ↓
[Max 5 iterations] — If tool call limit reached, return partial response
    ↓
Response (sync) or SSE stream
```

### AI Endpoints

| Level | Endpoint | Streaming |
|-------|----------|-----------|
| Personal | `POST /api/ai/personal/ask` | `POST /api/ai/personal/ask/stream` |
| Group | `POST /api/ai/group/ask` | `POST /api/ai/group/ask/stream` |
| Master | `POST /api/ai/master/ask` | `POST /api/ai/master/ask/stream` |

### Master AI Tools

| # | Tool Name | Purpose |
|---|----------|---------|
| 1 | `get_studio_groups` | List groups in a studio |
| 2 | `get_studio_analytics` | Overall studio metrics |
| 3 | `get_group_comparison` | Compare groups side-by-side |
| 4 | `get_storage_usage` | Storage breakdown by group |
| 5 | `get_member_permissions` | Member permissions overview |
| 6 | `get_group_documents` | Retrieve group documents |
| 7 | `get_group_performance` | Individual group metrics |
| 8 | `compare_groups` | Detailed group analysis |
| 9 | `get_studio_health` | Studio health score |
| 10 | `get_risk_groups` | Identify at-risk groups |

### Rate Limiting

- **1 request per user per prompt** (tracked in `AIRequestLog` table)
- **Max 5 tool calls** per ReAct loop
- **SSE streaming** for real-time token delivery

> 📖 Full AI documentation: [Docs/AI/](Docs/AI/)

---

## 🗄️ Database

### Entity Framework Core

All entities use **Fluent API** configuration in `Data/Configurations/`. No data annotations on entity classes.

### Key Entities

| Entity | Description |
|--------|-------------|
| `User` | User accounts with soft delete |
| `Group` | Study groups with RBAC roles |
| `GroupMember` | Junction table for group membership |
| `Studio` | Workspace containing multiple groups |
| `TaskItem` | Tasks with Kanban status |
| `GroupTaskStatus` | Kanban columns |
| `Document` | File metadata + Qdrant vector ID |
| `AIRequestLog` | AI usage tracking |
| `Payment` | Payment transactions |
| `SubscriptionPlan` | Pricing plans |

### Migrations

Migrations run automatically on startup:

```csharp
// Program.cs
app.UseMigrations();  // Automatic: Database.Migrate()
```

To create a new migration manually:
```bash
cd StudioStudio_Server
dotnet ef migrations add <MigrationName>
```

### Soft Delete

All entities use `IsActive = false` for soft deletion. Hard deletes are not used anywhere in the system.

---

## ⚙️ Background Jobs

### TaskNotificationJob

- Runs periodically (configurable interval)
- Checks task deadlines
- Sends notifications for approaching due dates

### EmbeddingJob

- Processes uploaded documents asynchronously
- Chunks text and generates embeddings via Gemini
- Stores vectors in Qdrant

### DeleteBackgroundJob

- Handles async deletion from B2 storage
- Removes vector records from Qdrant
- Updates document status

---

## 📊 Monitoring

### Health Checks

```http
GET /health
```

Returns overall status + individual checks:

| Check | Endpoint |
|-------|---------|
| Database | PostgreSQL connectivity |
| Redis | Cache connectivity |
| External | Gemini API, Google OAuth, B2, PayOS |

### Prometheus Metrics

```http
GET /metrics
```

Exposes metrics in Prometheus format:
- `http_requests_total` — Request counter
- `http_request_duration_seconds` — Request latency histogram
- `ai_requests_total` — AI request counter
- `active_signalr_connections` — Active WebSocket connections

### Grafana Dashboards

Grafana is available at **http://localhost:3001** (login: `admin` / `admin`)

Pre-configured dashboards for:
- API request rates and latencies
- AI usage statistics
- System health
- Revenue analytics

---

## 🔑 Environment Variables

Copy from `.env.docker.example` for Docker deployment or create `.env` for local development:

```bash
# Application
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://+:8080

# Database
DB_CONNECTION_STRING=Host=postgres;Port=5432;Database=study_studio;Username=postgres;Password=postgrespassword

# Redis
REDIS_CONNECTION_STRING=localhost:6379,password=

# JWT
JWT_SECRET_KEY=<your-256-bit-secret>
JWT_EXPIRY_MINUTES=60
REFRESH_TOKEN_EXPIRY_DAYS=30

# Google OAuth
GOOGLE_CLIENT_ID=<your-google-client-id>
GOOGLE_CLIENT_SECRET=<your-google-client-secret>

# Gemini AI
GEMINI_API_KEY=<your-gemini-api-key>

# Qdrant
QDRANT_URL=http://localhost:6333
QDRANT_API_KEY=
QDRANT_COLLECTION_NAME=study_studio_docs

# Backblaze B2
B2_ENDPOINT=https://s3.us-west-000.backblazeb2.com
B2_ACCESS_KEY=<your-b2-access-key>
B2_SECRET_KEY=<your-b2-secret-key>
B2_BUCKET_NAME=study-studio-documents

# PayOS
PAYOS_CLIENT_ID=<your-payos-client-id>
PAYOS_API_KEY=<your-payos-api-key>
PAYOS_CHECKSUM_KEY=<your-payos-checksum-key>

# Email (SMTP)
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USERNAME=<your-email>
SMTP_PASSWORD=<your-email-password>
```

---

## 🧪 Testing

xUnit is used for unit testing. Test files are co-located with their source files:

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "ClassName=AuthServiceTests"
```

### Test Structure

```
StudioStudio_Server/
├── Services/
│   ├── AuthService.cs
│   └── AuthServiceTests.cs     # Co-located tests
└── ...
```

---

## 📚 Documentation

| Document | Location |
|----------|----------|
| Project overview | [Root README.md](../README.md) |
| Frontend overview | [mystudio/README.md](../mystudio/README.md) |
| AI system | [Docs/AI/](Docs/AI/) |
| AI architecture | [Docs/AI/AI-ARCHITECTURE.md](Docs/AI/AI-ARCHITECTURE.md) |
| Feature specs | [MAJOR_FEATURES.md](../MAJOR_FEATURES.md) |
| Backend architecture | [CLAUDE.md](./CLAUDE.md) |

---

## 📋 Changelog

> Lịch sử các lần cập nhật chính của backend, ghi rõ tính năng bổ sung qua từng giai đoạn.

| Version | Date | Description |
|---------|------|-------------|
| **v1.0** | 2026-02-08 | **Init Project** — Khởi tạo project ASP.NET Core 8.0, cấu hình CI/CD, Exception Handling Middleware, i18n cho error messages (vi/en) |
| **v1.1** | 2026-02-11 | **Authentication** — Đăng ký, đăng nhập, JWT token, refresh token, Google OAuth, email verification, reset password |
| **v1.2** | 2026-02-23 | **Group Collaboration** — CRUD nhóm, quản lý thành viên, phân quyền RBAC, mời/thêm/xóa thành viên |
| **v2.0** | 2026-03 | **Task & Document Management** — CRUD tasks nhóm & cá nhân, Kanban drag-drop, calendar view, trash/restore, upload Backblaze B2, Qdrant vector search |
| **v2.1** | 2026-03 | **Studio Management** — Workspace đa nhóm, batch assign CSV, random group assignment, @mention notifications |
| **v2.2** | 2026-03 | **SignalR Real-time** — Group chat, @mention notifications, real-time updates, activity logging |
| **v3.0** | 2026-03 | **AI Integration** — Gemini API, ReAct Agent, 3-tier AI (Personal → Group → Master), SSE streaming, 10 AI tools |
| **v3.1** | 2026-03-26 | **Notifications & Email** — Gửi email notification, push notifications, background jobs, announcements, reports |
| **v3.2** | 2026-03-31 | **Analytics Dashboard** — KPI metrics, productivity trends, heatmaps, group comparison, ECharts + Recharts integration |
| **v4.0** | 2026-04-07 | **Admin & Polish** — User/group management, archive nhóm/studio, accept member, admin restore, Redis cache optimization, CI/CD refinement, CodeRabbit integration |

---

## 👥 Team — SEP490-G62

| Name | Student ID | Role |
|------|-----------|------|
| Vũ Xuân Bắc | HE182325 | Technical Leader |
| Lê Tuấn Dũng | HE180884 | BA / Test Leader |
| Lê Đức Mạnh | HE180916 | PM / Developer |
| Dương Tiến Đạt | HE180717 | Design / Developer |
| Nguyễn Quang Minh | HE180190 | Developer |

**Supervisor:** Nguyễn Thị Hạnh

---

## ⬆️

Back to top: [README](#studystudio--backend-api)