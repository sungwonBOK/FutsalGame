using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class DefenseControllerTests
{
    [Test]
    public void StartDefense_SpendsTheSameStaminaAsDodgeAndOpensTheDefenseWindow()
    {
        GameObject player = new GameObject("Player");

        try
        {
            player.AddComponent<Rigidbody>();
            player.AddComponent<CharacterState>();
            CharacterMotor motor = player.AddComponent<CharacterMotor>();
            CharacterLocomotion locomotion = player.AddComponent<CharacterLocomotion>();
            DefenseController defense = player.AddComponent<DefenseController>();
            InvokePrivate(motor, "Awake");
            InvokePrivate(locomotion, "Awake");
            InvokePrivate(defense, "Awake");

            bool started = defense.TryStartDefense();

            Assert.That(started, Is.True);
            Assert.That(locomotion.Stamina01, Is.EqualTo(0.7f).Within(0.001f));
            Assert.That(defense.IsDefending, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    private static void InvokePrivate(object target, string methodName)
    {
        target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, null);
    }
}
