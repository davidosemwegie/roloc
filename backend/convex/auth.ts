import { Anonymous } from "@convex-dev/auth/providers/Anonymous";
import { convexAuth } from "@convex-dev/auth/server";
import type { DataModel } from "./_generated/dataModel";
import { publicLimits, registrationProfile } from "./leaderboardModel";

export const { auth, signIn, signOut, store, isAuthenticated } = convexAuth({
  providers: [Anonymous<DataModel>({
    profile: params => registrationProfile(params.closedTestCode),
  })],
  callbacks: {
    async afterUserCreatedOrUpdated(ctx, args) {
      // Auth callbacks do not expose a trustworthy client IP. Bound total new
      // installations globally; refreshes of existing credentials are unaffected.
      if (!args.existingUserId) await publicLimits.limit(ctx, "registrations", { key: "global", throws: true });
    },
  },
});
