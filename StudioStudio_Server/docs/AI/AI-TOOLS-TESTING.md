# AI Tools Testing Prompts

Document này chứa các sample prompts để test tất cả 18 AI tools còn lại trong pipeline.

## Cấu trúc

```
├── Personal AI Tools (3)
├── Group AI Tools (8)
└── Studio/Master AI Tools (7)
```

---

## Personal AI (User-scoped, no group_id)

### 1. get_personal_tasks
```
Liệt kê công việc cá nhân của tôi
```
```
Xem 5 công việc cá nhân gần nhất
```
```
Tôi có bao nhiêu task đang làm dở?
```
```
Show me my personal tasks
```

### 2. get_personal_deadlines
```
Cho tôi xem các deadline sắp tới
```
```
Có deadline nào trong 7 ngày tới không?
```
```
Những công việc cá nhân nào sắp hết hạn?
```
```
What are my upcoming deadlines in the next month?
```

### 3. get_personal_stats
```
Thống kê công việc cá nhân của tôi
```
```
Cho tôi xem tiến độ học tập của tôi
```
```
Xem productivity score của tôi
```
```
Show me my personal productivity stats
```

---

## Group AI (group-scoped, group_id from context)

### 4. get_tasks
```
Xem danh sách công việc của nhóm
```
```
Liệt kê các task đang in progress
```
```
Tìm task có priority cao
```
```
Những công việc nào đã quá hạn?
```
```
Xem trang 2 của danh sách task
```
```
Tìm task có từ khóa "backend"
```
```
Show me tasks with priority High
```
```
What tasks are not started yet?
```
```
Xem các task severity Major
```
```
Những task nào có priority từ Medium trở lên?
```

### 5. get_group_stats
```
Thống kê tổng quan của nhóm
```
```
Cho tôi xem số liệu của nhóm
```
```
Group stats summary
```
```
Tình hình công việc của nhóm như thế nào?
```

### 6. get_members
```
Xem danh sách thành viên nhóm
```
```
Ai là thành viên trong nhóm này?
```
```
Liệt kê các Moderator của nhóm
```
```
Show me all members in this group
```

### 7. get_deadlines
```
Những công việc nào sắp đến deadline?
```
```
Xem deadline của nhóm trong 14 ngày tới
```
```
Có task nào quá hạn không?
```
```
Show me upcoming deadlines for the next week
```
```
What are the overdue tasks in this group?
```

### 8. search_documents
```
Tìm tài liệu về [chủ đề]
```
```
Search trong tài liệu nhóm với từ khóa "meeting notes"
```
```
Có tài liệu nào liên quan đến [từ khóa] không?
```
```
Search documents about project requirements
```
```
Tìm tài liệu trong document có id cụ thể
```

### 9. get_group_documents
```
Xem danh sách tài liệu đã upload lên nhóm
```
```
Liệt kê các file của nhóm
```
```
Show me all documents in this group
```
```
Xem 5 tài liệu mới nhất của nhóm
```

### 10. get_group_performance
```
Phân tích hiệu suất của nhóm
```
```
Nhóm hoạt động tốt không?
```
```
Performance report for this group
```
```
Đánh giá tiến độ của nhóm
```

### 11. get_group_risk
```
Phân tích rủi ro của nhóm
```
```
Nhóm có vấn đề gì không?
```
```
Risk analysis for this group
```
```
Có dấu hiệu nhóm không hoạt động tốt không?
```

---

## Studio/Master AI (studio-scoped, studio_id from context, owner only)

### 12. get_studio_groups
```
Xem danh sách tất cả các nhóm trong studio
```
```
Liệt kê các nhóm trong workspace của tôi
```
```
Show me all groups in my studio
```
```
Xem thống kê nhanh của các nhóm
```

### 13. get_studio_analytics
```
Thống kê toàn bộ studio
```
```
Tổng quan hoạt động của workspace
```
```
Studio analytics for the last month
```
```
Show me studio analytics
```

### 14. compare_groups
```
So sánh các nhóm trong studio
```
```
Nhóm nào hoạt động tốt nhất?
```
```
So sánh hiệu suất giữa các nhóm
```
```
Compare groups by completion rate
```
```
Which group has the most overdue tasks?
```

### 15. get_member_permissions
```
Kiểm tra quyền của tôi trong studio
```
```
Tôi có quyền gì trong workspace này?
```
```
Check my permissions in this studio
```

### 16. get_studio_health
```
Kiểm tra sức khoẻ của studio
```
```
Studio có đang hoạt động tốt không?
```
```
Overall health check for my workspace
```
```
Tổng quan tình trạng workspace
```

### 17. search_studio_documents
```
Tìm tài liệu trong toàn bộ studio
```
```
Search tất cả tài liệu với từ khóa "API documentation"
```
```
Có tài liệu nào về [chủ đề] trong workspace không?
```
```
Search all documents across all groups
```
```
Find documents about project guidelines
```

### 18. get_risk_groups
```
Những nhóm nào đang có vấn đề?
```
```
Xác định các nhóm có nguy cơ
```
```
Risk groups analysis for studio
```
```
Which groups need attention?
```

