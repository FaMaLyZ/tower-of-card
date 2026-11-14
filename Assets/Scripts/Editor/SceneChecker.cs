using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Menu: CardTower > 5. Check Open Scene
///
/// Scans the open scene for the two things that break a scene silently:
///   - Missing Scripts (a component whose script file was deleted or renamed outside Unity)
///   - empty required fields (Inspector references marked [RequiredField])
/// It also checks there is exactly one conductor. Run it after every step of the Master Plan.
public static class SceneChecker
{
    [MenuItem("CardTower/5. Check Open Scene")]
    public static void CheckOpenScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        int problems = CountProblems(scene);

        if (problems == 0) Debug.Log($"Scene check: '{scene.name}' is OK. No Missing Script, no empty required field.");
        else Debug.LogError($"Scene check: '{scene.name}' has {problems} problem(s). Click each message above to find the object.");
    }

    /// Logs every problem and returns how many were found.
    public static int CountProblems(Scene scene)
    {
        int problems = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                problems += CountMissingScripts(t.gameObject);
                problems += CountEmptyRequiredFields(t.gameObject);
            }

        problems += CountConductorProblems();
        return problems;
    }

    static int CountMissingScripts(GameObject go)
    {
        int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
        if (missing > 0) Debug.LogError($"Scene check: Missing Script on '{GetPath(go)}' ({missing}).", go);
        return missing;
    }

    static int CountEmptyRequiredFields(GameObject go)
    {
        int empty = 0;

        foreach (MonoBehaviour script in go.GetComponents<MonoBehaviour>())
        {
            if (script == null) continue;       // a Missing Script, already reported

            foreach (FieldInfo field in script.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!IsEmptyRequiredField(field, script)) continue;
                Debug.LogError($"Scene check: '{script.GetType().Name}.{field.Name}' is empty on '{GetPath(go)}'.", script);
                empty++;
            }
        }
        return empty;
    }

    static bool IsEmptyRequiredField(FieldInfo field, MonoBehaviour script)
    {
        if (field.GetCustomAttribute<RequiredFieldAttribute>() == null) return false;

        object value = field.GetValue(script);
        if (value is Object unityObject) return unityObject == null;    // also true for a destroyed object
        return value == null;
    }

    static int CountConductorProblems()
    {
        bool sceneHasGameplay = Object.FindAnyObjectByType<TowerBuilder>(FindObjectsInactive.Include) != null;
        if (!sceneHasGameplay) return 0;        // a menu scene needs no conductor

        int conductors = Object.FindObjectsByType<GameFlowController>(FindObjectsInactive.Include).Length;
        if (conductors == 1) return 0;

        Debug.LogError(conductors == 0
            ? "Scene check: this scene has a TowerBuilder but no GameFlowController. Add an empty object with a GameFlowController and drag GameConfig onto it."
            : $"Scene check: {conductors} GameFlowControllers in the scene. There must be exactly one.");
        return 1;
    }

    static string GetPath(GameObject go)
    {
        string path = go.name;
        for (Transform parent = go.transform.parent; parent != null; parent = parent.parent)
            path = parent.name + "/" + path;
        return path;
    }
}
