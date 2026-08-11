using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class NetworkPlayerPowerGaugeTests
{
    [Test]
    public void NetPlayer_UsesTheSharedConfiguredPowerGauge()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Game/Prefabs/NetPlayer.prefab");

        Assert.That(prefab, Is.Not.Null);
        PowerGauge gauge = prefab.GetComponent<PowerGauge>();
        Assert.That(gauge, Is.Not.Null);

        SerializedObject serializedGauge = new SerializedObject(gauge);
        SerializedProperty config = serializedGauge.FindProperty("config");
        Assert.That(config.objectReferenceValue, Is.Not.Null);
        Assert.That(config.objectReferenceValue.name, Is.EqualTo("DefaultPowerGaugeConfig"));
    }
}
