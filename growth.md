# Ring Rush growth strategy

Reference date: September 6, 2026. This document records the growth discussion and proposals; it does not authorize advertising spend, publishing, or game changes.

## 1. Goals and current uncertainty

Build enjoyable repeat play and profitable growth. The desired outcome is **5× return on advertising spend (ROAS) within 30 days of acquisition**. This is an ambitious business target, not an expected result or a promise.

Ring Rush does not yet have measured acquisition costs, retention, or player revenue in this discussion. Its ability to acquire players profitably remains unproven. More movement, more features, or more advertising will not by themselves establish a business case.

Distinguish these categories throughout future decisions:

| Category | Meaning here |
|---|---|
| Observed results | Measured Ring Rush player and revenue data; not yet available here. |
| User target | 5× ROAS within 30 days. |
| Budget preference | Start at $20/day if a paid test is launched. |
| Planning assumption | Budget around 0.5× day-30 ROAS for an initial test; actual revenue could be zero. |
| Suggested milestones | Recover acquisition spend, then investigate repeatable 1.2–1.5× day-30 ROAS before substantial scaling. |
| Proposed experiments | The product, monetization, and marketing ideas below; not implementation commitments. |

The current recommendation is **organic discovery first**, followed by an optional, capped paid test if early evidence makes it worth testing. Losing money can be the cost of a bounded experiment; it is not a viable ongoing growth strategy.

## 2. Product improvements to test

These are proposals, not claims about implemented or approved features. Confirm the game's current behavior before planning any implementation.

| Proposal | Intended benefit | What to test |
|---|---|---|
| Forgiving main mode alongside strict Rush mode | Let players establish a rhythm while preserving sudden-death challenge. | Compare a gentler opening and three chances against the current strict experience; measure returns as well as run length. |
| Skill combos and perfect matches | Give players something to master beyond surviving the timer. | Reward centered drops while ordinary valid drops still succeed; keep feedback readable. |
| Sound, subtle haptics, and short celebrations | Make successful actions satisfying. | Ensure feedback never obscures the next action or delays input. |
| Earned cosmetic progression | Give a completed run lasting value, including after a loss. | Start with a small collection of puck finishes, trails, rings, or backgrounds and a visible next unlock. |
| Daily challenges and shareable scores | Offer a reason to return and invite a friend. | Use a common sequence and rules for comparable results; show actual personal improvement. |
| Fair, readable variations | Balance novelty with control. | Compare moving-board play with a simpler version; use recognizable calm, challenge, and recovery periods. |
| Fast restarts | Preserve the desire to try again. | Show score, best, earned progress, and a prominent replay action without several intervening screens. |

Prioritize the forgiving-mode experiment, skill feedback, and measurement before building a large progression system. A longer session is useful only if players enjoy it and return.

## 3. Monetization

Proposed integration direction from the discussion: Unity Ads through LevelPlay, beginning with test ads. No integration or production advertising is authorized by this document.

- Offer an **optional rewarded continue, at most once per run**, with a clear restart alternative.
- Test occasional interstitials at natural breaks. Use elapsed active playtime as well as run count to limit frequency; avoid an ad after every short failure. Exact caps require a separate implementation decision and measurement.
- Offer a purchase to remove forced ads, with rewarded ads remaining optional.
- Consider optional rewarded boosts to cosmetic earnings only after the cosmetic progression is enjoyable on its own.
- Keep difficulty independent of whether a player watches an ad or purchases anything.
- Measure revenue changes together with retention and replay behavior. More impressions can be counterproductive if they drive players away.

## 4. Paid acquisition economics

### Budget and proposed experiment

The final starting-budget preference is **$20/day**, replacing the earlier $100/day discussion. At that rate, seven days costs $140 and 30 days costs $600.

An optional first experiment is **seven acquisition days, capped at $140**, followed by observation of those players through day 30. This is a proposal, not permission to launch a campaign or keep spending automatically. Be prepared for the entire $140 to be unrecovered.

Before spending, have a public store release, working acquisition attribution, retention tracking, and attributable revenue reporting. A proposed first test would concentrate on one channel and one country with three genuine gameplay creatives; Meta app-install campaigns were suggested as a starting hypothesis, not a selected or proven channel. Platform, country, and campaign details remain undecided.

