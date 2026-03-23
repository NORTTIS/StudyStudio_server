# Group AI Workflow

> **Workflow chi tiết cho Group AI — AI hỗ trợ nhóm học tập**

---

## 1. Mô Tả Nghiệp Vụ

**Group AI** là trợ lý AI hoạt động trong phạm vi một nhóm học tập. Tất cả thành viên trong nhóm đều có quyền hỏi AI về công việc, tiến độ, deadline và hiệu suất của nhóm. AI có khả năng phân tích toàn diện dựa trên dữ liệu nhóm thực tế.

### User Stories

| # | Mô tả | Trigger |
|---|--------|---------|
| US-01 | Xem tiến độ chung của nhóm | Click "Tiến độ nhóm" |
| US-02 | Xem deadline nhóm sắp tới | Hỏi AI về deadline |
| US-03 | Phân tích hiệu suất nhóm | Click "Hiệu suất nhóm" |
| US-04 | Hỏi về công việc cụ thể trong nhóm | Hỏi tự do |
| US-05 | Tìm kiếm tài liệu nhóm | Hỏi AI về tài liệu |
| US-06 | Streaming response real-time | Mỗi câu hỏi |
| US-07 | Hiển thị thông tin AI và quota | Info panel |
| US-08 | Nhận gợi ý cải thiện nhóm | Suggestions endpoint |
| US-09 | Member ghép tên vào task references | AI markdown parsing |
| US-10 | Chuyển ngôn ngữ EN/VI | Accept-Language header |

---

## 2. Sequence Diagram — Ask Group AI (Streaming)

```
Actor       Frontend            GroupAIController          AIAgent         ToolRegistry        Repository         LLM
  │              │                       │                    │                 │                │             │
  │              │ POST /api/ai/group/ask/stream             │                 │                │             │
  │ askQuestion()│───────────────────────┼───────────────────┼                 │                │             │
  │              │ {groupId, question}   │                    │                 │                │             │
  │              │              ┌────────┼─────Validate Auth─────────┼────────────────┼────────────────┼             │
  │              │              │ JWT → userId                 │                 │                │             │
  │              │              └────────┼─────────────────────────┼────────────────┼────────────────┼             │
  │              │              ┌────────┼────Check Group Membership─────┼────────────────┼────────────────┼             │
  │              │              │ IsUserInGroup(groupId, userId)?  │                 │                │             │
  │              │              │◀─────────────────────────────IsUserInGroupAsync──┼────────────────┼             │
  │              │              │         │                       │                 │                │             │
  │              │              │    [Not member → 403]        │                 │                │             │
  │              │              └────────┼─────────────────────────┼────────────────┼────────────────┼             │
  │              │              ┌────────┼─────Check Rate Limit────────┼────────────────┼────────────────┼             │
  │              │              │ todayRequests < dailyLimit?   │                 │                │             │
  │              │              │◀──────────CountTodayRequests────────┼────────────────┼             │
  │              │              └────────┼─────────────────────────┼────────────────┼────────────────┼             │
  │              │              ┌────────┼────Build AIQueryContext────────┐         │                │             │
  │              │              │ AIQueryContext { UserId, GroupId, Language }      │                │             │
  │              │              └────────┼─────────────────────────┼────────────────┼────────────────┼             │
  │              │              │ ProcessAsync(question, context)│                 │                │             │
  │              │              │──────────────────────────────┼─────────────────┼────────────────▶│             │
  │              │              │              ┌────────────────┴─GetRoleSystemPrompt─┴─┐         │             │
  │              │              │              │ GroupId != null → Default System Prompt │         │             │
  │              │              │              │ (khác với Personal: có context nhóm)     │         │             │
  │              │              │              └──────────────────────────────────────────┘         │             │
  │              │              │              ┌───────────────────DecideAction─────────────────┐     │             │
  │              │              │              │ LLM chọn: tool_call hoặc direct answer     │     │             │
  │              │              │              │─────────────────────────────────────────────▶│             │
  │              │              │              │◀────────────────────────────────────────────│             │
  │              │              │              │         AgentDecision                       │             │
  │              │              │              │         { ShouldCallTool, FinalAnswer }      │             │
  │              │              │              │                                            │             │
  │              │              │              │  [Loop: max 5 tools]                       │             │
  │              │              │              │         │                                  │             │
  │              │              │    [ShouldCallTool]         │                                  │             │
  │              │              │              │  ExecuteTool(toolName, params)            │             │
  │              │              │              │────────────────▶│ GetAllTools()      │             │
  │              │              │              │                 │──────────▶ (find tool)             │
  │              │              │              │                 │◀──────────│             │             │
  │              │              │              │   ┌─────────────ExecuteAsync───────────┐   │             │
  │              │              │              │   │ ToolRegistry → IAITool           │   │             │
  │              │              │              │   │ • get_group_performance           │   │             │
  │              │              │              │   │ • get_group_documents              │   │             │
  │              │              │              │   │ • ...                             │   │             │
  │              │              │              │   └───────────────────────────────────┘   │             │
  │              │              │              │◀────────────────────────────────────────────│             │
  │              │              │              │ history.AddCall(toolName, result)        │             │
  │              │              │              │ Loop: DecideAction again ───────────────▶│             │
  │              │              │              │         │                                  │             │
  │              │              │              │◀─────────┘  [FinalAnswer ready]           │             │
  │              │              │◀───────────────────────────AIAgentResult─────────────────┘             │
  │              │              │              ┌───────────Log AI Request─────────────┐   │             │
  │              │              │              │ AIRequestLog { UserId, TokenUsed }   │   │             │
  │              │              │              └──────────────────────────────────────┘   │             │
  │              │              │ ┌─────────────SSE: Metadata──────────────────────────┐ │             │
  │◀─────────────│              │ │ remainingRequests = dailyLimit - 1               │ │             │
  │ SSE metadata │              │ │ data: {type:"metadata", remainingRequests, ...}    │ │             │
  │◀─────────────│              │ └─────────────────────────────────────────────────┘ │             │
  │              │───────────────┤                                          │           │             │
  │◀─────────────│ SSE chunk     │ ┌─────────────SSE: Chunk──────────────────────────┐ │             │
  │ SSE chunk    │              │ │ data: {type:"chunk", content:"..."}            │ │             │
  │◀─────────────│              │ └─────────────────────────────────────────────────┘ │             │
  │              │───────────────┤                                          │           │             │
  │◀─────────────│ SSE done     │ data: {type:"done"}                          │           │             │
  │ SSE done     │              │                                          │           │             │
  │              │───────────────┤                                          │           │             │
  │              │ Response.CompleteAsync()                                 │           │             │
```

