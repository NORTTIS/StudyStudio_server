# AI System Documentation

> Tài liệu hệ thống Agentic AI trong StudyStudio

---

## 📚 Table of Contents

| Document | Mô tả |
|----------|--------|
| **[AI-ARCHITECTURE.md](./AI-ARCHITECTURE.md)** | Tổng quan kiến trúc — 3 lớp AI, ReAct loop, tool registry, rate limiting |
| **[AI-PERSONAL-WORKFLOW.md](./AI-PERSONAL-WORKFLOW.md)** | Personal AI — Ask + Stream + Suggestions + Rate limit + Frontend |
| **[AI-GROUP-WORKFLOW.md](./AI-GROUP-WORKFLOW.md)** | Group AI — Ask + Stream + Membership + Group tools |
| **[AI-MASTER-WORKFLOW.md](./AI-MASTER-WORKFLOW.md)** | Master AI — Owner-only, 10 tools, Dashboard, Studio analytics |

---

## 🔑 Key Concepts

### Ba Lớp AI

```
┌──────────────────────────────────────────────────────────────┐
│                        AIAgent                                │
│                   (ReAct Loop Engine)                        │
│                                                               │
│  ┌─────────────┐ ┌─────────────┐ ┌──────────────────────┐  │
│  │ Personal AI │ │  Group AI  │ │    Master AI         │  │
│  │ /personal/* │ │  /group/*  │ │    /master/*        │  │
│  │             │ │            │ │                      │  │
│  │ GroupId=null│ │ GroupId=   │ │  StudioId=studioId  │  │
│  │ Personal    │ │ groupId    │ │  Owner prompt        │  │
│  │ Prompt      │ │ Default    │ │  10 tools available  │  │
│  │             │ │ Prompt     │ │                      │  │
│  │ Default     │ │            │ │                      │  │
│  │ Tools       │ │ 2 Group    │ │  Full Toolset        │  │
│  │             │ │ Tools      │ │                      │  │
│  └─────────────┘ └─────────────┘ └──────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
```

### ReAct Loop (mỗi request)

```
1. LLM DecideAction → tool_call OR direct_answer
2. IF tool_call → ExecuteTool → Add to history → Loop
3. IF direct_answer → Return final answer
MAX: 5 tool calls per request
```

### SSE Streaming Protocol

```json
data: {"type":"metadata","remainingRequests":15,"dailyLimit":30,"toolCount":2}
data: {"type":"chunk","content":"Tôi đã phân tích..."}
data: {"type":"chunk","content":" và tìm thấy..."}
data: {"type":"done"}
```

---

## 📁 File Structure

```
StudioStudio_Server/
├── Controllers/
│   ├── PersonalAIController.cs        # Personal AI endpoints
│   ├── GroupAIController.cs          # Group AI endpoints
│   └── MasterAIController.cs         # Studio Owner AI endpoints
│
├── Services/AI/
│   ├── AIAgent.cs                   # Core ReAct engine
│   ├── AIToolRegistry.cs            # Tool registration
│   ├── Models/
│   │   ├── AIQueryContext.cs        # Context (UserId, GroupId, StudioId)
│   │   ├── AIAgentResult.cs         # Result (Answer, ToolCalls, Time)
│   │   └── AgentDecision.cs         # LLM decision
│   └── Tools/                        # 10 IAITool implementations
│       ├── GetStudioGroupsTool.cs
│       ├── GetStudioAnalyticsTool.cs
│       ├── GetMemberPermissionsTool.cs
│       ├── GetGroupDocumentsTool.cs
│       ├── GetGroupPerformanceTool.cs
│       ├── CompareGroupsTool.cs
│       ├── GetStudioHealthTool.cs
│       └── GetRiskGroupsTool.cs
│
└── Docs/AI/
    ├── README.md                    # This file
    ├── AI-ARCHITECTURE.md           # Architecture overview
    ├── AI-PERSONAL-WORKFLOW.md      # Personal AI workflow
    ├── AI-GROUP-WORKFLOW.md         # Group AI workflow
    └── AI-MASTER-WORKFLOW.md         # Master AI workflow
```

---

## 🧪 Quick Testing

```bash
# 1. Get JWT token (login)
curl -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","password":"..."}'

# 2. Ask Personal AI (sync)
curl -X POST http://localhost:8080/api/ai/personal/ask \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {token}" \
  -d '{"question":"Những việc cần làm hôm nay?"}'

# 3. Ask Personal AI (SSE stream)
curl -N -X POST http://localhost:8080/api/ai/personal/ask/stream \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {token}" \
  -H "Accept: text/event-stream" \
  -d '{"question":"Tóm tắt tuần này của tôi"}'

# 4. Get Personal Suggestions
curl http://localhost:8080/api/ai/personal/suggestions \
  -H "Authorization: Bearer {token}"

# 5. Ask Group AI (requires membership)
curl -X POST http://localhost:8080/api/ai/group/ask/stream \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {token}" \
  -H "Accept: text/event-stream" \
  -d '{"groupId":"{groupId}","question":"Tiến độ nhóm như thế nào?"}'

# 6. Ask Master AI (requires Studio Owner)
curl -X POST http://localhost:8080/api/ai/master/ask/stream \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {token}" \
  -H "Accept: text/event-stream" \
  -d '{"studioId":"{studioId}","question":"Tóm tắt studio hôm nay"}'
```

---

## 🔧 Configuration

```json
// appsettings.json
{
  "Gemini": {
    "ApiKey": "...",
    "Model": "gemini-2.0-flash",
    "MaxTokens": 2048,
    "Temperature": 0.7
  }
}
```

**Rate Limits:** Controlled by `UserSubscription.MaxAiRequestsPerDay` (default: 20).
