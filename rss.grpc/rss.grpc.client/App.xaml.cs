using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;
using System.Threading;

namespace rss.grpc.client
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        Process p;
        public App() {
            this.Exit += App_Exit;
            var ps = Process.GetProcesses();
            //try to start server if not working
            try
            {
                if (ps.Where(p => p.ProcessName == "rss.grpc.server").Count() == 0)
                {
                    p = new Process();

                    p.StartInfo.FileName = "cmd.exe";
                    p.StartInfo.Arguments = "/c rss.grpc.server.exe";

                    p.StartInfo.WorkingDirectory = Environment
                        .CurrentDirectory + @"\..\..\..\..\..\rss.grpc.server\bin\Debug\netcoreapp3.1";

                    p.Start();

                    while (p.StartTime == null)
                    {
                        Thread.Sleep(1000);
                    }
                }
            }
            catch
            {
                MessageBox.Show("Grpc server could not be started! Please start it manually ant trye again!", 
                    "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            //dont need to stop aggregator
            //if (p != null) {
                //p.Kill();
            //}
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
        }

        private void Application_DispatcherUnhandledException(object sender, 
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            MessageBox.Show("Oops, an error has been occurred! Program will be closed.\n" + e.Exception.Message, "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            
            App.Current.Shutdown();
        }
    }
}
