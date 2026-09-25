// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.
//
// RELIC workflow: TrenchBroom -> .map (source of truth) -> Import Map -> Play.
// World building happens in TrenchBroom, NOT in this editor.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Prowl.Editor.Core;
using Prowl.Editor.GUI.SceneView;
using Prowl.Editor.Projects;
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

public class RelicMapImporterPanel : DockPanel
{
    [MenuItem("Window/Relic/Map Importer", priority: 50)]
    static void Open() => EditorApplication.Instance?.OpenPanel(typeof(RelicMapImporterPanel));

    public override string Title => "Map Importer";
    public override string Icon => EditorIcons.Map;

    private static string _mapPath = RelicTrenchBroom.CurrentMapPath ?? string.Empty;
    private static RelicBuildReport? _lastReport;
    private static List<RelicValidationIssue> _issues = new();
    private static string _status = "No map imported yet. Build your world in TrenchBroom.";
    private static float _unitScale = RelicBrushBuilder.DefaultUnitScale;
    private static bool _buildCollision = true;
    private static bool _spawnPlayerRig = true;
    private static bool _loadTextures = true;

    public override void OnGUI(Paper paper, float width, float height)
    {
        var font = EditorTheme.DefaultFont;
        if (font == null) return;

        using (paper.Column("relic_root").Width(width).Height(height).Padding(0, 0, 8, 12).Gap(8).Enter())
        {
            SectionHeader(paper, "relic_h", "Relic Map Importer", first: true);

            paper.Box("relic_sub").Height(UnitValue.Auto)
                .Text("Source of truth: the .map file. Build in TrenchBroom, press Import, press Play.", font)
                .TextColor(EditorTheme.Ink300).FontSize(EditorTheme.FontSizeSmall).Alignment(TextAlignment.MiddleLeft);

            // Current path row.
            using (paper.Row("relic_pathrow").Height(28).Gap(8).Enter())
            {
                paper.Box("relic_path").Height(28)
                    .Text(string.IsNullOrEmpty(_mapPath) ? "(no .map selected)" : _mapPath, font)
                    .TextColor(EditorTheme.Ink400).FontSize(EditorTheme.FontSizeSmall)
                    .Alignment(TextAlignment.MiddleLeft).TextTruncate();
                Origami.Button(paper, "relic_browse", $"{EditorIcons.FolderOpen}  Browse", () =>
                    EditorApplication.OpenFileDialog(FileDialogMode.Open, path =>
                    {
                        if (path == null) return;
                        _mapPath = path;
                        RelicTrenchBroom.CurrentMapPath = path;
                    }, null, new[] { "*.map" }, new[] { "Valve Map (*.map)" })).Show();
            }

            // Action grid: Import / Reimport / Open in TB / Validate / Generate.
            using (paper.Row("relic_row1").Height(32).Gap(8).Enter())
            {
                CtaButton(paper, "relic_import", $"{EditorIcons.Hammer}  Import Map", EditorTheme.Accent, () => Import(false), grow: true);
                CtaButton(paper, "relic_reimport", $"{EditorIcons.ArrowsRotate}  Reimport", EditorTheme.Accent, () => Import(true), grow: true);
            }
            using (paper.Row("relic_row2").Height(32).Gap(8).Enter())
            {
                Origami.Button(paper, "relic_open_tb", $"{EditorIcons.LocationDot}  Open In TrenchBroom", () => OpenInTrenchBroom()).Show();
                Origami.Button(paper, "relic_validate", $"{EditorIcons.Check}  Validate Map", () => Validate()).Show();
            }

            // Options.
            SettingsRow(paper, "relic_opt_col", "Collision", () =>
                Origami.Checkbox(paper, "relic_opt_col_v", _buildCollision, v => _buildCollision = v).Show());
            SettingsRow(paper, "relic_opt_rig", "Spawn Player Rig", () =>
                Origami.Checkbox(paper, "relic_opt_rig_v", _spawnPlayerRig, v => _spawnPlayerRig = v).Show());
            SettingsRow(paper, "relic_opt_tex", "Load Textures", () =>
                Origami.Checkbox(paper, "relic_opt_tex_v", _loadTextures, v => _loadTextures = v).Show());
            SettingsRow(paper, "relic_opt_scale", "Unit Scale (m/unit)", () =>
                Origami.Slider(paper, "relic_opt_scale_v", _unitScale, v => _unitScale = v, 1f / 64f, 1f).Format("F4").Show());

            // Status + last report.
            paper.Box("relic_status").Height(UnitValue.Auto)
                .Text(_status, font).TextColor(EditorTheme.Ink400)
                .FontSize(EditorTheme.FontSizeSmall).Alignment(TextAlignment.MiddleLeft);

            if (_lastReport != null)
            {
                var r = _lastReport;
                paper.Box("relic_report").Height(UnitValue.Auto)
                    .Text($"Meshes: {r.WorldMeshes}  Tris: {r.WorldTriangles}  Entities: {r.EntitiesSpawned}  Warnings: {r.Warnings}", font)
                    .TextColor(EditorTheme.Ink300).FontSize(EditorTheme.FontSizeSmall).Alignment(TextAlignment.MiddleLeft);
                paper.Box("relic_report_tex").Height(UnitValue.Auto)
                    .Text($"Textured: {r.TexturedMaterials}  Procedural fallback: {r.FallbackMaterials}  Missing: {r.MissingTextures.Count}", font)
                    .TextColor(r.MissingTextures.Count > 0 ? EditorTheme.Amber400 : EditorTheme.Ink300)
                    .FontSize(EditorTheme.FontSizeSmall).Alignment(TextAlignment.MiddleLeft);

                if (r.MissingTextures.Count > 0)
                {
                    using (paper.Row("relic_missing_row").Height(28).Gap(8).Enter())
                    {
                        paper.Box("relic_missing").Height(28)
                            .Text($"Missing: {string.Join(", ", r.MissingTextures.Take(8))}" +
                                (r.MissingTextures.Count > 8 ? $" (+{r.MissingTextures.Count - 8} more)" : ""), font)
                            .TextColor(EditorTheme.Amber400).FontSize(EditorTheme.FontSizeSmall)
                            .Alignment(TextAlignment.MiddleLeft).TextTruncate();
                        Origami.Button(paper, "relic_open_texdir", $"{EditorIcons.FolderOpen}  Textures", () =>
                            OpenTexturesFolder()).Show();
                    }
                }
            }

            // Validation issues.
            if (_issues.Count > 0)
            {
                SectionHeader(paper, "relic_ih", "Validation");
                Origami.ScrollView(paper, "relic_iscroll", width - 24, Math.Max(120, height - 420)).Body(() =>
                {
                    using (paper.Column("relic_ilist").Height(UnitValue.Auto).Gap(2).Enter())
                    {
                        foreach (var issue in _issues)
                        {
                            var color = issue.Severity == "error" ? EditorTheme.Red400
                                : issue.Severity == "warning" ? EditorTheme.Amber400 : EditorTheme.Green400;
                            paper.Box($"relic_i_{issue.Message.GetHashCode()}").Height(UnitValue.Auto)
                                .Text($"[{issue.Severity}] {issue.Message}", font)
                                .TextColor(color).FontSize(EditorTheme.FontSizeSmall).Alignment(TextAlignment.MiddleLeft);
                        }
                    }
                });
            }
        }
    }

