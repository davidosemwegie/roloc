/* eslint-disable */
/**
 * Generated `api` utility.
 *
 * THIS CODE IS AUTOMATICALLY GENERATED.
 *
 * To regenerate, run `npx convex dev`.
 * @module
 */

import type * as auth from "../auth.js";
import type * as crons from "../crons.js";
import type * as daily from "../daily.js";
import type * as http from "../http.js";
import type * as leaderboard from "../leaderboard.js";
import type * as leaderboardModel from "../leaderboardModel.js";
import type * as model from "../model.js";
import type * as operations from "../operations.js";
import type * as publication from "../publication.js";
import type * as rules from "../rules.js";
import type * as telemetry from "../telemetry.js";
import type * as telemetryDelivery from "../telemetryDelivery.js";
import type * as telemetryModel from "../telemetryModel.js";
import type * as telemetryValidators from "../telemetryValidators.js";
import type * as validation from "../validation.js";
import type * as validators from "../validators.js";

import type {
  ApiFromModules,
  FilterApi,
  FunctionReference,
} from "convex/server";

declare const fullApi: ApiFromModules<{
  auth: typeof auth;
  crons: typeof crons;
  daily: typeof daily;
  http: typeof http;
  leaderboard: typeof leaderboard;
  leaderboardModel: typeof leaderboardModel;
  model: typeof model;
  operations: typeof operations;
  publication: typeof publication;
  rules: typeof rules;
  telemetry: typeof telemetry;
  telemetryDelivery: typeof telemetryDelivery;
  telemetryModel: typeof telemetryModel;
  telemetryValidators: typeof telemetryValidators;
  validation: typeof validation;
  validators: typeof validators;
}>;

/**
 * A utility for referencing Convex functions in your app's public API.
 *
 * Usage:
 * ```js
 * const myFunctionReference = api.myModule.myFunction;
 * ```
 */
export declare const api: FilterApi<
  typeof fullApi,
  FunctionReference<any, "public">
>;

/**
 * A utility for referencing Convex functions in your app's internal API.
 *
 * Usage:
 * ```js
 * const myFunctionReference = internal.myModule.myFunction;
 * ```
 */
export declare const internal: FilterApi<
  typeof fullApi,
  FunctionReference<any, "internal">
>;

export declare const components: {
  dailyScores: import("@convex-dev/aggregate/_generated/component.js").ComponentApi<"dailyScores">;
  leaderboardScores: import("@convex-dev/aggregate/_generated/component.js").ComponentApi<"leaderboardScores">;
  leaderboardAllTimeScores: import("@convex-dev/aggregate/_generated/component.js").ComponentApi<"leaderboardAllTimeScores">;
  workflow: import("@convex-dev/workflow/_generated/component.js").ComponentApi<"workflow">;
  rateLimiter: import("@convex-dev/rate-limiter/_generated/component.js").ComponentApi<"rateLimiter">;
};
