using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Roloc.Services.Tests
{
    public sealed class DistributionAnalyticsPolicyTests
    {
        [Test] public void EditorIsNeverEligibleThroughProductionPolicy() => Assert.That(DistributionAnalyticsPolicy.BuildEligible, Is.False);
        [TestCase(typeof(DailyClient), "StartArgs")]
        [TestCase(typeof(LeaderboardClient), "StartArgs")]
        [TestCase(typeof(LeaderboardClient), "ProfileArgs")]
        public void TicketAndProfileWireTypesDefaultToIneligible(Type owner, string typeName)
        {
            var args = Activator.CreateInstance(owner.GetNestedType(typeName, BindingFlags.NonPublic), true);
            Assert.That(JsonUtility.ToJson(args), Does.Contain("\"analyticsEligible\":false"));
        }
        [TestCase(true, true, false, false, true, true, false, true)]
        [TestCase(false, true, false, false, true, true, false, false)]
        [TestCase(true, false, false, false, true, true, false, false)]
        [TestCase(true, true, true, false, true, true, false, false)]
        [TestCase(true, true, false, true, true, true, false, false)]
        [TestCase(true, true, false, false, false, true, false, false)]
        [TestCase(true, true, false, false, true, false, false, false)]
        [TestCase(true, true, false, false, true, true, true, false)]
        public void EveryDistributionConditionIsRequired(bool distribution, bool ios, bool editor, bool development,
            bool player, bool physical, bool debug, bool expected)
        {
            Assert.That(DistributionAnalyticsPolicy.Evaluate(distribution, ios, editor, development, player, physical, debug), Is.EqualTo(expected));
        }
    }
}
