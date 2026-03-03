# ?? PlantUML Diagrams - Document RAG System

Thý m?c này ch?a các PlantUML diagrams mô t? ki?n trúc và lu?ng ho?t ð?ng c?a h? th?ng AI Document Q&A v?i Hybrid RAG.

## ?? Danh sách Diagrams

### 1. `01_document_upload_flow.puml` - Document Upload Flow
**Sequence Diagram** mô t? chi ti?t lu?ng upload document:
- Request presigned URL
- Direct upload to B2
- Complete upload & trigger processing
- Background job: Extract ? Chunk ? Embed ? Store

**Key Points**:
- Frontend upload tr?c ti?p lên B2 (không qua VPS)
- Background processing v?i status tracking
- Error handling t?ng bý?c

---

### 2. `02_ai_qna_hybrid_rag_flow.puml` - AI Q&A Flow
**Sequence Diagram** mô t? lu?ng Hybrid RAG:
- User h?i câu h?i
- Embed question (Gemini)
- Search documents (Qdrant) + Query tasks (PostgreSQL)
- Build context
- Generate answer (Groq LLM)

**Key Points**:
- Hybrid approach: Documents + Task statistics
- Parallel data retrieval
- Context building strategy

---

### 3. `03_system_architecture.puml` - System Architecture
**Component Diagram** mô t? overall architecture:
- Frontend ? Backend ? External Services
- Controller ? Service ? Repository pattern
- Database và External APIs

**Key Points**:
- Layered architecture
- Service separation
- External service integrations

---

### 4. `04_data_flow_state.puml` - Data Flow State Machine
**State Diagram** mô t? tr?ng thái c?a document t? upload ð?n AI answer:
- Document Upload ? Processing ? Ready
- User Question ? Retrieval ? Generation ? Answer

**Key Points**:
- Document lifecycle states
- Processing timeline
- State transitions

---

### 5. `05_embedding_vector_detail.puml` - Embedding & Vector Storage
**Sequence Diagram** chi ti?t v?:
- Batch embedding generation
- Vector storage format
- Search query mechanism

**Key Points**:
- Gemini embedding API calls
- Qdrant upsert operations
- Vector search v?i filters

---

### 6. `06_error_handling_flow.puml` - Error Handling
**Sequence Diagram** mô t? error scenarios:
- Validation errors (immediate)
- Upload errors
- Processing errors (background)
- AI Q&A errors

**Key Points**:
- Status tracking cho failures
- Error messages
- User feedback

---

### 7. `07_database_schema.puml` - Database Schema
**Entity Relationship Diagram** mô t? PostgreSQL schema:
- GroupAttachments (document metadata)
- Tasks (for statistics)
- Groups, Users, GroupParticipants
- AIRequestLogs

**Key Points**:
- Document processing fields
- Relationships between entities
- Soft delete patterns

---

### 8. `08_qdrant_vector_structure.puml` - Qdrant Vector Structure
**Component Diagram** mô t? Qdrant collection structure:
- Vector format (768 dimensions)
- Payload schema
- Point ID format
- Example data

**Key Points**:
- Vector ID format: `{groupId}_{docId}_{chunkIndex}`
- Payload fields và purposes
- Indexing strategy

---

### 9. `09_api_endpoints_overview.puml` - API Endpoints
**Use Case Diagram** t?ng quan các endpoints:
- Document management endpoints
- AI Q&A endpoint
- User interactions

**Key Points**:
- Endpoint grouping
- Authentication requirements
- Request/Response formats

---

### 10. `10_class_diagram_services.puml` - Service Layer Classes
**Class Diagram** mô t? service layer:
- Interfaces và Implementations
- Dependencies between services
- Key methods

**Key Points**:
- Dependency Injection pattern
- Service responsibilities
- Repository pattern

---

## ?? Cách s? d?ng

### Online Viewer
M? file `.puml` và paste vào:
- [PlantUML Online Server](http://www.plantuml.com/plantuml/uml/)
- [PlantText](https://www.planttext.com/)

### VS Code
Install extension:
- **PlantUML** by jebbs

Xem diagram:
- `Alt+D` - Preview current diagram
- `Ctrl+Shift+P` ? "PlantUML: Export Current Diagram"

### Command Line
```bash
# Install PlantUML
npm install -g node-plantuml

# Generate PNG
puml generate 01_document_upload_flow.puml -o output.png

# Generate SVG
puml generate 01_document_upload_flow.puml -o output.svg -t svg
```

---

## ?? Diagram Legend

| Màu | ? ngh?a |
|-----|---------|
| ?? LightBlue | POST endpoints / Create actions |
| ?? LightGreen | GET endpoints / Read actions |
| ?? LightCoral | DELETE endpoints / Remove actions |
| ?? LightYellow | AI/ML processing |

---

## ?? Update Diagrams

Khi có thay ð?i backend logic:
1. Update file `.puml` týõng ?ng
2. Re-export diagrams n?u c?n
3. Update README n?u thêm diagram m?i

---

## ?? References

- [PlantUML Documentation](https://plantuml.com/)
- [PlantUML Sequence Diagram Guide](https://plantuml.com/sequence-diagram)
- [PlantUML Class Diagram Guide](https://plantuml.com/class-diagram)
- [PlantUML State Diagram Guide](https://plantuml.com/state-diagram)

---

**Last Updated**: 2024-03-10
**Maintained By**: Study Studio Development Team
