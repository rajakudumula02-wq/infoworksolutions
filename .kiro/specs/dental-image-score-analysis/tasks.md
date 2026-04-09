# Implementation Plan: Dental Image Score Analysis

## Overview

Implement an async REST API pipeline for AI-powered dental image analysis. The implementation follows four layers: API, Validation, AI Analysis, and Storage. TypeScript is used throughout.

## Tasks

- [x] 1. Set up project structure, core types, and interfaces
  - Create directory structure for api, validation, ai, storage, and queue modules
  - Define all TypeScript types: `AnalysisRequest`, `ImageMeta`, `IndicatorScore`, `ScoreReport`, `AnalysisResult`, `AnalysisError`, `ValidationResult`, `QualityCheckResult`, `ModelOutput`
  - Define all service interfaces: `ValidationService`, `ImageQualityChecker`, `AnalysisQueue`, `AIWorker`, `AIModelClient`, `StorageService`, `ScoreReportSerializer`
  - Set up testing framework and property-based testing library (`fast-check`)
  - _Requirements: 1.1, 2.2, 2.3, 2.4, 2.5, 3.5, 3.6_

- [x] 2. Implement ValidationService and ImageQualityChecker
  - [x] 2.1 Implement `ValidationService.validateUpload`
    - Check MIME type / extension against `image/jpeg`, `image/png`, `application/dicom`; return 415 on mismatch
    - Check file size ≤ 20 MB; return 400 on violation
    - Decode image header to read pixel dimensions; return 422 if below 300×300
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 5.2_

  - [x] 2.2 Write property test for format validation (Property 1)
    - **Property 1: Format validation accepts supported types and rejects unsupported types**
    - **Validates: Requirements 1.1, 1.3, 1.5**
    - Tag: `// Feature: dental-image-score-analysis, Property 1: Format validation accepts supported types and rejects unsupported types`

  - [x] 2.3 Write property test for size validation (Property 2)
    - **Property 2: Size validation rejects oversized files with HTTP 400**
    - **Validates: Requirements 1.2, 1.4**
    - Tag: `// Feature: dental-image-score-analysis, Property 2: Size validation rejects oversized files with HTTP 400`

  - [x] 2.4 Write property test for low-resolution rejection (Property 13)
    - **Property 13: Low-resolution images are rejected and not stored**
    - **Validates: Requirements 5.2, 5.4**
    - Tag: `// Feature: dental-image-score-analysis, Property 13: Low-resolution images are rejected and not stored`

  - [x] 2.5 Implement `ImageQualityChecker.check`
    - Laplacian variance heuristic for blur detection
    - Histogram analysis for overexposed / underexposed detection
    - Return `{ pass: false; reason; detail }` with non-empty detail string on failure
    - _Requirements: 2.7, 5.1, 5.3_

  - [x] 2.6 Write property test for quality error description (Property 6)
    - **Property 6: Quality error includes a descriptive reason**
    - **Validates: Requirements 2.7, 5.3**
    - Tag: `// Feature: dental-image-score-analysis, Property 6: Quality error includes a descriptive reason`

- [x] 3. Implement StorageService
  - [x] 3.1 Implement `StorageService` with in-memory or DB-backed store
    - `saveImage` — store encrypted image bytes keyed by `(userId, analysisId)`
    - `getImage` — retrieve bytes by `analysisId`
    - `saveResult` / `getResult` — persist and fetch `AnalysisResult` records
    - `listResults` — return cursor-paginated results sorted by `uploadTimestamp` descending, max 20 per page
    - Enforce encryption-at-rest for both object store and DB records
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 6.4_

  - [x] 3.2 Write property test for persistence per user (Property 10)
    - **Property 10: Completed results are persisted per user**
    - **Validates: Requirements 4.1**
    - Tag: `// Feature: dental-image-score-analysis, Property 10: Completed results are persisted per user`

  - [x] 3.3 Write property test for history sort order (Property 11)
    - **Property 11: History is sorted descending by upload timestamp**
    - **Validates: Requirements 4.2**
    - Tag: `// Feature: dental-image-score-analysis, Property 11: History is sorted descending by upload timestamp`

  - [x] 3.4 Write property test for pagination invariants (Property 12)
    - **Property 12: Pagination structure invariants**
    - **Validates: Requirements 4.3, 4.4**
    - Tag: `// Feature: dental-image-score-analysis, Property 12: Pagination structure invariants`

- [x] 4. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Implement ScoreReportSerializer
  - [x] 5.1 Implement `ScoreReportSerializer.serialize` and `deserialize`
    - `serialize`: produce valid JSON string from `ScoreReport`
    - `deserialize`: parse JSON string into `ScoreReport`; throw `ParseError` on malformed input
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

  - [x] 5.2 Write property test for serialization round-trip (Property 16)
    - **Property 16: Score_Report serialization round-trip**
    - **Validates: Requirements 7.1, 7.2, 7.4, 7.5**
    - Tag: `// Feature: dental-image-score-analysis, Property 16: Score_Report serialization round-trip`

  - [x] 5.3 Write property test for malformed JSON rejection (Property 17)
    - **Property 17: Malformed JSON returns HTTP 400**
    - **Validates: Requirements 7.3**
    - Tag: `// Feature: dental-image-score-analysis, Property 17: Malformed JSON returns HTTP 400`

