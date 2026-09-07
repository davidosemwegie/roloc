import { defineConfig } from "vitest/config";
export default defineConfig({
  test: { environment: "edge-runtime", include: ["tests/**/*.test.ts"], server: { deps: { inline: ["convex-test", "@convex-dev/aggregate", "@convex-dev/workflow", "@convex-dev/workpool", "@convex-dev/rate-limiter"] } } },
});
