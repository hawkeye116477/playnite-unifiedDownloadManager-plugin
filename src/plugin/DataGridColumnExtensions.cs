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
    }
}
