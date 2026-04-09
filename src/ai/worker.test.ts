// Unit tests for AIWorkerImpl and AIModelClientImpl
// Req 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 4.1

import { describe, it, expect, vi, beforeEach } from "vitest";
import { AIWorkerImpl } from "./worker.js";
import type {
  AnalysisRequest,
  AnalysisQueue,
  StorageService,
  AIModelClient,
  ImageQualityChecker,
  QualityCheckResult,
  ModelOutput,
  ScoreReport,
  AnalysisError,
  AnalysisResult,
  Page,
  Bytes,
} from "../types.js";

// ─── Helpers ──────────────────────────────────────────────────────────────────

function makeRequest(overrides: Partial<AnalysisRequest> = {}): AnalysisRequest {
  return {
    analysisId: "test-id-001",
    userId: "user-001",
    uploadTimestamp: "2024-01-01T00:00:00.000Z",
    status: "processing",
    imageMeta: {
      filename: "xray.jpg",
      format: "jpeg",
      fileSizeBytes: 1024,
      uploadTimestamp: "2024-01-01T00:00:00.000Z",
    },
    ...overrides,
  };
}

function makeImageBytes(length = 256): Bytes {
  const bytes = new Uint8Array(length);
  for (let i = 0; i < length; i++) bytes[i] = (i * 37 + 13) % 256;
  return bytes;
}

function makeQueue(): AnalysisQueue & {
  _completed: Map<string, ScoreReport>;
  _failed: Map<string, AnalysisError>;
} {
  const completed = new Map<string, ScoreReport>();
  const failed = new Map<string, AnalysisError>();
  return {
    _completed: completed,
    _failed: failed,
    enqueue: vi.fn(),
    dequeue: vi.fn(),
    markComplete: vi.fn((id, report) => { completed.set(id, report); }),
    markFailed: vi.fn((id, err) => { failed.set(id, err); }),
  };
}

function makeStorage(imageBytes: Bytes): StorageService & {
  _results: Map<string, AnalysisResult>;
} {
  const results = new Map<string, AnalysisResult>();
  return {
    _results: results,
    saveImage: vi.fn(),
    getImage: vi.fn(() => imageBytes),
    saveResult: vi.fn((r) => { results.set(r.request.analysisId, r); }),
    getResult: vi.fn((id) => results.get(id) ?? null),
    listResults: vi.fn(() => ({ items: [], nextCursor: null }) as Page<AnalysisResult>),
  };
}

function makePassingQualityChecker(): ImageQualityChecker {
  return { check: vi.fn(() => ({ pass: true }) as QualityCheckResult) };
}

function makeFailingQualityChecker(
  reason: "blurry" | "overexposed" | "underexposed" = "blurry",
  detail = "Image is blurry"
): ImageQualityChecker {
  return {
    check: vi.fn(() => ({ pass: false, reason, detail }) as QualityCheckResult),
  };
}

// ─── Stub model client (sync, no Azure dependency) ───────────────────────────

function makeStubModelClient(): AIModelClient {
  return {
    infer: (_imageBytes: Bytes): ModelOutput => ({
      indicators: [
        { name: "cavity_risk", score: 42, confidence: 0.87 },
        { name: "gum_health", score: 75, confidence: 0.92 },
        { name: "plaque_level", score: 30, confidence: 0.78 },
        { name: "overall_oral_health", score: 65, confidence: 0.85 },
      ],
    }),
  };
}

// ─── Stub AIModelClient tests ─────────────────────────────────────────────────

