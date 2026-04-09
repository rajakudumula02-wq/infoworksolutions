import { describe, it, expect, beforeEach } from "vitest";
import fc from "fast-check";
import { StorageServiceImpl } from "./index.js";
import type { AnalysisResult, AnalysisRequest, ImageMeta, Bytes } from "../types.js";

// ─── Helpers ──────────────────────────────────────────────────────────────────

function makeRequest(overrides: Partial<AnalysisRequest> = {}): AnalysisRequest {
  return {
    analysisId: "analysis-1",
    userId: "user-1",
    uploadTimestamp: new Date().toISOString(),
    status: "complete",
    imageMeta: {
      filename: "test.png",
      format: "png",
      fileSizeBytes: 1024,
      uploadTimestamp: new Date().toISOString(),
    },
    ...overrides,
  };
}

function makeResult(overrides: Partial<AnalysisResult> = {}): AnalysisResult {
  const request = makeRequest();
  return {
    request,
    report: {
      analysisId: request.analysisId,
      completedAt: new Date().toISOString(),
      indicators: [
        { indicator: "cavity_risk", score: 50, confidence: 0.8 },
        { indicator: "gum_health", score: 70, confidence: 0.9 },
        { indicator: "plaque_level", score: 30, confidence: 0.75 },
        { indicator: "overall_oral_health", score: 60, confidence: 0.85 },
      ],
      imageMeta: request.imageMeta,
    },
    error: null,
    ...overrides,
  };
}

function makeImageBytes(size = 64): Bytes {
  const b = new Uint8Array(size);
  for (let i = 0; i < size; i++) b[i] = i % 256;
  return b;
}

const META: ImageMeta = {
  filename: "scan.png",
  format: "png",
  fileSizeBytes: 64,
  uploadTimestamp: "2024-01-01T00:00:00.000Z",
};

// ─── Unit tests ───────────────────────────────────────────────────────────────

describe("StorageServiceImpl", () => {
  let svc: StorageServiceImpl;

  beforeEach(() => {
    svc = new StorageServiceImpl();
  });

  describe("saveImage / getImage", () => {
    it("round-trips image bytes", () => {
      const bytes = makeImageBytes(128);
      svc.saveImage("user-1", "analysis-1", bytes, META);
      const retrieved = svc.getImage("analysis-1");
      expect(retrieved).toEqual(bytes);
    });

    it("throws when analysisId not found", () => {
      expect(() => svc.getImage("nonexistent")).toThrow();
    });

    it("stores bytes encrypted (raw store differs from original)", () => {
      const bytes = makeImageBytes(32);
      svc.saveImage("user-1", "analysis-1", bytes, META);
      // Access internal store to verify encryption
      const internal = (svc as any).imageStore.get("analysis-1");
      expect(internal.encryptedBytes).not.toEqual(bytes);
    });
  });

  describe("saveResult / getResult", () => {
    it("returns null for unknown analysisId", () => {
      expect(svc.getResult("unknown")).toBeNull();
    });

    it("round-trips an AnalysisResult", () => {
      const result = makeResult();
      svc.saveResult(result);
      const retrieved = svc.getResult(result.request.analysisId);
      expect(retrieved).toEqual(result);
    });

    it("overwrites an existing result", () => {
      const result = makeResult();
      svc.saveResult(result);
      const updated = { ...result, error: { code: "model_error" as const, message: "fail" } };
      svc.saveResult(updated);
      expect(svc.getResult(result.request.analysisId)).toEqual(updated);
    });
  });

  describe("listResults", () => {
    it("returns empty page for user with no results", () => {
      const page = svc.listResults("user-x", null, 20);
      expect(page.items).toHaveLength(0);
      expect(page.nextCursor).toBeNull();
    });

    it("returns only results for the requested user", () => {
      const r1 = makeResult({ request: makeRequest({ userId: "user-A", analysisId: "a1" }) });
      const r2 = makeResult({ request: makeRequest({ userId: "user-B", analysisId: "b1" }) });
      svc.saveResult(r1);
      svc.saveResult(r2);
      const page = svc.listResults("user-A", null, 20);
      expect(page.items).toHaveLength(1);
      expect(page.items[0].request.userId).toBe("user-A");
    });

    it("caps page size at 20", () => {
      for (let i = 0; i < 25; i++) {
        svc.saveResult(makeResult({
          request: makeRequest({ analysisId: `a${i}`, userId: "user-1" }),
        }));
      }
      const page = svc.listResults("user-1", null, 25);
      expect(page.items.length).toBeLessThanOrEqual(20);
      expect(page.nextCursor).not.toBeNull();
    });

    it("returns nextCursor=null when all results fit in one page", () => {
      for (let i = 0; i < 5; i++) {
        svc.saveResult(makeResult({
          request: makeRequest({ analysisId: `a${i}`, userId: "user-1" }),
        }));
      }
      const page = svc.listResults("user-1", null, 20);
      expect(page.items).toHaveLength(5);
      expect(page.nextCursor).toBeNull();
    });

    it("paginates correctly across pages", () => {
      for (let i = 0; i < 25; i++) {
        svc.saveResult(makeResult({
          request: makeRequest({ analysisId: `a${i}`, userId: "user-1" }),
        }));
      }
      const page1 = svc.listResults("user-1", null, 20);
      expect(page1.items).toHaveLength(20);
      expect(page1.nextCursor).not.toBeNull();

      const page2 = svc.listResults("user-1", page1.nextCursor, 20);
      expect(page2.items).toHaveLength(5);
      expect(page2.nextCursor).toBeNull();
    });

    it("sorts results descending by uploadTimestamp", () => {
      const timestamps = [
        "2024-01-03T00:00:00.000Z",
        "2024-01-01T00:00:00.000Z",
        "2024-01-02T00:00:00.000Z",
      ];
      timestamps.forEach((ts, i) => {
        svc.saveResult(makeResult({
          request: makeRequest({ analysisId: `a${i}`, userId: "user-1", uploadTimestamp: ts }),
        }));
      });
      const page = svc.listResults("user-1", null, 20);
      const returned = page.items.map((r) => r.request.uploadTimestamp);
      expect(returned[0]).toBe("2024-01-03T00:00:00.000Z");
      expect(returned[1]).toBe("2024-01-02T00:00:00.000Z");
      expect(returned[2]).toBe("2024-01-01T00:00:00.000Z");
    });
  });
});

