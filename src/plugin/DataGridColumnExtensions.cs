using System.Windows;

namespace UnifiedDownloadManagerNS
{
    public static class DataGridColumnExtensions
    {
        public static readonly DependencyProperty ColumnIdProperty =
            DependencyProperty.RegisterAttached(
                "ColumnId",
                typeof(string),
                typeof(DataGridColumnExtensions));

        public static void SetColumnId(DependencyObject obj, string value)
        {
            obj.SetValue(ColumnIdProperty, value);
        }

        public static string GetColumnId(DependencyObject obj)
        {
            return (string)obj.GetValue(ColumnIdProperty);
        }

        public static readonly DependencyProperty IsLockedProperty =
            DependencyProperty.RegisterAttached(
                "IsLocked",
                typeof(bool),
                typeof(DataGridColumnExtensions),
                new PropertyMetadata(false));

        public static void SetIsLocked(DependencyObject obj, bool value)
        {
            obj.SetValue(IsLockedProperty, value);
        }

        public static bool GetIsLocked(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsLockedProperty);
        }
    }
}