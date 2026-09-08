using System.Collections;
using NUnit.Framework;
using Roloc.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Roloc.Tests
{
    public sealed class RibbonGraphicTests
    {
        GameObject root;
        RibbonGraphic ribbon;
        Mesh mesh;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Ribbon test canvas", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var child = new GameObject("Ribbon", typeof(RectTransform));
            child.transform.SetParent(root.transform, false);
            child.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 100);
            ribbon = child.AddComponent<RibbonGraphic>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        void ReadMesh()
        {
            Canvas.ForceUpdateCanvases();
            mesh = ribbon.canvasRenderer.GetMesh();
        }

        void AssertFiniteMesh()
        {
            Assert.That(mesh.vertexCount, Is.GreaterThan(0));
            foreach (var vertex in mesh.vertices)
            {
                Assert.That(float.IsNaN(vertex.x) || float.IsInfinity(vertex.x), Is.False);
                Assert.That(float.IsNaN(vertex.y) || float.IsInfinity(vertex.y), Is.False);
                Assert.That(float.IsNaN(vertex.z) || float.IsInfinity(vertex.z), Is.False);
            }
        }

        [Test]
        public void FastDragsStayBoundedAndProduceConnectedGeometry()
        {
            ribbon.AddPoint(Vector2.zero);
            ribbon.AddPoint(new Vector2(180, 0));
            Assert.That(ribbon.PointCount, Is.GreaterThan(2), "Fast movement should be resampled.");
            for (int i = 1; i <= 100; i++) ribbon.AddPoint(new Vector2(i * 1000, 0));
            Assert.That(ribbon.PointCount, Is.InRange(2, 64));
            ReadMesh();
            AssertFiniteMesh();
            Assert.That(mesh.vertexCount, Is.LessThanOrEqualTo(64 * 5));
            Assert.That(mesh.triangles.Length, Is.GreaterThan(0));
            // Straight movement must never generate a spike wider than the ribbon.
            Assert.That(mesh.bounds.size.y, Is.LessThanOrEqualTo(10.001f));
        }

        [Test]
        public void DuplicateAndReversedPointsDoNotCreateInvalidVerticesOrSpikes()
        {
            ribbon.AddPoint(Vector2.zero);
            ribbon.AddPoint(Vector2.zero);
            Assert.That(ribbon.PointCount, Is.EqualTo(1));
            ribbon.AddPoint(new Vector2(30, 0));
            ribbon.AddPoint(Vector2.zero);
            ribbon.AddPoint(new Vector2(0, 30));
            int count = ribbon.PointCount;
            ribbon.AddPoint(new Vector2(float.NaN, 0));
            ribbon.AddPoint(new Vector2(0, float.PositiveInfinity));
            Assert.That(ribbon.PointCount, Is.EqualTo(count));
            ReadMesh();
            AssertFiniteMesh();
            foreach (var vertex in mesh.vertices)
            {
                Assert.That(vertex.x, Is.InRange(-5.001f, 35.001f));
                Assert.That(vertex.y, Is.InRange(-5.001f, 35.001f));
            }
        }

        [UnityTest]
        public IEnumerator DynamicTrailExpiresWithoutNewPointerInput()
        {
            ribbon.AddPoint(Vector2.zero);
            ribbon.AddPoint(new Vector2(30, 0));
            ReadMesh();
            AssertFiniteMesh();
            yield return new WaitForSecondsRealtime(.32f);
            yield return null;
            Assert.That(ribbon.PointCount, Is.Zero);
            ReadMesh();
            Assert.That(mesh.vertexCount, Is.Zero);
        }

        [Test]
        public void ClearAndDisableDiscardTrailHistory()
        {
            ribbon.AddPoint(Vector2.zero);
            ribbon.AddPoint(new Vector2(30, 0));
            ribbon.Clear();
            Assert.That(ribbon.PointCount, Is.Zero);
            ReadMesh();
            Assert.That(mesh.vertexCount, Is.Zero);
            ribbon.AddPoint(Vector2.zero);
            ribbon.AddPoint(new Vector2(30, 0));
            ribbon.gameObject.SetActive(false);
            Assert.That(ribbon.PointCount, Is.Zero);
            ribbon.gameObject.SetActive(true);
            ReadMesh();
            Assert.That(mesh.vertexCount, Is.Zero);
            Assert.That(ribbon.raycastTarget, Is.False);
        }

        [Test]
        public void PreviewDrawsCurvedRibbonWithinRectAndIgnoresDynamicPoints()
        {
            ribbon.Preview = true;
            ribbon.AddPoint(new Vector2(1000, 1000));
            Assert.That(ribbon.PointCount, Is.Zero);
            foreach (var size in new[] { new Vector2(40, 80), new Vector2(120, 60) })
            {
                ribbon.rectTransform.sizeDelta = size;
                ReadMesh();
                AssertFiniteMesh();
                var rect = ribbon.rectTransform.rect;
                foreach (var vertex in mesh.vertices)
                {
                    Assert.That(vertex.x, Is.InRange(rect.xMin, rect.xMax));
                    Assert.That(vertex.y, Is.InRange(rect.yMin, rect.yMax));
                }
            }
            ribbon.Preview = false;
            ReadMesh();
            Assert.That(mesh.vertexCount, Is.Zero);
        }
    }
}
