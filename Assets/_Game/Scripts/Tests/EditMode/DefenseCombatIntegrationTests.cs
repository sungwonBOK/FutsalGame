using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class DefenseCombatIntegrationTests
{
    [Test]
    public void Punch_DoesNotStunAPlayerWhoBlocksWithinTheDefenseWindow()
    {
        GameObject attackerObject = new GameObject("Attacker");
        GameObject defenderObject = new GameObject("Defender");

        try
        {
            defenderObject.transform.position = Vector3.forward;
            CombatController attacker = CreateCombatant(attackerObject);
            CombatController defender = CreateCombatant(defenderObject);
            DefenseController defense = defenderObject.GetComponent<DefenseController>();
            Assert.That(defense.TryStartDefense(), Is.True);
            Physics.SyncTransforms();

            attacker.Punch(Vector3.forward);

            Assert.That(defenderObject.GetComponent<CharacterState>().IsStunned, Is.False);
            Assert.That(defense.IsDefending, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(attackerObject);
            Object.DestroyImmediate(defenderObject);
        }
    }

    [Test]
    public void Tackle_DoesNotStunAPlayerAfterASuccessfulBlockAcrossFollowingPhysicsTicks()
    {
        GameObject attackerObject = new GameObject("Attacker");
        GameObject defenderObject = new GameObject("Defender");

        try
        {
            defenderObject.transform.position = Vector3.forward * 0.5f;
            CombatController attacker = CreateCombatant(attackerObject);
            CombatController defender = CreateCombatant(defenderObject);
            DefenseController defense = defenderObject.GetComponent<DefenseController>();
            Assert.That(defense.TryStartDefense(), Is.True);
            Physics.SyncTransforms();

            attacker.SlideTackle(Vector3.forward);
            InvokePrivate(attacker, "FixedUpdate");
            InvokePrivate(attacker, "FixedUpdate");

            Assert.That(defenderObject.GetComponent<CharacterState>().IsStunned, Is.False);
            Assert.That(defense.IsDefending, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(attackerObject);
            Object.DestroyImmediate(defenderObject);
        }
    }

    [Test]
    public void Grab_DoesNotRestrictAPlayerWhoBlocksWithinTheDefenseWindow()
    {
        GameObject attackerObject = new GameObject("Attacker");
        GameObject defenderObject = new GameObject("Defender");

        try
        {
            defenderObject.transform.position = Vector3.forward;
            CombatController attacker = CreateCombatant(attackerObject);
            CombatController defender = CreateCombatant(defenderObject);
            DefenseController defense = defenderObject.GetComponent<DefenseController>();
            InvokePrivate(attackerObject.GetComponent<GrabController>(), "Awake");
            Assert.That(defense.TryStartDefense(), Is.True);
            Physics.SyncTransforms();

            Assert.That(attacker.TryGrab(Vector3.forward), Is.False);
            Assert.That(defender.IsHeldByGrab, Is.False);
            Assert.That(defense.IsDefending, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(attackerObject);
            Object.DestroyImmediate(defenderObject);
        }
    }

    private static CombatController CreateCombatant(GameObject gameObject)
    {
        gameObject.AddComponent<Rigidbody>();
        gameObject.AddComponent<SphereCollider>();
        CharacterState state = gameObject.AddComponent<CharacterState>();
        CharacterMotor motor = gameObject.AddComponent<CharacterMotor>();
        CharacterLocomotion locomotion = gameObject.AddComponent<CharacterLocomotion>();
        CombatController combat = gameObject.AddComponent<CombatController>();

        InvokePrivate(state, "Awake");
        InvokePrivate(motor, "Awake");
        InvokePrivate(locomotion, "Awake");
        InvokePrivate(combat, "Awake");
        DefenseController defense = gameObject.GetComponent<DefenseController>();
        InvokePrivate(defense, "Awake");
        return combat;
    }

    private static void InvokePrivate(object target, string methodName)
    {
        target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, null);
    }
}