// ─── Property-based tests ─────────────────────────────────────────────────────

// ── Arbitraries ───────────────────────────────────────────────────────────────

const arbIso = fc.date({ min: new Date("2020-01-01"), max: new Date("2030-01-01") })
  .map((d) => d.toISOString());

const arbIndicatorName = fc.constantFrom(
  "cavity_risk" as const,
  "gum_health" as const,
  "plaque_level" as const,
  "overall_oral_health" as const
);

const arbImageMeta = fc.record({
  filename: fc.string({ minLength: 1, maxLength: 40 }),
  format: fc.constantFrom("jpeg" as const, "png" as const, "dicom" as const),
  fileSizeBytes: fc.integer({ min: 1, max: 20 * 1024 * 1024 }),
  uploadTimestamp: arbIso,
});

const arbRequest = (userId?: string, analysisId?: string) =>
  fc.record({
    analysisId: analysisId !== undefined ? fc.constant(analysisId) : fc.uuid(),
    userId: userId !== undefined ? fc.constant(userId) : fc.uuid(),
    uploadTimestamp: arbIso,
    status: fc.constant("complete" as const),
    imageMeta: arbImageMeta,
  });

const arbResult = (userId?: string, analysisId?: string) =>
  arbRequest(userId, analysisId).chain((req) =>
    fc.record({
      request: fc.constant(req),
      report: fc.option(
        fc.record({
          analysisId: fc.constant(req.analysisId),
          completedAt: arbIso,
          indicators: fc.array(
            fc.record({
              indicator: arbIndicatorName,
              score: fc.integer({ min: 0, max: 100 }),
              confidence: fc.float({ min: 0, max: 1, noNaN: true }),
            }),
            { minLength: 1, maxLength: 4 }
          ),
          imageMeta: fc.constant(req.imageMeta),
        }),
        { nil: null }
      ),
      error: fc.constant(null),
    })
  );

// ── Property 10: Completed results are persisted per user ─────────────────────
// Feature: dental-image-score-analysis, Property 10: Completed results are persisted per user
describe("Property 10: Completed results are persisted per user", () => {
  it("saved result appears in listResults for the owning user", () => {
    fc.assert(
      fc.property(fc.uuid(), fc.uuid(), (userId, analysisId) => {
        const svc = new StorageServiceImpl();
        const result = {
          request: makeRequest({ userId, analysisId }),
          report: null,
          error: null,
        };
        svc.saveResult(result);
        const page = svc.listResults(userId, null, 20);
        return page.items.some((r) => r.request.analysisId === analysisId);
      }),
      { numRuns: 100 }
    );
  });
});

// ── Property 11: History is sorted descending by upload timestamp ─────────────
// Feature: dental-image-score-analysis, Property 11: History is sorted descending by upload timestamp
describe("Property 11: History is sorted descending by upload timestamp", () => {
  it("listResults returns items in descending uploadTimestamp order", () => {
    fc.assert(
      fc.property(
        fc.uuid(),
        fc.array(arbIso, { minLength: 2, maxLength: 15 }),
        (userId, timestamps) => {
          const svc = new StorageServiceImpl();
          timestamps.forEach((ts, i) => {
            svc.saveResult({
              request: makeRequest({ userId, analysisId: `id-${i}`, uploadTimestamp: ts }),
              report: null,
              error: null,
            });
          });
          const page = svc.listResults(userId, null, 20);
          const returned = page.items.map((r) => r.request.uploadTimestamp);
          for (let i = 1; i < returned.length; i++) {
            if (returned[i] > returned[i - 1]) return false;
          }
          return true;
        }
      ),
      { numRuns: 100 }
    );
  });
});

// ── Property 12: Pagination structure invariants ──────────────────────────────
// Feature: dental-image-score-analysis, Property 12: Pagination structure invariants
describe("Property 12: Pagination structure invariants", () => {
  it("page size is always ≤ 20 and nextCursor is non-null iff more results exist", () => {
    fc.assert(
      fc.property(
        fc.uuid(),
        fc.integer({ min: 1, max: 40 }),
        (userId, totalCount) => {
          const svc = new StorageServiceImpl();
          for (let i = 0; i < totalCount; i++) {
            svc.saveResult({
              request: makeRequest({ userId, analysisId: `id-${i}` }),
              report: null,
              error: null,
            });
          }
          const page = svc.listResults(userId, null, 20);

          // Page size must be ≤ 20
          if (page.items.length > 20) return false;

          // nextCursor must be non-null iff more results exist beyond this page
          const hasMore = totalCount > 20;
          if (hasMore && page.nextCursor === null) return false;
          if (!hasMore && page.nextCursor !== null) return false;

          return true;
        }
      ),
      { numRuns: 100 }
    );
  });
});
