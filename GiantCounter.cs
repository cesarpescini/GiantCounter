using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Cache;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using SharpDX;
using Color = SharpDX.Color;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;


namespace GiantCounter 
{
public class GiantCounterSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(true);
    public HotkeyNode SnapshotHotkey { get; set; } = new HotkeyNode(Keys.F8);
    public HotkeyNode ClearSnapshotsHotkey { get; set; } = new HotkeyNode(Keys.F7);
    public ColorNode CounterColor { get; set; } = new ColorNode(new Color(255, 255, 0, 255));
    public RangeNode<int> CounterFontSize { get; set; } = new RangeNode<int>(32, 16, 96);
    public RangeNode<float> OverlayX { get; set; } = new RangeNode<float>(0.5f, 0f, 1f); // relative to screen width
    public RangeNode<float> OverlayY { get; set; } = new RangeNode<float>(0.16f, 0f, 1f); // relative to screen height
    public ColorNode RitualRuneCircleColor { get; set; } = new ColorNode(new Color(255, 0, 255, 128)); // Default: semi-transparent magenta
    public RangeNode<int> RitualRuneCircleSize { get; set; } = new RangeNode<int>(1000, 750, 1500);
    public ToggleNode CountExilesNearRitual { get; set; } = new ToggleNode(false); // Default: count all exiles
    public RangeNode<int> RitualRange { get; set; } = new RangeNode<int>(1000, 500, 2000); // Default: 1000, matches circle
}

public class GiantCounter : BaseSettingsPlugin<GiantCounterSettings>
{
    private List<Entity> _exiles = new();
    private List<int> _snapshots = new();
    private int _lastAreaHash;
    private int _snapshotCount = 0;
    private string _statusText = "";
    private Color _statusColor = new Color(255, 255, 0, 255); // Yellow
    private Color _statusBgColor = new Color(0, 0, 0, 180); // Semi-transparent black
    private float hue = 0.0f;
    private float huePerFrame = 360.0f / (144.0f * 8.0f); // 4-second cycle at 144 FPS
    private Color background = new Color(0, 0, 0, 125);
    private Color rainbowColor;
    private string statusText;
    private bool _dragging = false;
    private Vector2 _dragOffset;
    private System.Drawing.Rectangle screenBounds;

    public override void OnLoad()
    {
        Name = "GiantCounter";
    }

    public override void AreaChange(AreaInstance area)
    {
        _exiles.Clear();
        _lastAreaHash = area.GetHashCode();
    }

    public override void Render()
    {
        if (!Settings.Enable) return;
        screenBounds = Screen.PrimaryScreen.Bounds;
        hue = (Core.FramesCount * huePerFrame) % 360.0f;
        rainbowColor = Util.HslToRgb(hue);

        if (Settings.SnapshotHotkey.PressedOnce())
        {
            TakeSnapshot();
        }
        if (Settings.ClearSnapshotsHotkey.PressedOnce())
        {
            _snapshots.Clear();
            _snapshotCount = 0;
        }
        SnapshotExiles();
        DrawStatusAndSnapshots();
        DrawClearSnapshotsButton();
        DrawGiantCounters();
        DrawRitualRuneCircles();
    }

    private void TakeSnapshot()
    {
        _snapshotCount++;
        _snapshots.Add(_exiles.Count);
    }

    private void SnapshotExiles()
    {
        var allExiles = GameController.Entities
            .Where(e => e.IsValid && e.Path.StartsWith("Metadata/Monsters/Exiles/") &&
                        e.TryGetComponent<Positioned>(out var pos) &&
                        pos.Scale == 1.8f)
            .ToList();
        if (Settings.CountExilesNearRitual.Value)
        {
            // Find all ritual altars
            var ritualRunes = GameController.Entities
                .Where(e => e.IsValid && e.Path == "Metadata/Terrain/Leagues/Ritual/RitualRuneInteractable" && e.TryGetComponent<Positioned>(out _))
                .ToList();
            var runePositions = ritualRunes
                .Select(r => {
                    r.TryGetComponent<Positioned>(out var pos); return GameController.IngameState.Data.ToWorldWithTerrainHeight(pos.GridPosition);
                })
                .ToList();
            int ritualRange = Settings.RitualRange.Value;
            _exiles = allExiles
                .Where(exile =>
                    exile.TryGetComponent<Positioned>(out var pos) &&
                    runePositions.Any(runePos => Vector3.Distance(GameController.IngameState.Data.ToWorldWithTerrainHeight(pos.GridPosition), runePos) <= ritualRange)
                )
                .ToList();
        }
        else
        {
            _exiles = allExiles;
        }
        _statusText = Settings.CountExilesNearRitual.Value
            ? $"Giants In Range: {_exiles.Count}"
            : $"Giants Visible: {_exiles.Count}";
    }