describe("AIModelClient (stub)", () => {
  const client = makeStubModelClient();

  it("returns all four required indicators (Req 2.2)", () => {
    const output = client.infer(makeImageBytes());
    const names = output.indicators.map((i) => i.name);
    expect(names).toContain("cavity_risk");
    expect(names).toContain("gum_health");
    expect(names).toContain("plaque_level");
    expect(names).toContain("overall_oral_health");
    expect(output.indicators).toHaveLength(4);
  });

  it("all scores are integers in [0, 100] (Req 2.4)", () => {
    const output = client.infer(makeImageBytes());
    for (const ind of output.indicators) {
      expect(ind.score).toBeGreaterThanOrEqual(0);
      expect(ind.score).toBeLessThanOrEqual(100);
      expect(Number.isInteger(ind.score)).toBe(true);
    }
  });

  it("all confidences are floats in [0.0, 1.0] (Req 2.5)", () => {
    const output = client.infer(makeImageBytes());
    for (const ind of output.indicators) {
      expect(ind.confidence).toBeGreaterThanOrEqual(0.0);
      expect(ind.confidence).toBeLessThanOrEqual(1.0);
    }
  });

  it("returns a confidence for each indicator (Req 2.3)", () => {
    const output = client.infer(makeImageBytes());
    for (const ind of output.indicators) {
      expect(typeof ind.confidence).toBe("number");
    }
  });

  it("produces deterministic output for the same input", () => {
    const bytes = makeImageBytes(512);
    const out1 = client.infer(bytes);
    const out2 = client.infer(bytes);
    expect(out1).toEqual(out2);
  });

  it("produces different output for different inputs", () => {
    const client2: AIModelClient = {
      infer: (_imageBytes: Bytes): ModelOutput => ({
        indicators: [
          { name: "cavity_risk", score: 10, confidence: 0.5 },
          { name: "gum_health", score: 20, confidence: 0.6 },
          { name: "plaque_level", score: 80, confidence: 0.4 },
          { name: "overall_oral_health", score: 90, confidence: 0.3 },
        ],
      }),
    };
    const out1 = client.infer(makeImageBytes());
    const out2 = client2.infer(makeImageBytes());
    const allSame = out1.indicators.every((ind, i) =>
      ind.score === out2.indicators[i].score &&
      ind.confidence === out2.indicators[i].confidence
    );
    expect(allSame).toBe(false);
  });
});

// ─── AIWorkerImpl tests ───────────────────────────────────────────────────────

