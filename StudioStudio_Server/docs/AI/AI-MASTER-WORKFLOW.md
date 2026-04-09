# Master AI (Studio AI) Workflow

> **Workflow chi tiết cho Master AI — AI Toàn Studio dành cho Owner quản lý toàn bộ hoạt động**

---

## 1. Mô Tả Nghiệp Vụ

**Master AI** (Studio AI) là trợ lý AI cấp cao nhất, chỉ dành cho **Owner của Studio**. Với quyền truy cập toàn bộ dữ liệu studio, Master AI có thể:
- Tổng hợp tình hình tất cả các nhóm
- So sánh hiệu suất giữa các nhóm
- Phân tích rủi ro toàn studio
- Đề xuất chiến lược điều phối
- Giám sát health của studio

### User Stories

| # | Mô tả | Trigger |
|---|--------|---------|
| US-01 | Dashboard AI với stat cards thực | AIMaster page load |
| US-02 | Xem tổng quan tất cả nhóm | "Tóm tắt studio hôm nay" |
| US-03 | Xem analytics từng nhóm | Analytics tab |
| US-04 | Phân tích rủi ro các nhóm | "Phân tích rủi ro" |
| US-05 | So sánh hiệu suất nhóm | "Xếp hạng nhóm" |
| US-06 | Streaming chat real-time với AI | Chat input |
| US-07 | Xem số request còn lại | Header stat cards |
| US-08 | Quick actions với prompt có sẵn | Quick action buttons |
| US-09 | Insights điều hành tự động | Dashboard insights panel |
| US-10 | AI → Kanban deep-link | Task reference in AI response |

---

## 2. Sequence Diagram — Ask Master AI (Streaming)

```
Actor        Frontend           MasterAIController         AIAgent          ToolRegistry          Repository          LLM
  │              │                       │                    │                   │                 │               │
  │              │ POST /api/ai/master/ask/stream             │                   │                 │               │
  │ askQuestion()│───────────────────────┼───────────────────┼                   │                 │               │
  │              │ {studioId, question}  │                    │                   │                 │               │
  │              │              ┌────────┼─────────Validate Auth─────────────┼─────────────────┼─────────────────┼               │
  │              │              │ JWT → userId                     │                   │                 │               │
  │              │              └────────┼─────────────────────────┼─────────────────┼─────────────────┼─────────────────┤               │
  │              │              ┌────────┼─────────Validate Studio Owner────────┐│                 │               │
  │              │              │ studio = GetById(studioId)    │                 │                 │               │
  │              │              │◀─────────────────────────────GetByIdAsync──┤                 │               │
  │              │              │       │                       │                 │                 │               │
  │              │              │   [studio == null → 404]     │                 │                 │               │
  │              │              │   [studio.OwnerId != userId → 403] │          │                 │               │
  │              │              │       │                       │                 │                 │               │
  │              │              │   ┌───┴──Check Rate Limit─────────────┐  │                 │               │
  │              │              │   │ todayRequests < dailyLimit?       │  │                 │               │
  │              │              │   │◀────────CountTodayRequests───────┼─────────────────┤               │
  │              │              │   └─────────────────────────────────┼─────────────────┼─────────────────┤               │
  │              │              │   ┌───────Build AIQueryContext───────────┐ │                 │               │
  │              │              │   │ AIQueryContext { UserId, StudioId, Language } │  │               │
  │              │              │   └─────────────────────────────────┼─────────────────┼─────────────────┤               │
  │              │              │   ProcessAsync(question, context)      │                 │               │
  │              │              │────────────────────────────────────┼─────────────────┼────────────────▶│               │
  │              │              │              ┌─────────────────────────┴─GetRoleSystemPrompt─┴─────┐     │               │
  │              │              │              │ StudioId != null → Owner System Prompt (VI/EN)   │     │               │
  │              │              │              │ (Rõ ràng hơn Personal/Group: Owner quản lý Studio)│     │               │
  │              │              │              └────────────────────────────────────────────────────┘     │               │
  │              │              │              ┌───────────────────────DecideAction───────────────────────┐ │               │
  │              │              │              │ LLM chọn: tool_call hoặc direct answer              │ │               │
  │              │              │              │─────────────────────────────────────────────────────▶│ │               │
  │              │              │              │◀────────────────────────────────────────────────────│ │               │
  │              │              │              │              AgentDecision { ShouldCallTool, ... }   │ │               │
  │              │              │              │                                                       │ │               │
  │              │              │              │  [Loop: max 5 tools]                              │ │               │
  │              │              │    [ShouldCallTool]          │                                       │ │               │
  │              │              │              │  ExecuteTool(toolName, params)  │                   │ │               │
  │              │              │              │─────────────────────────────────▶│ GetAllTools()   │ │               │
  │              │              │              │                                │────────▶ (find tool) │          │
  │              │              │              │                                │◀────────│           │ │               │
  │              │              │              │  toolResult = ExecuteAsync(context, params)       │ │               │
  │              │              │              │────────────────────────────────────────▶│ Repository  │ │               │
  │              │              │              │                                │◀───────────────────────────────│││
  │              │              │              │  history.AddCall(toolName, result)  │               │ │               │
  │              │              │              │  Loop: DecideAction ──────────────▶│               │ │               │
  │              │              │              │◀──────────────────────────────────│               │ │               │
  │              │              │              │           [FinalAnswer ready]    │               │ │               │
  │              │              │◀───────────────────────────────────────────────│               │ │               │
  │              │              │ AIAgentResult { Answer, ToolCallCount, ... }   │               │ │               │
  │              │              │   ┌──────────Log AI Request──────────────────────┐  │               │ │               │
  │              │              │   │ AIRequestLog { UserId, TokenUsed=1×100 }    │  │               │ │               │
  │              │              │   └────────────────────────────────────────────┘  │               │ │               │
  │              │              │ ┌──────────────SSE: Metadata──────────────────────────┐ │               │ │               │
  │◀─────────────│              │ │ remainingRequests = dailyLimit - 1              │ │               │ │               │
  │ SSE metadata │              │ │ data: {type:"metadata", remainingRequests, dailyLimit, toolCount} │ │               │ │
  │◀─────────────│              │ └──────────────────────────────────────────────────┘ │               │ │               │
  │              │───────────────┤                                          │          │               │ │               │
  │◀─────────────│ SSE chunk    │ ┌──────────────SSE: Chunk──────────────────────────┐ │               │ │               │
  │ SSE chunk    │              │ │ data: {type:"chunk", content:"..."}             │ │               │ │               │
  │◀─────────────│              │ └──────────────────────────────────────────────────┘ │               │ │               │
  │              │───────────────┤                                          │          │               │ │               │
  │◀─────────────│ SSE done     │ data: {type:"done"}                        │          │               │ │               │
  │ SSE done     │              │                                          │          │               │ │               │
  │              │───────────────┤                                          │          │               │ │               │
  │              │ Response.CompleteAsync()                                 │          │               │ │               │
```

