# Analytics Dashboard - Hướng Dẫn Sử Dụng

## Tổng Quan

Analytics System cung cấp dữ liệu pre-aggregated cho dashboard với query time < 50ms. Hệ thống tự động chạy background jobs để thu thập và tính toán metrics.

---

## 1. User Dashboard

### 1.1 Productivity Score
**Biểu đồ:** Circular Progress / Gauge Chart
**Dữ liệu:** `UserDashboardResponse.ProductivityScore` (0-100)

| Score Range | Màu | Ý nghĩa |
|-------------|-----|---------|
| 0-25 | Đỏ | Cần cải thiện |
| 26-50 | Cam | Trung bình |
| 51-75 | Vàng | Khá |
| 76-100 | Xanh | Xuất sắc |

**Công thức tính:**
```
ProductivityScore = TaskScore (40%) + CreationScore (20%) + CommentScore (20%) + MessageScore (20%)
- TaskScore = min(TasksCompleted × 5, 40)
- CreationScore = min(TasksCreated × 2, 20)
- CommentScore = min(Comments × 2, 20)
- MessageScore = min(Messages × 1, 20)
```

### 1.2 Activity Heatmap
**Biểu đồ:** GitHub-style Heatmap / Calendar Grid
**Dữ liệu:** `UserDashboardResponse.ActivityHeatmap[]`
```json
[
  { "date": "2026-03-01", "activityCount": 5 },
  { "date": "2026-03-02", "activityCount": 12 },
  ...
]
```
**Hiển thị:**
- Mỗi ô = 1 ngày
- Màu sắc theo cường độ hoạt động (0 → xanh đậm)
- Hover hiển thị số lượng hoạt động

### 1.3 Task Completion Trend
**Biểu đồ:** Line Chart / Area Chart
**Dữ liệu:** `UserDashboardResponse.TaskCompletionTrend[]`
```json
[
  { "date": "2026-03-01", "tasksCreated": 3, "tasksCompleted": 1 },
  { "date": "2026-03-02", "tasksCreated": 2, "tasksCompleted": 4 },
  ...
]
```
**Hiển thị:**
- 2 đường line: Tasks Created (màu xanh), Tasks Completed (màu cam)
- Trục X: Ngày
- Trục Y: Số lượng task

### 1.4 Deadline Performance
**Biểu đồ:** Donut Chart / Pie Chart
**Dữ liệu:** `UserDashboardResponse.DeadlinePerformance`
```json
{
  "onTimeCount": 15,
  "lateCount": 5,
  "onTimePercentage": 75.0
}
```
**Hiển thị:**
- 2 phần: On-time (xanh), Late (đỏ)
- Center: Percentage %

---

## 2. Group Dashboard

### 2.1 Completion Rate
**Biểu đồ:** Circular Progress / Gauge
**Dữ liệu:** `GroupAnalyticsResponse.CompletionRate` (0-100%)

### 2.2 Progress Over Time
**Biểu đồ:** Stacked Area Chart
**Dữ liệu:** `GroupAnalyticsResponse.Progress[]`
```json
[
  { "date": "2026-03-01", "totalTasks": 20, "completedTasks": 10, "completionRate": 50 },
  ...
]
```
**Hiển thị:**
- Area 1 (xanh): Completed Tasks
- Area 2 (xám): Remaining Tasks

### 2.3 Performance Radar
**Biểu đồ:** Radar Chart (Spider Web)
**Dữ liệu:** `GroupAnalyticsResponse.PerformanceRadar[]`
```json
[
  { "metric": "Task Completion", "score": 85 },
  { "metric": "Member Activity", "score": 70 },
  { "metric": "Communication", "score": 60 },
  { "metric": "Collaboration", "score": 75 },
  { "metric": "Overdue Control", "score": 90 }
]
```
**Metrics:**
| Metric | Cách tính |
|--------|-----------|
| Task Completion | CompletionRate |
| Member Activity | ActiveMembers > 0 ? 100 : 0 |
| Communication | min(Messages × 10, 100) |
| Collaboration | min(Comments × 10, 100) |
| Overdue Control | OverdueTasks == 0 ? 100 : max(100 - OverdueTasks × 20, 0) |

