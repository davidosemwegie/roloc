// Self-contained geometry extracted from the retired game. No runtime dependencies.
export function bounds(x: number, y: number, width: number, height: number) {
  return {
    topLeft: {x, y}, topRight: {x: x + width, y},
    bottomLeft: {x, y: y + height}, bottomRight: {x: x + width, y: y + height},
    center: {x: x + width / 2, y: y + height / 2},
  };
}
// Original strict timing and ring-shuffle schedule, preserved as a gameplay reference.
export function seconds(score: number) {
  return score >= 40 ? 1.75 : score >= 20 ? 2 : score >= 5 ? 2.5 : 3;
}
export function shuffleRings(score: number) {
  return score > 80 || (score > 60 && score % 2 === 0) || (score > 30 && score % 5 === 0);
}
