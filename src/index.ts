// Entry point — wires all components together and starts the Express server
// Task 8.1, 8.2

import "dotenv/config";
import express from "express";
import { fileURLToPath } from "url";
import { join, dirname } from "path";
import { createAnalysesRouter } from "./api/index.js";
import { ValidationServiceImpl } from "./validation/index.js";
import { ImageQualityCheckerImpl } from "./validation/qualityChecker.js";
import { StorageServiceImpl } from "./storage/index.js";
import { AnalysisQueueImpl } from "./queue/index.js";
import { AIWorkerImpl } from "./ai/worker.js";
import { AIModelClientImpl } from "./ai/modelClient.js";

// ─── Instantiate all services ─────────────────────────────────────────────────

const validationService = new ValidationServiceImpl();
const qualityChecker = new ImageQualityCheckerImpl();
const storageService = new StorageServiceImpl();
const queue = new AnalysisQueueImpl();
const modelClient = new AIModelClientImpl();
const aiWorker = new AIWorkerImpl(queue, storageService, modelClient, qualityChecker);

// ─── Background worker loop (Task 8.2) ───────────────────────────────────────
// Polls the queue every 500ms and processes any pending requests.

function startWorkerLoop(): void {
  setInterval(async () => {
    const request = queue.dequeue();
    if (request) {
      try {
        await aiWorker.process(request);
        console.log(`[worker] Analysis complete: ${request.analysisId}`);
      } catch (err) {
        console.error(`[worker] Analysis failed: ${request.analysisId} —`, err instanceof Error ? err.message : err);
      }
    }
  }, 500);
}

// ─── Express app (Task 8.1) ───────────────────────────────────────────────────

const app = express();
app.use(express.json());

// Serve the HTML frontend
const __dirname = dirname(fileURLToPath(import.meta.url));
app.use(express.static(join(__dirname, "../public")));

// Mount the analyses router — all deps injected
app.use("/analyses", createAnalysesRouter(validationService, storageService, queue));

// Health check
app.get("/health", (_req, res) => {
  res.json({ status: "ok" });
});

// Debug endpoint — shows queue and storage state
app.get("/debug", (_req, res) => {
  const q = queue as unknown as {
    pending: unknown[];
    processing: Map<string, unknown>;
    completed: Map<string, unknown>;
    failed: Map<string, unknown>;
  };
  res.json({
    queue: {
      pending: q.pending?.length ?? "?",
      processing: q.processing?.size ?? "?",
      completed: q.completed?.size ?? "?",
      failed: [...(q.failed?.entries() ?? [])].map(([id, err]) => ({ id, err })),
    },
    env: {
      endpoint: process.env.AZURE_OPENAI_ENDPOINT ? "set" : "MISSING",
      apiKey: process.env.AZURE_OPENAI_API_KEY ? "set" : "MISSING",
      deployment: process.env.AZURE_OPENAI_DEPLOYMENT ?? "MISSING",
    }
  });
});

// ─── Start ────────────────────────────────────────────────────────────────────

const PORT = process.env.PORT ? parseInt(process.env.PORT, 10) : 3000;

startWorkerLoop();

app.listen(PORT, () => {
  console.log(`Dental image analysis API running on port ${PORT}`);
});

export { app, queue, storageService, validationService };
