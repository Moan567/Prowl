// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.
//
// One button: Open Current Map In TrenchBroom. One button: Reimport Current Map.
// No manual workflow.

using System.IO;

using Prowl.Editor.Core;
using Prowl.Editor.GUI.SceneView;
using Prowl.Editor.Projects;
using Prowl.Editor.Relic;
using Prowl.Editor.Theming;
using Prowl.OrigamiUI;
using Prowl.PaperUI;
using Prowl.PaperUI.LayoutEngine;
using Prowl.Runtime;
using Prowl.Runtime.Relic;
using Prowl.Runtime.Relic.Map;
using Prowl.Runtime.Resources;

using static Prowl.Editor.GUI.EditorGUI;

namespace Prowl.Editor.GUI.Panels.Relic;

public class RelicTrenchBroomPanel : DockPanel
{
    [MenuItem("Window/Relic/TrenchBroom Integration", priority: 51)]
    static void Open() => EditorApplication.Instance?.OpenPanel(typeof(RelicTrenchBroomPanel));

    public override string Title => "TrenchBroom";
    public override string Icon => EditorIcons.Cube;

    private static string _status = string.Empty;

    public override void OnGUI(Paper paper, float width, float height)
    {
        var font = EditorTheme.DefaultFont;
        if (font == null) return;

        string? root = null;
        try { root = Project.Current?.RootPath; } catch { }
        string? exe = null;
        try { exe = RelicTrenchBroom.FindExecutable(root); } catch { }
        string map = RelicTrenchBroom.CurrentMapPath ?? string.Empty;
        string? fgd = null;
        string? gamePath = null;
        string? texDir = null;
        string? mapsDir = null;
        try
        {
            fgd = RelicTrenchBroom.FindFgd(root);
            gamePath = RelicTrenchBroom.FindGamePath(root);
            texDir = RelicTrenchBroom.FindTextureDir(root);
            mapsDir = RelicTrenchBroom.FindMapsDir(root);
        }
        catch { }

        using (paper.Column("tb_root").Width(width).Height(height).Padding(0, 0, 8, 12).Gap(8).Enter())
        {
            SectionHeader(paper, "tb_h", "TrenchBroom Integration", first: true);

            StatusRow(paper, "tb_exe", "TrenchBroom", string.IsNullOrEmpty(exe) ? "NOT FOUND" : exe, font,
                string.IsNullOrEmpty(exe) ? EditorTheme.Red400 : EditorTheme.Green400);
            StatusRow(paper, "tb_game", "Game Path", string.IsNullOrEmpty(gamePath) ? "not found (point TB here)" : gamePath, font,
                string.IsNullOrEmpty(gamePath) ? EditorTheme.Amber400 : EditorTheme.Green400);
            StatusRow(paper, "tb_map", "Current Map", string.IsNullOrEmpty(map) ? "(none — import a .map first)" : map, font, EditorTheme.Ink400);
            StatusRow(paper, "tb_fgd", "Relic.fgd", string.IsNullOrEmpty(fgd) ? "not found" : fgd, font,
                string.IsNullOrEmpty(fgd) ? EditorTheme.Amber400 : EditorTheme.Green400);
            StatusRow(paper, "tb_tex", "Textures", string.IsNullOrEmpty(texDir) ? "not found" : texDir, font,
                string.IsNullOrEmpty(texDir) || !Directory.Exists(texDir) ? EditorTheme.Amber400 : EditorTheme.Green400);
            StatusRow(paper, "tb_maps", "Maps", string.IsNullOrEmpty(mapsDir) ? "not found" : mapsDir, font,
                string.IsNullOrEmpty(mapsDir) || !Directory.Exists(mapsDir) ? EditorTheme.Amber400 : EditorTheme.Green400);

            using (paper.Row("tb_row").Height(32).Gap(8).Enter())
            {
                CtaButton(paper, "tb_open", $"{EditorIcons.LocationDot}  Open Current Map In TrenchBroom", EditorTheme.Accent,
                    () => OpenCurrent(exe), grow: true);
            }
            using (paper.Row("tb_row2").Height(32).Gap(8).Enter())
            {
                Origami.Button(paper, "tb_reimport", $"{EditorIcons.ArrowsRotate}  Reimport Current Map", () => Reimport()).Show();
            }
            SettingsRow(paper, "tb_auto", "Auto-reload on save", () =>
                Origami.Checkbox(paper, "tb_auto_v", RelicMapAutoReload.Enabled, v => RelicMapAutoReload.Enabled = v).Show());
            {
                string watchText = RelicMapAutoReload.IsWatching
                    ? $"Watching — {RelicMapAutoReload.Status}"
                    : string.IsNullOrEmpty(RelicMapAutoReload.Status)
                        ? "Auto-reload idle — import a .map first."
                        : RelicMapAutoReload.Status;
                var f2 = font;
                paper.Box("tb_autoreload").Height(UnitValue.Auto)
                    .Text(watchText, f2).TextColor(EditorTheme.Ink300)
                    .FontSize(EditorTheme.FontSizeSmall).Alignment(TextAlignment.MiddleLeft);
            }
            using (paper.Row("tb_row3").Height(32).Gap(8).Enter())
            {
                Origami.Button(paper, "tb_ftex", $"{EditorIcons.FolderOpen}  Textures", () => RevealDir(texDir)).Show();
                Origami.Button(paper, "tb_fmaps", $"{EditorIcons.FolderOpen}  Maps", () => RevealDir(mapsDir)).Show();
                Origami.Button(paper, "tb_fgame", $"{EditorIcons.FolderOpen}  Game Path", () => RevealDir(gamePath)).Show();
            }

            if (!string.IsNullOrEmpty(_status))
                paper.Box("tb_status").Height(UnitValue.Auto)
                    .Text(_status, font).TextColor(EditorTheme.Ink400)
                    .FontSize(EditorTheme.FontSizeSmall).Alignment(TextAlignment.MiddleLeft);

            SectionHeader(paper, "tb_setup_h", "Setup (once)");
            HelpText(paper, "tb_s1", "1. TrenchBroom.exe is auto-detected next to the repo, or set TRENCHBROOM_PATH.", font);
            HelpText(paper, "tb_s2", "2. In TrenchBroom: New Map > Relic, game path = Game Path above (has Assets/Textures + Relic.fgd).", font);
            HelpText(paper, "tb_s3", "3. Drop PNGs into Textures — face names match file names (brick -> brick.png). Missing ones list in the Map Importer.", font);
            HelpText(paper, "tb_s4", "4. Map with 1 unit = 1 inch, Z-up. Import via Window > Relic > Map Importer, then Play.", font);
        }
    }

