# Studio Analytics - Frontend Implementation Guide

## Table of Contents
1. [API Endpoint](#1-api-endpoint)
2. [Response Data Structure](#2-response-data-structure)
3. [UI Components & Charts](#3-ui-components--charts)
4. [Component Layout](#4-component-layout)
5. [Color Scheme](#5-color-scheme)
6. [Data Flow](#6-data-flow)
7. [Error Handling](#7-error-handling)
8. [Loading States](#8-loading-states)

---

## 1. API Endpoint

### Get Studio Analytics
```
GET /api/analytics/studio/{studioId}
```

**Headers:**
```
Authorization: Bearer {access_token}
```

**Query Parameters:**
| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `studioId` | UUID | Yes | - | Studio ID from URL |
| `startDate` | DateTime | No | Today - 30 days | Start date (ISO 8601) |
| `endDate` | DateTime | No | Today | End date (ISO 8601) |

**Example:**
```
GET /api/analytics/studio/550e8400-e29b-41d4-a716-446655440000?startDate=2026-02-01&endDate=2026-03-18
```

---

## 2. Response Data Structure

### Success Response (200 OK)
```json
{
  "status": "success",
  "code": "SUCCESS010",
  "message": "Data retrieved successfully",
  "data": {
    "completionRate": 72.5,
    "activeUsers": 24,
    "engagementScore": 68.3,
    "groupComparison": [
      {
        "groupId": "550e8400-e29b-41d4-a716-446655440001",
        "groupName": "Team Alpha",
        "totalTasks": 45,
        "completedTasks": 38,
        "completionRate": 84.44,
        "activeMembers": 8
      },
      {
        "groupId": "550e8400-e29b-41d4-a716-446655440002",
        "groupName": "Team Beta",
        "totalTasks": 30,
        "completedTasks": 18,
        "completionRate": 60.0,
        "activeMembers": 5
      },
      {
        "groupId": "550e8400-e29b-41d4-a716-446655440003",
        "groupName": "Team Gamma",
        "totalTasks": 25,
        "completedTasks": 12,
        "completionRate": 48.0,
        "activeMembers": 4
      }
    ],
    "completionRateHistory": [
      { "date": "2026-02-01", "completionRate": 65.2, "activeUsers": 18 },
      { "date": "2026-02-02", "completionRate": 66.1, "activeUsers": 19 },
      { "date": "2026-02-03", "completionRate": 67.0, "activeUsers": 20 },
      { "date": "2026-02-04", "completionRate": 65.8, "activeUsers": 19 },
      { "date": "2026-02-05", "completionRate": 68.2, "activeUsers": 21 },
      { "date": "2026-02-06", "completionRate": 69.5, "activeUsers": 22 },
      { "date": "2026-02-07", "completionRate": 70.1, "activeUsers": 23 },
      { "date": "2026-02-08", "completionRate": 68.9, "activeUsers": 21 },
      { "date": "2026-02-09", "completionRate": 69.3, "activeUsers": 22 },
      { "date": "2026-02-10", "completionRate": 70.5, "activeUsers": 23 }
    ],
    "groupHeatmapComparison": [
      {
        "date": "2026-03-01",
        "groups": [
          { "groupId": "550e8400-e29b-41d4-a716-446655440001", "groupName": "Team Alpha", "activityCount": 15, "messagesCount": 10, "commentsCount": 3, "tasksCompleted": 2 },
          { "groupId": "550e8400-e29b-41d4-a716-446655440002", "groupName": "Team Beta", "activityCount": 8, "messagesCount": 5, "commentsCount": 2, "tasksCompleted": 1 },
          { "groupId": "550e8400-e29b-41d4-a716-446655440003", "groupName": "Team Gamma", "activityCount": 20, "messagesCount": 12, "commentsCount": 5, "tasksCompleted": 3 }
        ]
      },
      {
        "date": "2026-03-02",
        "groups": [
          { "groupId": "550e8400-e29b-41d4-a716-446655440001", "groupName": "Team Alpha", "activityCount": 12, "messagesCount": 8, "commentsCount": 2, "tasksCompleted": 2 },
          { "groupId": "550e8400-e29b-41d4-a716-446655440002", "groupName": "Team Beta", "activityCount": 15, "messagesCount": 9, "commentsCount": 4, "tasksCompleted": 2 },
          { "groupId": "550e8400-e29b-41d4-a716-446655440003", "groupName": "Team Gamma", "activityCount": 5, "messagesCount": 3, "commentsCount": 1, "tasksCompleted": 1 }
        ]
      }
    ]
  }
}
```

### Error Responses
```json
// 401 Unauthorized
{
  "status": "error",
  "code": "AUTH001",
  "message": "Invalid credentials"
}

// 403 Forbidden (not a studio member)
{
  "status": "error",
  "code": "STUDIO005",
  "message": "You do not have permission to access this studio"
}
```

---

## 3. UI Components & Charts

### 3.1 Overview Cards (Top Row)

#### Completion Rate Card
- **Type:** Circular Progress / Gauge Chart
- **Value:** `completionRate` (0-100)
- **Display:**
  - Large percentage number in center
  - Circular progress ring around it
  - Label "Completion Rate" below

#### Active Users Card
- **Type:** Big Number / Counter
- **Value:** `activeUsers` (integer)
- **Display:**
  - Large number (32px+ font)
  - Icon (users/people icon)
  - Label "Active Users"

#### Engagement Score Card
- **Type:** Gauge Chart with color zones
- **Value:** `engagementScore` (0-100)
- **Display:**
  - Gauge from 0-100
  - Color zones: Red (0-25), Orange (26-50), Yellow (51-75), Green (76-100)
  - Score label

### 3.2 Group Comparison (Middle Section)

#### Group Comparison Table
- **Type:** Table with sortable columns + bar indicators
- **Data:** `groupComparison[]`
- **Columns:**
  | Column | Width | Content |
  |--------|-------|---------|
  | Rank | 60px | 1, 2, 3... |
  | Group Name | flex | Group name with avatar/color |
  | Tasks | 120px | completed / total |
  | Progress | 200px | Progress bar + % |
  | Members | 100px | Active count |

- **Features:**
  - Sortable by any column
  - Top 3 highlighted with medal icons 🥇🥈🥉

### 3.3 Completion Rate History Chart

- **Type:** Line Chart / Area Chart
- **Data:** `completionRateHistory[]`
- **X-Axis:** Date (days)
- **Y-Axis:** Completion Rate (0-100%)
- **Lines:**
  - Line 1: Completion Rate (primary color)
  - Optional: Area fill under the line
- **Interactions:**
  - Hover tooltip showing exact values
  - Click to see details for that day

### 3.4 Group Heatmap Comparison (Bottom Section)

- **Type:** Multi-group Heatmap Matrix
- **Data:** `groupHeatmapComparison[]`
- **Layout:**
  ```
  ┌─────────────┬─────┬─────┬─────┬─────┬─────┬─────┬─────┐
  │   Group     │ Mon │ Tue │ Wed │ Thu │ Fri │ Sat │ Sun │
  ├─────────────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┤
  │ Team Alpha  │  15 │  12 │  18 │  10 │   8 │   5 │   3 │
  │ Team Beta   │   8 │  15 │  12 │  20 │  11 │   6 │   2 │
  │ Team Gamma  │  20 │   5 │   8 │  15 │   9 │   4 │   7 │
  └─────────────┴─────┴─────┴─────┴─────┴─────┴─────┴─────┘
  ```

- **Cell Colors:** Gradient based on activity count
  - 0: Gray (#E5E7EB)
  - 1-5: Light Green (#D1FAE5)
  - 6-10: Medium Green (#6EE7B7)
  - 11-15: Green (#10B981)
  - 16+: Dark Green (#047857)

- **Interactions:**
  - Hover: Show tooltip with breakdown
    ```
    Team Alpha - Mar 1
    ┌──────────────────┐
    │ Messages: 10     │
    │ Comments: 3     │
    │ Tasks Done: 2    │
    │ Total: 15       │
    └──────────────────┘
    ```
  - Click cell: Filter other charts to show only that group

---

## 4. Component Layout

### Recommended Layout (Desktop)
```
┌─────────────────────────────────────────────────────────────────┐
│                        STUDIO ANALYTICS                        │
│                     [Date Range Picker]                        │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐             │
│  │ Completion  │  │  Active     │  │ Engagement  │             │
│  │    72.5%    │  │    24       │  │    68.3    │             │
│  │    (○)      │  │    👥       │  │    ( gauge)│             │
│  └─────────────┘  └─────────────┘  └─────────────┘             │
├─────────────────────────────────────────────────────────────────┤
│                    COMPLETION RATE HISTORY                      │
│         📈 Line Chart (30 days trend)                          │
│                                                                  │
├─────────────────────────────────────────────────────────────────┤
│                       GROUP COMPARISON                         │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ 🥇 Team Alpha    │ 38/45  │ ████████████ 84%  │ 8 👤    │  │
│  │ 🥈 Team Beta     │ 18/30  │ ████████ 60%      │ 5 👤    │  │
│  │ 🥉 Team Gamma   │ 12/25  │ ██████ 48%        │ 4 👤    │  │
│  └──────────────────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────────────────┤
│                  GROUP ACTIVITY HEATMAP                        │
│  Groups: [All ▼]  Last 30 days                                │
│  ┌────────┬─────────────────────────────────────────────────┐   │
│  │Team α  │███████████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│   │
│  │Team β  │████████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│   │
│  │Team γ  │███████████████████████████████░░░░░░░░░░░░░░░░│   │
│  └────────┴─────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### Responsive Breakpoints

| Breakpoint | Width | Layout Changes |
|------------|-------|----------------|
| Desktop | ≥1200px | Full layout as above |
| Tablet | 768-1199px | 2 cards per row, scrollable table |
| Mobile | <768px | 1 card per row, horizontal scroll table |

---

## 5. Color Scheme

### Primary Colors
```css
--studio-primary: #3B82F6;        /* Blue - Main actions */
--studio-primary-light: #60A5FA;  /* Blue - Hover states */
--studio-primary-dark: #1D4ED8;   /* Blue - Active states */
```

### Status Colors
```css
--color-success: #10B981;   /* Green - Good/Complete */
--color-success-light: #D1FAE5;
--color-warning: #F59E0B;   /* Orange - Warning */
--color-warning-light: #FEF3C7;
--color-danger: #EF4444;    /* Red - Low/Bad */
--color-danger-light: #FEE2E2;
--color-neutral: #6B7280;   /* Gray - Neutral */
--color-neutral-light: #F3F4F6;
```

### Engagement Score Zones
```css
--engagement-low: #EF4444;      /* 0-25 */
--engagement-medium: #F59E0B;    /* 26-50 */
--engagement-good: #FBBF24;      /* 51-75 */
--engagement-excellent: #10B981; /* 76-100 */
```

### Heatmap Gradient
```css
--heatmap-0: #E5E7EB;   /* 0 activities */
--heatmap-1: #D1FAE5;   /* 1-5 */
--heatmap-2: #6EE7B7;   /* 6-10 */
--heatmap-3: #10B981;   /* 11-15 */
--heatmap-4: #047857;    /* 16+ */
```

---

## 6. Data Flow

### Fetching Data
```typescript
// Example with React + Axios
const fetchStudioAnalytics = async (studioId: string, startDate?: string, endDate?: string) => {
  const params = new URLSearchParams();
  if (startDate) params.append('startDate', startDate);
  if (endDate) params.append('endDate', endDate);

  const response = await axios.get(`/api/analytics/studio/${studioId}?${params}`);
  return response.data.data;
};
```

### State Management
```typescript
interface StudioAnalyticsState {
  data: StudioAnalyticsResponse | null;
  isLoading: boolean;
  error: string | null;
  dateRange: {
    startDate: Date;
    endDate: Date;
  };
}
```

### Data Transformation for Charts
```typescript
// Transform for Line Chart
const lineChartData = data.completionRateHistory.map(item => ({
  date: new Date(item.date),
  value: item.completionRate,
  label: `${item.completionRate}%`
}));

// Transform for Heatmap
const heatmapData = data.groupHeatmapComparison.map(day => ({
  date: day.date,
  groups: day.groups.map(g => ({
    id: g.groupId,
    name: g.groupName,
    value: g.activityCount,
    details: {
      messages: g.messagesCount,
      comments: g.commentsCount,
      tasks: g.tasksCompleted
    }
  }))
}));
```

---

## 7. Error Handling

### Error States
| Error Code | Message | User Action |
|------------|---------|-------------|
| AUTH001 | Invalid credentials | Redirect to login |
| STUDIO005 | No permission | Show "Access Denied" message |
| ANALYTICS001 | Analytics not found | Show empty state with "No data yet" |
| SYS001 | Unexpected error | Show error message + retry button |

### Empty State
When there's no data yet:
```
┌─────────────────────────────────────┐
│                                     │
│           📊                        │
│                                     │
│      No analytics data yet         │
│                                     │
│  Start using the studio to see    │
│  your analytics here!             │
│                                     │
└─────────────────────────────────────┘
```

---

## 8. Loading States

### Skeleton Loading
```
┌─────────────┐ ┌─────────────┐ ┌─────────────┐
│  ████████   │ │  ████████   │ │  ████████   │
│  ████████   │ │  ████████   │ │  ████████   │
│  ████████   │ │  ████████   │ │  ████████   │
└─────────────┘ └─────────────┘ └─────────────┘

┌─────────────────────────────────────┐
│  ████████████████████████████████   │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│  ████████  ████████  ████████      │
│  ████████  ████████  ████████      │
│  ████████  ████████  ████████      │
└─────────────────────────────────────┘
```

---

## 9. TypeScript Interfaces

```typescript
interface StudioAnalyticsResponse {
  completionRate: number;
  activeUsers: number;
  engagementScore: number;
  groupComparison: GroupComparisonData[];
  completionRateHistory: StudioProgressData[];
  groupHeatmapComparison: GroupHeatmapComparisonData[];
}

interface GroupComparisonData {
  groupId: string;
  groupName: string;
  totalTasks: number;
  completedTasks: number;
  completionRate: number;
  activeMembers: number;
}

interface StudioProgressData {
  date: string; // "2026-03-01"
  completionRate: number;
  activeUsers: number;
}

interface GroupHeatmapComparisonData {
  date: string;
  groups: GroupActivityItem[];
}

interface GroupActivityItem {
  groupId: string;
  groupName: string;
  activityCount: number;
  messagesCount: number;
  commentsCount: number;
  tasksCompleted: number;
}
```

---

## 10. Implementation Checklist

- [ ] Create API service to fetch studio analytics
- [ ] Create state management (Redux/Context/Zustand)
- [ ] Build Overview Cards component (3 cards)
- [ ] Build Completion Rate Line Chart
- [ ] Build Group Comparison Table
- [ ] Build Group Heatmap Matrix
- [ ] Add date range picker
- [ ] Add loading skeletons
- [ ] Add error handling
- [ ] Add responsive styles
- [ ] Test with real API

---

## 11. Recommended Libraries

| Purpose | Library | Notes |
|---------|---------|-------|
| Charts | Recharts | Best for React |
| Table | TanStack Table | Headless, fully customizable |
| Heatmap | Custom or react-calendar-heatmap | Build custom for multi-group |
| Icons | Lucide React | Clean, modern icons |
| Date Picker | react-day-picker | Accessible date picker |
| Loading | react-loading-skeleton | Clean skeleton states |
