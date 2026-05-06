using NUnit.Framework;
using UnityEngine;

namespace ParticleThumbnailAndPreview.Editor.Tests
{
    public class PreviewBoundsVisualizerTests
    {
        [Test]
        public void FormatDimensionLabelForTests_UsesPrefixAndTwoDecimals()
        {
            string label = PreviewBoundsVisualizer.FormatDimensionLabelForTests('W', 1.234f);
            Assert.AreEqual("W 1.23", label);
        }

        [Test]
        public void GetDimensionAnchorForTests_ReturnsEdgeMidpoints()
        {
            Bounds bounds = new Bounds(new Vector3(10f, 20f, 30f), new Vector3(4f, 6f, 8f));

            Assert.AreEqual(new Vector3(10f, 17f, 26f), PreviewBoundsVisualizer.GetDimensionAnchorForTests(bounds, 0));
            Assert.AreEqual(new Vector3(8f, 20f, 26f), PreviewBoundsVisualizer.GetDimensionAnchorForTests(bounds, 1));
            Assert.AreEqual(new Vector3(8f, 17f, 30f), PreviewBoundsVisualizer.GetDimensionAnchorForTests(bounds, 2));
        }

        [Test]
        public void GetDimensionColorForTests_UsesAxisColors()
        {
            Assert.AreEqual(new Color(0.93f, 0.37f, 0.37f, 1f), PreviewBoundsVisualizer.GetDimensionColorForTests(0));
            Assert.AreEqual(new Color(0.44f, 0.86f, 0.44f, 1f), PreviewBoundsVisualizer.GetDimensionColorForTests(1));
            Assert.AreEqual(new Color(0.4f, 0.7f, 0.97f, 1f), PreviewBoundsVisualizer.GetDimensionColorForTests(2));
        }
    }
}
