using System.Reflection;
using MapGen.Core.Settings;

namespace MapGen.WinForms.UI;

public sealed class SettingsUiBuilder
{
    private readonly Dictionary<PropertyInfo, Control> _editors = [];

    public Control BuildPanel(GenerationSettings settings, SettingImportance importance)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(8)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var props = typeof(GenerationSettings).GetProperties()
            .Select(p => (Prop: p, Meta: p.GetCustomAttribute<SettingMetadataAttribute>()))
            .Where(x => x.Meta is not null && x.Meta.Importance == importance)
            .OrderBy(x => x.Meta!.Category).ThenBy(x => x.Meta!.DisplayName);

        foreach (var (prop, meta) in props)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowCount++;
            var label = new Label { Text = meta!.DisplayName, AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 0) };
            var editor = CreateEditor(prop, meta, settings);
            panel.Controls.Add(label);
            panel.Controls.Add(editor);
            _editors[prop] = editor;
        }

        return panel;
    }

    public void ApplyToSettings(GenerationSettings settings)
    {
        foreach (var (prop, control) in _editors)
        {
            object? value = control switch
            {
                NumericUpDown n when prop.PropertyType == typeof(int) => (int)n.Value,
                NumericUpDown n when prop.PropertyType == typeof(double) => (double)n.Value,
                ComboBox c => c.SelectedItem,
                CheckBox b => b.Checked,
                _ => null
            };
            if (value is not null) prop.SetValue(settings, value);
        }
    }

    public void ReloadFromSettings(GenerationSettings settings)
    {
        foreach (var (prop, control) in _editors)
        {
            var value = prop.GetValue(settings);
            switch (control)
            {
                case NumericUpDown n when value is int i:
                    n.Value = Math.Clamp(i, (int)n.Minimum, (int)n.Maximum);
                    break;
                case NumericUpDown n when value is double d:
                    n.Value = (decimal)Math.Clamp(d, (double)n.Minimum, (double)n.Maximum);
                    break;
                case ComboBox c:
                    c.SelectedItem = value;
                    break;
                case CheckBox b when value is bool flag:
                    b.Checked = flag;
                    break;
            }
        }
    }

    private static Control CreateEditor(PropertyInfo prop, SettingMetadataAttribute meta, GenerationSettings settings)
    {
        var value = prop.GetValue(settings);
        if (prop.PropertyType.IsEnum)
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
            combo.Items.AddRange(Enum.GetValues(prop.PropertyType).Cast<object>().ToArray());
            combo.SelectedItem = value;
            return combo;
        }
        if (prop.PropertyType == typeof(bool))
        {
            return new CheckBox { Checked = value is true, AutoSize = true };
        }
        if (prop.PropertyType == typeof(int) || prop.PropertyType == typeof(double))
        {
            var n = new NumericUpDown
            {
                DecimalPlaces = prop.PropertyType == typeof(double) ? 2 : 0,
                Increment = (decimal)(meta.Step ?? 1),
                Minimum = (decimal)(meta.Min ?? 0),
                Maximum = (decimal)(meta.Max ?? 999999),
                Dock = DockStyle.Top
            };
            n.Value = Convert.ToDecimal(value);
            return n;
        }

        return new TextBox { Text = value?.ToString() ?? string.Empty };
    }
}
