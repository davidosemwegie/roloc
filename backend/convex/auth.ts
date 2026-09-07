import { Anonymous } from "@convex-dev/auth/providers/Anonymous";
import { convexAuth } from "@convex-dev/auth/server";
import { ConvexError } from "convex/values";

export const { auth, signIn, signOut, store, isAuthenticated } = convexAuth({
  providers: [Anonymous({
    profile(params) {
      // Closed TestFlight access, not device attestation or a public abuse defense.
      const expected = process.env.RING_RUSH_CLOSED_TEST_CODE;
      if (!expected || typeof params.closedTestCode !== "string" || params.closedTestCode !== expected)
        throw new ConvexError({ code: "CLOSED_TEST", message: "This Daily is currently available to invited testers." });
      return { isAnonymous: true, closedTestEpoch: process.env.RING_RUSH_CLOSED_TEST_EPOCH ?? "1" };
    },
  })],
});