    private void DrawStatusAndSnapshots()
    {
        var screenSize = GameController.Window.GetWindowRectangleTimeCache;
        var overlayX = Settings.OverlayX.Value * screenSize.Width;
        var overlayY = Settings.OverlayY.Value * screenSize.Height;
        var center = new Vector2(overlayX, overlayY);
        var lines = new List<string> { _statusText };
        int i = 1;
        foreach (var count in _snapshots)
        {
            lines.Add($"Snapshot {i}: {count} Giants");
            i++;
        }
        int fontSize = 24;
        float scale = 1.5f;
        var lineSizes = lines.Select(line => Graphics.MeasureText(line, fontSize) * scale).ToList();
        float maxWidth = lineSizes.Max(s => s.X);
        float totalHeight = lineSizes.Sum(s => s.Y) + (lines.Count - 1) * 8;
        float y = center.Y - totalHeight / 2;
        for (int idx = 0; idx < lines.Count; idx++)
        {
            var line = lines[idx];
            var size = lineSizes[idx];
            var textPos = new Vector2(center.X, y + size.Y / 2 + 2);
            using (Graphics.SetTextScale(scale))
            {
                // Outline
                Graphics.DrawText(line, textPos + new Vector2(-1, -1), Color.Black, FontAlign.Center);
                Graphics.DrawText(line, textPos + new Vector2(-1, 1), Color.Black, FontAlign.Center);
                Graphics.DrawText(line, textPos + new Vector2(1, -1), Color.Black, FontAlign.Center);
                Graphics.DrawText(line, textPos + new Vector2(1, 1), Color.Black, FontAlign.Center);
                // Main text
                Graphics.DrawTextWithBackground(line, textPos, rainbowColor, FontAlign.Center, background);
            }
            y += size.Y + 8;
        }
    }

    private void DrawClearSnapshotsButton()
    {
        var screenSize = GameController.Window.GetWindowRectangleTimeCache;
        var overlayX = Settings.OverlayX.Value * screenSize.Width;
        var overlayY = Settings.OverlayY.Value * screenSize.Height;
        var pos = new Vector2(overlayX - 60, overlayY + 80 + _snapshots.Count * 32 + 10);
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(pos.X, pos.Y), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.6f);
        ImGui.Begin("Clear Snapshots", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDecoration);
        if (ImGui.Button("Clear Snapshots"))
        {
            _snapshots.Clear();
            _snapshotCount = 0;
        }
        ImGui.SameLine();
        ImGui.Text($"(Hotkey: {Settings.ClearSnapshotsHotkey.Value})");
        ImGui.End();
    }

    private void DrawGiantCounters()
    {
        int counter = 1;
        foreach (var exile in _exiles)
        {
            if (!exile.IsValid) continue;
            if (!exile.TryGetComponent<Positioned>(out var pos)) continue;
            var worldPos = GameController.IngameState.Data.ToWorldWithTerrainHeight(pos.GridPosition);
            var screenPos = GameController.IngameState.Camera.WorldToScreen(worldPos);
            using (Graphics.SetTextScale(Settings.CounterFontSize.Value / 16f))
            {
                // Outline
                Graphics.DrawText(counter.ToString(), screenPos + new Vector2(-2, -2), Color.Black, FontAlign.Center);
                Graphics.DrawText(counter.ToString(), screenPos + new Vector2(-2, 2), Color.Black, FontAlign.Center);
                Graphics.DrawText(counter.ToString(), screenPos + new Vector2(2, -2), Color.Black, FontAlign.Center);
                Graphics.DrawText(counter.ToString(), screenPos + new Vector2(2, 2), Color.Black, FontAlign.Center);
                // Main text
                Graphics.DrawTextWithBackground(counter.ToString(), screenPos, Settings.CounterColor.Value, FontAlign.Center, background);
            }
            counter++;
        }
    }

    private void DrawFilledCircleInWorldPosition(Vector3 position, float radius, Color color)
    {
        var circlePoints = new List<Vector2>();
        const int segments = 32;
        const float segmentAngle = 2f * MathF.PI / segments;

        for (var i = 0; i < segments; i++)
        {
            var angle = i * segmentAngle;
            var currentOffset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            var nextOffset = new Vector2(MathF.Cos(angle + segmentAngle), MathF.Sin(angle + segmentAngle)) * radius;

            var currentWorldPos = position + new Vector3(currentOffset, 0);
            var nextWorldPos = position + new Vector3(nextOffset, 0);

            circlePoints.Add(GameController.IngameState.Camera.WorldToScreen(currentWorldPos));
            circlePoints.Add(GameController.IngameState.Camera.WorldToScreen(nextWorldPos));
        }

        Graphics.DrawConvexPolyFilled(circlePoints.ToArray(), color);
        Graphics.DrawPolyLine(circlePoints.ToArray(), color, 2);
    }

    private void DrawRitualRuneCircles()
    {
        var ritualRunes = GameController.Entities
            .Where(e => e.IsValid && e.Path == "Metadata/Terrain/Leagues/Ritual/RitualRuneInteractable")
            .ToList();
        foreach (var rune in ritualRunes)
        {
            if (!rune.TryGetComponent<Positioned>(out var pos)) continue;
            var worldPos = GameController.IngameState.Data.ToWorldWithTerrainHeight(pos.GridPosition);
            float radius = Settings.RitualRuneCircleSize.Value;
            var color = Settings.RitualRuneCircleColor.Value;
            DrawFilledCircleInWorldPosition(worldPos, radius, color);
        }
    }

    public override void EntityAdded(Entity entity) { }
    public override void EntityRemoved(Entity entity) { }
} 
}