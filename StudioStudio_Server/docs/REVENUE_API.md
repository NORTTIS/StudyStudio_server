# Revenue Statistics API Documentation

## Overview

API endpoint for admin revenue reporting and analytics. All endpoints require **admin authentication**.

**Base URL:** `/api/admin/revenue`

---

## Authentication

All endpoints require a valid JWT token with admin role. Include the token in the Authorization header:

```
Authorization: Bearer {access_token}
```

---

## Endpoints

### 1. Revenue Overview

Get overall revenue metrics and key performance indicators.

**Endpoint:** `GET /api/admin/revenue/overview`

**Query Parameters:** None

**Response:**

```json
{
  "status": "success",
  "code": "SUCCESS010",
  "message": "Revenue overview retrieved successfully",
  "data": {
    "totalRevenue": 150000.00,
    "monthlyRevenue": 12500.00,
    "yearlyRevenue": 85000.00,
    "totalTransactions": 150,
    "successfulTransactions": 142,
    "failedTransactions": 8,
    "successRate": 94.67,
    "activeSubscriptions": 45,
    "arpu": 250.00,
    "mrr": 4500.00
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `totalRevenue` | decimal | Total revenue all time |
| `monthlyRevenue` | decimal | Revenue for current month |
| `yearlyRevenue` | decimal | Revenue for current year |
| `totalTransactions` | int | Total number of payment transactions |
| `successfulTransactions` | int | Number of successful payments |
| `failedTransactions` | int | Number of failed/cancelled payments |
| `successRate` | decimal | Payment success percentage (%) |
| `activeSubscriptions` | int | Currently active subscriptions |
| `arpu` | decimal | Average Revenue Per User |
| `mrr` | decimal | Monthly Recurring Revenue |

---

### 2. Revenue By Period

Get revenue breakdown by time period (daily, weekly, monthly, yearly).

**Endpoint:** `GET /api/admin/revenue/by-period`

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `startDate` | datetime | Yes | Start date (ISO 8601 format) |
| `endDate` | datetime | Yes | End date (ISO 8601 format) |
| `period` | string | No | Period type: `daily`, `weekly`, `monthly`, `yearly` (default: `daily`) |
| `planId` | guid | No | Filter by specific subscription plan |

**Example Request:**

```
GET /api/admin/revenue/by-period?startDate=2026-01-01T00:00:00Z&endDate=2026-03-12T23:59:59Z&period=monthly
```

**Response:**

```json
{
  "status": "success",
  "code": "SUCCESS010",
  "message": "Revenue by period retrieved successfully",
  "data": {
    "period": "monthly",
    "startDate": "2026-01-01T00:00:00Z",
    "endDate": "2026-03-12T23:59:59Z",
    "totalRevenue": 15000.00,
    "transactionCount": 45,
    "averageOrderValue": 333.33,
    "breakdown": [
      {
        "date": "2026-01-01T00:00:00Z",
        "revenue": 5000.00,
        "transactionCount": 15,
        "newSubscriptions": 10,
        "renewals": 5
      },
      {
        "date": "2026-02-01T00:00:00Z",
        "revenue": 5500.00,
        "transactionCount": 16,
        "newSubscriptions": 12,
        "renewals": 4
      },
      {
        "date": "2026-03-01T00:00:00Z",
        "revenue": 4500.00,
        "transactionCount": 14,
        "newSubscriptions": 8,
        "renewals": 6
      }
    ]
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `period` | string | Period type used |
| `startDate` | datetime | Start date of query |
| `endDate` | datetime | End date of query |
| `totalRevenue` | decimal | Total revenue in period |
| `transactionCount` | int | Total transactions |
| `averageOrderValue` | decimal | Average revenue per transaction |
| `breakdown` | array | Array of period data points |

**RevenueDataPoint:**

| Field | Type | Description |
|-------|------|-------------|
| `date` | datetime | Start date of the period |
| `revenue` | decimal | Revenue for this period |
| `transactionCount` | int | Number of transactions |
| `newSubscriptions` | int | New subscriptions in period |
| `renewals` | int | Renewed subscriptions in period |

---

### 3. Revenue By Plan

Get revenue breakdown by subscription plan.

**Endpoint:** `GET /api/admin/revenue/by-plan`

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `startDate` | datetime | No | Start date (default: start of current month) |
| `endDate` | datetime | No | End date (default: current date) |

**Example Request:**

```
GET /api/admin/revenue/by-plan?startDate=2026-01-01T00:00:00Z&endDate=2026-03-12T23:59:59Z
```

**Response:**

```json
{
  "status": "success",
  "code": "SUCCESS010",
  "message": "Revenue by plan retrieved successfully",
  "data": {
    "plans": [
      {
        "planId": "a1b2c3d4-1234-5678-90ab-cdef12345678",
        "planName": "Premium Monthly",
        "price": 9.99,
        "billingCycle": "Monthly",
        "totalRevenue": 8000.00,
        "transactionCount": 25,
        "activeSubscriptions": 20,
        "percentage": 53.33,
        "trend": "up"
      },
      {
        "planId": "b2c3d4e5-2345-6789-01bc-def234567890",
        "planName": "Basic Monthly",
        "price": 4.99,
        "billingCycle": "Monthly",
        "totalRevenue": 5000.00,
        "transactionCount": 30,
        "activeSubscriptions": 25,
        "percentage": 33.33,
        "trend": "stable"
      },
      {
        "planId": "c3d4e5f6-3456-7890-12cd-ef3456789012",
        "planName": "Pro Monthly",
        "price": 19.99,
        "billingCycle": "Monthly",
        "totalRevenue": 2000.00,
        "transactionCount": 5,
        "activeSubscriptions": 5,
        "percentage": 13.34,
        "trend": "down"
      }
    ]
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `plans` | array | Array of plan summaries |

**PlanRevenueSummary:**

| Field | Type | Description |
|-------|------|-------------|
| `planId` | guid | Plan ID |
| `planName` | string | Name of the plan |
| `price` | decimal | Plan price |
| `billingCycle` | string | Billing cycle (Monthly, Free) |
| `totalRevenue` | decimal | Total revenue from this plan |
| `transactionCount` | int | Number of transactions |
| `activeSubscriptions` | int | Currently active subscriptions |
| `percentage` | decimal | Percentage of total revenue |
| `trend` | string | Trend compared to previous period: `up`, `down`, `stable` |

---

### 4. Revenue Trends

Get revenue trends with optional comparison to previous period.

**Endpoint:** `GET /api/admin/revenue/trends`

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `period` | string | No | Period preset: `last7days`, `last30days`, `last90days`, `last12months`, `custom` (default: `last30days`) |
| `startDate` | datetime | No* | Start date (required if period=`custom`) |
| `endDate` | datetime | No* | End date (required if period=`custom`) |
| `comparison` | bool | No | Include previous period data (default: false) |

*Required if period is `custom`

**Example Request:**

```
GET /api/admin/revenue/trends?period=last30days&comparison=true
```

**Response:**

```json
{
  "status": "success",
  "code": "SUCCESS010",
  "message": "Revenue trends retrieved successfully",
  "data": {
    "currentPeriod": {
      "period": "last30days",
      "startDate": "2026-02-10T00:00:00Z",
      "endDate": "2026-03-12T23:59:59Z",
      "totalRevenue": 15000.00,
      "transactionCount": 45,
      "newCustomers": 20,
      "churnedCustomers": 3,
      "averageOrderValue": 333.33
    },
    "previousPeriod": {
      "period": "previous",
      "startDate": "2026-01-09T00:00:00Z",
      "endDate": "2026-02-09T23:59:59Z",
      "totalRevenue": 12000.00,
      "transactionCount": 38,
      "newCustomers": 0,
      "churnedCustomers": 0,
      "averageOrderValue": 315.79
    },
    "growthRate": 25.00,
    "trendDirection": "up"
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `currentPeriod` | object | Current period data |
| `previousPeriod` | object | Previous period data (if comparison=true) |
| `growthRate` | decimal | Growth percentage |
| `trendDirection` | string | `up`, `down`, or `stable` |

**TrendData:**

| Field | Type | Description |
|-------|------|-------------|
| `period` | string | Period identifier |
| `startDate` | datetime | Start date |
| `endDate` | datetime | End date |
| `totalRevenue` | decimal | Total revenue |
| `transactionCount` | int | Number of transactions |
| `newCustomers` | int | New customers acquired |
| `churnedCustomers` | int | Customers churned |
| `averageOrderValue` | decimal | Average order value |

---

### 5. Top Plans

Get top performing subscription plans.

**Endpoint:** `GET /api/admin/revenue/top-plans`

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `limit` | int | No | Number of plans to return (default: 5, max: 10) |
| `startDate` | datetime | No | Start date for analysis period |
| `endDate` | datetime | No | End date for analysis period |
| `sortBy` | string | No | Sort by: `revenue`, `subscriptions`, `growth` (default: `revenue`) |

**Example Request:**

```
GET /api/admin/revenue/top-plans?limit=5&sortBy=revenue
```

**Response:**

```json
{
  "status": "success",
  "code": "SUCCESS010",
  "message": "Top plans retrieved successfully",
  "data": {
    "topPlans": [
      {
        "rank": 1,
        "planId": "a1b2c3d4-1234-5678-90ab-cdef12345678",
        "planName": "Premium Monthly",
        "price": 9.99,
        "totalRevenue": 8000.00,
        "activeSubscriptions": 20,
        "newSubscriptions": 15,
        "conversionRate": 5.50
      },
      {
        "rank": 2,
        "planId": "b2c3d4e5-2345-6789-01bc-def234567890",
        "planName": "Basic Monthly",
        "price": 4.99,
        "totalRevenue": 5000.00,
        "activeSubscriptions": 25,
        "newSubscriptions": 10,
        "conversionRate": 8.20
      }
    ]
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `topPlans` | array | Array of top performing plans |

**TopPlanItem:**

| Field | Type | Description |
|-------|------|-------------|
| `rank` | int | Rank (1 = highest) |
| `planId` | guid | Plan ID |
| `planName` | string | Plan name |
| `price` | decimal | Plan price |
| `totalRevenue` | decimal | Total revenue |
| `activeSubscriptions` | int | Active subscriptions |
| `newSubscriptions` | int | New subscriptions in period |
| `conversionRate` | decimal | Conversion rate (%) |

---

### 6. Revenue Transactions

Get paginated list of revenue transactions.

**Endpoint:** `GET /api/admin/revenue/transactions`

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `pageNumber` | int | No | Page number (default: 1) |
| `pageSize` | int | No | Page size (default: 20, max: 100) |
| `startDate` | datetime | No | Filter by start date |
| `endDate` | datetime | No | Filter by end date |
| `planId` | guid | No | Filter by plan ID |
| `paymentStatus` | string | No | Filter by status: `PENDING`, `SUCCESS`, `CANCELLED`, `FAILED` |
| `searchTerm` | string | No | Search by user email, name, or order code |

**Example Request:**

```
GET /api/admin/revenue/transactions?pageNumber=1&pageSize=20&paymentStatus=SUCCESS
```

**Response:**

```json
{
  "status": "success",
  "code": "SUCCESS010",
  "message": "Revenue transactions retrieved successfully",
  "data": {
    "pageNumber": 1,
    "pageSize": 20,
    "totalCount": 150,
    "totalPages": 8,
    "transactions": [
      {
        "paymentId": "a1b2c3d4-1234-5678-90ab-cdef12345678",
        "orderCode": 1734567890123,
        "userId": "u1b2c3d4-1234-5678-90ab-cdef12345678",
        "userEmail": "user@example.com",
        "userName": "John Doe",
        "planId": "p1b2c3d4-1234-5678-90ab-cdef12345678",
        "planName": "Premium Monthly",
        "amount": 9.99,
        "paymentMethod": "payos",
        "paymentStatus": "SUCCESS",
        "createdAt": "2026-03-10T10:30:00Z",
        "paidAt": "2026-03-10T10:31:00Z"
      }
    ]
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `pageNumber` | int | Current page |
| `pageSize` | int | Items per page |
| `totalCount` | int | Total items |
| `totalPages` | int | Total pages |
| `transactions` | array | Array of transactions |

**TransactionDetail:**

| Field | Type | Description |
|-------|------|-------------|
| `paymentId` | guid | Payment ID |
| `orderCode` | long | PayOS order code |
| `userId` | guid | User ID |
| `userEmail` | string | User email |
| `userName` | string | User full name |
| `planId` | guid | Plan ID |
| `planName` | string | Plan name |
| `amount` | decimal | Payment amount |
| `paymentMethod` | string | Payment method |
| `paymentStatus` | string | Payment status |
| `createdAt` | datetime | Creation timestamp |
| `paidAt` | datetime | Payment completion timestamp |

---

### 7. MRR Breakdown

Get Monthly Recurring Revenue breakdown by month.

**Endpoint:** `GET /api/admin/revenue/mrr`

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `year` | int | No | Year to get MRR data for (default: current year) |

**Example Request:**

```
GET /api/admin/revenue/mrr?year=2026
```

**Response:**

```json
{
  "status": "success",
  "code": "SUCCESS010",
  "message": "MRR breakdown retrieved successfully",
  "data": {
    "currentMRR": 4500.00,
    "monthlyBreakdown": [
      {
        "month": 1,
        "year": 2026,
        "startMRR": 3000.00,
        "newMRR": 1000.00,
        "expansionMRR": 200.00,
        "churnMRR": 0.00,
        "contractionMRR": 0.00,
        "endMRR": 4200.00,
        "netMRR": 1200.00
      },
      {
        "month": 2,
        "year": 2026,
        "startMRR": 4200.00,
        "newMRR": 500.00,
        "expansionMRR": 100.00,
        "churnMRR": 200.00,
        "contractionMRR": 0.00,
        "endMRR": 4600.00,
        "netMRR": 400.00
      },
      {
        "month": 3,
        "year": 2026,
        "startMRR": 4600.00,
        "newMRR": 0.00,
        "expansionMRR": 0.00,
        "churnMRR": 100.00,
        "contractionMRR": 0.00,
        "endMRR": 4500.00,
        "netMRR": -100.00
      }
    ]
  }
}
```

**Response Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `currentMRR` | decimal | Current month's MRR |
| `monthlyBreakdown` | array | Array of monthly MRR data |

**MRRMonthData:**

| Field | Type | Description |
|-------|------|-------------|
| `month` | int | Month (1-12) |
| `year` | int | Year |
| `startMRR` | decimal | MRR at start of month |
| `newMRR` | decimal | New MRR from new subscriptions |
| `expansionMRR` | decimal | Expansion revenue (upgrades) |
| `churnMRR` | decimal | Churned MRR (cancellations) |
| `contractionMRR` | decimal | Contraction revenue (downgrades) |
| `endMRR` | decimal | MRR at end of month |
| `netMRR` | decimal | Net change in MRR |

---

### 8. Export Revenue Report

Export revenue report to Excel file.

**Endpoint:** `GET /api/admin/revenue/export`

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `reportType` | string | No | Report type: `overview`, `by-period`, `by-plan`, `transactions` (default: `overview`) |
| `startDate` | datetime | No | Start date for report |
| `endDate` | datetime | No | End date for report |
| `period` | string | No | Period type for `by-period` report: `daily`, `weekly`, `monthly`, `yearly` |
| `includeCharts` | bool | No | Include chart data (default: false) |

**Example Request:**

```
GET /api/admin/revenue/export?reportType=by-period&startDate=2026-01-01&endDate=2026-03-12&period=monthly
```

**Response:** Binary Excel file (.xlsx)

**Content-Type:** `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`

**Content-Disposition:** `attachment; filename=revenue_report_20260312103000.xlsx`

---

## Error Responses

### Error Codes

| Code | Description |
|------|-------------|
| `REVENUE001` | Invalid date range (StartDate must be before EndDate) |
| `REVENUE002` | Invalid period parameter |
| `REVENUE003` | Custom period requires StartDate and EndDate |
| `REVENUE004` | Invalid limit (must be between 1 and 10) |
| `AUTH003` | Forbidden (admin access required) |
| `AUTH002` | Token expired |

### Error Response Format

```json
{
  "status": "error",
  "code": "REVENUE001",
  "message": "StartDate must be before EndDate",
  "data": null
}
```

---

## HTTP Status Codes

| Code | Description |
|------|-------------|
| `200` | Success |
| `400` | Bad Request (invalid parameters) |
| `401` | Unauthorized (invalid/missing token) |
| `403` | Forbidden (not admin) |
| `500` | Internal Server Error |

---

## Notes

- All datetime values are in **UTC** (ISO 8601 format)
- All monetary values are in **VND** (Vietnamese Dong)
- Pagination is 1-indexed (first page is page 1)
- Default date format: `YYYY-MM-DDTHH:mm:ssZ`
- All endpoints return wrapped in `ApiResponse<T>` format