---

## 3. Sequence Diagram — Dashboard Load (Stats)

```
Actor        Frontend              AIMaster Component          API/Analytics
  │              │                           │                    │
  │ load()       │                           │                    │
  │──────────────┼───────────────────────────┼────────────────────┤
  │              │  useEffect: studioId ready │                    │
  │              │────────────────────────────▶│                    │
  │              │                           │                    │
  │              │  1. getUserProfile()       │                    │
  │              │────────────────────────────┼────────────────────▶│
  │              │                           │◀────────────────────│
  │              │                           │ { aiRequestsUsedToday,  │
  │              │                           │   aiDailyLimit }      │
  │              │                           │                    │
  │              │  2. getStudioGroupAnalytics(studioId)            │
  │              │────────────────────────────┼────────────────────▶│
  │              │                           │◀────────────────────│
  │              │                           │ GroupComparisonData[] │
  │              │                           │  (activeGroups,      │
  │              │                           │   overdueTasks,       │
  │              │                           │   completionRate...)   │
  │              │                           │                    │
  │              │  Derive alerts:           │                    │
  │              │  - overdue > 0 → alert   │                    │
  │              │  - completion < 50% → warn│                    │
  │              │  - completion >= 75 → ok │                    │
  │              │                           │                    │
  │              │  Derive insights:         │                    │
  │              │  - weakest groups         │                    │
  │              │  - at-risk tasks          │                    │
  │              │  - avg completion %        │                    │
  │              │                           │                    │
  │◀─────────────│                           │                    │
  │  Render stat cards, alerts, insights     │                    │
```

---

## 4. Sequence Diagram — Get Master Suggestions

