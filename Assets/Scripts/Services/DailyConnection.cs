using UnityEngine;

namespace Roloc.Services
{
    [CreateAssetMenu(menuName = "Ring Rush/Daily connection", fileName = "DailyConnection")]
    public sealed class DailyConnection : ScriptableObject
    {
        [Tooltip("Convex deployment URL, for example https://your-deployment.convex.cloud. Never an admin key.")]
        public string url = "";
        [Tooltip("Closed TestFlight invitation code. Keep the actual Resources/DailyConnection asset out of Git.")]
        public string closedTestCode = "";
    }
}
