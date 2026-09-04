using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Utils;

public sealed class OneWayPlatformPrefabEditorTests
{
    private const string BasePrefabPath =
        "Assets/Prefabs/PF_OneWayPlatform.prefab";
    private const string SixByOnePrefabPath =
        "Assets/Prefabs/PF_OneWayPlatform_6x1.prefab";
    private const string TwelveByOnePrefabPath =
        "Assets/Prefabs/PF_OneWayPlatform_12x1.prefab";
    private const string SixByOneSpritePath =
        "Assets/Art/platform/platform_6_1.png";
    private const string TwelveByOneSpritePath =
        "Assets/Art/platform/platform_12_1.png";
    private const float GeometryTolerance = 0.02f;

    [TestCase(SixByOnePrefabPath, SixByOneSpritePath, 6f)]
    [TestCase(TwelveByOnePrefabPath, TwelveByOneSpritePath, 12f)]
    public void PlatformVariantInheritsSharedPhysicsAndMatchesItsArtwork(
        string variantPath,
        string spritePath,
        float expectedWidth)
    {
        GameObject variantAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
        Assert.That(variantAsset, Is.Not.Null);
        Assert.That(
            PrefabUtility.GetPrefabAssetType(variantAsset),
            Is.EqualTo(PrefabAssetType.Variant));

        GameObject sourceAsset =
            PrefabUtility.GetCorrespondingObjectFromSource(variantAsset);
        Assert.That(sourceAsset, Is.Not.Null);
        Assert.That(
            AssetDatabase.GetAssetPath(sourceAsset),
            Is.EqualTo(BasePrefabPath));

        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(variantPath);
            Assert.That(root, Is.Not.Null);
            Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(root.layer,
                Is.EqualTo(LayerMask.NameToLayer("OneWayPlatform")));

            Rigidbody2D body = root.GetComponent<Rigidbody2D>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Static));
            Assert.That(body.simulated, Is.True);

            EdgeCollider2D edge = root.GetComponent<EdgeCollider2D>();
            Assert.That(edge, Is.Not.Null);
            Assert.That(edge.enabled, Is.True);
            Assert.That(edge.isTrigger, Is.False);
            Assert.That(edge.usedByEffector, Is.True);
            Assert.That(edge.points, Has.Length.EqualTo(2));

            float expectedHalfWidth = expectedWidth * 0.5f;
            Assert.That(edge.points[0],
                Is.EqualTo(new Vector2(-expectedHalfWidth, 0.5f))
                    .Using(Vector2ComparerWithEqualsOperator.Instance));
            Assert.That(edge.points[1],
                Is.EqualTo(new Vector2(expectedHalfWidth, 0.5f))
                    .Using(Vector2ComparerWithEqualsOperator.Instance));

            PlatformEffector2D effector =
                root.GetComponent<PlatformEffector2D>();
            Assert.That(effector, Is.Not.Null);
            Assert.That(effector.enabled, Is.True);
            Assert.That(effector.useColliderMask, Is.True);
            Assert.That(effector.colliderMask,
                Is.EqualTo(1 << LayerMask.NameToLayer("Player")));
            Assert.That(effector.useOneWay, Is.True);
            Assert.That(effector.useOneWayGrouping, Is.True);
            Assert.That(effector.surfaceArc, Is.EqualTo(120f));

            Transform visual = root.transform.Find("Visual");
            Assert.That(visual, Is.Not.Null);
            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sprite, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(renderer.sprite),
                Is.EqualTo(spritePath));

            Bounds visualBounds = renderer.bounds;
            Assert.That(
                visualBounds.size.x,
                Is.EqualTo(expectedWidth).Within(GeometryTolerance));
            Assert.That(
                visualBounds.size.y,
                Is.EqualTo(1f).Within(GeometryTolerance));

            Vector3 firstPoint = edge.transform.TransformPoint(
                edge.points[0] + edge.offset);
            Vector3 secondPoint = edge.transform.TransformPoint(
                edge.points[1] + edge.offset);
            float leftEdge = Mathf.Min(firstPoint.x, secondPoint.x);
            float rightEdge = Mathf.Max(firstPoint.x, secondPoint.x);
            float surfaceY = (firstPoint.y + secondPoint.y) * 0.5f;

            Assert.That(leftEdge,
                Is.EqualTo(visualBounds.min.x).Within(GeometryTolerance));
            Assert.That(rightEdge,
                Is.EqualTo(visualBounds.max.x).Within(GeometryTolerance));
            Assert.That(surfaceY,
                Is.GreaterThanOrEqualTo(
                    visualBounds.center.y - GeometryTolerance));
            Assert.That(surfaceY,
                Is.LessThanOrEqualTo(
                    visualBounds.max.y + GeometryTolerance));
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
