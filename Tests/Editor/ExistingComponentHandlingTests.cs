using System;
using net.bekobeko.utilitytools.core;
using net.bekobeko.utilitytools.windows;
using NUnit.Framework;
using UnityEditor;

namespace net.bekobeko.utilitytools.tests
{
    public sealed class ExistingComponentHandlingTests
    {
        private string _preferenceKey;

        [SetUp]
        public void SetUp()
        {
            _preferenceKey = $"net.bekobeko.utilitytools.tests.{Guid.NewGuid():N}";
        }

        [TearDown]
        public void TearDown()
        {
            EditorPrefs.DeleteKey(_preferenceKey);
        }

        [Test]
        public void Preferences_WithoutSavedValue_DefaultsToSkip()
        {
            Assert.That(
                ExistingComponentHandlingPreferences.Load(_preferenceKey),
                Is.EqualTo(ExistingComponentHandlingMode.Skip));
        }

        [Test]
        public void Preferences_SaveAndLoad_RestoresMode()
        {
            ExistingComponentHandlingPreferences.Save(
                _preferenceKey,
                ExistingComponentHandlingMode.Merge);

            Assert.That(
                ExistingComponentHandlingPreferences.Load(_preferenceKey),
                Is.EqualTo(ExistingComponentHandlingMode.Merge));
        }

        [Test]
        public void Preferences_InvalidStoredValue_FallsBackToSkip()
        {
            EditorPrefs.SetInt(_preferenceKey, int.MaxValue);

            Assert.That(
                ExistingComponentHandlingPreferences.Load(_preferenceKey),
                Is.EqualTo(ExistingComponentHandlingMode.Skip));
        }

        [Test]
        public void Session_ApplyToAll_ReusesChoiceWithoutCallingResolverAgain()
        {
            var callCount = 0;
            var session = new ConflictResolutionSession(_ =>
            {
                callCount++;
                return new ConflictResolutionDecision(ConflictResolutionAction.Overwrite, true);
            });

            var first = session.Resolve(CreateConflict("first"));
            var second = session.Resolve(CreateConflict("second"));

            Assert.That(first, Is.EqualTo(ConflictResolutionAction.Overwrite));
            Assert.That(second, Is.EqualTo(ConflictResolutionAction.Overwrite));
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void Session_Cancel_IsNotStoredAsApplyToAll()
        {
            var callCount = 0;
            var session = new ConflictResolutionSession(_ =>
            {
                callCount++;
                return new ConflictResolutionDecision(ConflictResolutionAction.Cancel, true);
            });

            session.Resolve(CreateConflict("first"));
            session.Resolve(CreateConflict("second"));

            Assert.That(callCount, Is.EqualTo(2));
        }

        [Test]
        public void SeparateSessions_DoNotShareApplyToAllChoice()
        {
            var firstCalls = 0;
            var secondCalls = 0;
            var firstSession = new ConflictResolutionSession(_ =>
            {
                firstCalls++;
                return new ConflictResolutionDecision(ConflictResolutionAction.Skip, true);
            });
            var secondSession = new ConflictResolutionSession(_ =>
            {
                secondCalls++;
                return new ConflictResolutionDecision(ConflictResolutionAction.Overwrite, false);
            });

            firstSession.Resolve(CreateConflict("first"));
            secondSession.Resolve(CreateConflict("second"));

            Assert.That(firstCalls, Is.EqualTo(1));
            Assert.That(secondCalls, Is.EqualTo(1));
        }

        private static ConflictInfo CreateConflict(string subject)
        {
            return new ConflictInfo(subject, "existing", "generated");
        }
    }
}
