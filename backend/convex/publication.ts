import { v } from "convex/values";
import { internalMutation } from "./_generated/server";
import { DAY, midnight, utcDate } from "./model";

export const ensureUpcoming = internalMutation({
  args: {}, returns: v.number(),
  handler: async (ctx) => {
    const today = midnight(Date.now());
    let created = 0;
    for (let offset = 0; offset <= 7; offset++) {
      const opensAt = today + offset * DAY;
      const date = utcDate(opensAt);
      const found = await ctx.db.query("challenges").withIndex("by_date", q => q.eq("date", date)).unique();
      if (found) continue; // Published rules and seeds never change.
      await ctx.db.insert("challenges", { date, seed: Math.floor(Math.random() * 4294967296), rulesVersion: 1,
        variant: Math.floor(opensAt / DAY) % 2 === 0 ? "lively" : "still", opensAt, closesAt: opensAt + DAY,
        uploadDeadline: opensAt + DAY + 3_600_000, expiresAt: opensAt + 367 * DAY });
      created++;
    }
    return created;
  },
});
