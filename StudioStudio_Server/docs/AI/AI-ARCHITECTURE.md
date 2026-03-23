# AI System Architecture

> **Tài liệu tổng quan về kiến trúc hệ thống Agentic AI trong StudyStudio**

---

## 1. Tổng Quan

StudyStudio sử dụng mô hình **Agentic AI** — mỗi câu hỏi từ người dùng được xử lý bởi một **AIAgent** có khả năng tự quyết định gọi tools (ReAct pattern: Reasoning + Acting) để lấy dữ liệu trực tiếp từ database thay vì chỉ dựa vào knowledge có sẵn.

```
┌─────────────┐     SSE / HTTP      ┌─────────────────────────────────────────────────┐
│   Frontend  │ ───────────────▶  │              Backend ASP.NET Core 8.0           │
│   (React)   │                    │                                                  │
│             │ ◀──────────────── │  ┌─────────────────┐  ┌──────────────────────┐ │
│  AIHome     │   streaming text   │  │  Controller     │  │     AIAgent          │ │
│  Group AI   │                    │  │  - PersonalAI   │─▶│  ReAct Loop           │ │
│  AIMaster   │                    │  │  - GroupAI      │  │  - DecideAction       │ │
└─────────────┘                    │  │  - MasterAI     │  │  - ExecuteTool        │ │
                                   │  └────────┬────────┘  └──────────┬───────────┘ │
                                   │           │                      │             │
                                   │           │              ┌───────▼────────┐   │
                                   │           │              │ Tool Registry   │   │
                                   │           │              │ (10 tools)       │   │
                                   │           │              └───────┬────────┘   │
                                   │           │                      │             │
                                   │           │              ┌───────▼────────┐   │
                                   │           └─────────────▶│  PostgreSQL    │   │
                                   │                          │  Repository    │   │
                                   │                          └────────────────┘   │
                                   └─────────────────────────────────────────────────┘
```

---

## 2. Ba Lớp AI

| Lớp | Controller | Route | Role Prompt | AiQueryContext | Ai Tools |
|------|-----------|-------|------------|----------------|---------|
| **Personal AI** | `PersonalAIController` | `/api/ai/personal/*` | Personal System Prompt | `GroupId = null` | Default tools |
| **Group AI** | `GroupAIController` | `/api/ai/group/*` | Default System Prompt | `GroupId = groupId` | Default tools |
| **Master AI** | `MasterAIController` | `/api/ai/master/*` | Owner System Prompt | `StudioId = studioId` | Full toolset |

### 2.1 Context Role Detection

```csharp
// AIAgent.GetRoleSystemPrompt()
if (context.StudioId != null) → Owner System Prompt   // Master AI
else if (context.GroupId != null) → Default Prompt    // Group AI
else → Personal System Prompt                         // Personal AI
```

---

## 3. ReAct Loop — Core Agent Logic

```
User Question
      │
      ▼
┌─────────────────────────┐
│  1. DecideAction        │  ◀── LLM chọn: tool_call hoặc direct_answer
│     (LLM + System Prompt │      Dựa trên câu hỏi + available tools manifest
│      + Tools Manifest)  │
└────────────┬────────────┘
             │
    ┌────────┴────────┐
    │ ShouldCallTool?  │
    └────────┬────────┘
             │
     ┌───────┴───────┐
     │               │
   YES              NO
     │               │
     ▼               ▼
┌─────────────┐  Final Answer
│ 2. Execute   │
│    Tool      │  (via ToolRegistry → IAITool.ExecuteAsync)
└──────┬───────┘
       │
       ▼
┌─────────────┐
│ Add result  │  Tool result → history
│ to history  │
└──────┬──────┘
       │
       │  ◀── Loop (max 5 tools)
       │
       └────▶ Back to DecideAction
```

**MaxToolCalls**: 5 — ngăn infinite loop.

---

## 4. Tool Registry & Available Tools

10 tools được đăng ký trong `DependencyInjection.cs`:

| # | Tool Name | Description | Scope |
|---|-----------|-------------|-------|
| 1 | `get_studio_groups` | Danh sách nhóm trong studio | Studio |
| 2 | `get_studio_analytics` | Analytics tổng quan studio | Studio |
| 3 | `get_group_comparison` | So sánh nhiều nhóm | Studio |
| 4 | `get_storage_usage` | Dung lượng lưu trữ | Studio |
| 5 | `get_member_permissions` | Quyền thành viên | Studio |
| 6 | `get_group_documents` | Tài liệu nhóm | Group |
| 7 | `get_group_performance` | Hiệu suất nhóm | Group |
| 8 | `compare_groups` | So sánh hiệu suất nhóm | Studio |
| 9 | `get_studio_health` | Health check studio | Studio |
| 10 | `get_risk_groups` | Nhóm có nguy cơ | Studio |

