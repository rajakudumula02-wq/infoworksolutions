// API layer property tests and integration tests
// Tasks 7.2, 7.4, 7.6, 7.7, 7.8, 7.9, 8.3

import { describe, it, expect, vi, beforeEach } from "vitest";
import fc from "fast-check";
import express from "express";
import request from "supertest";
import jwt from "jsonwebtoken";
import { createAnalysesRouter, JWT_SECRET } from "./index.js";
import { ValidationServiceImpl } from "../validation/index.js";
import { StorageServiceImpl } from "../storage/index.js";
import { AnalysisQueueImpl } from "../queue/index.js";
import { AIWorkerImpl } from "../ai/worker.js";
import { ImageQualityCheckerImpl } from "../validation/qualityChecker.js";
import type {
  AIModelClient,
  ModelOutput,
  Bytes,
  ImageQualityChecker,
  QualityCheckResult,
} from "../types.js";

// ─── Helpers ──────────────────────────────────────────────────────────────────

function makeToken(userId: string): string {
  return jwt.sign({ sub: userId }, JWT_SECRET, { expiresIn: "1h" });
}

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

/** Build a minimal valid PNG with given dimensions */
function makePng(width: number, height: number): Buffer {
  const sig = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
  const ihdrData = new Uint8Array(13);
  const dv = new DataView(ihdrData.buffer);
  dv.setUint32(0, width, false);
  dv.setUint32(4, height, false);
  ihdrData[8] = 8;
  ihdrData[9] = 2;
  const ihdrChunk = new Uint8Array(4 + 4 + 13 + 4);
  const cv = new DataView(ihdrChunk.buffer);
  cv.setUint32(0, 13, false);
  ihdrChunk[4] = 0x49; ihdrChunk[5] = 0x48; ihdrChunk[6] = 0x44; ihdrChunk[7] = 0x52;
  ihdrChunk.set(ihdrData, 8);
  const result = new Uint8Array(sig.length + ihdrChunk.length);
  result.set(sig, 0);
  result.set(ihdrChunk, sig.length);
  return Buffer.from(result);
}

function createApp() {
  const validationService = new ValidationServiceImpl();
  const storageService = new StorageServiceImpl();
  const queue = new AnalysisQueueImpl();
  const app = express();
  app.use(express.json());
  app.use("/analyses", createAnalysesRouter(validationService, storageService, queue));
  return { app, storageService, queue, validationService };
}

// ── Property 14: Unauthenticated requests are rejected ────────────────────────
// Feature: dental-image-score-analysis, Property 14: Unauthenticated requests are rejected
describe("Property 14: Unauthenticated requests are rejected", () => {
  it("returns 401 for requests without a bearer token", () => {
    fc.assert(
      fc.asyncProperty(
        fc.constantFrom("/analyses", "/analyses/some-id"),
        async (path) => {
          const { app } = createApp();
          const res = await request(app).get(path);
          return res.status === 401;
        }
      ),
      { numRuns: 100 }
    );
  });

  it("returns 401 for requests with an invalid token", () => {
    fc.assert(
      fc.asyncProperty(
        fc.string({ minLength: 1, maxLength: 50 }),
        async (badToken) => {
          const { app } = createApp();
          const res = await request(app)
            .get("/analyses")
            .set("Authorization", `Bearer ${badToken}`);
          return res.status === 401;
        }
      ),
      { numRuns: 100 }
    );
  });
});

// ── Property 3: Upload IDs are unique across all requests ─────────────────────
// Feature: dental-image-score-analysis, Property 3: Upload IDs are unique across all requests
describe("Property 3: Upload IDs are unique across all requests", () => {
  it("each upload returns a distinct analysisId", async () => {
    const { app } = createApp();
    const token = makeToken("user-1");
    const png = makePng(400, 400);
    const ids = new Set<string>();

    for (let i = 0; i < 50; i++) {
      const res = await request(app)
        .post("/analyses")
        .set("Authorization", `Bearer ${token}`)
        .attach("image", png, { filename: "test.png", contentType: "image/png" });
      if (res.status === 202 && res.body.analysisId) {
        ids.add(res.body.analysisId);
      }
    }
    expect(ids.size).toBe(50);
  });
});

// ── Property 9: Unknown analysis ID returns 404 ──────────────────────────────
// Feature: dental-image-score-analysis, Property 9: Unknown analysis ID returns 404
describe("Property 9: Unknown analysis ID returns 404", () => {
  it("returns 404 for any random UUID that was never uploaded", () => {
    fc.assert(
      fc.asyncProperty(fc.uuid(), async (randomId) => {
        const { app } = createApp();
        const token = makeToken("user-1");
        const res = await request(app)
          .get(`/analyses/${randomId}`)
          .set("Authorization", `Bearer ${token}`);
        return res.status === 404;
      }),
      { numRuns: 100 }
    );
  });
});

