namespace CloudAndBear.Ui.ViewModels;

/// <summary>Ikone kao putanje na mreži 16x16 (crta ih Path sa Stroke, bez popune).</summary>
public static class Icons
{
    public const string Terminal = "M2,4 L6,8 L2,12 M8,12 L14,12";

    public const string Network =
        "M2,6 A8,8 0 0 1 14,6 M4.2,8.6 A5,5 0 0 1 11.8,8.6 M6.4,11.2 A2,2 0 0 1 9.6,11.2 M8,13.4 L8,13.5";

    public const string Containers =
        "M8,1.5 L14,4.75 L14,11.25 L8,14.5 L2,11.25 L2,4.75 Z M2,4.75 L8,8 L14,4.75 M8,8 L8,14.5";

    public const string Kubernetes =
        "M8,1.5 L13.6,4.75 L13.6,11.25 L8,14.5 L2.4,11.25 L2.4,4.75 Z M10,8 A2,2 0 1 1 6,8 A2,2 0 1 1 10,8";

    public const string System = "M2.5,11.5 A6,6 0 1 1 13.5,11.5 M8,10 L11,6.5";

    public const string Style = "M8,2 C8,2 3.5,7 3.5,10 A4.5,4.5 0 0 0 12.5,10 C12.5,7 8,2 8,2 Z";

    public const string Maximize = "M3.5,3.5 H12.5 V12.5 H3.5 Z";

    public const string Restore = "M5.5,5.5 H12.5 V12.5 H5.5 Z M3.5,10 V3.5 H10";
}