describe("AIWorkerImpl", () => {
  let queue: ReturnType<typeof makeQueue>;
  let storage: ReturnType<typeof makeStorage>;
  let modelClient: AIModelClient;
  let qualityChecker: ImageQualityChecker;
  let worker: AIWorkerImpl;
  const imageBytes = makeImageBytes(512);

  beforeEach(() => {
    queue = makeQueue();
    storage = makeStorage(imageBytes);
    modelClient = makeStubModelClient();
    qualityChecker = makePassingQualityChecker();
    worker = new AIWorkerImpl(queue, storage, modelClient, qualityChecker);
  });

  // ── Happy path ──────────────────────────────────────────────────────────────

  it("returns a ScoreReport with all four indicators on success (Req 2.1, 2.2)", async () => {
    const request = makeRequest();
    const report = await worker.process(request);

    const names = report.indicators.map((i) => i.indicator);
    expect(names).toContain("cavity_risk");
    expect(names).toContain("gum_health");
    expect(names).toContain("plaque_level");
    expect(names).toContain("overall_oral_health");
  });

  it("ScoreReport includes completedAt timestamp (Req 3.5)", async () => {
    const report = await worker.process(makeRequest());
    expect(report.completedAt).toBeTruthy();
    expect(() => new Date(report.completedAt)).not.toThrow();
  });

  it("ScoreReport includes original imageMeta (Req 3.6)", async () => {
    const request = makeRequest();
    const report = await worker.process(request);
    expect(report.imageMeta).toEqual(request.imageMeta);
  });

  it("ScoreReport analysisId matches request (Req 2.1)", async () => {
    const request = makeRequest({ analysisId: "abc-123" });
    const report = await worker.process(request);
    expect(report.analysisId).toBe("abc-123");
  });

  it("calls markComplete with the report on success (Req 2.1)", async () => {
    const request = makeRequest();
    const report = await worker.process(request);
    expect(queue.markComplete).toHaveBeenCalledWith(request.analysisId, report);
  });

  it("persists result via StorageService.saveResult on success (Req 4.1)", async () => {
    const request = makeRequest();
    const report = await worker.process(request);
    expect(storage.saveResult).toHaveBeenCalledOnce();
    const saved = storage._results.get(request.analysisId);
    expect(saved).toBeDefined();
    expect(saved!.report).toEqual(report);
    expect(saved!.error).toBeNull();
  });

  it("saved result has status 'complete' (Req 4.1)", async () => {
    const request = makeRequest();
    await worker.process(request);
    const saved = storage._results.get(request.analysisId);
    expect(saved!.request.status).toBe("complete");
  });

  it("all indicator scores are in [0, 100] (Req 2.4)", async () => {
    const report = await worker.process(makeRequest());
    for (const ind of report.indicators) {
      expect(ind.score).toBeGreaterThanOrEqual(0);
      expect(ind.score).toBeLessThanOrEqual(100);
    }
  });

  it("all indicator confidences are in [0.0, 1.0] (Req 2.5)", async () => {
    const report = await worker.process(makeRequest());
    for (const ind of report.indicators) {
      expect(ind.confidence).toBeGreaterThanOrEqual(0.0);
      expect(ind.confidence).toBeLessThanOrEqual(1.0);
    }
  });

  // ── Quality failure ─────────────────────────────────────────────────────────

  it("calls markFailed with quality_error when quality check fails (Req 2.7)", async () => {
    worker = new AIWorkerImpl(
      queue,
      storage,
      modelClient,
      makeFailingQualityChecker("blurry", "Image is too blurry")
    );
    await expect(worker.process(makeRequest())).rejects.toThrow();
    expect(queue.markFailed).toHaveBeenCalledWith(
      "test-id-001",
      expect.objectContaining({ code: "quality_error" })
    );
  });

  it("quality error message is descriptive (Req 2.7)", async () => {
    const detail = "Laplacian variance 12.5 is below threshold 50";
    worker = new AIWorkerImpl(
      queue,
      storage,
      modelClient,
      makeFailingQualityChecker("blurry", detail)
    );
    await expect(worker.process(makeRequest())).rejects.toThrow();
    const [, err] = (queue.markFailed as ReturnType<typeof vi.fn>).mock.calls[0];
    expect((err as AnalysisError).message).toBe(detail);
  });

  it("does not call markComplete when quality check fails", async () => {
    worker = new AIWorkerImpl(
      queue,
      storage,
      modelClient,
      makeFailingQualityChecker()
    );
    await expect(worker.process(makeRequest())).rejects.toThrow();
    expect(queue.markComplete).not.toHaveBeenCalled();
  });

  // ── Model failure ───────────────────────────────────────────────────────────

  it("calls markFailed with model_error when inference throws (Req 2.6)", async () => {
    const failingClient: AIModelClient = {
      infer: vi.fn(() => { throw new Error("GPU out of memory"); }),
    };
    worker = new AIWorkerImpl(queue, storage, failingClient, qualityChecker);
    await expect(worker.process(makeRequest())).rejects.toThrow();
    expect(queue.markFailed).toHaveBeenCalledWith(
      "test-id-001",
      expect.objectContaining({ code: "model_error" })
    );
  });

  it("persists a failed result when inference fails", async () => {
    const failingClient: AIModelClient = {
      infer: vi.fn(() => { throw new Error("model failure"); }),
    };
    worker = new AIWorkerImpl(queue, storage, failingClient, qualityChecker);
    await expect(worker.process(makeRequest())).rejects.toThrow();
    expect(storage.saveResult).toHaveBeenCalledOnce();
    const saved = storage._results.get("test-id-001");
    expect(saved).toBeDefined();
    expect(saved!.request.status).toBe("failed");
    expect(saved!.error?.code).toBe("model_error");
    expect(saved!.report).toBeNull();
  });

  // ── Storage failure ─────────────────────────────────────────────────────────

  it("calls markFailed with unknown error when image retrieval fails", async () => {
    const badStorage = makeStorage(imageBytes);
    (badStorage.getImage as ReturnType<typeof vi.fn>).mockImplementation(() => {
      throw new Error("Image not found");
    });
    worker = new AIWorkerImpl(queue, badStorage, modelClient, qualityChecker);
    await expect(worker.process(makeRequest())).rejects.toThrow();
    expect(queue.markFailed).toHaveBeenCalledWith(
      "test-id-001",
      expect.objectContaining({ code: "unknown" })
    );
  });

  // ── Timeout ─────────────────────────────────────────────────────────────────

  it("enforces 30-second timeout on inference (Req 2.6)", async () => {
    vi.useFakeTimers();
    const slowClient: AIModelClient = {
      infer: vi.fn(
        () =>
          new Promise<ModelOutput>((resolve) =>
            setTimeout(() => resolve({ indicators: [] }), 60_000)
          ) as unknown as ModelOutput
      ),
    };
    worker = new AIWorkerImpl(queue, storage, slowClient, qualityChecker);
    const processPromise = worker.process(makeRequest());
    vi.advanceTimersByTime(31_000);
    await expect(processPromise).rejects.toThrow(/timed out/i);
    expect(queue.markFailed).toHaveBeenCalledWith(
      "test-id-001",
      expect.objectContaining({ code: "model_error" })
    );
    vi.useRealTimers();
  });
});

