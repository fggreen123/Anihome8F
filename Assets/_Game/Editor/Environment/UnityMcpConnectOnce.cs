using System;
using System.IO;
using System.Threading.Tasks;
using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class UnityMcpConnectOnce
{
    private const string RequestPath = "Temp/UnityMcpConnect.request";
    private const string StatusPath = "Temp/UnityMcpConnect.status";

    static UnityMcpConnectOnce() { EditorApplication.update += Tick; }

    private static void Tick()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        EditorApplication.update -= Tick;
        File.Delete(RequestPath);
        Connect();
    }

    private static void Status(string message)
    {
        File.WriteAllText(StatusPath, DateTime.Now.ToString("s") + " " + message);
        Debug.Log("[UnityMcpConnect] " + message);
    }

    private static async void Connect()
    {
        try
        {
            Status("Preparing local HTTP connection");
            var config = EditorConfigurationCache.Instance;
            config.UnpinStdioForSession();
            config.SetHttpTransportScope("local");
            config.SetHttpBaseUrl("http://127.0.0.1:8080");
            config.SetUseHttpTransport(true);
            EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad", true);
            var server = MCPServiceLocator.Server;
            if (!server.IsLocalHttpServerReachable() && !server.StartLocalHttpServer(quiet: true))
            {
                Status("ERROR: local server could not start");
                return;
            }
            for (int i = 0; i < 120 && !server.IsLocalHttpServerReachable(); i++)
                await Task.Delay(500);
            if (!server.IsLocalHttpServerReachable())
            {
                Status("ERROR: local server did not become reachable");
                return;
            }
            bool started = await MCPServiceLocator.Bridge.StartAsync();
            Status(started ? "CONNECTED: Unity HTTP bridge started" : "ERROR: Unity bridge did not start");
        }
        catch (Exception ex) { Status("ERROR: " + ex.Message); }
    }
}
