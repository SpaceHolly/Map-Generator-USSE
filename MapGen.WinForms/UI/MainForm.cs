using MapGen.Core.Generation;
using MapGen.Core.Settings;
using MapGen.WinForms.Rendering;

namespace MapGen.WinForms.UI;

public sealed class MainForm : Form
{
    private readonly MapCanvas _canvas = new();
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly ListBox _warnings = new() { Dock = DockStyle.Bottom, Height = 140 };
    private readonly SettingsUiBuilder _requiredBuilder = new();
    private readonly SettingsUiBuilder _advancedBuilder = new();

    private GenerationSettings _settings = DefaultSettingsProvider.BuildDefaults(Era.Industrial, Setting.Building);

    public MainForm()
    {
        Text = "Умный генератор карт (MVP)";
        Width = 1400;
        Height = 900;

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 900 };
        split.Panel1.Controls.Add(_canvas);

        var side = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
        side.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));

        var tabRequired = new TabPage("Необходимые") { AutoScroll = true };
        tabRequired.Controls.Add(_requiredBuilder.BuildPanel(_settings, SettingImportance.Required));
        var tabAdvanced = new TabPage("Дополнительные") { AutoScroll = true };
        tabAdvanced.Controls.Add(_advancedBuilder.BuildPanel(_settings, SettingImportance.Advanced));
        _tabs.TabPages.Add(tabRequired);
        _tabs.TabPages.Add(tabAdvanced);

        var buttons = BuildButtons();
        side.Controls.Add(_tabs, 0, 0);
        side.Controls.Add(buttons, 0, 1);
        side.Controls.Add(_warnings, 0, 2);

        split.Panel2.Controls.Add(side);
        Controls.Add(split);

        GenerateMap();
    }

    private Control BuildButtons()
    {
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };

        var btnGenerate = new Button { Text = "Generate", AutoSize = true };
        btnGenerate.Click += (_, _) => GenerateMap();

        var btnResetReq = new Button { Text = "Reset Required", AutoSize = true };
        btnResetReq.Click += (_, _) =>
        {
            ApplyUi();
            var d = DefaultSettingsProvider.BuildDefaults(_settings.Era, _settings.Setting);
            foreach (var p in typeof(GenerationSettings).GetProperties())
            {
                var meta = (SettingMetadataAttribute?)Attribute.GetCustomAttribute(p, typeof(SettingMetadataAttribute));
                if (meta?.Importance == SettingImportance.Required) p.SetValue(_settings, p.GetValue(d));
            }
            ReloadUi();
        };

        var btnResetAll = new Button { Text = "Reset All", AutoSize = true };
        btnResetAll.Click += (_, _) =>
        {
            ApplyUi();
            _settings = DefaultSettingsProvider.BuildDefaults(_settings.Era, _settings.Setting);
            ReloadUi();
        };

        var btnRndSeed = new Button { Text = "Randomize Seed", AutoSize = true };
        btnRndSeed.Click += (_, _) =>
        {
            ApplyUi();
            _settings.Seed = Random.Shared.Next(0, int.MaxValue);
            ReloadUi();
        };

        flow.Controls.AddRange([btnGenerate, btnResetReq, btnResetAll, btnRndSeed]);
        return flow;
    }

    private void ApplyUi()
    {
        _requiredBuilder.ApplyToSettings(_settings);
        _advancedBuilder.ApplyToSettings(_settings);
    }

    private void ReloadUi()
    {
        _requiredBuilder.ReloadFromSettings(_settings);
        _advancedBuilder.ReloadFromSettings(_settings);
    }

    private void GenerateMap()
    {
        ApplyUi();
        var generator = new MapGenerator();
        var result = generator.Generate(_settings, _settings.Seed);
        _canvas.Map = result.Map;
        _canvas.Invalidate();

        _warnings.Items.Clear();
        foreach (var warning in result.Warnings) _warnings.Items.Add(warning);
    }
}
