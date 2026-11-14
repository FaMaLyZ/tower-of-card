using UnityEngine;

/// Put [RequiredField] on an Inspector reference that must not be left empty.
/// The scene checker (menu Tower of Cards > Check Open Scene) reports every empty one.
public class RequiredFieldAttribute : PropertyAttribute
{
}
