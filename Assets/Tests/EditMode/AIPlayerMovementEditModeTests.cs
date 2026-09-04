using NUnit.Framework;

public sealed class AIPlayerMovementEditModeTests
{
    [TestCase(0f, -1f)]
    [TestCase(0.499f, -1f)]
    [TestCase(0.5f, 1f)]
    [TestCase(1f, 1f)]
    public void SelectsRoamDirectionFromRandomSample(
        float randomSample,
        float expectedDirection)
    {
        Assert.That(
            AIPlayerCommandSource.SelectRoamDirection(randomSample),
            Is.EqualTo(expectedDirection));
    }

    [TestCase(0f, 0.6f)]
    [TestCase(0.5f, 1.3f)]
    [TestCase(1f, 2f)]
    public void SelectsRoamDurationWithinConfiguredRange(
        float randomSample,
        float expectedDuration)
    {
        Assert.That(
            AIPlayerCommandSource.SelectRoamDuration(
                randomSample,
                0.6f,
                2f),
            Is.EqualTo(expectedDuration).Within(0.0001f));
    }

    [TestCase(
        0f,
        0.4f,
        false,
        false)]
    [TestCase(
        0f,
        0.4f,
        true,
        true)]
    [TestCase(
        0.399f,
        0.4f,
        true,
        true)]
    [TestCase(
        0.4f,
        0.4f,
        true,
        false)]
    [TestCase(
        1f,
        0.4f,
        true,
        false)]
    public void ExplorationDropRequiresASelectedSafePlatform(
        float randomSample,
        float dropChance,
        bool hasSuitablePlatformBelow,
        bool expectedDrop)
    {
        AIPlayerCommandSource.ExplorationVerticalAction expectedAction =
            expectedDrop
                ? AIPlayerCommandSource.ExplorationVerticalAction.Drop
                : AIPlayerCommandSource.ExplorationVerticalAction.Jump;
        Assert.That(
            AIPlayerCommandSource.SelectExplorationVerticalAction(
                randomSample,
                dropChance,
                hasSuitablePlatformBelow),
            Is.EqualTo(expectedAction));
    }

    [TestCase(2.2f, 0f, 0.5f, -2f, 2f, -1f)]
    [TestCase(-2.2f, 0f, 0.5f, -2f, 2f, 1f)]
    [TestCase(1.9f, 1f, 0.5f, -2f, 2f, -1f)]
    [TestCase(-1.9f, -1f, 0.5f, -2f, 2f, 1f)]
    [TestCase(2.2f, -2f, 0.5f, -2f, 2f, -1f)]
    [TestCase(-2.2f, 2f, 0.5f, -2f, 2f, 1f)]
    [TestCase(0f, 1f, 0.5f, -2f, 2f, 0f)]
    public void AirRecoverySteersPredictedLandingIntoSafeRange(
        float currentX,
        float horizontalVelocity,
        float timeToLanding,
        float safeMinimumX,
        float safeMaximumX,
        float expectedMovement)
    {
        float movement = AIPlayerCommandSource.CalculateRecoveryMovement(
            currentX,
            horizontalVelocity,
            timeToLanding,
            safeMinimumX,
            safeMaximumX);

        Assert.That(movement, Is.EqualTo(expectedMovement));
    }

    [TestCase(0f, 0.5f, 0.5f, true)]
    [TestCase(0f, -0.5f, 0.5f, true)]
    [TestCase(0f, 0.51f, 0.5f, false)]
    [TestCase(2f, 2f, 0f, true)]
    public void HeightAlignmentUsesInclusiveTolerance(
        float selfY,
        float targetY,
        float tolerance,
        bool expectedAligned)
    {
        Assert.That(
            AIPlayerCommandSource.IsHeightAligned(selfY, targetY, tolerance),
            Is.EqualTo(expectedAligned));
    }
}