> Personal AI sử dụng Default prompt + Default tools.
> Group AI sử dụng Default prompt + Group-scoped tools.
> Master AI sử dụng Owner prompt + tất cả 10 tools.

---

## 5. Response Modes

| Mode | Controller Method | Trả về | Use Case |
|------|-----------------|--------|----------|
| **Sync** | `POST /ask` | `AIResponse` JSON | API test, simple integrations |
| **Stream** | `POST /ask/stream` | SSE (`text/event-stream`) | Real-time chat UI |

### SSE Event Format

```json
// Metadata event (gửi trước)
data: {"type":"metadata","remainingRequests":15,"dailyLimit":30,"toolCount":2}

// Chunk event (gửi nhiều lần khi có text)
data: {"type":"chunk","content":"Tôi đã phân tích..."}

// Done event (hoàn thành)
data: {"type":"done"}

// Error event (nếu có lỗi)
data: {"type":"error","message":"Mô tả lỗi"}
```

---

## 6. Rate Limiting

```
AIRequestLog table
      │
      ▼
CountTodayRequests(userId, today)
      │
      ▼
Subscription.MaxAiRequestsPerDay (default: 20)
      │
      ├── Allowed: todayRequests < dailyLimit
      │
      └── Response: 429 Too Many Requests
```

- **Count**: 1 request = 1 record trong `AIRequestLog`, không phụ thuộc số tool calls bên trong.
- **Fallback**: Nếu không có subscription → `MaxAiRequestsPerDay = 20`.

---

## 7. API Endpoints Summary

```
Personal AI  ── POST /api/ai/personal/ask          → AIResponse (sync)
              ── POST /api/ai/personal/ask/stream  → SSE
              ── GET  /api/ai/personal/suggestions → AIResponse (proactive tips)

Group AI     ── POST /api/ai/group/ask             → AIResponse (sync)
              ── POST /api/ai/group/ask/stream     → SSE
              ── GET  /api/ai/group/info/{groupId} → AI info
              ── GET  /api/ai/group/suggestions/{groupId} → AIResponse

Master AI    ── POST /api/ai/master/ask            → AIResponse (sync)
              ── POST /api/ai/master/ask/stream     → SSE
              ── GET  /api/ai/master/info/{studioId} → AI info
              ── GET  /api/ai/master/stats/{studioId} → Usage stats
              ── GET  /api/ai/master/suggestions/{studioId} → AIResponse
```

---

## 8. Project Structure

```
StudioStudio_Server/
├── Controllers/
│   ├── PersonalAIController.cs      # Personal AI endpoints
│   ├── GroupAIController.cs          # Group AI endpoints
│   └── MasterAIController.cs         # Studio Master AI endpoints
│
├── Services/AI/
│   ├── AIAgent.cs                   # Core ReAct agent logic
│   ├── AIToolRegistry.cs            # Tool registration & manifest
│   ├── ILLMService.cs               # LLM interface (Gemini)
│   ├── Models/
│   │   ├── AIQueryContext.cs        # User/Group/Studio context
│   │   ├── AIAgentResult.cs         # Result model
│   │   └── AgentDecision.cs          # LLM decision model
│   ├── Tools/
│   │   ├── Interfaces/IAITool.cs     # Tool contract
│   │   ├── GetStudioGroupsTool.cs
│   │   ├── GetStudioAnalyticsTool.cs
│   │   ├── GetGroupComparisonTool.cs
│   │   ├── GetStorageUsageTool.cs
│   │   ├── GetMemberPermissionsTool.cs
│   │   ├── GetGroupDocumentsTool.cs
│   │   ├── GetGroupPerformanceTool.cs
│   │   ├── CompareGroupsTool.cs
│   │   ├── GetStudioHealthTool.cs
│   │   └── GetRiskGroupsTool.cs
│   └── DependencyInjection.cs        # DI registration
│
└── Docs/AI/
    ├── AI-ARCHITECTURE.md           # This file
    ├── AI-PERSONAL-WORKFLOW.md      # Personal AI workflow
    ├── AI-GROUP-WORKFLOW.md          # Group AI workflow
    └── AI-MASTER-WORKFLOW.md         # Master AI workflow
```
