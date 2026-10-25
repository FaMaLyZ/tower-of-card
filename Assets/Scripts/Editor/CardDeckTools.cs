using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// Editor helpers for building a standard 52-card deck.  Menu:  CardTower > ...
public static class CardDeckTools
{
    const string RootFolder = "Assets/CardData";
    const string DeckFolder = "Assets/CardData/Standard52";

    static readonly string[] RankNames =
        { "", "Ace", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Jack", "Queen", "King" };

    // ------------------------------------------------------------------
    //  1. Create the 52 CardData assets
    // ------------------------------------------------------------------
    [MenuItem("CardTower/1. Create Standard 52 CardData")]
    static void CreateStandard52()
    {
        EnsureFolder(RootFolder, "Assets", "CardData");
        EnsureFolder(DeckFolder, RootFolder, "Standard52");

        int created = 0;
        foreach (CardSuit suit in Enum.GetValues(typeof(CardSuit)))
        {
            for (int rank = 1; rank <= 13; rank++)
            {
                string path = DeckFolder + "/" + RankNames[rank] + "Of" + suit + ".asset";
                if (AssetDatabase.LoadAssetAtPath<CardData>(path) != null) continue;

                var data = ScriptableObject.CreateInstance<CardData>();
                data.cardName = RankNames[rank] + " of " + suit;
                data.suit = suit;
                data.rank = (CardRank)rank;
                data.tint = (suit == CardSuit.Hearts || suit == CardSuit.Diamonds)
                    ? new Color(1f, 0.6f, 0.6f) : new Color(0.75f, 0.75f, 0.85f);

                AssetDatabase.CreateAsset(data, path);
                created++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("CardTower: created " + created + " CardData assets in " + DeckFolder);
    }

    // ------------------------------------------------------------------
    //  2. Assign the 3D models by matching their names
    // ------------------------------------------------------------------
    [MenuItem("CardTower/2. Assign Models From Selected Folder")]
    static void AssignModels()
    {
        string folder = GetSelectedFolder();
        if (folder == null)
        {
            EditorUtility.DisplayDialog("CardTower",
                "Select the Project FOLDER that contains your 52 card models first.", "OK");
            return;
        }

        var cards = LoadAllCardData();
        if (cards.Count == 0)
        {
            EditorUtility.DisplayDialog("CardTower", "No CardData found. Run step 1 first.", "OK");
            return;
        }

        int assigned = 0;
        var unmatched = new List<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;

            if (!TryParseCardName(go.name, out CardSuit suit, out int rank))
            {
                unmatched.Add(go.name);
                continue;
            }

            CardData card = cards.Find(c => c.suit == suit && (int)c.rank == rank);
            if (card == null) { unmatched.Add(go.name); continue; }

            card.model = go;
            EditorUtility.SetDirty(card);
            assigned++;
        }

        AssetDatabase.SaveAssets();

        string message = "Assigned " + assigned + " models.";
        if (unmatched.Count > 0)
            message += "\nCould not match " + unmatched.Count + " (assign these by hand):\n" + string.Join(", ", unmatched);
        Debug.Log("CardTower: " + message);
        EditorUtility.DisplayDialog("CardTower", message, "OK");
    }

    // ------------------------------------------------------------------
    //  3. Put every CardData into the selected DeckManager
    // ------------------------------------------------------------------
    [MenuItem("CardTower/3. Fill Selected DeckManager With All CardData")]
    static void FillDeckManager()
    {
        DeckManager deck = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<DeckManager>() : null;

        if (deck == null)
        {
            EditorUtility.DisplayDialog("CardTower",
                "Select the GameObject that has the DeckManager in the Hierarchy first.", "OK");
            return;
        }

        var cards = LoadAllCardData();
        cards.Sort((a, b) => a.suit != b.suit ? a.suit.CompareTo(b.suit) : a.rank.CompareTo(b.rank));

        var so = new SerializedObject(deck);
        var prop = so.FindProperty("cardSet");
        prop.arraySize = cards.Count;
        for (int i = 0; i < cards.Count; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
        so.ApplyModifiedProperties();

        Debug.Log("CardTower: DeckManager Card Set now holds " + cards.Count + " cards.");
    }

    // ------------------------------------------------------------------
    //  Helpers
    // ------------------------------------------------------------------

    static List<CardData> LoadAllCardData()
    {
        var list = new List<CardData>();
        foreach (string guid in AssetDatabase.FindAssets("t:CardData"))
        {
            var card = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
            if (card != null) list.Add(card);
        }
        return list;
    }

    static string GetSelectedFolder()
    {
        foreach (string guid in Selection.assetGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(path)) return path;
        }
        return null;
    }

    static void EnsureFolder(string path, string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
    }

    // Reads names like "AceOfSpades", "Queen_of_Hearts", "Heart10", "10H", "KS", "spades_7" ...
    static bool TryParseCardName(string raw, out CardSuit suit, out int rank)
    {
        suit = CardSuit.Clubs;
        rank = 0;

        string spaced = Regex.Replace(raw, "([a-z])([A-Z])", "$1 $2");        // camelCase
        spaced = Regex.Replace(spaced, "([A-Za-z])([0-9])", "$1 $2");          // letters|digits
        spaced = Regex.Replace(spaced, "([0-9])([A-Za-z])", "$1 $2");
        string[] tokens = Regex.Split(spaced.ToLowerInvariant(), "[^a-z0-9]+");

        bool haveSuit = false, haveRank = false;

        // pass 1: whole words and numbers
        foreach (string t in tokens)
        {
            if (!haveSuit && SuitFromWord(t, out CardSuit s)) { suit = s; haveSuit = true; continue; }
            if (!haveRank && RankFromWord(t, out int r)) { rank = r; haveRank = true; }
        }

        // pass 2: short forms such as "ah", "ks", or single letters "h", "k"
        foreach (string t in tokens)
        {
            if (t.Length == 2 && "ajqk".IndexOf(t[0]) >= 0 && "cdhs".IndexOf(t[1]) >= 0)
            {
                if (!haveRank) { rank = RankFromLetter(t[0]); haveRank = true; }
                if (!haveSuit) { suit = SuitFromLetter(t[1]); haveSuit = true; }
            }
            else if (t.Length == 1)
            {
                if (!haveSuit && "cdhs".IndexOf(t[0]) >= 0) { suit = SuitFromLetter(t[0]); haveSuit = true; }
                else if (!haveRank && "ajqk".IndexOf(t[0]) >= 0) { rank = RankFromLetter(t[0]); haveRank = true; }
            }
        }

        return haveSuit && haveRank;
    }

    static bool SuitFromWord(string t, out CardSuit suit)
    {
        suit = CardSuit.Clubs;
        switch (t)
        {
            case "club": case "clubs": suit = CardSuit.Clubs; return true;
            case "diamond": case "diamonds": suit = CardSuit.Diamonds; return true;
            case "heart": case "hearts": suit = CardSuit.Hearts; return true;
            case "spade": case "spades": suit = CardSuit.Spades; return true;
        }
        return false;
    }

    static bool RankFromWord(string t, out int rank)
    {
        rank = 0;
        switch (t)
        {
            case "ace": rank = 1; return true;
            case "two": rank = 2; return true;
            case "three": rank = 3; return true;
            case "four": rank = 4; return true;
            case "five": rank = 5; return true;
            case "six": rank = 6; return true;
            case "seven": rank = 7; return true;
            case "eight": rank = 8; return true;
            case "nine": rank = 9; return true;
            case "ten": rank = 10; return true;
            case "jack": rank = 10; return true;
            case "queen": rank = 10; return true;
            case "king": rank = 10; return true;
        }

        if (int.TryParse(t, out int n) && n >= 1 && n <= 14)
        {
            rank = n == 14 ? 1 : n;
            return true;
        }
        return false;
    }

    static int RankFromLetter(char c)
    {
        switch (c) { case 'a': return 1; case 'j': return 11; case 'q': return 12; default: return 13; }
    }

    static CardSuit SuitFromLetter(char c)
    {
        switch (c) { case 'c': return CardSuit.Clubs; case 'd': return CardSuit.Diamonds; case 'h': return CardSuit.Hearts; default: return CardSuit.Spades; }
    }
}