using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class CharacterAnimatorDefenseTests
{
    private const string ControllerPath = "Assets/_Game/Animation/FutsalCharacter.controller";

    [TestCase("LeftBlock", "LeftBlock")]
    [TestCase("RightBlock", "RightBlock")]
    [TestCase("BackBlock", "BackBlock")]
    public void AnimatorController_ProvidesEachDefenseTriggerAndState(string triggerName, string stateName)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        Assert.That(controller, Is.Not.Null);
        Assert.That(controller.parameters, Has.Some.Matches<AnimatorControllerParameter>(parameter =>
            parameter.name == triggerName && parameter.type == AnimatorControllerParameterType.Trigger));
        Assert.That(controller.layers[0].stateMachine.states, Has.Some.Matches<ChildAnimatorState>(state =>
            state.state.name == stateName));
    }
}
