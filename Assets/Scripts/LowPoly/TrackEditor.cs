using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor tratí ve hře (pohled shora):
/// levé tlačítko - vybrat a táhnout bod / přidat nový bod za vybraný, pravé tlačítko - smazat bod,
/// kolečko nebo Q/E - výška vybraného bodu, WASD / prostřední tlačítko - posun pohledu, +/- zoom.
/// Trať se průběžně kontroluje (zatáčky, křížení, stoupání).
/// </summary>
public class TrackEditor : MonoBehaviour
{
    public Func<Color, Material> material;
    public Action<int> onTestDrive;
    public Action onBack;

    private Camera cam;
    private readonly List<Vector3> points = new List<Vector3>();
    private readonly List<Transform> markers = new List<Transform>();
    private LineRenderer line;
    private int selected = -1;
    private bool dragging;
    private int theme;
    private int trackId;
    private string trackName = "MY TRACK";
    private Text status, info, nameText;
    private TrackGenerator.Validation validation;
    private Mesh markerMesh;
    private Material markerMat, selectedMat, startMat, okMat, badMat;
    private Vector3 lastMouse;
    private Canvas canvas;

    public void Open(CustomTracks.Track existing)
    {
        var camGo = new GameObject("EditorCamera");
        camGo.tag = "MainCamera";
        cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 180f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.32f, 0.5f, 0.3f);
        cam.transform.SetPositionAndRotation(new Vector3(100f, 300f, 60f), Quaternion.Euler(90f, 0f, 0f));
        cam.farClipPlane = 1000f;
        camGo.AddComponent<AudioListener>();

        var lineGo = new GameObject("TrackLine");
        line = lineGo.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.widthMultiplier = 14f;
        line.alignment = LineAlignment.View; // ortografická kamera shora -> pás leží "naplocho"
        okMat = material(new Color(0.35f, 0.36f, 0.4f));
        badMat = material(new Color(0.8f, 0.2f, 0.15f));
        markerMat = material(new Color(0.95f, 0.95f, 0.95f));
        selectedMat = material(new Color(1f, 0.78f, 0.18f));
        startMat = material(new Color(0.2f, 0.8f, 0.3f));
        line.sharedMaterial = okMat;

        var mb = new MeshBuilder(1);
        mb.Box(0, Vector3.zero, new Vector3(8f, 2f, 8f));
        markerMesh = mb.ToMesh("Marker");