    private static void Import(bool reimport)
    {
        if (string.IsNullOrEmpty(_mapPath) || !File.Exists(_mapPath))
        {
            _status = "Select a .map file first (Browse).";
            Toasts.Warning("Relic", "Select a .map file first.");
            return;
        }
        var scene = Scene.Current;
        if (scene == null || !scene.IsValid())
        {
            _status = "No scene open. Create or open a scene first.";
            Toasts.Warning("Relic", "Open a scene first, then Import Map.");
            return;
        }
        try
        {
            var map = RelicMapParser.Load(_mapPath);
            RelicTrenchBroom.CurrentMapPath = _mapPath;
            var options = new RelicBuildOptions
            {
                UnitScale = _unitScale,
                BuildCollision = _buildCollision,
                SpawnPlayerRig = _spawnPlayerRig,
                LoadTextures = _loadTextures,
                ProjectRoot = Project.Current?.RootPath
            };
            var report = RelicSceneBuilder.Build(map, scene, options);
            _lastReport = report;
            _issues = RelicMapValidator.Validate(map);
            EditorSceneManager.MarkDirty();
            _status = reimport
                ? $"Reimported '{Path.GetFileName(_mapPath)}' — press Play to walk around."
                : $"Imported '{Path.GetFileName(_mapPath)}' — {report.WorldTriangles} tris, {report.EntitiesSpawned} entities. Press Play.";
            Runtime.Debug.Log($"[Relic] {_status}");
            foreach (var m in report.Messages) Runtime.Debug.LogWarning($"[Relic] {m}");
            foreach (var g in report.GeometryIssues) Runtime.Debug.LogError($"[Relic][Geometry] {g}");
            if (report.GeometryIssues.Count == 0)
                Runtime.Debug.Log($"[Relic][Geometry] {report.WorldTriangles} triangles, all windings agree with normals (culling-safe).");
            Toasts.Success("Relic", _status);
        }
        catch (Exception ex)
        {
            _status = $"Import failed: {ex.Message}";
            Runtime.Debug.LogError($"[Relic] Import failed: {ex}");
            Toasts.Error("Relic", _status);
        }
    }

    private static void Validate()
    {
        if (string.IsNullOrEmpty(_mapPath) || !File.Exists(_mapPath))
        {
            _status = "Select a .map file first (Browse).";
            return;
        }
        try
        {
            var map = RelicMapParser.Load(_mapPath);
            _issues = RelicMapValidator.Validate(map);
            _status = $"Validated '{Path.GetFileName(_mapPath)}': {_issues.Count} issue(s).";
        }
        catch (Exception ex)
        {
            _status = $"Validation failed: {ex.Message}";
        }
    }

    /// <summary>Reveal the texture folder (creating the project one if needed).</summary>
    internal static string OpenTexturesFolder()
    {
        string? dir = RelicTrenchBroom.FindTextureDir(Project.Current?.RootPath);
        if (string.IsNullOrEmpty(dir) && Project.Current != null)
            dir = Path.Combine(Project.Current.RootPath, "Assets", "Textures");
        if (string.IsNullOrEmpty(dir)) return string.Empty;
        try
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute = true });
        }
        catch (Exception ex) { Runtime.Debug.LogWarning($"[Relic] Could not open textures folder: {ex.Message}"); }
        return dir;
    }

    private static void OpenInTrenchBroom()
    {
        if (string.IsNullOrEmpty(_mapPath))
        {
            Toasts.Warning("Relic", "Select a .map file first.");
            return;
        }
        RelicTrenchBroom.CurrentMapPath = _mapPath;
        var exe = RelicTrenchBroom.FindExecutable(Project.Current?.RootPath);
        if (string.IsNullOrEmpty(exe))
        {
            _status = "TrenchBroom not found. Set TRENCHBROOM_PATH or install it (see TrenchBroom panel).";
            Toasts.Warning("Relic", _status);
            return;
        }
        if (RelicTrenchBroom.OpenCurrentMap(exe)) _status = "Opened in TrenchBroom. Edit, save, then Reimport.";
        else { _status = "Failed to launch TrenchBroom."; Toasts.Error("Relic", _status); }
    }
}
