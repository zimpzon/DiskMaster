using DiskMasterLib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MainForm
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Register your services here
                    services.AddSingleton<FolderExplorer>();
                    services.AddTransient<MainForm>();
                })
                .Build();

            var mainForm = host.Services.GetRequiredService<MainForm>();
            Application.Run(mainForm);
        }
    }
}
