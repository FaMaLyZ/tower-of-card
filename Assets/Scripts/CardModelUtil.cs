using UnityEngine;

/// Helpers for fitting any 3D card model (any size, pivot or orientation) into the game.
public static class CardModelUtil
{
    /// Bounds of all meshes under "root", measured in root's own local space.
    public static Bounds GetLocalBounds(Transform root)
    {
        bool has = false;
        var bounds = new Bounds(Vector3.zero, Vector3.zero);

        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Encapsulate(ref bounds, ref has, mf.sharedMesh.bounds,
                        root.worldToLocalMatrix * mf.transform.localToWorldMatrix);
        }

        foreach (var sm in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (sm.sharedMesh == null) continue;
            Encapsulate(ref bounds, ref has, sm.sharedMesh.bounds,
                        root.worldToLocalMatrix * sm.transform.localToWorldMatrix);
        }

        return bounds;
    }

    static void Encapsulate(ref Bounds bounds, ref bool has, Bounds meshBounds, Matrix4x4 toRoot)
    {
        Vector3 c = meshBounds.center, e = meshBounds.extents;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x,
                                             (i & 2) == 0 ? -e.y : e.y,
                                             (i & 4) == 0 ? -e.z : e.z);
            Vector3 p = toRoot.MultiplyPoint3x4(corner);

            if (!has) { bounds = new Bounds(p, Vector3.zero); has = true; }
            else bounds.Encapsulate(p);
        }
    }

    /// Finds the rotation that stands a card model upright, whatever way it was exported:
    ///   longest side  -> Y (the length, card stands on this)
    ///   middle side   -> Z (the width)
    ///   thinnest side -> X (the thickness)
    public static Quaternion GetOrientation(Vector3 size)
    {
        int longest = 0, shortest = 0;
        for (int i = 1; i < 3; i++)
        {
            if (size[i] > size[longest]) longest = i;
            if (size[i] < size[shortest]) shortest = i;
        }
        if (longest == shortest) return Quaternion.identity;   // perfect cube, nothing to detect

        int middle = 3 - longest - shortest;

        // where each model axis (0 = x, 1 = y, 2 = z) should point
        var target = new Vector3[3];
        target[shortest] = Vector3.right;
        target[longest] = Vector3.up;
        target[middle] = Vector3.forward;

        // a mirrored mapping is not a rotation - flip the width direction to fix it
        if (Vector3.Dot(target[0], Vector3.Cross(target[1], target[2])) < 0f)
            target[middle] = -target[middle];

        return Quaternion.LookRotation(target[2], target[1]);
    }
}