- [x] 6. Implement AnalysisQueue and AIWorker
  - [x] 6.1 Implement `AnalysisQueue`
    - `enqueue`, `dequeue`, `markComplete`, `markFailed` operations
    - _Requirements: 2.1, 2.6_

  - [x] 6.2 Implement `AIWorker.process`
    - Dequeue `AnalysisRequest`, fetch image from `StorageService`
    - Run `ImageQualityChecker`; on failure call `markFailed` with `quality_error`
    - Call `AIModelClient.infer`; on failure call `markFailed` with `model_error`
    - On success build `ScoreReport` and call `markComplete`, then `StorageService.saveResult`
    - Ensure inference completes within 30 seconds
    - _Requirements: 2.1, 2.6, 2.7, 4.1_

  - [x] 6.3 Implement stub `AIModelClient.infer`
    - Return a `ModelOutput` with all four indicators (`cavity_risk`, `gum_health`, `plaque_level`, `overall_oral_health`), scores in [0, 100], confidences in [0.0, 1.0]
    - _Requirements: 2.2, 2.3, 2.4, 2.5_

  - [x] 6.4 Write property test for Score_Report structure invariant (Property 4)
    - **Property 4: Score_Report structure invariant**
    - **Validates: Requirements 2.2, 2.3, 3.5, 3.6**
    - Tag: `// Feature: dental-image-score-analysis, Property 4: Score_Report structure invariant`

  - [x] 6.5 Write property test for indicator score and confidence ranges (Property 5)
    - **Property 5: Indicator score and confidence range invariants**
    - **Validates: Requirements 2.4, 2.5**
    - Tag: `// Feature: dental-image-score-analysis, Property 5: Indicator score and confidence range invariants`

- [x] 7. Implement API endpoints with auth middleware
  - [x] 7.1 Implement JWT auth middleware
    - Reject any request missing or carrying an invalid bearer token with HTTP 401
    - Attach authenticated `userId` to request context
    - _Requirements: 6.1_

  - [x] 7.2 Write property test for unauthenticated rejection (Property 14)
    - **Property 14: Unauthenticated requests are rejected**
    - **Validates: Requirements 6.1**
    - Tag: `// Feature: dental-image-score-analysis, Property 14: Unauthenticated requests are rejected`

  - [x] 7.3 Implement `POST /analyses`
    - Run `ValidationService.validateUpload`; return appropriate 400 / 415 / 422 on failure
    - Store image via `StorageService.saveImage`
    - Generate UUID v4 `analysisId`, enqueue `AnalysisRequest`, return 202 `{ analysisId, status: "pending" }` within 2 seconds
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 5.4_

  - [x] 7.4 Write property test for upload ID uniqueness (Property 3)
    - **Property 3: Upload IDs are unique across all requests**
    - **Validates: Requirements 1.6**
    - Tag: `// Feature: dental-image-score-analysis, Property 3: Upload IDs are unique across all requests`

  - [x] 7.5 Implement `GET /analyses/{analysisId}`
    - Verify `analysisId` belongs to authenticated user; return 403 if not
    - Return 404 with error envelope if `analysisId` unknown
    - Return 202 `{ analysisId, status: "processing" }` while pending/processing
    - Return 200 `ScoreReport` when complete; return 500 with generic message on `model_error` / `storage_error`
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 6.2, 6.3_

  - [x] 7.6 Write property test for retrieval round-trip (Property 7)
    - **Property 7: Analysis retrieval round-trip**
    - **Validates: Requirements 3.1, 3.3**
    - Tag: `// Feature: dental-image-score-analysis, Property 7: Analysis retrieval round-trip`

  - [x] 7.7 Write property test for in-progress status (Property 8)
    - **Property 8: In-progress requests return 202**
    - **Validates: Requirements 3.2**
    - Tag: `// Feature: dental-image-score-analysis, Property 8: In-progress requests return 202`

  - [x] 7.8 Write property test for unknown ID returning 404 (Property 9)
    - **Property 9: Unknown analysis ID returns 404**
    - **Validates: Requirements 3.4**
    - Tag: `// Feature: dental-image-score-analysis, Property 9: Unknown analysis ID returns 404`

  - [x] 7.9 Write property test for cross-user 403 (Property 15)
    - **Property 15: Cross-user access is forbidden**
    - **Validates: Requirements 6.2, 6.3**
    - Tag: `// Feature: dental-image-score-analysis, Property 15: Cross-user access is forbidden`

  - [x] 7.10 Implement `GET /analyses`
    - Return paginated history for authenticated user, sorted by `uploadTimestamp` descending
    - Default page size 20; include `nextCursor` when more results exist
    - Return results within 3 seconds
    - _Requirements: 4.2, 4.3, 4.4, 4.5_

- [x] 8. Wire all components together
  - [x] 8.1 Connect API handlers to ValidationService, StorageService, AnalysisQueue, and ScoreReportSerializer
    - Ensure error envelope `{ error: { code, message } }` is used consistently across all error responses
    - _Requirements: 1.4, 1.5, 3.4, 5.2, 5.3, 6.1, 6.2, 6.3, 7.3_

  - [x] 8.2 Connect AIWorker to AnalysisQueue, AIModelClient, and StorageService
    - Ensure worker loop processes queued requests end-to-end
    - _Requirements: 2.1, 2.6, 4.1_

  - [x] 8.3 Write unit tests for happy-path and edge cases
    - Happy path: upload → queue → worker → retrieve
    - Edge cases: exactly 20 MB file, exactly 300×300 image, confidence = 0.0 and 1.0, score = 0 and 100
    - _Requirements: 1.2, 1.6, 2.4, 2.5, 5.2_

- [x] 9. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for a faster MVP
- Each task references specific requirements for traceability
- Property tests must run a minimum of 100 iterations and include the comment tag referencing the property
- Checkpoints ensure incremental validation before moving to the next layer
