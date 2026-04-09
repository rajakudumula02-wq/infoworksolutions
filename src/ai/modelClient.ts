// AIModelClient — Azure OpenAI (AI Foundry) GPT-4o Vision implementation
// Req 2.2, 2.3, 2.4, 2.5

import OpenAI from "openai";
import type { Bytes, ModelOutput, AIModelClient } from "../types.js";

const SYSTEM_PROMPT = `You are a dental health AI assistant. Analyze the provided dental image and return scores for the following indicators.

Respond ONLY with a valid JSON object in this exact format, no other text:
{
  "cavity_risk": { "score": <0-100>, "confidence": <0.0-1.0> },
  "gum_health": { "score": <0-100>, "confidence": <0.0-1.0> },
  "plaque_level": { "score": <0-100>, "confidence": <0.0-1.0> },
  "overall_oral_health": { "score": <0-100>, "confidence": <0.0-1.0> }
}

Scoring guide:
- cavity_risk: 0 = no cavities, 100 = severe cavities
- gum_health: 0 = very poor gum health, 100 = excellent gum health
- plaque_level: 0 = no plaque, 100 = heavy plaque buildup
- overall_oral_health: 0 = very poor, 100 = excellent overall health
- confidence: how confident you are in the score (0.0 = not confident, 1.0 = very confident)`;

export class AIModelClientImpl implements AIModelClient {
  private readonly deploymentName: string;
  private readonly endpoint: string;
  private readonly apiKey: string;
  private readonly apiVersion: string;

  constructor() {
    this.endpoint = process.env.AZURE_OPENAI_ENDPOINT ?? "";
    this.apiKey = process.env.AZURE_OPENAI_API_KEY ?? "";
    this.deploymentName = process.env.AZURE_OPENAI_DEPLOYMENT ?? "gpt-4o";
    this.apiVersion = process.env.AZURE_OPENAI_API_VERSION ?? "2024-02-15-preview";
  }

  private getClient(): OpenAI {
    if (!this.endpoint) throw new Error("AZURE_OPENAI_ENDPOINT environment variable is not set.");
    if (!this.apiKey) throw new Error("AZURE_OPENAI_API_KEY environment variable is not set.");
    return new OpenAI({
      apiKey: this.apiKey,
      baseURL: `${this.endpoint.replace(/\/$/, "")}/openai/deployments/${this.deploymentName}`,
      defaultQuery: { "api-version": this.apiVersion },
      defaultHeaders: { "api-key": this.apiKey },
    });
  }

  infer(_imageBytes: Bytes): ModelOutput {
    throw new Error("Use inferAsync() for Azure OpenAI vision — synchronous inference is not supported.");
  }

  async inferAsync(imageBytes: Bytes): Promise<ModelOutput> {
    const client = this.getClient();
    const base64 = Buffer.from(imageBytes).toString("base64");

    const response = await client.chat.completions.create({
      model: this.deploymentName,
      max_tokens: 300,
      messages: [
        { role: "system", content: SYSTEM_PROMPT },
        {
          role: "user",
          content: [
            {
              type: "image_url",
              image_url: {
                url: `data:image/jpeg;base64,${base64}`,
                detail: "high",
              },
            },
            { type: "text", text: "Analyze this dental image and provide scores." },
          ],
        },
      ],
    });

    const content = response.choices[0]?.message?.content;
    if (!content) throw new Error("Azure OpenAI returned an empty response.");

    let parsed: Record<string, { score: number; confidence: number }>;
    try {
      const clean = content.replace(/```json\n?|\n?```/g, "").trim();
      parsed = JSON.parse(clean);
    } catch {
      throw new Error(`Failed to parse Azure OpenAI response as JSON: ${content}`);
    }

    const indicators: ModelOutput["indicators"] = [
      "cavity_risk",
      "gum_health",
      "plaque_level",
      "overall_oral_health",
    ].map((name) => {
      const ind = parsed[name];
      if (!ind) throw new Error(`Missing indicator "${name}" in Azure OpenAI response`);
      return {
        name: name as ModelOutput["indicators"][number]["name"],
        score: Math.min(100, Math.max(0, Math.round(ind.score))),
        confidence: Math.min(1.0, Math.max(0.0, ind.confidence)),
      };
    });

    return { indicators };
  }
}
