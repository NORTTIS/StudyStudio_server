# Personal AI Workflow

> **Workflow chi tiết cho Personal AI — AI Cá Nhân hỗ trợ công việc cá nhân**

---

## 1. Mô Tả Nghiệp Vụ

**Personal AI** là trợ lý AI cá nhân hoạt động trong phạm vi riêng tư của từng người dùng. Không ai khác có quyền truy cập dữ liệu cá nhân. AI có thể trả lời câu hỏi về công việc, tiến độ, deadline, và đưa ra gợi ý cải thiện năng suất.

### User Stories

| # | Mô tả | Trigger |
|---|--------|---------|
| US-01 | Xem danh sách công việc cần làm hôm nay | Click "Việc hôm nay" |
| US-02 | Xem công việc đã quá hạn | Hỏi AI về overdue |
| US-03 | Tóm tắt tiến độ tuần này | Click "Tóm tắt tuần" |
| US-04 | Xem deadline sắp tới | Click "Deadline sắp tới" |
| US-05 | Nhận gợi ý cải thiện năng suất | Auto trên AIHome |
| US-06 | Streaming response real-time | Mỗi câu hỏi |
| US-07 | Xem số request còn lại | Sau mỗi response |
| US-08 | Đạt giới hạn → thông báo | Khi hết quota |
| US-09 | Chuyển ngôn ngữ EN/VI | Accept-Language header |
| US-10 | Quick prompts gợi ý sẵn | AIHome UI |

---

## 2. Sequence Diagram — Ask Personal AI (Streaming)

```
Actor       Frontend           PersonalAIController       AIAgent         ToolRegistry       Repository        LLM
  │              │                      │                    │                  │                │             │
  │ askQuestion()│                      │                    │                  │                │             │
  │──────────────┼──────────────────────┼                    │                  │                │             │
  │              │ POST /ask/stream     │                    │                  │                │             │
  │              │ {question}            │                    │                  │                │             │
  │              │──────────────────────▶                    │                  │                │             │
  │              │              ┌───────Validate Auth────────┼──────────────────┼────────────────┼             │
  │              │              │ (JWT → userId)             │                  │                │             │
  │              │              └────────────────────────────┼──────────────────┼────────────────┼             │
  │              │              ┌──────Check Rate Limit──────┼──────────────────┼────────────────┼             │
  │              │              │ todayRequests < dailyLimit? │                │                │             │
  │              │              │ MaxAiRequestsPerDay (default 20)             │                │             │
  │              │              │◀────────CountTodayRequests(userId)────────────┼────────────────┼             │
  │              │              └────────────────────────────┼──────────────────┼────────────────┼             │
  │              │              ┌───────Build Context────────┼──────────────────┼────────────────┼             │
  │              │              │ AIQueryContext { UserId, Language, GroupId=null }              │             │
  │              │              └────────────────────────────┼──────────────────┼────────────────┼             │
  │              │              │ ProcessAsync(question, context)              │                │             │
  │              │              │──────────────────────────────────────────────▶│                │             │
  │              │              │                    ┌───────GetRoleSystemPrompt──┐             │             │
  │              │              │                    │ GroupId=null → Personal   │             │             │
  │              │              │                    │ System Prompt (VI/EN)     │             │             │
  │              │              │                    └──────────────────────────┘             │             │
  │              │              │                    ┌───────GetToolsManifest──────┐           │             │
  │              │              │                    │ Default tools available    │           │             │
  │              │              │                    └──────────────────────────┘           │             │
  │              │              │                    ┌───────DecideAction──────────┐        │             │
  │              │              │                    │ LLM: tool_call or answer?   │        │             │
  │              │              │                    │ (with system prompt)        │        │             │
  │              │              │                    │─────────────────────────────│────────▶│             │
  │              │              │                    │◀────────────────────────────│────────│             │
  │              │              │                    │   AgentDecision             │        │             │
  │              │              │                    │   { ShouldCallTool,         │        │             │
  │              │              │                    │     ToolName?, Parameters?,  │        │             │
  │              │              │                    │     FinalAnswer? }           │        │             │
  │              │              │                    │                             │        │             │
  │              │              │         ┌──────────Loop (max 5 tools)─────────┘        │             │
  │              │              │         │                  │                  │                │             │
  │              │              │    [If ShouldCallTool] │                  │                │             │
  │              │              │         │  ExecuteTool(toolName, params)                 │             │
  │              │              │         │─────────────────────────────▶│ GetAllTools()    │             │
  │              │              │         │                             │────────▶│ (find tool) │             │
  │              │              │         │                             │◀────────│             │             │
  │              │              │         │  toolResult = ExecuteAsync(context, params)    │             │
  │              │              │         │─────────────────────────────────────▶│              │             │
  │              │              │         │                             │◀─────────────────────────────────│
  │              │              │         │  history.AddCall(...)       │              │             │
  │              │              │         │                             │              │             │
  │              │              │         │  [Loop] DecideAction again ──│──────────────▶│             │
  │              │              │         │         │                  │              │             │
  │              │              │         └─────────┼──────────────────┘              │             │
  │              │              │                   │  [FinalAnswer ready]            │             │
  │              │              │◀─────────────────────────────────────────────────│             │
  │              │              │ AIAgentResult { Answer, ToolCallCount, ... }      │             │
  │              │              │              ┌──────Log AI Request────────────────┐  │             │
  │              │              │              │ AIRequestLog { UserId, TokenUsed } │  │             │
  │              │              │              │ AddAsync(log)                     │  │             │
  │              │              │              └─────────────────────────────────────┘  │             │
  │              │              │ ┌──────SSE: Metadata─────────────────────────────┐ │             │
  │◀─────────────│              │ │ remainingRequests = dailyLimit - 1            │ │             │
  │ SSE metadata │              │ │ data: {type:"metadata", remainingRequests, ...} │ │             │
  │◀─────────────│              │ └──────────────────────────────────────────────┘ │             │
  │              │──────────────┤                                          │        │             │
  │◀─────────────│ SSE chunk    │ ┌──────SSE: Chunk──────────────────────────┐ │             │
  │ SSE chunk    │              │ │ data: {type:"chunk", content:"..."}      │ │             │
  │◀─────────────│              │ └──────────────────────────────────────────┘ │             │
  │              │──────────────┤                                          │        │             │
  │◀─────────────│ SSE done     │ data: {type:"done"}                       │        │             │
  │ SSE done     │              │                                          │        │             │
  │              │──────────────┤                                          │        │             │
  │              │ Response.CompleteAsync()                                 │        │             │
```