### Formulas and measurement window

**Day-30 ROAS = cumulative attributable revenue earned within 30 days of acquisition ÷ advertising spend used to acquire those players.**

**Cost per install (CPI) = advertising spend ÷ attributed installs.**

**Required average day-30 revenue per acquired player = target ROAS × CPI.**

Average player revenue includes everyone acquired, including people who leave immediately. It is not revenue per remaining active player or per paying player.

Each acquisition cohort gets its own full 30-day window. Spending $600 over a month at a 5× target means $3,000 attributable revenue across those cohorts by their respective day-30 endpoints; it does not mean all $3,000 must arrive during the acquisition calendar month. Do not divide all game revenue, including organic players and earlier cohorts, by current advertising spend and label that cohort ROAS.

### Install-cost scenarios

These are arithmetic scenarios, **not CPI forecasts or market benchmarks**.

| Assumed CPI | Installs per $20 | Installs per $140 | Average revenue per install required for 5× |
|---:|---:|---:|---:|
| $0.50 | 40 | 280 | $2.50 |
| $1.00 | 20 | 140 | $5.00 |
| $2.00 | 10 | 70 | $10.00 |
| $4.00 | 5 | 35 | $20.00 |

### Revenue scenarios for the proposed $140 test

These assessments are planning judgments, not published industry performance bands.

| Day-30 ROAS | Attributable revenue | Revenue less acquisition spend | Interpretation |
|---:|---:|---:|---|
| 0× | $0 | −$140 | Possible downside. |
| 0.5× | $70 | −$70 | Conservative starting budget assumption; losing money. |
| 1× | $140 | $0 | Acquisition spend recovered. |
| 1.2× | $168 | $28 | Encouraging if repeatable and other costs are covered. |
| 1.5× | $210 | $70 | Suggested result to investigate before scaling. |
| 2× | $280 | $140 | Strong result for this proposed experiment. |
| 5× | $700 | $560 | User target; do not assume it will occur. |

**Revenue is not profit.** The third column excludes development, operations, creative production, measurement tools, and other costs. Keep the revenue basis consistent, including treatment of platform fees where purchases are involved.

The 0.5× assumption is deliberately a budgeting allowance for loss, not an evidence-based forecast. The suggested 1.2–1.5× milestone is also not a prediction. Small campaigns produce noisy results, and a successful small test does not prove the same return will persist at a larger budget.

### Why retention and scale matter

For illustration only, four served ads per active player-day at a blended $10 revenue per thousand impressions produces **$0.04 per active player-day**. At that assumed yield, recovering a $1 CPI requires 25 active days per acquired player on average; reaching 5× requires 125 active days. A 30-day window contains at most 30 active days per person, so this particular set of assumptions cannot support 5× at a $1 CPI through ads alone. Higher player revenue, lower CPI, or another revenue stream would be necessary. Neither yield nor CPI is measured for Ring Rush.

At the same illustrative $0.04 yield, 100,000 daily active players would generate $4,000/day, while 25 million would generate $1 million/day. Downloads are not daily active players. These examples explain the scale involved; they are not forecasts.

### How to interpret a test

- Expensive installs: investigate creative, targeting, and store-page conversion.
- Cheap installs but poor returns: investigate the acquired audience, onboarding, and core gameplay.
- Good retention but weak revenue: investigate monetization without sacrificing enjoyment.
- Observe revenue at days 1, 7, 14, and 30. Do not treat an immature cohort as a completed day-30 result.
- Require repeatable evidence before increasing spend. Do not assume later revenue will recover an early loss without supporting data.

## 5. Organic discovery first

Organic discovery means acquiring players without paying for each install. It still costs time and creative effort, and reach is not guaranteed.

### Initial content experiment

Create **ten authentic gameplay clips over two weeks**, approximately 10–20 seconds each, and reuse them across TikTok, Instagram Reels, and YouTube Shorts. Lead with actual gameplay, make the premise understandable immediately, and provide an appropriate path to the store listing. Before public release, tester recruitment can use a beta invitation instead of implying that the game is already in the store.

Suggested hooks:

