using CloudConverter.App.UI;
using MipLabel = Microsoft.InformationProtection.Label;

namespace CloudConverter.App.Mip;

/// <summary>MIP 변환 시 적용할 민감도 레이블 선택</summary>
internal sealed class LabelPickerForm : ThemedForm
{
    private readonly ComboBox _labels = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
    private readonly System.Windows.Forms.Label _description = new() { AutoSize = true, MaximumSize = new Size(360, 0), ForeColor = Color.DimGray };

    public MipLabel? SelectedLabel => (_labels.SelectedItem as LabelChoice?)?.Label;

    public LabelPickerForm(IReadOnlyList<LabelChoice> choices, string? preselectId)
    {
        Text = "적용할 레이블 선택";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.White;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(20, 16, 20, 14);

        foreach (var choice in choices)
            _labels.Items.Add(choice);
        var index = choices.ToList().FindIndex(c => string.Equals(c.Label.Id, preselectId, StringComparison.OrdinalIgnoreCase));
        _labels.SelectedIndex = index >= 0 ? index : 0;
        _labels.SelectedIndexChanged += (_, _) => UpdateDescription();
        UpdateDescription();

        var ok = new FlatButton("확인", FlatButtonKind.Primary) { Width = 90, DialogResult = DialogResult.OK, Margin = new Padding(0, 0, 6, 0) };
        var cancel = new FlatButton("취소") { Width = 90, DialogResult = DialogResult.Cancel, Margin = Padding.Empty };
        AcceptButton = ok;
        CancelButton = cancel;

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Anchor = AnchorStyles.Right, Margin = new Padding(0, 14, 0, 0) };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        var root = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, RowCount = 4, Dock = DockStyle.Fill };
        root.Controls.Add(new System.Windows.Forms.Label { Text = "MIP로 변환할 때 적용할 민감도 레이블을 선택하세요.", AutoSize = true, Margin = new Padding(0, 0, 0, 10) }, 0, 0);
        root.Controls.Add(_labels, 0, 1);
        root.Controls.Add(_description, 0, 2);
        root.Controls.Add(buttons, 0, 3);
        Controls.Add(root);
    }

    private void UpdateDescription()
    {
        var label = SelectedLabel;
        _description.Text = label is null ? "" : string.IsNullOrWhiteSpace(label.Description) ? label.Tooltip ?? "" : label.Description;
        _description.Margin = new Padding(0, 8, 0, 0);
    }
}
