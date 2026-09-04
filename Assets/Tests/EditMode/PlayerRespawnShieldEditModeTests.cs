using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PlayerRespawnShieldEditModeTests
{
    [Test]
    public void ShieldUsesReducedImpulseUntilTheThreeSecondBoundary()
    {
        GameObject shieldObject = new("RespawnShieldTest");

        try
        {
            PlayerRespawnShield shield =
                shieldObject.AddComponent<PlayerRespawnShield>();
            GameObject barrierObject = new("BarrierVisual");
            barrierObject.transform.SetParent(shieldObject.transform);
            SpriteRenderer barrierRenderer =
                barrierObject.AddComponent<SpriteRenderer>();
            barrierObject.SetActive(false);

            SerializedObject serializedShield = new(shield);
            serializedShield.FindProperty("barrierRenderer").objectReferenceValue =
                barrierRenderer;
            serializedShield.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(shield.Duration, Is.EqualTo(3f));
            Assert.That(shield.ImpulseReduction, Is.EqualTo(0.8f));
            Assert.That(shield.IsBarrierVisible, Is.False);

            shield.ActivateAt(10f);

            Assert.That(shield.IsActiveAt(10f), Is.True);
            Assert.That(shield.IsBarrierVisible, Is.True);
            Assert.That(shield.IsActiveAt(12.999f), Is.True);
            shield.RefreshVisualAt(12.999f);
            Assert.That(shield.IsBarrierVisible, Is.True);
            Assert.That(
                shield.GetIncomingImpulseMultiplierAt(12.999f),
                Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(shield.IsActiveAt(13f), Is.False);
            shield.RefreshVisualAt(13f);
            Assert.That(shield.IsBarrierVisible, Is.False);
            Assert.That(
                shield.GetIncomingImpulseMultiplierAt(13f),
                Is.EqualTo(1f));

            shield.ActivateAt(20f);
            Assert.That(shield.IsBarrierVisible, Is.True);
            shield.Deactivate();
            Assert.That(shield.IsBarrierVisible, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(shieldObject);
        }
    }
}
