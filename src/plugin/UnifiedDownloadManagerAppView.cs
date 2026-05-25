using System.Threading.Tasks;
using Playnite;

namespace UnifiedDownloadManagerNS;

public class UnifiedDownloadManagerAppView : AppViewItem
{
    public UnifiedDownloadManagerAppView()
    {
        View = UnifiedDownloadManager.GetDownloadManagerPanel();
    }
    
    public override async Task ActivateViewAsync(ActivateViewAsyncArgs args)
    {
        // This gets called when the view is activated.
    }

    public override async Task DeactivateViewAsync(DeactivateViewAsyncArgs args)
    {
        // This gets called when the view is de-activated.
    }
}