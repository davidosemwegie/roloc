import { cronJobs } from "convex/server";
import { internal } from "./_generated/api";
const crons = cronJobs();
crons.daily("Prepare immutable Dailies", { hourUTC: 23, minuteUTC: 50 }, internal.publication.ensureUpcoming, {});
crons.interval("Check Daily operational health", { minutes: 5 }, internal.operations.checkHealth, {});
crons.interval("Finalize Daily standings", { minutes: 1 }, internal.operations.sealDue, {});
crons.interval("Expire unsubmitted attempts", { hours: 1 }, internal.operations.expireOpen, {});
crons.daily("Remove expired ranking data", { hourUTC: 2, minuteUTC: 15 }, internal.operations.purge, {});
export default crons;