```
Actor        Frontend           MasterAIController         AIAgent          LLM
  │              │                       │                    │             │
  │ getSuggestions(studioId)│              │                    │             │
  │──────────────┼───────────────────────┼────────────────────┼             │
  │              │ GET /suggestions/{studioId}                  │             │
  │              │──────────────────────▶│                    │             │
  │              │              ┌────────┼──Validate Auth───────┼─────────────┤
  │              │              │ JWT → userId                 │             │
  │              │              └────────┼─────────────────────┼─────────────┤
  │              │              ┌────────┼──Validate Owner────────┐         │
  │              │              │ studio.OwnerId == userId?   │         │
  │              │              │◀──────────GetByIdAsync───────┼         │
  │              │              │   [403 if not owner]        │         │
  │              │              └────────┼─────────────────────┼─────────────┤
  │              │              ┌────────┼──Check Rate Limit────────┤       │
  │              │              │◀────────CountTodayRequests──────┼       │
  │              │              └────────┼─────────────────────┼─────────────┤
  │              │              │ Hardcoded prompt: 5 suggestions      │
  │              │              │ "Phân tích toàn bộ dữ liệu Studio..." │
  │              │              │ ProcessAsync(prompt, context)           │
  │              │              │─────────────────────────────────────▶│
  │              │              │◀─────────────────────────────────────│
  │              │              │ AIAgentResult { Answer }             │
  │              │              │ LogAIRequest(1)                      │
  │              │◀──────────────────────────────────────────────────│
  │              │ 200 OK { Answer, Data: {remainingRequests} }       │
  │ Display 5    │              │              │                       │
  │ suggestions  │              │              │                       │
```

---

## 5. API Contract

### POST `/api/ai/master/ask`

**Request:**
```json
{
  "studioId": "e5f6a7b8-...",
  "question": "Tóm tắt tình hình tất cả các nhóm trong studio"
}
```

**Response:**
```json
{
  "success": true,
  "answer": "**Tổng quan Studio:**\n- Tổng cộng **8 nhóm** đang hoạt động...\n- **Top performer:** Nhóm Toán nâng cao (92% hoàn thành)...",
  "data": {
    "toolCallCount": 3,
    "processingTimeMs": 3420,
    "reasoningSteps": ["Analyzing question...", "Decision: Call tool 'get_studio_groups'", ...],
    "remainingRequests": 12,
    "dailyLimit": 30
  },
  "message": "Success"
}
```

### POST `/api/ai/master/ask/stream`

**SSE Events:**
```
data: {"type":"metadata","remainingRequests":11,"dailyLimit":30,"toolCount":3}

data: {"type":"chunk","content":"**Tổng quan Studio:**\n\n"}

data: {"type":"chunk","content":"- Tổng cộng **8 nhóm** đang hoạt động\n- **Top performer:"}

data: {"type":"chunk","content":" Nhóm Toán nâng cao (92% hoàn thành)\n- ⚠️ **Cần chú ý:** Nhóm CLB Văn học..."}

data: {"type":"done"}
```

### GET `/api/ai/master/info/{studioId}`

```json
{
  "success": true,
  "data": {
    "studioId": "...",
    "studioName": "StudyHub 2025",
    "aiType": "Master AI",
    "description": "Trợ lý AI quản lý toàn Studio - chỉ dành cho Owner",
    "capabilities": [
      "Tổng quan thống kê Studio",
      "Phân tích hiệu suất các nhóm",
      "Quản lý thành viên Studio",
      "Báo cáo và insights",
      "Đề xuất cải thiện"
    ],
    "restrictions": [
      "Chỉ Owner mới có quyền sử dụng",
      "Có quyền truy cập tất cả data trong Studio"
    ],
    "rateLimit": {
      "remainingRequests": 11,
      "dailyLimit": 30,
      "plan": "Premium"
    },
    "studioStats": {
      "totalGroups": 8,
      "totalMembers": 45
    }
  }
}
```

### GET `/api/ai/master/stats/{studioId}`

```json
{
  "success": true,
  "data": {
    "studioId": "...",
    "dateRange": { "start": "2025-03-22", "end": "2025-03-22" },
    "usage": {
      "totalRequests": 19,
      "totalTokens": 5700,
      "avgTokensPerRequest": 300
    },
    "rateLimit": {
      "remainingRequests": 11,
      "dailyLimit": 30,
      "plan": "Premium"
    }
  }
}
```

---

## 6. Owner Permission Flow

