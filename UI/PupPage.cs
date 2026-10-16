using System.Windows.Controls;

namespace Pupstrap.UI;

public abstract class PupPage : Page
{
    protected PupPage()
    {
        // Page defaults to black text and doesn't inherit from the window.
        SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");
    }
}