    private static void RevealDir(string? dir)
    {
        if (string.IsNullOrEmpty(dir))
        {
            _status = "Folder not found for this project.";
            Toasts.Warning("Relic", _status);
            return;
        }
        try
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute = true });
        }
        catch (System.Exception ex) { _status = $"Could not open folder: {ex.Message}"; }
    }

    private static void StatusRow(Paper paper, string id, string label, string value, Scribe.FontFile font, System.Drawing.Color color)
    {
        SettingsRow(paper, id, label, () =>
            paper.Box(id + "_v").Height(20)
                .Text(value, font).TextColor(color)
                .FontSize(EditorTheme.FontSizeSmall).Alignment(TextAlignment.MiddleRight).TextTruncate());
    }

    private static void HelpText(Paper paper, string id, string text, Scribe.FontFile font)
    {
        paper.Box(id).Height(UnitValue.Auto)
            .Text(text, font).TextColor(EditorTheme.Ink300)
            .FontSize(EditorTheme.FontSizeSmall).Alignment(TextAlignment.MiddleLeft);
    }

    private static void OpenCurrent(string? exe)
    {
        if (string.IsNullOrEmpty(RelicTrenchBroom.CurrentMapPath) || !File.Exists(RelicTrenchBroom.CurrentMapPath))
        {
            _status = "No current map. Import a .map first.";
            Toasts.Warning("Relic", _status);
            return;
        }
        if (string.IsNullOrEmpty(exe))
        {
            _status = "TrenchBroom not found. Install it or set TRENCHBROOM_PATH env var.";
            Toasts.Warning("Relic", _status);
            return;
        }
        _status = RelicTrenchBroom.OpenCurrentMap(exe)
            ? "Opened in TrenchBroom."
            : "Failed to launch TrenchBroom.";
    }

    private static void Reimport()
    {
        var path = RelicTrenchBroom.CurrentMapPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            _status = "No current map. Import a .map first.";
            return;
        }
        var scene = Scene.Current;
        if (scene == null || !scene.IsValid())
        {
            _status = "No scene open.";
            return;
        }
        try
        {
            var map = RelicMapParser.Load(path);
            var options = new RelicBuildOptions { ProjectRoot = Project.Current?.RootPath };
            RelicSceneBuilder.ClearGeneratedObjects(scene, path);
            var report = RelicSceneBuilder.Build(map, scene, options);
            foreach (var g in report.GeometryIssues) Runtime.Debug.LogError($"[Relic][Geometry] {g}");
            foreach (var m in report.MissingTextures) Runtime.Debug.LogWarning($"[Relic] Missing texture '{m}' — fallback color used.");
            EditorSceneManager.MarkDirty();
            RelicMapAutoReload.NotifyImported(path, options);
            _status = $"Reimported '{Path.GetFileName(path)}': {report.WorldTriangles} tris, {report.EntitiesSpawned} entities.";
            Toasts.Success("Relic", _status);
        }
        catch (System.Exception ex)
        {
            _status = $"Reimport failed: {ex.Message}";
            Toasts.Error("Relic", _status);
        }
    }
}
