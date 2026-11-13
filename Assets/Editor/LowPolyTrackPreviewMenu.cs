using UnityEditor;
using UnityEngine;

/// <summary>
/// Menu "CARS > Low Poly" v editoru: postaví vybranou trať přímo do otevřené scény, aby byla vidět bez spuštění hry.
/// Náhled se do scény neukládá (HideFlags.DontSave) a při spuštění hry se sám smaže.
/// </summary>
public static class LowPolyTrackPreviewMenu
{
    [MenuItem("CARS/Low Poly/Preview Green Valley")] private static void P0() => Preview(0, false, false);
    [MenuItem("CARS/Low Poly/Preview Desert Canyon")] private static void P1() => Preview(1, false, false);
    [MenuItem("CARS/Low Poly/Preview Alpine Pass")] private static void P2() => Preview(2, false, false);
    [MenuItem("CARS/Low Poly/Preview Sunset Coast")] private static void P3() => Preview(3, false, false);
    [MenuItem("CARS/Low Poly/Preview Neon City")] private static void P4() => Preview(4, true, false);
    [MenuItem("CARS/Low Poly/Preview Green Valley (Night Rain)")] private static void P5() => Preview(0, true, true);

    [MenuItem("CARS/Low Poly/Clear Preview")]
    private static void Clear()
    {
        var old = GameObject.Find(LowPolyRaceBootstrap.EditorPreviewName);
        if (old != null) Object.DestroyImmediate(old);
    }

    private static void Preview(int index, bool night, bool rain)
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Náhled tratě funguje jen mimo Play mode.");
            return;
        }
        var bootstrap = Object.FindObjectOfType<LowPolyRaceBootstrap>();
        if (bootstrap == null)
        {
            Debug.LogWarning("Otevři scénu LowPolyTrack (chybí objekt s LowPolyRaceBootstrap).");
            return;
        }

        GameObject root = bootstrap.BuildEditorPreview(index, night, rain);
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.hideFlags = HideFlags.DontSave;
        Selection.activeGameObject = root;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.FrameSelected();
    }
}