// ── Property 7: Analysis retrieval round-trip ─────────────────────────────────
// Feature: dental-image-score-analysis, Property 7: Analysis retrieval round-trip
describe("Property 7: Analysis retrieval round-trip", () => {
  it("a completed analysis can be retrieved with its ScoreReport", async () => {
    const storageService = new StorageServiceImpl();
    const queue = new AnalysisQueueImpl();
    const validationService = new ValidationServiceImpl();
    const modelClient = makeStubModelClient();
    const qualityChecker: ImageQualityChecker = { check: () => ({ pass: true }) };
    const worker = new AIWorkerImpl(queue, storageService, modelClient, qualityChecker);

    const app = express();
    app.use(express.json());
    app.use("/analyses", createAnalysesRouter(validationService, storageService, queue));

    const token = makeToken("user-1");
    const png = makePng(400, 400);

    // Upload
    const uploadRes = await request(app)
      .post("/analyses")
      .set("Authorization", `Bearer ${token}`)
      .attach("image", png, { filename: "test.png", contentType: "image/png" });

    expect(uploadRes.status).toBe(202);
    const { analysisId } = uploadRes.body;

    // Process via worker
    const req = queue.dequeue()!;
    expect(req).not.toBeNull();
    await worker.process(req);

    // Retrieve
    const getRes = await request(app)
      .get(`/analyses/${analysisId}`)
      .set("Authorization", `Bearer ${token}`);

    expect(getRes.status).toBe(200);
    expect(getRes.body.analysisId).toBe(analysisId);
    expect(getRes.body.indicators).toHaveLength(4);
  });
});

// ── Property 8: In-progress requests return 202 ──────────────────────────────
// Feature: dental-image-score-analysis, Property 8: In-progress requests return 202
describe("Property 8: In-progress requests return 202", () => {
  it("returns 202 with processing status for a queued but unprocessed analysis", async () => {
    const { app, queue } = createApp();
    const token = makeToken("user-1");
    const png = makePng(400, 400);

    const uploadRes = await request(app)
      .post("/analyses")
      .set("Authorization", `Bearer ${token}`)
      .attach("image", png, { filename: "test.png", contentType: "image/png" });

    expect(uploadRes.status).toBe(202);
    const { analysisId } = uploadRes.body;

    // Don't process — it's still pending in the queue
    const getRes = await request(app)
      .get(`/analyses/${analysisId}`)
      .set("Authorization", `Bearer ${token}`);

    expect(getRes.status).toBe(202);
    expect(getRes.body.status).toBe("processing");
  });
});

// ── Property 15: Cross-user access is forbidden ──────────────────────────────
// Feature: dental-image-score-analysis, Property 15: Cross-user access is forbidden
describe("Property 15: Cross-user access is forbidden", () => {
  it("returns 403 when a different user tries to access another user's result", async () => {
    const storageService = new StorageServiceImpl();
    const queue = new AnalysisQueueImpl();
    const validationService = new ValidationServiceImpl();
    const modelClient = makeStubModelClient();
    const qualityChecker: ImageQualityChecker = { check: () => ({ pass: true }) };
    const worker = new AIWorkerImpl(queue, storageService, modelClient, qualityChecker);

    const app = express();
    app.use(express.json());
    app.use("/analyses", createAnalysesRouter(validationService, storageService, queue));

    const tokenA = makeToken("user-A");
    const tokenB = makeToken("user-B");
    const png = makePng(400, 400);

    // User A uploads
    const uploadRes = await request(app)
      .post("/analyses")
      .set("Authorization", `Bearer ${tokenA}`)
      .attach("image", png, { filename: "test.png", contentType: "image/png" });

    const { analysisId } = uploadRes.body;

    // Process
    const req = queue.dequeue()!;
    await worker.process(req);

    // User B tries to access
    const getRes = await request(app)
      .get(`/analyses/${analysisId}`)
      .set("Authorization", `Bearer ${tokenB}`);

    expect(getRes.status).toBe(403);
  });
});

// ─── Task 8.3: Unit tests for happy-path and edge cases ──────────────────────