### 2.4 Member Contribution
**Biểu đồ:** Horizontal Bar Chart / Treemap
**Dữ liệu:** `GroupAnalyticsResponse.MemberContribution[]`
```json
[
  { "userId": "...", "userName": "John Doe", "tasksCompleted": 10, "tasksCreated": 5, "messagesSent": 20, "contributionPercentage": 35.5 },
  ...
]
```
**Hiển thị:**
- Bar chart với breakdown: Tasks + Messages
- Sắp xếp giảm dần theo contribution %

### 2.5 Group Activity Heatmap
**Biểu đồ:** Heatmap theo tuần
**Dữ liệu:** `GroupAnalyticsResponse.ActivityHeatmap[]`

---

## 3. Studio Dashboard

### 3.1 Completion Rate
**Biểu đồ:** Large Circular Progress
**Dữ liệu:** `StudioAnalyticsResponse.CompletionRate`

### 3.2 Active Users
**Biểu đồ:** Big Number / Counter Card
**Dữ liệu:** `StudioAnalyticsResponse.ActiveUsers`

### 3.3 Engagement Score
**Biểu đồ:** Gauge Chart
**Dữ liệu:** `StudioAnalyticsResponse.EngagementScore` (0-100)

**Công thức:**
```
EngagementScore = GroupActivity (30%) + MemberActivity (30%) + CompletionRate (40%)
- GroupActivity = (ActiveGroups / TotalGroups) × 30
- MemberActivity = (ActiveMembers / TotalMembers) × 30
- CompletionRate = OverallCompletionRate × 0.4
```

### 3.5 Completion Rate History
**Biểu đồ:** Line Chart
**Dữ liệu:** `StudioAnalyticsResponse.CompletionRateHistory[]`

### 3.4 Group Comparison
**Biểu đồ:** Grouped Bar Chart / Leaderboard Table
**Dữ liệu:** `StudioAnalyticsResponse.GroupComparison[]`
```json
[
  { "groupId": "...", "groupName": "Group A", "totalTasks": 50, "completedTasks": 40, "completionRate": 80, "activeMembers": 8 },
  { "groupId": "...", "groupName": "Group B", "totalTasks": 30, "completedTasks": 20, "completionRate": 66.7, "activeMembers": 5 },
  ...
]
```
**Hiển thị:**
- Bar chart so sánh completion rate các nhóm
- Table leaderboard với metrics chi tiết

### 3.6 Group Heatmap Comparison
**Biểu đồ:** Multi-group Heatmap / Activity Grid
**Dữ liệu:** `StudioAnalyticsResponse.GroupHeatmapComparison[]`
```json
[
  {
    "date": "2026-03-01",
    "groups": [
      { "groupId": "...", "groupName": "Group A", "activityCount": 15, "messagesCount": 10, "commentsCount": 3, "tasksCompleted": 2 },
      { "groupId": "...", "groupName": "Group B", "activityCount": 8, "messagesCount": 5, "commentsCount": 2, "tasksCompleted": 1 },
      { "groupId": "...", "groupName": "Group C", "activityCount": 20, "messagesCount": 12, "commentsCount": 5, "tasksCompleted": 3 }
    ]
  },
  ...
]
```
**Hiển thị:**
- Heatmap matrix: Rows = Groups, Columns = Days
- Mỗi ô hiển thị tổng activity count
- Hover hiển thị chi tiết: Messages, Comments, Tasks Completed
- So sánh trực quan giữa các nhóm trong cùng khoảng thời gian

**Color scale:** Từ thấp đến cao (xanh nhạt → xanh đậm)

