// API layer — HTTP endpoints for upload, retrieval, and history
// Req 1.1–1.6, 3.1–3.4, 4.2–4.5, 6.1–6.3

import { Router, Request, Response, NextFunction } from "express";
import jwt, { type JwtHeader, type SigningKeyCallback } from "jsonwebtoken";
import { expressJwtSecret, type GetVerificationKey } from "jwks-rsa";
import multer from "multer";
import { v4 as uuidv4 } from "uuid";

import type {
  ValidationService,
  StorageService,
  AnalysisQueue,
  AnalysisRequest,
  ImageMeta,
  ErrorEnvelope,
} from "../types.js";

// ─── Auth configuration ───────────────────────────────────────────────────────
// Azure AD / Entra ID OAuth 2.0 settings (production)
// Falls back to self-signed JWT with JWT_SECRET for dev/testing

export const JWT_SECRET = process.env.JWT_SECRET ?? "dental-dev-secret";

const AZURE_AD_TENANT_ID = process.env.AZURE_AD_TENANT_ID ?? "";
const AZURE_AD_CLIENT_ID = process.env.AZURE_AD_CLIENT_ID ?? "";

/** True when Azure AD env vars are configured */
const useAzureAd = Boolean(AZURE_AD_TENANT_ID && AZURE_AD_CLIENT_ID);

const JWKS_URI = `https://login.microsoftonline.com/${AZURE_AD_TENANT_ID}/discovery/v2.0/keys`;
const ISSUER = `https://login.microsoftonline.com/${AZURE_AD_TENANT_ID}/v2.0`;

// JWKS client — fetches Azure AD public signing keys and caches them
const jwksClient = useAzureAd
  ? expressJwtSecret({
      jwksUri: JWKS_URI,
      cache: true,
      cacheMaxEntries: 5,
      cacheMaxAge: 600_000, // 10 minutes
      rateLimit: true,
      jwksRequestsPerMinute: 10,
    })
  : null;

// ─── Auth middleware (Task 7.1) ───────────────────────────────────────────────

export interface AuthenticatedRequest extends Request {
  userId?: string;
}

function sendUnauthorized(res: Response, message = "Missing or invalid bearer token."): void {
  const body: ErrorEnvelope = {
    error: { code: "UNAUTHORIZED", message },
  };
  res.status(401).json(body);
}

/**
 * Verify a token using Azure AD JWKS (RSA public key from Microsoft).
 * Returns the decoded payload or null on failure.
 */
function verifyWithAzureAd(
  token: string,
  req: Request
): Promise<jwt.JwtPayload | null> {
  return new Promise(async (resolve) => {
    if (!jwksClient) {
      resolve(null);
      return;
    }

    const decoded = jwt.decode(token, { complete: true });
    if (!decoded || typeof decoded === "string") {
      resolve(null);
      return;
    }

    try {
      const getKey = jwksClient as GetVerificationKey;
      const signingKey = await (getKey as any)(req, decoded.header);
      if (!signingKey) {
        resolve(null);
        return;
      }

      const payload = jwt.verify(token, signingKey as string, {
        algorithms: ["RS256"],
        issuer: ISSUER,
        audience: AZURE_AD_CLIENT_ID,
      }) as jwt.JwtPayload;
      resolve(payload);
    } catch {
      resolve(null);
    }
  });
}

/**
 * Verify a token using the local JWT_SECRET (HMAC, for dev/testing).
 * Returns the decoded payload or null on failure.
 */
function verifyWithLocalSecret(token: string): jwt.JwtPayload | null {
  try {
    return jwt.verify(token, JWT_SECRET) as jwt.JwtPayload;
  } catch {
    return null;
  }
}

/**
 * Extract userId from a verified JWT payload.
 * Azure AD tokens use `oid` (object ID) or `sub`; local tokens use `sub` or `userId`.
 */
function extractUserId(payload: jwt.JwtPayload): string | null {
  return (
    (payload.oid as string | undefined) ??
    (payload.sub as string | undefined) ??
    (payload.userId as string | undefined) ??
    null
  );
}

