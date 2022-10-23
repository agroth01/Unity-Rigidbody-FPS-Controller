using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using URC.Audio;

[CustomEditor(typeof(Footsteps))]
public class FootstepsEditor : Editor
{
    // Load properties
    public SerializedProperty m_footstepModeProp;
    public SerializedProperty m_audioSourceProp;
    public SerializedProperty m_footstepSoundsProp;
    public SerializedProperty m_selectionModeProp;
    public SerializedProperty m_pitchVariationProp;
    public SerializedProperty m_volumeVariationProp;
    public SerializedProperty m_layerOverridesProp;
    public SerializedProperty m_overrideSurfacesProp;
    public SerializedProperty m_resetDelayProp;
    public SerializedProperty m_footstepPrewarmingProp;
    public SerializedProperty m_fixedFrequencyProp;
    public SerializedProperty m_velocityThresholdProp;
    public SerializedProperty m_velocityDrivenFrequencyProp;

    private void OnEnable()
    {
        // Setup properties
        m_footstepModeProp = serializedObject.FindProperty("m_footstepMode");
        m_audioSourceProp = serializedObject.FindProperty("m_audioSource");
        m_footstepSoundsProp = serializedObject.FindProperty("m_footstepSounds");
        m_selectionModeProp = serializedObject.FindProperty("m_selectionMode");
        m_pitchVariationProp = serializedObject.FindProperty("m_pitchVariation");
        m_volumeVariationProp = serializedObject.FindProperty("m_volumeVariation");
        m_layerOverridesProp = serializedObject.FindProperty("m_layerOverrides");
        m_overrideSurfacesProp = serializedObject.FindProperty("m_overrideSurfaces");
        m_resetDelayProp = serializedObject.FindProperty("m_resetDelay");
        m_footstepPrewarmingProp = serializedObject.FindProperty("m_footstepPrewarming");
        m_fixedFrequencyProp = serializedObject.FindProperty("m_fixedFrequency");
        m_velocityThresholdProp = serializedObject.FindProperty("m_velocityThreshold");
        m_velocityDrivenFrequencyProp = serializedObject.FindProperty("m_velocityDrivenFrequency");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // General settings
        EditorGUILayout.PropertyField(m_footstepModeProp);
        EditorGUILayout.PropertyField(m_audioSourceProp);

        // Sounds
        EditorGUILayout.PropertyField(m_footstepSoundsProp);
        EditorGUILayout.PropertyField(m_selectionModeProp);
        EditorGUILayout.PropertyField(m_pitchVariationProp);
        EditorGUILayout.PropertyField(m_volumeVariationProp);

        // Overrides
        EditorGUILayout.PropertyField(m_layerOverridesProp);
        EditorGUILayout.PropertyField(m_overrideSurfacesProp);

        // Resetting
        EditorGUILayout.PropertyField(m_resetDelayProp);
        EditorGUILayout.PropertyField(m_footstepPrewarmingProp);

        // Fixed mode options
        if (m_footstepModeProp.enumValueIndex == (int)Footsteps.FootstepMode.Fixed)
        {
            EditorGUILayout.PropertyField(m_fixedFrequencyProp);
        }

        // Velocity driven mode options
        if (m_footstepModeProp.enumValueIndex == (int)Footsteps.FootstepMode.VelocityDriven)
        {
            EditorGUILayout.PropertyField(m_velocityThresholdProp);
            EditorGUILayout.PropertyField(m_velocityDrivenFrequencyProp);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
