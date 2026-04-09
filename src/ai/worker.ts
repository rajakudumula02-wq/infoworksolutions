// AIWorker — pulls from the queue, runs quality check, then calls the model
// Req 2.1, 2.6, 2.7, 4.1

import type {
  AnalysisRequest,
  ScoreReport,
  AnalysisError,
  IndicatorScore,
  ModelOutput,
  AIWorker,
  AIModelClient,
  ImageQualityChecker,
  StorageService,
  AnalysisQueue,
} from "../types.js";

/** Inference must complete within 30 seconds (Req 2.6) */
const INFERENCE_TIMEOUT_MS = 30_000;

export class AIWorkerImpl implements AIWorker {
  constructor(
    private readonly queue: AnalysisQueue,
    private readonly storage: StorageService,
    private readonly modelClient: AIModelClient,
    private readonly qualityChecker: ImageQualityChecker
  ) {}

  async process(request: AnalysisRequest): Promise<ScoreReport> {
    const { analysisId, userId, imageMeta } = request;

    // 1. Fetch image bytes from storage
    let imageBytes: Uint8Array;
    try {
      imageBytes = this.storage.getImage(analysisId);
    } catch (err) {
      const error: AnalysisError = {
        code: "unknown",
        message: `Failed to retrieve image: ${err instanceof Error ? err.message : String(err)}`,
      };
      this.queue.markFailed(analysisId, error);
      this.storage.saveResult({ request: { ...request, status: "failed" }, report: null, error });
      throw new Error(error.message);
    }

    // 2. Quality check (Req 2.7)
    const qualityResult = this.qualityChecker.check(imageBytes);
    if (!qualityResult.pass) {
      const error: AnalysisError = {
        code: "quality_error",
        message: qualityResult.detail,
      };
      this.queue.markFailed(analysisId, error);
      this.storage.saveResult({ request: { ...request, status: "failed" }, report: null, error });
      throw new Error(error.message);
    }

    // 3. Run inference with a 30-second timeout (Req 2.6)
    let modelOutput: ModelOutput;
    try {
      const inferFn = "inferAsync" in this.modelClient
        ? (this.modelClient as { inferAsync: (b: Uint8Array) => Promise<ModelOutput> }).inferAsync(imageBytes)
        : Promise.resolve(this.modelClient.infer(imageBytes));

      modelOutput = await Promise.race([
        inferFn,
        new Promise<never>((_, reject) =>
          setTimeout(
            () => reject(new Error("Inference timed out after 30 seconds")),
            INFERENCE_TIMEOUT_MS
          )
        ),
      ]);
    } catch (err) {
      const error: AnalysisError = {
        code: "model_error",
        message: err instanceof Error ? err.message : String(err),
      };
      this.queue.markFailed(analysisId, error);
      this.storage.saveResult({ request: { ...request, status: "failed" }, report: null, error });
      throw new Error(error.message);
    }

    // 4. Build ScoreReport (Req 2.2, 2.3, 2.4, 2.5, 3.5, 3.6)
    const indicators: IndicatorScore[] = modelOutput.indicators.map((ind: ModelOutput["indicators"][number]) => ({
      indicator: ind.name,
      score: Math.min(100, Math.max(0, Math.round(ind.score))),
      confidence: Math.min(1.0, Math.max(0.0, ind.confidence)),
    }));

    const report: ScoreReport = {
      analysisId,
      completedAt: new Date().toISOString(),
      indicators,
      imageMeta,
    };

    // 5. Mark complete in queue and persist result (Req 4.1)
    this.queue.markComplete(analysisId, report);

    const analysisResult = {
      request: { ...request, status: "complete" as const },
      report,
      error: null,
    };
    this.storage.saveResult(analysisResult);

    return report;
  }
}