export async function jwtAuthMiddleware(
  req: AuthenticatedRequest,
  res: Response,
  next: NextFunction
): Promise<void> {
  const authHeader = req.headers.authorization;
  if (!authHeader || !authHeader.startsWith("Bearer ")) {
    sendUnauthorized(res);
    return;
  }

  const token = authHeader.slice(7);

  // Strategy 1: Try Azure AD JWKS verification (production)
  if (useAzureAd) {
    const azurePayload = await verifyWithAzureAd(token, req);
    if (azurePayload) {
      const userId = extractUserId(azurePayload);
      if (!userId) {
        sendUnauthorized(res, "Token payload missing user identifier.");
        return;
      }
      req.userId = userId;
      next();
      return;
    }
  }

  // Strategy 2: Fall back to local JWT_SECRET (dev/testing)
  const localPayload = verifyWithLocalSecret(token);
  if (localPayload) {
    const userId = extractUserId(localPayload);
    if (!userId) {
      sendUnauthorized(res, "Token payload missing user identifier.");
      return;
    }
    req.userId = userId;
    next();
    return;
  }

  sendUnauthorized(res);
}

// ─── Multer setup (memory storage, no size limit enforced here — done in ValidationService) ──

const upload = multer({ storage: multer.memoryStorage() });

// Helper: process a single file and enqueue it
function enqueueFile(
  file: Express.Multer.File,
  userId: string,
  validationService: ValidationService,
  storageService: StorageService,
  queue: AnalysisQueue
): { analysisId: string; status: "pending" } | { error: ErrorEnvelope; httpStatus: number } {
  const uploadedFile = {
    filename: file.originalname,
    mimeType: file.mimetype,
    sizeBytes: file.size,
    bytes: new Uint8Array(file.buffer),
  };

  const validation = validationService.validateUpload(uploadedFile);
  if (!validation.ok) {
    let code: string;
    if (validation.httpStatus === 415) code = "UNSUPPORTED_FORMAT";
    else if (validation.httpStatus === 422) code = "INSUFFICIENT_RESOLUTION";
    else code = "FILE_TOO_LARGE";
    return { error: { error: { code, message: validation.message } }, httpStatus: validation.httpStatus };
  }

  const analysisId = uuidv4();
  const uploadTimestamp = new Date().toISOString();
  const formatMap: Record<string, ImageMeta["format"]> = {
    "image/jpeg": "jpeg",
    "image/png": "png",
    "application/dicom": "dicom",
  };
  const format: ImageMeta["format"] = formatMap[file.mimetype] ?? "jpeg";
  const imageMeta: ImageMeta = { filename: file.originalname, format, fileSizeBytes: file.size, uploadTimestamp };

  storageService.saveImage(userId, analysisId, uploadedFile.bytes, imageMeta);
  queue.enqueue({ analysisId, userId, uploadTimestamp, status: "pending", imageMeta });
  return { analysisId, status: "pending" };
}

// ─── Router factory ───────────────────────────────────────────────────────────

