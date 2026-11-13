using UnityEngine;

/// <summary>
/// Ladicí okno (F1): živé hodnoty auta a posuvníky pro sílu motoru, přilnavost, zatáčení, přítlak a obtížnost AI.
/// Hodnoty platí jen pro aktuální jízdu - dobré nastavení pak stačí přepsat do LowPolyCarFactory.Stats().
/// </summary>
public class TuningOverlay : MonoBehaviour
{
    public CarController car;
    private bool visible;
    private Rect window = new Rect(20, 300, 330, 430);

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) visible = !visible;
    }

    private void OnGUI()
    {
        if (!visible || car == null) return;
        window = GUI.Window(4242, window, Draw, "TUNING (F1)");
    }

    private void Draw(int id)
    {
        GUILayout.Label($"Speed: {car.SpeedKmh:0} km/h   Nitro: {car.Nitro * 100f:0}%");
        GUILayout.Label($"Drift: {car.DriftAmount:0.00}   Surface drag: {car.SurfaceDrag:0.00}");
        if (car.wheelColliderLeftBack.GetGroundHit(out WheelHit hit))
            GUILayout.Label($"Rear slip  side {hit.sidewaysSlip:0.00}  fwd {hit.forwardSlip:0.00}");

        car.motorTorque = Slider("Motor torque", car.motorTorque, 100f, 800f);
        car.maxSpeedKmh = Slider("Top speed", car.maxSpeedKmh, 100f, 320f);
        car.maxSteer = Slider("Max steer", car.maxSteer, 10f, 45f);
        car.gripMultiplier = Slider("Grip", car.gripMultiplier, 0.4f, 1.8f);
        car.driftRearGrip = Slider("Drift rear grip", car.driftRearGrip, 0.2f, 1f);
        car.downforce = Slider("Downforce", car.downforce, 0f, 150f);
        car.antiRoll = Slider("Anti roll", car.antiRoll, 0f, 10000f);

        float skill = 1f;
        var ais = FindObjectsOfType<AIPlayer>();
        if (ais.Length > 0) skill = ais[0].skill;
        float newSkill = Slider("AI skill", skill, 0.5f, 1.3f);
        if (!Mathf.Approximately(newSkill, skill))
            foreach (var ai in ais) ai.skill = newSkill;

        GUI.DragWindow();
    }

    private static float Slider(string label, float value, float min, float max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(110));
        value = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(140));
        GUILayout.Label(value.ToString("0.00"), GUILayout.Width(50));
        GUILayout.EndHorizontal();
        return value;
    }
}
