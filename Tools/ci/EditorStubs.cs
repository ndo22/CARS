namespace UnityEditor {
  public class MenuItem : System.Attribute { public MenuItem(string p){} }
  public static class Selection { public static UnityEngine.GameObject activeGameObject; }
  public class SceneView { public static SceneView lastActiveSceneView; public bool FrameSelected(){ return true; } }
}