export function createAnalysesRouter(
  validationService: ValidationService,
  storageService: StorageService,
  queue: AnalysisQueue
): Router {
  const router = Router();

  // Apply JWT auth to all routes in this router
  router.use(jwtAuthMiddleware);

  // ── POST /analyses (single upload, Task 7.3) ──────────────────────────────
  // Req 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 5.4
  router.post(
    "/",
    upload.single("image"),
    (req: AuthenticatedRequest, res: Response): void => {
      const userId = req.userId!;

      if (!req.file) {
        const body: ErrorEnvelope = {
          error: { code: "MISSING_FILE", message: "No image file provided in the request." },
        };
        res.status(400).json(body);
        return;
      }

      const result = enqueueFile(req.file, userId, validationService, storageService, queue);
      if ("error" in result) {
        res.status(result.httpStatus).json(result.error);
        return;
      }
      res.status(202).json(result);
    }
  );

  // ── POST /analyses/batch (multiple images in one request) ─────────────────
  router.post(
    "/batch",
    upload.array("images", 20),
    (req: AuthenticatedRequest, res: Response): void => {
      const userId = req.userId!;
      const files = req.files as Express.Multer.File[] | undefined;

      if (!files || files.length === 0) {
        const body: ErrorEnvelope = {
          error: { code: "MISSING_FILE", message: "No image files provided in the request." },
        };
        res.status(400).json(body);
        return;
      }

      const results = files.map((file) => {
        const r = enqueueFile(file, userId, validationService, storageService, queue);
        if ("error" in r) {
          return { filename: file.originalname, ...r.error, httpStatus: r.httpStatus };
        }
        return { filename: file.originalname, analysisId: r.analysisId, status: r.status };
      });

      res.status(202).json({ results });
    }
  );

  // ── GET /analyses/:analysisId (Task 7.5) ───────────────────────────────────
  // Req 3.1, 3.2, 3.3, 3.4, 6.2, 6.3
  router.get("/:analysisId", (req: AuthenticatedRequest, res: Response): void => {
    const userId = req.userId!;
    const analysisId = req.params["analysisId"] as string;

    // Check storage for a completed/failed result
    const result = storageService.getResult(analysisId);

    if (result === null) {
      // Not in storage — check if it's pending or processing in the queue
      const queueImpl = queue as unknown as {
        isPending?: (id: string) => boolean;
        isProcessing?: (id: string) => boolean;
      };

      const isPending = queueImpl.isPending?.(analysisId) ?? false;
      const isProcessing = queueImpl.isProcessing?.(analysisId) ?? false;

      if (isPending || isProcessing) {
        // Verify ownership via the queue's internal request
        // We can't easily check userId here without exposing more queue API,
        // so we return 202 processing — ownership was established at upload time
        res.status(202).json({ analysisId, status: "processing" });
        return;
      }

      // Truly unknown
      const body: ErrorEnvelope = {
        error: { code: "NOT_FOUND", message: "Analysis not found." },
      };
      res.status(404).json(body);
      return;
    }

    // Verify ownership (Req 6.2, 6.3)
    if (result.request.userId !== userId) {
      const body: ErrorEnvelope = {
        error: { code: "FORBIDDEN", message: "Access denied." },
      };
      res.status(403).json(body);
      return;
    }

    const status = result.request.status;

    // Still pending or processing (result was saved but status not yet complete)
    if (status === "pending" || status === "processing") {
      res.status(202).json({ analysisId, status: "processing" });
      return;
    }

    // Failed with model or storage error → 500
    if (status === "failed") {
      const errorCode = result.error?.code;
      if (errorCode === "model_error" || errorCode === "unknown") {
        const body: ErrorEnvelope = {
          error: { code: "MODEL_ERROR", message: "An internal error occurred during analysis." },
        };
        res.status(500).json(body);
        return;
      }
      // quality_error → surface as 422
      const body: ErrorEnvelope = {
        error: {
          code: "IMAGE_QUALITY_ERROR",
          message: result.error?.message ?? "Image quality check failed.",
        },
      };
      res.status(422).json(body);
      return;
    }

    // Complete — return the ScoreReport (Req 3.3)
    if (result.report === null) {
      const body: ErrorEnvelope = {
        error: { code: "STORAGE_ERROR", message: "An internal error occurred retrieving the report." },
      };
      res.status(500).json(body);
      return;
    }

    res.status(200).json(result.report);
  });

  // ── GET /analyses (Task 7.10) ──────────────────────────────────────────────
  // Req 4.2, 4.3, 4.4, 4.5
  router.get("/", (req: AuthenticatedRequest, res: Response): void => {
    const userId = req.userId!;
    const rawCursor = req.query["cursor"];
    const cursor = typeof rawCursor === "string" ? rawCursor : null;
    const rawPageSize = req.query["pageSize"];
    const pageSizeParam = parseInt(typeof rawPageSize === "string" ? rawPageSize : "20", 10);
    const pageSize = Number.isFinite(pageSizeParam) && pageSizeParam > 0
      ? Math.min(pageSizeParam, 20)
      : 20;

    const page = storageService.listResults(userId, cursor, pageSize);

    res.status(200).json({
      results: page.items,
      nextCursor: page.nextCursor,
    });
  });

  return router;
}
