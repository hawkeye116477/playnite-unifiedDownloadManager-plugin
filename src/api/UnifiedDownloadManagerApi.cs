using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnifiedDownloadManagerApiNS
{
    public class UnifiedDownloadManagerApi
    {
        private IPlayniteAPI playniteAPI = API.Instance;
        public Playnite.SDK.Plugins.Plugin udmPlugin => playniteAPI.Addons.Plugins.Find(plugin => plugin.Id.Equals(UnifiedDownloadManagerSharedProperties.Id));
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly IUnifiedTaskManager manager;

        public UnifiedDownloadManagerApi()
        {
            manager = GetTaskManager();
            if (manager == null)
            {
                return;
            }
        }

        private IUnifiedTaskManager GetTaskManager()
        {
            var pluginInterface = udmPlugin as IUnifiedDownloadManager;
            return pluginInterface.Manager;
        }

        //private object InvokeMethod(string methodName, params object[] args)
        //{
        //    if (manager == null)
        //    {
        //        return null;
        //    }

        //    var method = manager.GetType().GetMethod(methodName);
        //    if (method == null)
        //    {
        //        logger.Error($"Method '{methodName}' not found in TaskManager");
        //        return null;
        //    }

        //    var parameters = method.GetParameters();
        //    if (parameters == null)
        //    {
        //        return null;
        //    }
        //    var finalArgs = new object[parameters.Length];
        //    for (int i = 0; i < parameters.Length; i++)
        //    {
        //        if (i < args.Length && args[i] != null)
        //        {
        //            //var paramType = parameters[i].ParameterType;
        //            //if (!paramType.IsValueType && paramType != typeof(string))
        //            //{
        //            //    var pluginArg = Activator.CreateInstance(paramType);
        //            //    foreach (var prop in paramType.GetProperties())
        //            //    {
        //            //        var sourceProp = args[i].GetType().GetProperty(prop.Name);
        //            //        if (sourceProp != null)
        //            //        {
        //            //            prop.SetValue(pluginArg, sourceProp.GetValue(args[i]));
        //            //        }
        //            //    }
        //            //    finalArgs[i] = pluginArg;
        //            //}
        //            //else

        //            finalArgs[i] = args[i];

        //        }
        //        else
        //        {
        //            finalArgs[i] = Type.Missing;
        //        }
        //    }
        //    return method?.Invoke(manager, finalArgs);
        //}

        public async Task EnqueueTasks(List<UnifiedDownload> downloadManagerDataList, bool silently = false)
        {
            await manager.EnqueueTasks(downloadManagerDataList, silently);
        }

        //public void EditTask(string appId, string pluginId, string propertyName, object value)
        //{

        //    InvokeMethod("EditTask", appId, pluginId, propertyName, value);
        //}

        public UnifiedDownload GetTask(string appId, string pluginId)
        {
            return manager.GetTask(appId, pluginId);
        }

        //public void SetTaskStatus(string appId, string pluginId, UnifiedDownloadStatus downloadStatus)
        //{
        //    InvokeMethod("EditTask", appId, pluginId, "status", downloadStatus);
        //}

        //public void SetTaskProgress(string appId, string pluginId, double progress)
        //{
        //    InvokeMethod("EditTask", appId, pluginId, "progress", progress);
        //}

    }
}