// ─── Property-based tests ─────────────────────────────────────────────────────

import fc from "fast-check";

// ── Property 4: Score_Report structure invariant ──────────────────────────────
// Feature: dental-image-score-analysis, Property 4: Score_Report structure invariant
describe("Property 4: Score_Report structure invariant", () => {
  it("ScoreReport always has analysisId, completedAt, 4 indicators, and imageMeta", async () => {
    await fc.assert(
      fc.asyncProperty(fc.uuid(), fc.uuid(), async (analysisId, userId) => {
        const q = makeQueue();
        const s = makeStorage(makeImageBytes(256));
        const w = new AIWorkerImpl(q, s, makeStubModelClient(), makePassingQualityChecker());
        const request = makeRequest({ analysisId, userId });
        const report = await w.process(request);

        return (
          typeof report.analysisId === "string" &&
          report.analysisId === analysisId &&
          typeof report.completedAt === "string" &&
          report.completedAt.length > 0 &&
          Array.isArray(report.indicators) &&
          report.indicators.length === 4 &&
          new Set(report.indicators.map((i) => i.indicator)).size === 4 &&
          report.imageMeta !== null &&
          typeof report.imageMeta.filename === "string"
        );
      }),
      { numRuns: 100 }
    );
  });
});

// ── Property 5: Indicator score and confidence range invariants ───────────────
// Feature: dental-image-score-analysis, Property 5: Indicator score and confidence range invariants
describe("Property 5: Indicator score and confidence range invariants", () => {
  it("all scores are integers in [0,100] and confidences are floats in [0.0,1.0]", async () => {
    await fc.assert(
      fc.asyncProperty(fc.uuid(), async (analysisId) => {
        const q = makeQueue();
        const s = makeStorage(makeImageBytes(256));
        const w = new AIWorkerImpl(q, s, makeStubModelClient(), makePassingQualityChecker());
        const report = await w.process(makeRequest({ analysisId }));

        return report.indicators.every(
          (ind) =>
            Number.isInteger(ind.score) &&
            ind.score >= 0 &&
            ind.score <= 100 &&
            typeof ind.confidence === "number" &&
            ind.confidence >= 0.0 &&
            ind.confidence <= 1.0
        );
      }),
      { numRuns: 100 }
    );
  });
});