- “Looks easy until the rings start moving.”
- A satisfying sequence of perfect matches, if that feature is implemented.
- A genuine close attempt at beating a personal best.
- The board smoothly transitioning into a moving variation.

Test distinct ideas rather than making many barely different clips. Show real game behavior and avoid unsupported claims about how few people can win.

### Sharing and store presentation

- Propose a shareable score card such as “I scored 47. Beat that.” with an appropriate download link.
- A daily challenge could give friends the same sequence and rules to compare.
- Use a clear App Store subtitle, accurate keywords, readable screenshots, and a gameplay preview.
- Apple allows featuring nominations; editorial selection is an optional opportunity, not a distribution plan to rely on.

Set **100 outside players as an initial learning goal**, not a promised outcome. Track installs, subsequent play, and retention rather than judging success by views alone. Organic and paid audiences may behave differently, so organic revenue does not establish paid-acquisition profitability.

## 6. Measurement and evidence

### Measurement checklist

| Area | What to record |
|---|---|
| Acquisition | Source/campaign where measurable, spend, attributed installs, CPI, and store conversion where available. |
| First experience | Tutorial start/completion, first run, and early exits. |
| Gameplay | Mode, score, run length, failure reason, variation active at failure, and replay rate/time to restart. |
| Retention | Day 1, 7, and 30 returns, grouped by install cohort and source. Use a consistent definition; exact-day retention means returning on that specific day. |
| Monetization | Ad impressions and revenue, rewarded-ad offers/completions, purchases where implemented, and cumulative revenue per acquired player. |
| Economics | Cohort revenue and ROAS at days 1, 7, 14, and 30, including cohort size and maturity. |

Document attribution gaps, reporting delays, and sample size. Keep assumptions separate from measured values. No results are populated in this reference document.

### Sources and corrections

1. [Gamigion: Block Blast by Hungry Studio is doing $1M a Day](https://www.gamigion.com/block-blast-by-hungry-studio-is-doing-1m-a-day/). Industry reporting of approximately $1 million daily revenue; do not treat it as audited accounts or an iPhone-only figure.
2. [Felix Braberg: The Biggest Ad Monetized Game on Black Friday 2025](https://felixbraberg.substack.com/p/the-biggest-ad-monetized-game-on), January 15, 2026. Estimates Block Blast at $1.8 million in ad revenue on Black Friday 2025. The author explicitly identifies the figures as personal estimates without inside knowledge. One strong advertising day is not a normal daily average, and revenue is not profit.
3. [Block Blast official announcement: 70M DAU and more than 10,000 experiments](https://www.blockblast.com/news/70m-dau-10000-experiments), January 8, 2026. Hungry Studio reported 70 million daily and 300 million monthly active users globally, and more than 10,000 A/B tests in 2025. These are company-reported figures, not independently verified Ring Rush comparables.
4. [AppsFlyer: The State of App Monetization — 2024 Edition](https://www.appsflyer.com/resources/reports/app-marketing-monetization-report-2024/). In its historical high-income-market sample, ad-supported hypercasual games approached but remained below 1× ROAS around day 60. **1× after 60 days is not an industry standard.** Results differ by category, platform, audience, and monetization model. This finding does not predict Ring Rush's payback period. Our 1.2–1.5× day-30 milestone is a proposed business target, not a benchmark from this report.
5. [Apple: App Store search](https://developer.apple.com/app-store/search/). Relevant keywords affect search visibility; screenshots and previews may appear in search results.
6. [Apple: Creating Your Product Page](https://developer.apple.com/app-store/product-page/). Guidance on presenting the app through metadata, screenshots, and previews.
7. [Apple: Getting Featured on the App Store](https://developer.apple.com/app-store/getting-featured/). Featuring nominations can submit an app or update for editorial consideration; selection is not guaranteed.
8. [Meta: Use app events to reach, optimize and measure](https://www.facebookblueprint.com/student/path/253008-use-app-events-to-target-optimize-measure). Background on measuring and optimizing app campaigns; consult current setup requirements before any launch.

The immediate decision is to learn whether players enjoy and return to Ring Rush. Paid growth becomes a business investment only when acquisition costs and attributable revenue support it; neither paid profitability nor organic discovery is guaranteed.