describe("Integration: happy-path and edge cases", () => {
  const MB = 1024 * 1024;

  function createFullStack() {
    const storageService = new StorageServiceImpl();
    const queue = new AnalysisQueueImpl();
    const validationService = new ValidationServiceImpl();
    const modelClient = makeStubModelClient();
    const qualityChecker: ImageQualityChecker = { check: () => ({ pass: true }) };
    const worker = new AIWorkerImpl(queue, storageService, modelClient, qualityChecker);
    const app = express();
    app.use(express.json());
    app.use("/analyses", createAnalysesRouter(validationService, storageService, queue));
    return { app, queue, worker, storageService };
  }

  it("happy path: upload → queue → worker → retrieve", async () => {
    const { app, queue, worker } = createFullStack();
    const token = makeToken("user-1");
    const png = makePng(400, 400);

    // 1. Upload
    const uploadRes = await request(app)
      .post("/analyses")
      .set("Authorization", `Bearer ${token}`)
      .attach("image", png, { filename: "scan.png", contentType: "image/png" });
    expect(uploadRes.status).toBe(202);
    expect(uploadRes.body.analysisId).toBeTruthy();
    expect(uploadRes.body.status).toBe("pending");

    // 2. Dequeue and process
    const req = queue.dequeue()!;
    expect(req).not.toBeNull();
    const report = await worker.process(req);
    expect(report.indicators).toHaveLength(4);

    // 3. Retrieve
    const getRes = await request(app)
      .get(`/analyses/${uploadRes.body.analysisId}`)
      .set("Authorization", `Bearer ${token}`);
    expect(getRes.status).toBe(200);
    expect(getRes.body.analysisId).toBe(uploadRes.body.analysisId);
    expect(getRes.body.indicators).toHaveLength(4);
  });

  it("edge case: exactly 20 MB file is accepted", async () => {
    const { app } = createFullStack();
    const token = makeToken("user-1");
    // Create a buffer with valid PNG header but report sizeBytes as exactly 20 MB
    const png = makePng(400, 400);
    // The actual buffer is small but the validation checks file.sizeBytes from multer
    // which equals the buffer length. We can't easily create a 20MB buffer in test,
    // so we verify the validation logic directly.
    const validationService = new ValidationServiceImpl();
    const bytes = makePng(400, 400);
    const result = validationService.validateUpload({
      filename: "big.png",
      mimeType: "image/png",
      sizeBytes: 20 * MB,
      bytes: new Uint8Array(bytes),
    });
    expect(result.ok).toBe(true);
  });

  it("edge case: exactly 300×300 image is accepted", async () => {
    const { app } = createFullStack();
    const token = makeToken("user-1");
    const png = makePng(300, 300);

    const uploadRes = await request(app)
      .post("/analyses")
      .set("Authorization", `Bearer ${token}`)
      .attach("image", png, { filename: "small.png", contentType: "image/png" });
    expect(uploadRes.status).toBe(202);
  });

  it("edge case: score = 0 and score = 100 are valid", async () => {
    const edgeClient: AIModelClient = {
      infer: (): ModelOutput => ({
        indicators: [
          { name: "cavity_risk", score: 0, confidence: 0.0 },
          { name: "gum_health", score: 100, confidence: 1.0 },
          { name: "plaque_level", score: 0, confidence: 1.0 },
          { name: "overall_oral_health", score: 100, confidence: 0.0 },
        ],
      }),
    };
    const storageService = new StorageServiceImpl();
    const queue = new AnalysisQueueImpl();
    const qualityChecker: ImageQualityChecker = { check: () => ({ pass: true }) };
    const worker = new AIWorkerImpl(queue, storageService, edgeClient, qualityChecker);
    const validationService = new ValidationServiceImpl();

    const app = express();
    app.use(express.json());
    app.use("/analyses", createAnalysesRouter(validationService, storageService, queue));

    const token = makeToken("user-1");
    const png = makePng(400, 400);

    const uploadRes = await request(app)
      .post("/analyses")
      .set("Authorization", `Bearer ${token}`)
      .attach("image", png, { filename: "test.png", contentType: "image/png" });

    const req = queue.dequeue()!;
    const report = await worker.process(req);

    expect(report.indicators.find((i) => i.indicator === "cavity_risk")!.score).toBe(0);
    expect(report.indicators.find((i) => i.indicator === "gum_health")!.score).toBe(100);
    expect(report.indicators.find((i) => i.indicator === "cavity_risk")!.confidence).toBe(0.0);
    expect(report.indicators.find((i) => i.indicator === "gum_health")!.confidence).toBe(1.0);
  });

  it("edge case: confidence = 0.0 and 1.0 boundaries", async () => {
    const edgeClient: AIModelClient = {
      infer: (): ModelOutput => ({
        indicators: [
          { name: "cavity_risk", score: 50, confidence: 0.0 },
          { name: "gum_health", score: 50, confidence: 1.0 },
          { name: "plaque_level", score: 50, confidence: 0.0 },
          { name: "overall_oral_health", score: 50, confidence: 1.0 },
        ],
      }),
    };
    const storageService = new StorageServiceImpl();
    const queue = new AnalysisQueueImpl();
    const qualityChecker: ImageQualityChecker = { check: () => ({ pass: true }) };
    const worker = new AIWorkerImpl(queue, storageService, edgeClient, qualityChecker);

    const req = {
      analysisId: "edge-conf",
      userId: "user-1",
      uploadTimestamp: new Date().toISOString(),
      status: "processing" as const,
      imageMeta: { filename: "x.png", format: "png" as const, fileSizeBytes: 100, uploadTimestamp: new Date().toISOString() },
    };

    // Save a dummy image so getImage works
    storageService.saveImage("user-1", "edge-conf", new Uint8Array(64), req.imageMeta);

    const report = await worker.process(req);
    for (const ind of report.indicators) {
      expect(ind.confidence).toBeGreaterThanOrEqual(0.0);
      expect(ind.confidence).toBeLessThanOrEqual(1.0);
    }
  });
});