**Metrics breakdown:**
| Metric | Nguồn dữ liệu |
|--------|---------------|
| ActivityCount | Messages + Comments + TasksCompleted |
| MessagesCount | GroupMessages |
| CommentsCount | TaskComments |
| TasksCompleted | Tasks với Progress = 100 |

---

## 4. Background Jobs & Data Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                      USER ACTIVITIES                            │
│  (Task Create, Task Complete, Comment, Message)               │
└─────────────────────────┬───────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                   ActivityLog Table                            │
│  - UserId, ActionType, TargetId, GroupId, StudioId            │
└─────────────────────────┬───────────────────────────────────────┘
                          │
        ┌───────────────┬┴───────────────┬───────────────┐
        ▼               ▼                 ▼               ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│ UserActivity │ │ GroupAnalytics│ │ StudioAnalytics│ │TaskPerformance│
│  MetricsJob  │ │    Job       │ │     Job       │ │    Job       │
│   (10 min)   │ │  (10 min)    │ │  (daily)     │ │  (daily)     │
└──────┬───────┘ └──────┬───────┘ └──────┬───────┘ └──────┬───────┘
       │                │                 │                │
       ▼                ▼                 ▼                ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│UserActivity  │ │GroupAnalytics │ │StudioAnalytics│ │TaskPerformance│
│  Metrics     │ │               │ │               │ │   Metrics    │
└──────────────┘ └──────────────┘ └──────────────┘ └──────────────┘
       │                │                 │                │
       └────────────────┴────────┬────────┴────────────────┘
                                │
                                ▼
                    ┌───────────────────────┐
                    │   Analytics API      │
                    │  (Dashboard Queries) │
                    └───────────────────────┘
```

---

## 5. API Endpoints

### User
| Endpoint | Method | Returns |
|----------|--------|---------|
| `/api/analytics/user/dashboard` | GET | Full user dashboard |
| `/api/analytics/user/heatmap` | GET | Activity heatmap |
| `/api/analytics/user/trends` | GET | Task completion trends |
| `/api/analytics/user/deadline-performance` | GET | Deadline stats |

### Group
| Endpoint | Method | Returns |
|----------|--------|---------|
| `/api/analytics/group/{groupId}` | GET | Full group analytics |
| `/api/analytics/group/{groupId}/members` | GET | Member contributions |

### Studio
| Endpoint | Method | Returns |
|----------|--------|---------|
| `/api/analytics/studio/{studioId}` | GET | Full studio analytics |
| `/api/analytics/studio/{studioId}/groups` | GET | Group comparison |

---

## 6. Recommended Chart Libraries

### Frontend (React/Vue)
| Chart Type | Library | Usage |
|------------|---------|-------|
| Line/Area | Recharts, Chart.js | Trends, History |
| Bar | Recharts, Chart.js | Comparison, Contribution |
| Pie/Donut | Recharts, Chart.js | Distribution |
| Radar | Chart.js, Recharts | Performance metrics |
| Heatmap | react-calendar-heatmap | Activity heatmap |
| Gauge | react-gauge-chart | Scores |
| Progress Circle | react-circular-progressbar | Completion % |

### Color Palette
```css
--color-success: #10B981;    /* Xanh - Hoàn thành */
--color-warning: #F59E0B;   /* Cam - Đang xử lý */
--color-danger: #EF4444;    /* Đỏ - Quá hạn */
--color-primary: #3B82F6;   /* Xanh dương - Chính */
--color-secondary: #6B7280;  /* Xám - Phụ */
```

---

## 7. Query Parameters

Tất cả endpoints hỗ trợ:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `startDate` | DateTime | -30 days | Bắt đầu khoảng thời gian |
| `endDate` | DateTime | Today | Kết thúc khoảng thời gian |
| `days` | int | 30 | Số ngày (cho heatmap/trends) |

**Ví dụ:**
```
GET /api/analytics/user/dashboard?startDate=2026-01-01&endDate=2026-03-18
GET /api/analytics/user/heatmap?days=90
```