```
User asks Master AI
        │
        ▼
Studio exists?
  NO → 404 Not Found
  YES ↓
Is user Owner of Studio?
  NO → 403 Forbidden
  YES ↓
RateLimit check (dailyLimit)
  Fail → 429 Too Many Requests
  Pass ↓
AIAgent.ProcessAsync()
        │
        ▼
  10 Tools available:
  • get_studio_groups
  • get_studio_analytics
  • get_group_comparison
  • get_storage_usage
  • get_member_permissions
  • get_group_documents
  • get_group_performance
  • compare_groups
  • get_studio_health
  • get_risk_groups
```

---

## 7. Master AI Toolset (Full 10 Tools)

| Tool | Scope | Description | Used For |
|------|-------|-------------|---------|
| `get_studio_groups` | Studio | Danh sách + thống kê tất cả nhóm | Tổng quan, xếp hạng |
| `get_studio_analytics` | Studio | Analytics tổng quan | Dashboard stats |
| `get_group_comparison` | Studio | So sánh nhiều nhóm | Phân tích so sánh |
| `get_storage_usage` | Studio | Dung lượng lưu trữ | Health check |
| `get_member_permissions` | Studio | Quyền thành viên | Phân tích quyền hạn |
| `get_group_documents` | Group | Tài liệu nhóm | Deep dive nhóm |
| `get_group_performance` | Group | Hiệu suất chi tiết nhóm | Nhóm cụ thể |
| `compare_groups` | Studio | So sánh hiệu suất nhiều nhóm | Xếp hạng |
| `get_studio_health` | Studio | Health check toàn studio | Alerts |
| `get_risk_groups` | Studio | Nhóm có nguy cơ (HIGH/MEDIUM/LOW) | Risk alerts |

---

## 8. Dashboard Auto-Derived Components

Master dashboard tự động derive data từ `getStudioGroupAnalytics()` (không cần gọi AI):

### Stat Cards
```typescript
const activeGroups = groups.length;
const totalOverdue = groups.reduce((s, g) => s + (g.overdueTasksCount ?? 0), 0);
const warningGroups = groups.filter(g => (g.completionRate ?? 0) < 50).length;
const avgCompletion = activeGroups > 0
    ? Math.round(groups.reduce((s, g) => s + (g.completionRate ?? 0), 0) / activeGroups)
    : 0;
```

### Attention Card Alerts
```typescript
const alerts: AlertItem[] = [];
if (totalOverdue > 0) alerts.push({
    title: `${totalOverdue} task quá hạn`,
    desc: "Ưu tiên xử lý các công việc đã quá hạn.",
    tone: "orange"
});
if (warningGroups > 0) alerts.push({
    title: `${warningGroups} nhóm có tiến độ chậm`,
    desc: `Có ${warningGroups} nhóm có tỷ lệ hoàn thành dưới 50%.`,
    tone: "violet"
});
if (avgCompletion >= 75) alerts.push({
    title: `Tỷ lệ hoàn thành đạt ${avgCompletion}%`,
    desc: "Tiến độ chung của studio tốt.",
    tone: "default"
});
```

### Quick Prompts
```typescript
const quickPrompts = [
    "Tóm tắt tình hình studio hôm nay",
    "Nhóm nào đang hoạt động nhiều nhất?",
    "Phân tích tiến độ task của studio",
    "Những vấn đề cần ưu tiên xử lý?"
];
```

---

## 9. File Reference

```
Backend
├── Controllers/MasterAIController.cs              # Studio Owner AI endpoints
├── Services/AI/AIAgent.cs                       # ReAct loop, owner prompt
├── Services/AI/Tools/
│   ├── GetStudioGroupsTool.cs
│   ├── GetStudioAnalyticsTool.cs
│   ├── GetMemberPermissionsTool.cs
│   ├── GetGroupDocumentsTool.cs
│   ├── GetGroupPerformanceTool.cs
│   ├── CompareGroupsTool.cs
│   ├── GetStudioHealthTool.cs
│   └── GetRiskGroupsTool.cs
└── Docs/AI/AI-MASTER-WORKFLOW.md             # This file

Frontend
└── mystudio/src/
    ├── api/studio-ai.ts                      # askStudioAiStream()
    ├── api/analytics.ts                      # getStudioGroupAnalytics()
    └── components/features/studio/
        └── studio-detail/
            └── AIMaster.tsx                  # Full dashboard UI
```