---

## 3. Sequence Diagram — Get Group Suggestions

```
Actor       Frontend            GroupAIController          AIAgent         LLM
  │              │                       │                    │             │
  │ getSuggestions(groupId)│              │                    │             │
  │──────────────┼───────────────────────┼────────────────────┼             │
  │              │ GET /suggestions/{groupId}                  │             │
  │              │──────────────────────▶│                    │             │
  │              │              ┌───────┼────Validate Auth─────────┼────     │
  │              │              │ JWT → userId                   │           │
  │              │              └───────┼─────────────────────────┼─────────┘
  │              │              ┌───────┼──Check Membership──┐   │           │
  │              │              │ IsUserInGroup(groupId)     │   │           │
  │              │              └───────┼───────────────────┘   │           │
  │              │              ┌───────┼────Check Rate Limit─────┼────     │
  │              │              │ todayRequests < dailyLimit?    │           │
  │              │              │◀──────────CountTodayRequests────┼────     │
  │              │              └───────┼─────────────────────────┼─────────┘
  │              │              ┌───────┼──────Build Context─────────┐     │
  │              │              │ AIQueryContext { GroupId }        │     │
  │              │              └───────┼─────────────────────────┘     │
  │              │              │              │                       │
  │              │              │ Hardcoded prompt:                    │
  │              │              │ "Phân tích dữ liệu nhóm này...     │
  │              │              │  3-5 gợi ý cải thiện..."           │
  │              │              │ ProcessAsync(prompt, context)       │
  │              │              │──────────────────────────────────────▶│   │
  │              │              │◀──────────────────────────────────────│   │
  │              │              │ AIAgentResult { Answer }             │   │
  │              │              │ LogAIRequest(1)                     │   │
  │◀─────────────│◀─────────────────────────────────────────────────────│   │
  │              │ 200 OK { Answer, Data: {remainingRequests} }       │   │
  │ Display 3-5  │              │              │                       │
  │ suggestions   │              │              │                       │
```

---

## 4. API Contract

### POST `/api/ai/group/ask`

**Request:**
```json
{
  "groupId": "a1b2c3d4-...",
  "question": "Tiến độ nhóm tuần này như thế nào?"
}
```

**Response:**
```json
{
  "success": true,
  "answer": "Tuần này nhóm bạn có **8 công việc** được tạo, **5 đã hoàn thành**...",
  "data": {
    "toolCallCount": 1,
    "processingTimeMs": 1856,
    "reasoningSteps": ["Analyzing question...", "Decision: Call tool 'get_group_performance'"],
    "remainingRequests": 19,
    "dailyLimit": 30
  },
  "message": "Success"
}
```

