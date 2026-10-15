using System.Drawing.Drawing2D;

namespace CloudConverter.App.UI;

internal enum FlatButtonKind
{
    /// <summary>일반 버튼: 배경 #CCCCCD</summary>
    Normal,
    /// <summary>&lt;변환 시작&gt; 버튼: 배경 흰색, 비활성 #8294AA</summary>
    Primary,
}

/// <summary>디자인 가이드 색상을 비활성 상태까지 그대로 그리는 버튼</summary>
internal sealed class FlatButton : Button
{
    private bool _hover;
    private bool _pressed;

    public FlatButtonKind Kind { get; set; }

    public FlatButton(string text, FlatButtonKind kind = FlatButtonKind.Normal)
    {
        Text = text;
        Kind = kind;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Height = 28;
        MinimumSize = new Size(32, 22);
        Margin = new Padding(4, 0, 0, 0);
        AutoSize = false;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
    }

    public int PreferredWidthFor(string text) =>
        Math.Max(MinimumSize.Width, TextRenderer.MeasureText(text, Font).Width + 24);

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Color back, fore, border;
        if (Kind == FlatButtonKind.Primary)
        {
            back = Enabled ? Theme.PrimaryBack : Theme.PrimaryBackDisabled;
            fore = Enabled ? Theme.PrimaryText : Theme.PrimaryTextDisabled;
            border = back;
        }
        else
        {
            back = Enabled ? Theme.ButtonBack : Theme.ButtonBackDisabled;
            fore = Enabled ? Theme.ButtonText : Theme.ButtonTextDisabled;
            border = back;
        }
        if (Enabled && _pressed) back = ControlPaint.Dark(back, 0.08f);
        else if (Enabled && _hover) back = Kind == FlatButtonKind.Primary ? Theme.PrimaryBackHover : ControlPaint.Light(back, 0.35f);

        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.WindowBack);
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var brush = new SolidBrush(back)) g.FillRectangle(brush, rect);
        using (var pen = new Pen(border)) g.DrawRectangle(pen, rect);
        if (Focused && ShowFocusCues && Enabled)
        {
            var focus = Rectangle.Inflate(rect, -2, -2);
            ControlPaint.DrawFocusRectangle(g, focus, fore, back);
        }
        TextRenderer.DrawText(g, Text, Font, rect, fore,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}

/// <summary>원형 ⓘ (프로그램 정보) 버튼</summary>
internal sealed class InfoButton : Control
{
    private bool _hover;

    public InfoButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
                 | ControlStyles.SupportsTransparentBackColor, true);
        Size = new Size(22, 22);
        Cursor = Cursors.Hand;
        BackColor = Theme.WindowBack;
        Margin = new Padding(0, 0, 8, 0);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.WindowBack);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var size = Math.Min(Width, Height) - 2;
        var rect = new Rectangle((Width - size) / 2, (Height - size) / 2, size, size);
        using (var brush = new SolidBrush(_hover ? ControlPaint.Dark(Theme.InfoIcon, 0.1f) : Theme.InfoIcon)) g.FillEllipse(brush, rect);
        using var font = new Font("Segoe UI", size * 0.42f, FontStyle.Bold, GraphicsUnit.Pixel);
        TextRenderer.DrawText(g, "i", font, rect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}
