using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// [Ben] Swaps the old card look for the new 52-card set in "Assets/New Cards".
// It doesn't make new prefabs - it opens the existing card prefabs (the ones each CardData
// points to in "model" and "handModel") and only swaps their mesh + material. So the Bone /
// shake animation, CardData links and scenes all stay the same.
// Run it once from the menu: CardTower > 6. Swap To New Card Set
public static class SwapToNewCards
{
    const string NewCardsPrefab = "Assets/New Cards/Prefabs/NewCards.prefab";
    const string CardDataFolder = "Assets/CardData";

    [MenuItem("CardTower/6. Swap To New Card Set")]
    static void Swap()
    {
        // 1. Read the new cards: each child in NewCards.prefab is named like "HA", "S10", "DK"
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(NewCardsPrefab);
        if (source == null) { Debug.LogError($"[SwapCards] Can't find {NewCardsPrefab}"); return; }

        var newCards = new Dictionary<string, MeshRenderer>();
        foreach (var mf in source.GetComponentsInChildren<MeshFilter>(true))
        {
            string key = mf.name;
            if (key == "S1") key = "S2";   // the 2 of spades is named "S1" in NewCards.prefab
            newCards[key] = mf.GetComponent<MeshRenderer>();
        }
        Debug.Log($"[SwapCards] Found {newCards.Count} new cards");

        // 2. For every CardData, swap the mesh in both its tower model and its hand model
        int swapped = 0;
        var problems = new List<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:CardData", new[] { CardDataFolder }))
        {
            var data = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
            string key = Key(data);

            if (!newCards.TryGetValue(key, out MeshRenderer newCard))
            {
                problems.Add($"{data.name}: no new card called {key}");
                continue;
            }

            foreach (GameObject prefab in new[] { data.model, data.handModel })
            {
                if (prefab == null) { problems.Add($"{data.name}: model or handModel is empty"); continue; }
                if (SwapMesh(prefab, newCard)) swapped++;
                else problems.Add($"{data.name}: {prefab.name} has no MeshFilter");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[SwapCards] Done - swapped {swapped} prefabs");
        foreach (string p in problems) Debug.LogWarning($"[SwapCards] {p}");
    }

    // Opens the prefab, puts the new mesh + material on its card mesh, saves it
    static bool SwapMesh(GameObject prefab, MeshRenderer newCard)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(path);

        MeshFilter mf = root.GetComponentInChildren<MeshFilter>(true);
        bool ok = mf != null;
        if (ok)
        {
            mf.sharedMesh = newCard.GetComponent<MeshFilter>().sharedMesh;
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterials = newCard.sharedMaterials;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }

        PrefabUtility.UnloadPrefabContents(root);
        return ok;
    }

    // CardData -> new card name, e.g. Hearts + Ace = "HA", Spades + 10 = "S10"
    static string Key(CardData d)
    {
        string suit = d.suit.ToString().Substring(0, 1);   // C, D, H, S
        string rank = d.rank switch
        {
            CardRank.Ace => "A",
            CardRank.Jack => "J",
            CardRank.Queen => "Q",
            CardRank.King => "K",
            _ => ((int)d.rank).ToString()
        };
        return suit + rank;
    }
}