---

## 3. Sequence Diagram — Get Personal Suggestions

```
Actor       Frontend           PersonalAIController       AIAgent         LLM
  │              │                      │                    │             │
  │ getSuggestions()│                    │                    │             │
  │──────────────┼──────────────────────┼                    │             │
  │              │ GET /suggestions      │                    │             │
  │              │──────────────────────▶                    │             │
  │              │              ┌───────Validate Auth────────┼             │
  │              │              └──────────────────────────────┼             │
  │              │              ┌──────Check Rate Limit────────┼             │
  │              │              │ todayRequests < dailyLimit?  │             │
  │              │              │◀────────CountTodayRequests──┼             │
  │              │              └──────────────────────────────┼             │
  │              │              │                              │             │
  │              │              │ Fixed prompt:               │             │
  │              │              │ "Phân tích công việc...     │             │
  │              │              │  3-5 gợi ý cải thiện..."   │             │
  │              │              │ ProcessAsync(prompt, context)│             │
  │              │              │──────────────────────────────▶│             │
  │              │              │◀──────────────────────────────│             │
  │              │              │ AIAgentResult               │             │
  │              │              │ LogAIRequest(1)             │             │
  │              │◀──────────────────────────────────────────│             │
  │              │ 200 OK {Answer, Data: {RemainingRequests}}│             │
  │◀─────────────│              │                             │             │
  │ Show 3-5     │              │                             │             │
  │ suggestions   │              │                             │             │
```

---

## 4. API Contract

### POST `/api/ai/personal/ask`

**Request:**
```json
{
  "question": "Những công việc cần làm hôm nay của tôi?",
  "personalGroupId": null
}
```

**Response:**
```json
{
  "success": true,
  "answer": "Dựa trên dữ liệu của bạn, hôm nay bạn có **5 công việc** cần hoàn thành...",
  "data": {
    "toolCallCount": 1,
    "processingTimeMs": 1234,
    "reasoningSteps": ["Analyzing question: Những công việc..."],
    "remainingRequests": 14,
    "dailyLimit": 30
  },
  "message": "Success"
}
```

### POST `/api/ai/personal/ask/stream`

**Headers:**
```
Accept: text/event-stream
Content-Type: application/json
Authorization: Bearer {token}
Accept-Language: vi
```