### POST `/api/ai/group/ask/stream`

**Headers:**
```
Accept: text/event-stream
Authorization: Bearer {token}
Accept-Language: vi
```

**SSE Events:**
```
data: {"type":"metadata","remainingRequests":18,"dailyLimit":30,"toolCount":1}

data: {"type":"chunk","content":"Dựa"}

data: {"type":"chunk","content":" trên dữ liệu nhóm tuần này..."}

data: {"type":"done"}
```

### GET `/api/ai/group/info/{groupId}`

**Response:**
```json
{
  "success": true,
  "data": {
    "groupId": "...",
    "aiType": "Group AI",
    "description": "Trợ lý AI hỗ trợ nhóm học tập",
    "capabilities": [
      "Trả lời câu hỏi về công việc nhóm",
      "Tổng hợp thống kê tiến độ",
      "Gợi ý deadline",
      "Phân tích hiệu suất thành viên"
    ],
    "rateLimit": {
      "remainingRequests": 18,
      "dailyLimit": 30,
      "plan": "Basic"
    }
  }
}
```

### GET `/api/ai/group/suggestions/{groupId}`

**Response:**
```json
{
  "success": true,
  "answer": "1. **3 công việc quá hạn** cần ưu tiên xử lý ngay...\n2. Thành viên U3 chưa hoạt động 5 ngày...\n3. Tỷ lệ hoàn thành tuần này thấp hơn tuần trước 12%...",
  "data": {
    "toolCallCount": 2,
    "processingTimeMs": 2340,
    "remainingRequests": 17,
    "dailyLimit": 30,
    "suggestionType": "GroupImprovement"
  }
}
```

---

## 5. Permission Flow

```
User asks Group AI
        │
        ▼
IsUserInGroup(groupId, userId)?
        │
   ┌────┴────┐
   │         │
  YES        NO
   │         │
   ▼         ▼
 Continue  403 Forbidden
   │    "Ban khong co quyen truy cap nhom nay"
   ▼
RateLimit check
```

> **Mọi thành viên trong nhóm** đều có quyền hỏi Group AI. Không phân biệt vai trò (Member, Manager, Owner).

---

## 6. Group AI Available Tools

| Tool | Purpose | Parameters |
|------|---------|-----------|
| `get_group_performance` | Chi tiết hiệu suất nhóm | `groupId` |
| `get_group_documents` | Tài liệu của nhóm | `groupId` |

> Group AI chỉ có 2 tools trực tiếp. Các tool khác (get_studio_*) chỉ available cho Master AI.

---

## 7. Frontend Integration

**File:** `mystudio/src/components/features/group/ai-qa/GroupAiQaPage.tsx`

**Key state:**
```typescript
const [messages, setMessages] = useState<Message[]>([]);
const [requestsUsedToday, setRequestsUsedToday] = useState(0);
const [remainingRequests, setRemainingRequests] = useState<number | null>(null);
const [dailyLimit, setDailyLimit] = useState<number | null>(null);
```

**SSE streaming:**
```typescript
const response = await askGroupAiStream(
    { groupId, question },
    {
        onChunk: (fullText) => {
            setMessages(prev => {
                const last = prev[prev.length - 1];
                if (last?.role === "ai") {
                    return [...prev.slice(0, -1), { role: "ai", content: fullText }];
                }
                return [...prev, { role: "ai", content: fullText }];
            });
        },
        onMetadata: (metadata) => {
            if (metadata.remainingRequests != null) {
                setRemainingRequests(metadata.remainingRequests);
                if (metadata.dailyLimit != null) {
                    setRequestsUsedToday(metadata.dailyLimit - metadata.remainingRequests);
                }
            }
        }
    }
);
```

**Task reference parsing:**
```typescript
// AI response: "Bạn nên xử lý **#123** ngay"
function renderTaskReferences(content: string) {
    // Parse **#123** → clickable task button
    // Triggers TaskDetailModal open with task #123
}
```

---

## 8. File Reference

```
Backend
├── Controllers/GroupAIController.cs           # Endpoints, auth, membership
├── Services/AI/AIAgent.cs                   # ReAct loop
├── Services/AI/Tools/
│   ├── GetGroupPerformanceTool.cs            # Group performance
│   └── GetGroupDocumentsTool.cs              # Group documents
└── Docs/AI/AI-GROUP-WORKFLOW.md            # This file

Frontend
└── mystudio/src/
    ├── api/group.api.ts                     # askGroupAiStream()
    └── components/features/group/
        ├── ai-qa/GroupAiQaPage.tsx         # Chat UI
        └── analytic/GroupAnalyticPage.tsx  # Analytics charts
```