        if (existing != null)
        {
            trackId = existing.id;
            trackName = existing.name;
            theme = existing.theme;
            points.AddRange(existing.points);
        }
        else
        {
            trackId = UnityEngine.Random.Range(1000, 999999);
            trackName = "MY TRACK " + (CustomTracks.All.Count + 1);
            points.AddRange(CustomTracks.DefaultPoints());
        }
        BuildUI();
        Rebuild();
    }

    private void OnDestroy()
    {
        if (cam != null) Destroy(cam.gameObject);
        if (line != null) Destroy(line.gameObject);
        foreach (var m in markers) if (m != null) Destroy(m.gameObject);
        if (canvas != null) Destroy(canvas.gameObject);
    }

    // ---------------------------------------------------------------- UI

    private void BuildUI()
    {
        canvas = LowPolyUI.CreateCanvas("EditorCanvas");
        Transform root = canvas.transform;
        RectTransform panel = LowPolyUI.Panel(root, "EditorPanel", LowPolyUI.PanelColor);
        LowPolyUI.Place(panel, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(520f, 1030f));

        Text title = LowPolyUI.Label(panel, "TRACK EDITOR", 44, TextAnchor.UpperLeft, Color.white, "Title");
        LowPolyUI.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -18f), new Vector2(480f, 56f));
        nameText = LowPolyUI.Label(panel, trackName, 26, TextAnchor.UpperLeft, LowPolyUI.Accent, "Name", true);
        LowPolyUI.Place(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -76f), new Vector2(480f, 34f));

        Text help = LowPolyUI.Label(panel,
            "LEFT CLICK  SELECT, DRAG OR ADD POINT\nRIGHT CLICK  DELETE POINT\nWHEEL OR Q E  POINT HEIGHT\nWASD  MOVE VIEW     PLUS MINUS  ZOOM\nGREEN POINT  START, DRIVING TOWARDS 2ND",
            18, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.75f), "Help", true);
        LowPolyUI.Place(help.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -124f), new Vector2(480f, 140f));

        Text themeHeader = LowPolyUI.Label(panel, "THEME", 22, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.7f), "ThemeHeader");
        LowPolyUI.Place(themeHeader.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -282f), new Vector2(300f, 30f));
        var group = new LowPolyUI.ChoiceGroup();
        for (int i = 0; i < TrackLibrary.ThemeNames.Length; i++)
        {
            int index = i;
            Image b = LowPolyUI.Button(panel, TrackLibrary.ThemeNames[i], 18, () => { theme = index; group.Select(index); }, out Text label);
            LowPolyUI.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f + (i % 3) * 160f, -318f - (i / 3) * 56f), new Vector2(150f, 48f));
            group.Add(b, label, LowPolyUI.ButtonColor);
        }
        group.Select(theme);

        info = LowPolyUI.Label(panel, "", 22, TextAnchor.UpperLeft, Color.white, "Info", true);
        LowPolyUI.Place(info.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -450f), new Vector2(480f, 120f));
        status = LowPolyUI.Label(panel, "", 28, TextAnchor.UpperLeft, LowPolyUI.Accent, "Status", true);
        LowPolyUI.Place(status.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -580f), new Vector2(480f, 40f));

        string[] labels = { "SAVE", "TEST DRIVE", "RESET SHAPE", "DELETE TRACK", "< BACK" };
        Action[] actions = { () => Save(), TestDrive, ResetShape, DeleteTrack, () => onBack?.Invoke() };
        for (int i = 0; i < labels.Length; i++)
        {
            Image b = LowPolyUI.Button(panel, labels[i], 26, actions[i], out Text label);
            if (i == 1) { b.color = LowPolyUI.Accent; label.color = new Color(0.1f, 0.08f, 0.05f); }
            LowPolyUI.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -650f - i * 72f), new Vector2(472f, 62f));
        }
    }

    // ---------------------------------------------------------------- akce

    private int Save()
    {
        if (!validation.ok)
        {
            status.text = "FIX THE TRACK FIRST";
            return -1;
        }
        int index = CustomTracks.Put(new CustomTracks.Track { id = trackId, name = trackName, theme = theme, points = points.ToArray() });
        Achievements.Unlock("architect");
        status.text = "SAVED";
        // Body se při uložení posunou (start do počátku) - načíst zpět
        points.Clear();
        points.AddRange(CustomTracks.All[index].points);
        Rebuild();
        return index;
    }

    private void TestDrive()
    {
        int index = Save();
        if (index >= 0) onTestDrive?.Invoke(index);
    }

    private void ResetShape()
    {
        points.Clear();
        points.AddRange(CustomTracks.DefaultPoints());
        selected = -1;
        Rebuild();
    }

    private void DeleteTrack()
    {
        CustomTracks.Remove(trackId);
        status.text = "DELETED";
        onBack?.Invoke();
    }

    // ---------------------------------------------------------------- vstup

    private bool OverUI()
    {
        // Levý panel editoru
        return Input.mousePosition.x < 560f * Screen.height / 1080f;
    }

    private Vector3 MouseOnGround()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        var plane = new Plane(Vector3.up, Vector3.zero);
        return plane.Raycast(ray, out float d) ? ray.GetPoint(d) : Vector3.zero;
    }

    private int PointUnderMouse()
    {
        Vector3 m = MouseOnGround();
        float pick = cam.orthographicSize * 0.06f;
        int best = -1;
        float bestD = pick * pick;
        for (int i = 0; i < points.Count; i++)
        {
            float d = (new Vector2(points[i].x, points[i].z) - new Vector2(m.x, m.z)).sqrMagnitude;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    private void Update()
    {
        if (cam == null) return;
        bool changed = false;

        // Pohled
        Vector3 move = new Vector3((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0), 0f,
            (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
        cam.transform.position += move * cam.orthographicSize * Time.unscaledDeltaTime;
        if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus)) cam.orthographicSize = Mathf.Max(40f, cam.orthographicSize - 120f * Time.unscaledDeltaTime);
        if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus)) cam.orthographicSize = Mathf.Min(400f, cam.orthographicSize + 120f * Time.unscaledDeltaTime);
        if (Input.GetMouseButton(2))
        {
            Vector3 delta = Input.mousePosition - lastMouse;
            float scale = cam.orthographicSize * 2f / Screen.height;
            cam.transform.position -= new Vector3(delta.x, 0f, delta.y) * scale;
        }
        lastMouse = Input.mousePosition;

        if (!OverUI())
        {
            if (Input.GetMouseButtonDown(0))
            {
                int hit = PointUnderMouse();
                if (hit >= 0) { selected = hit; dragging = true; }
                else
                {
                    // Nový bod za vybraný (nebo na konec)
                    Vector3 p = MouseOnGround();
                    int insert = selected >= 0 ? selected + 1 : points.Count;
                    float y = selected >= 0 ? points[selected].y : 0f;
                    points.Insert(insert, new Vector3(p.x, y, p.z));
                    selected = insert;
                    dragging = true;
                    changed = true;
                }
            }
            if (Input.GetMouseButtonDown(1))
            {
                int hit = PointUnderMouse();
                if (hit >= 0 && points.Count > 4)
                {
                    points.RemoveAt(hit);
                    selected = -1;
                    changed = true;
                }
            }
            float wheel = Input.mouseScrollDelta.y;
            if (selected >= 0 && Mathf.Abs(wheel) > 0.01f)
            {
                points[selected] += Vector3.up * Mathf.Sign(wheel) * 0.5f;
                changed = true;
            }
            else if (selected < 0 && Mathf.Abs(wheel) > 0.01f)
                cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - wheel * 15f, 40f, 400f);
        }
        if (selected >= 0 && (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.E)))
        {
            points[selected] += Vector3.up * (Input.GetKeyDown(KeyCode.E) ? 0.5f : -0.5f);
            changed = true;
        }
        if (dragging && selected >= 0 && Input.GetMouseButton(0))
        {
            Vector3 p = MouseOnGround();
            Vector3 old = points[selected];
            if ((new Vector2(p.x, p.z) - new Vector2(old.x, old.z)).sqrMagnitude > 0.25f)
            {
                points[selected] = new Vector3(p.x, old.y, p.z);
                changed = true;
            }
        }
        if (Input.GetMouseButtonUp(0)) dragging = false;
        if (Input.GetKeyDown(KeyCode.Delete) && selected >= 0 && points.Count > 4)
        {
            points.RemoveAt(selected);
            selected = -1;
            changed = true;
        }

        if (changed) Rebuild();
        else UpdateMarkers();
    }

    // ---------------------------------------------------------------- náhled

    private TrackGenerator.TrackSettings Settings()
    {
        return new TrackGenerator.TrackSettings { controlPoints = points.ToArray(), heightScale = 1f, roadWidth = 14f };
    }

    private void Rebuild()
    {
        TrackGenerator.TrackSettings s = Settings();
        Vector3[] path = TrackGenerator.SamplePath(s);
        line.positionCount = path.Length;
        for (int i = 0; i < path.Length; i++) path[i].y = 0.5f;
        line.SetPositions(path);

        validation = TrackGenerator.Validate(s);
        line.sharedMaterial = validation.ok ? okMat : badMat;
        status.text = validation.message;
        status.color = validation.ok ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.4f, 0.35f);
        info.text = $"LENGTH  {validation.length / 1000f:0.00} KM\nTIGHTEST CORNER  {validation.minRadius:0} M\n" +
                    $"CLOSEST PASS  {validation.minGap:0} M\nSTEEPEST  {validation.maxSlope * 100f:0} %";
        info.text = info.text.Replace(',', '.');

        while (markers.Count < points.Count)
        {
            var go = new GameObject("Marker");
            go.AddComponent<MeshFilter>().sharedMesh = markerMesh;
            go.AddComponent<MeshRenderer>();
            markers.Add(go.transform);
        }
        while (markers.Count > points.Count)
        {
            Destroy(markers[markers.Count - 1].gameObject);
            markers.RemoveAt(markers.Count - 1);
        }
        UpdateMarkers();
    }

    private void UpdateMarkers()
    {
        float scale = cam.orthographicSize / 180f;
        for (int i = 0; i < markers.Count; i++)
        {
            markers[i].position = new Vector3(points[i].x, 2f, points[i].z);
            markers[i].localScale = Vector3.one * scale * (1f + points[i].y * 0.05f);
            markers[i].GetComponent<MeshRenderer>().sharedMaterial = i == selected ? selectedMat : i == 0 ? startMat : markerMat;
        }
    }
}