**Body:**
```json
{ "question": "Tóm tắt tiến độ tuần này của tôi" }
```

**SSE Events:**
```
data: {"type":"metadata","remainingRequests":15,"dailyLimit":30,"toolCount":1,"processingTime":1245}

data: {"type":"chunk","content":"Dựa"}

data: {"type":"chunk","content":" trên dữ liệu"}

data: {"type":"chunk","content":" tiến độ tuần này..."}

data: {"type":"done"}
```

### GET `/api/ai/personal/suggestions`

**Response:**
```json
{
  "success": true,
  "answer": "1. Bạn có **3 công việc quá hạn** cần ưu tiên xử lý...\n2. Deadline của...",
  "data": {
    "toolCallCount": 2,
    "processingTimeMs": 2100,
    "remainingRequests": 13,
    "dailyLimit": 30,
    "suggestionType": "PersonalProductivity"
  }
}
```

---

## 5. Rate Limit Flow

```
User sends request
        │
        ▼
CountTodayRequests(userId, DateTime.UtcNow.Date)
        │
        ▼
GetSubscription(userId) → MaxAiRequestsPerDay
        │
   ┌────┴────┐
   │         │
today < max  today >= max
   │         │
   ▼         ▼
 Allow    429 Too Many Requests
   │         │
   ▼         ▼
 LogAIRequest(userId, 1)
   │
   ▼
 Return response
   │
   ▼
 remainingRequests = max - today - 1
```

---

## 6. Frontend Integration

**File:** `mystudio/src/components/features/home/AIHome.tsx`

**State:**
```typescript
const [messages, setMessages] = useState<Message[]>([]);
const [usedToday, setUsedToday] = useState<number | null>(null);
const [dailyLimit, setDailyLimit] = useState<number | null>(null);

// Usage from user profile
const { user } = useUser();
useEffect(() => {
    setUsedToday(user?.aiRequestsUsedToday ?? null);
    setDailyLimit(user?.aiDailyLimit ?? null);
}, [user]);
```

**Streaming call:**
```typescript
await askPersonalAiStream(
    { question: value },
    {
        onChunk: (fullText, delta) => {
            setMessages(prev => {
                const last = prev[prev.length - 1];
                if (last?.role === "ai") {
                    return [...prev.slice(0, -1), { role: "ai", content: fullText }];
                }
                return [...prev, { role: "ai", content: fullText }];
            });
        },
        onMetadata: (meta) => {
            if (meta.remainingRequests != null) {
                const used = (meta.dailyLimit ?? 0) - meta.remainingRequests;
                setUsedToday(used);
            }
        }
    }
);
```

**Quick Prompts:**
```typescript
const quickActions = [
    { label: "Việc hôm nay", prompt: "Những công việc cần làm hôm nay của tôi?" },
    { label: "Xử lý quá hạn", prompt: "Công việc quá hạn của tôi?" },
    { label: "Tóm tắt tuần", prompt: "Tóm tắt tiến độ tuần này của tôi" },
    { label: "Deadline sắp tới", prompt: "Những deadline nào sắp tới?" },
];
```

---

## 7. Error Handling

| Error | HTTP Status | Message |
|-------|------------|---------|
| Unauthorized (no JWT) | 401 | `Unauthorized` |
| Rate limit exceeded | 429 | `Rate limit exceeded` |
| Unexpected error | 500 | `Đã xảy ra lỗi khi xử lý yêu cầu.` |

**Frontend error display:**
```typescript
} catch {
    setMessages(prev => [...prev, {
        role: "ai",
        content: "Xin lỗi, đã xảy ra lỗi. Vui lòng thử lại."
    }]);
} finally {
    setLoading(false);
}
```

---

## 8. File Reference

```
Backend
├── Controllers/PersonalAIController.cs      # Endpoints, auth, rate limit
├── Services/AI/AIAgent.cs                   # ReAct loop, role detection
├── Services/AI/AIToolRegistry.cs            # Tool registration
├── Services/AI/Tools/                      # 10 AI tools
└── Docs/AI/AI-PERSONAL-WORKFLOW.md         # This file

Frontend
└── mystudio/src/
    ├── api/personal-ai.ts                  # askPersonalAiStream()
    ├── api/personal-analytics.ts           # getPersonalDashboard(), etc.
    └── components/features/home/AIHome.tsx # UI component
```