---

## Multi-Tool Prompts (Test ReAct Loop)

Những prompts này yêu cầu nhiều hơn 1 tool call:

```
"Tổng hợp tình hình nhóm: liệt kê công việc đang chậm tiến độ, xem deadline sắp tới, và phân tích rủi ro"
```
→ expects: get_tasks + get_deadlines + get_group_risk

```
"So sánh hiệu suất các nhóm và cho tôi xem analytics tổng quan của studio"
```
→ expects: compare_groups + get_studio_analytics

```
"Xem thống kê cá nhân của tôi, liệt kê deadline sắp tới, và kiểm tra công việc đang làm dở"
```
→ expects: get_personal_stats + get_personal_deadlines + get_personal_tasks

```
"Phân tích toàn bộ studio: nhóm nào hoạt động tốt, nhóm nào cần cải thiện, và tìm tài liệu về [chủ đề]"
```
→ expects: compare_groups + get_risk_groups + search_studio_documents

```
"Tìm tài liệu liên quan đến [topic], xem danh sách công việc liên quan, và kiểm tra ai đang phụ trách"
```
→ expects: search_documents + get_tasks + get_members

```
"Xem thống kê nhóm, so sánh với các nhóm khác trong studio, và đề xuất cải thiện"
```
→ expects: get_group_stats + compare_groups

---

## Edge Cases & Error Handling

### Test permission errors:
```
User không phải member → tất cả group tools phải trả lỗi
```
```
Non-owner access Master AI → phải reject
```

### Test empty states:
```
Nhóm không có task nào
```
```
Không có tài liệu nào trong nhóm
```
```
Studio không có nhóm nào
```
```
Search không ra kết quả
```

### Test boundary values:
```
Yêu cầu page_size = 1000 cho get_tasks
```
```
days_ahead = 365 cho get_deadlines
```
```
threshold = 100 cho get_risk_groups
```

### Test invalid parameters:
```
Search với query rỗng
```
```
Filter status = giá trị không hợp lệ
```
```
priority = giá trị không tồn tại
```

---

## Test Scripts (curl)

### Personal AI
```bash
# get_personal_tasks
curl -X POST http://localhost:5006/api/ai/personal/ask \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"message": "Liệt kê công việc cá nhân của tôi"}'

# get_personal_deadlines
curl -X POST http://localhost:5006/api/ai/personal/ask \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"message": "Có deadline nào trong 7 ngày tới không?"}'

# get_personal_stats
curl -X POST http://localhost:5006/api/ai/personal/ask \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"message": "Thống kê công việc cá nhân của tôi"}'
```

### Group AI
```bash
# get_tasks
curl -X POST http://localhost:5006/api/ai/group/<groupId>/ask \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"message": "Xem danh sách công việc của nhóm"}'

# get_group_stats
curl -X POST http://localhost:5006/api/ai/group/<groupId>/ask \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"message": "Thống kê tổng quan của nhóm"}'

# search_documents
curl -X POST http://localhost:5006/api/ai/group/<groupId>/ask \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"message": "Tìm tài liệu về meeting notes"}'

# get_deadlines
curl -X POST http://localhost:5006/api/ai/group/<groupId>/ask \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"message": "Những công việc nào sắp đến deadline?"}'

# get_group_risk
curl -X POST http://localhost:5006/api/ai/group/<groupId>/ask \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"message": "Phân tích rủi ro của nhóm"}'
```

### Studio/Master AI
```bash
# get_studio_groups
curl -X POST http://localhost:5006/api/ai/master/<studioId>/ask \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"message": "Xem danh sách tất cả các nhóm trong studio"}'

# compare_groups
curl -X POST http://localhost:5006/api/ai/master/<studioId>/ask \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"message": "So sánh các nhóm trong studio"}'

# get_studio_health
curl -X POST http://localhost:5006/api/ai/master/<studioId>/ask \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"message": "Kiểm tra sức khoẻ của studio"}'

# get_risk_groups
curl -X POST http://localhost:5006/api/ai/master/<studioId>/ask \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"message": "Những nhóm nào đang có vấn đề?"}'

# search_studio_documents
curl -X POST http://localhost:5006/api/ai/master/<studioId>/ask \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"message": "Tìm tài liệu trong toàn bộ studio về API"}'
```

### Streaming
```bash
# Add /stream endpoint
curl -X POST http://localhost:5006/api/ai/group/<groupId>/ask/stream \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"message": "Phân tích nhóm này"}'
```

---

## Verification Checklist

Sau khi test, kiểm tra:

- [ ] Tất cả tools trả về đúng format JSON
- [ ] Không có tool nào crash server
- [ ] Token usage được ghi vào AIRequestLog
- [ ] Streaming SSE events đúng format (metadata, chunk, done)
- [ ] Rate limiting hoạt động (1 request/user/prompt)
- [ ] Permission checks hoạt động đúng
- [ ] N+1 đã fix: với studio có 10+ groups, thời gian response không tăng tuyến tính
- [ ] Load-to-memory đã fix: với group có 1000+ tasks, response không bị OOM
- [ ] ReAct loop không exceed 5 tool calls cho simple queries
