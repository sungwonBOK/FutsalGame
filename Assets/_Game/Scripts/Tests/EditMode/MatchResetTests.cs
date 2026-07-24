using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class MatchResetTests
{
    [Test]
    public void ResetCharacter_RestoresMobilityState()
    {
        GameObject managerObject = new GameObject("Game Manager");
        GameObject player = new GameObject("Player");

        try
        {
            GameManager manager = managerObject.AddComponent<GameManager>();
            player.AddComponent<Rigidbody>();
            player.AddComponent<CharacterState>();
            CharacterMotor motor = player.AddComponent<CharacterMotor>();
            CharacterLocomotion locomotion = player.AddComponent<CharacterLocomotion>();
            InvokePrivate(motor, "Awake");
            InvokePrivate(locomotion, "Awake");
            Assert.That(locomotion.TryDodge(Vector3.right), Is.True);

            InvokePrivate(manager, "ResetCharacter", player.transform, Vector3.zero, Quaternion.identity);

            Assert.That(locomotion.Stamina01, Is.EqualTo(1f));
            Assert.That(locomotion.IsDodging, Is.False);
            Assert.That(locomotion.CanDodge, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(managerObject);
        }
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, arguments);
    }
}
