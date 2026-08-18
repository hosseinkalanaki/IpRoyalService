using System.ServiceProcess;
using IpRoyalService;

namespace IpRoyalControl;

public static class ServiceManager
{
    public static ServiceControllerStatus? GetStatus()
    {
        try { using var service = new ServiceController(ServiceIdentity.Name); return service.Status; }
        catch (InvalidOperationException) { return null; }
    }

    public static void Start()
    {
        using var service = new ServiceController(ServiceIdentity.Name);
        service.Refresh();
        if (service.Status == ServiceControllerStatus.Running) return;
        if (service.Status == ServiceControllerStatus.Stopped) service.Start();
        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
    }

    public static void Stop()
    {
        using var service = new ServiceController(ServiceIdentity.Name);
        service.Refresh();
        if (service.Status == ServiceControllerStatus.Stopped) return;
        if (service.Status is ServiceControllerStatus.Running or ServiceControllerStatus.Paused) service.Stop();
        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
    }

    public static void Restart() { Stop(); Start(); }
}
