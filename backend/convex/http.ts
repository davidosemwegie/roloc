import { httpRouter } from "convex/server";
import { httpAction } from "./_generated/server";
import { internal } from "./_generated/api";
import { auth } from "./auth";
const http = httpRouter();
auth.addHttpRoutes(http);
http.route({ path: "/health/daily", method: "GET", handler: httpAction(async (ctx) => {
  const health = await ctx.runQuery(internal.operations.health, {});
  return new Response(JSON.stringify(health), { status: health.ready ? 200 : 503, headers: { "content-type": "application/json", "cache-control": "no-store" } });
}) });
export default http;
