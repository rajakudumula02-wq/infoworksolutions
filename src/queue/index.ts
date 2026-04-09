// AnalysisQueue — decouples HTTP request handling from AI inference
// Req 2.1, 2.6

import type {
  AnalysisRequest,
  AnalysisQueue,
  ScoreReport,
  AnalysisError,
} from "../types.js";

export class AnalysisQueueImpl implements AnalysisQueue {
  /** Pending requests waiting to be picked up by a worker */
  private readonly pending: AnalysisRequest[] = [];

  /** Completed results keyed by analysisId */
  private readonly completed = new Map<string, ScoreReport>();

  /** Failed results keyed by analysisId */
  private readonly failed = new Map<string, AnalysisError>();

  /** In-flight requests keyed by analysisId */
  private readonly processing = new Map<string, AnalysisRequest>();

  // ── enqueue ──────────────────────────────────────────────────────────────

  enqueue(request: AnalysisRequest): void {
    this.pending.push({ ...request, status: "pending" });
  }

  // ── dequeue ──────────────────────────────────────────────────────────────

  dequeue(): AnalysisRequest | null {
    const request = this.pending.shift();
    if (!request) return null;
    const processing: AnalysisRequest = { ...request, status: "processing" };
    this.processing.set(processing.analysisId, processing);
    return processing;
  }

  // ── markComplete ─────────────────────────────────────────────────────────

  markComplete(analysisId: string, result: ScoreReport): void {
    this.processing.delete(analysisId);
    this.completed.set(analysisId, result);
  }

  // ── markFailed ───────────────────────────────────────────────────────────

  markFailed(analysisId: string, error: AnalysisError): void {
    this.processing.delete(analysisId);
    this.failed.set(analysisId, error);
  }

  // ── Inspection helpers (used by API layer) ────────────────────────────────

  getCompleted(analysisId: string): ScoreReport | undefined {
    return this.completed.get(analysisId);
  }

  getFailed(analysisId: string): AnalysisError | undefined {
    return this.failed.get(analysisId);
  }

  isProcessing(analysisId: string): boolean {
    return this.processing.has(analysisId);
  }

  isPending(analysisId: string): boolean {
    return this.pending.some((r) => r.analysisId === analysisId);
  }
}
