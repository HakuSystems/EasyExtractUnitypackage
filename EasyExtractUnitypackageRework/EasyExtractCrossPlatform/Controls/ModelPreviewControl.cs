using System.Numerics;

namespace EasyExtractCrossPlatform.Controls;

public sealed class ModelPreviewControl : Control
{
    public static readonly StyledProperty<ModelPreviewData?> ModelProperty =
        AvaloniaProperty.Register<ModelPreviewControl, ModelPreviewData?>(nameof(Model));

    private Point? _lastPointerPosition;
    private double _pitch = -20.0;
    private double _yaw = 45.0;
    private double _zoom = 1.0;

    public ModelPreviewData? Model
    {
        get => GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        _lastPointerPosition = point.Position;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_lastPointerPosition is null)
            return;

        var position = e.GetPosition(this);
        var delta = position - _lastPointerPosition.Value;
        _lastPointerPosition = position;

        _yaw = (_yaw + delta.X * 0.5 + 360) % 360;
        _pitch = Math.Clamp(_pitch - delta.Y * 0.5, -89, 89);
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _lastPointerPosition = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _zoom = Math.Clamp(_zoom + e.Delta.Y * 0.1, 0.2, 4.0);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var model = Model;
        if (model is null || model.Vertices.Length == 0 || model.Indices.Length < 3)
        {
            DrawPlaceholder(context);
            return;
        }

        var bounds = Bounds;
        var center = bounds.Center;
        var scale = ComputeScale(model, bounds) * _zoom;

        var rotation = Matrix4x4.CreateFromYawPitchRoll(
            (float)(_yaw * Math.PI / 180d),
            (float)(_pitch * Math.PI / 180d),
            0);

        var transformed = new Point[model.Vertices.Length];
        for (var i = 0; i < model.Vertices.Length; i++)
        {
            var v = model.Vertices[i] - model.Center;
            var rotated = Vector3.Transform(v, rotation);
            var projected = new Point(center.X + rotated.X * scale, center.Y - rotated.Y * scale);
            transformed[i] = projected;
        }

        var pen = new Pen(Brushes.White, 1.5);
        for (var i = 0; i < model.Indices.Length; i += 3)
        {
            var a = transformed[model.Indices[i]];
            var b = transformed[model.Indices[i + 1]];
            var c = transformed[model.Indices[i + 2]];

            context.DrawLine(pen, a, b);
            context.DrawLine(pen, b, c);
            context.DrawLine(pen, c, a);
        }

        DrawAxis(context, center, scale);
    }

    private void DrawPlaceholder(DrawingContext context)
    {
        var rect = Bounds.Deflate(8);
        var pen = new Pen(Brushes.Gray);
        context.DrawRectangle(null, pen, rect);
        var formatted = new FormattedText(
            "No model preview available",
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            14,
            Brushes.Gray)
        {
            TextAlignment = TextAlignment.Center,
            MaxTextWidth = rect.Width
        };
        var textWidth = formatted.Width;
        var textHeight = formatted.Height;
        var position = new Point(
            rect.X + rect.Width / 2 - textWidth / 2,
            rect.Y + rect.Height / 2 - textHeight / 2);
        context.DrawText(formatted, position);
    }

    private static double ComputeScale(ModelPreviewData model, Rect bounds)
    {
        if (model.BoundingRadius <= 0)
            return 1.0;

        var size = Math.Min(bounds.Width, bounds.Height);
        return size / (model.BoundingRadius * 2.2);
    }

    private static void DrawAxis(DrawingContext context, Point center, double scale)
    {
        var axisLength = scale * 0.6;
        var axes = new[]
        {
            (Brushes.Red, new Point(axisLength, 0.0)),
            (Brushes.Lime, new Point(0.0, -axisLength)),
            (Brushes.DeepSkyBlue, new Point(-axisLength * 0.3, axisLength * 0.3))
        };

        foreach (var (brush, offset) in axes)
        {
            var pen = new Pen(brush);
            context.DrawLine(pen, center, center + offset);
        }
    }
}