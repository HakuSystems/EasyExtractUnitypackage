using System.Windows.Media;

namespace EasyExtract.Utilities;

/// <summary>
///     A decorator class that provides automatic DPI scaling for its child elements.
///     This class ensures that the user interface elements are appropriately scaled
///     relative to the system's DPI settings, preserving a consistent visual appearance
///     across different display configurations.
/// </summary>
/// <remarks>
///     The DpiDecorator subscribes to the Loaded event of the element and applies a
///     scaling transformation based on the system's DPI settings, calculated using
///     the TransformToDevice matrix provided by the CompositionTarget of the visual.
///     The scaling transform is applied to the LayoutTransform property of the decorator.
/// </remarks>
/// <example>
///     Use DpiDecorator to wrap elements in XAML where you need DPI scaling support.
/// </example>
public class DpiDecorator : Decorator
{
    /// <summary>
    ///     A custom WPF decorator that adjusts for DPI scaling issues.
    ///     This class ensures consistent rendering of WPF elements by applying
    ///     a layout transformation to negate DPI scaling effects.
    /// </summary>
    /// <remarks>
    ///     Utilizes <see cref="System.Windows.Media.ScaleTransform" /> to apply scaling transformations
    ///     based on the device's DPI settings. This ensures proper layout and sizing
    ///     independent of the screen's DPI settings. The DPI transformation is calculated
    ///     using the rendering target's TransformToDevice matrix.
    /// </remarks>
    /// <example>
    ///     Typically used as a wrapper for WPF content elements in XAML to ensure DPI consistency.
    /// </example>
    public DpiDecorator()
    {
        Loaded += (s, e) =>
        {
            var m = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;
            var dpiTransform = new ScaleTransform(1 / m.M11, 1 / m.M22);
            if (dpiTransform.CanFreeze)
                dpiTransform.Freeze();
            LayoutTransform = dpiTransform;
        };
    }
}