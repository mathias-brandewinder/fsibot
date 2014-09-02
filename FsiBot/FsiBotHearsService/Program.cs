using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FsiBotHears;
using Topshelf;

namespace FsiBotHearsService
{
    class Program
    {
        static void Main(string[] args)
        {
            HostFactory.Run(x =>
            {
                x.Service<Listener>(s =>
                {
                    s.ConstructUsing(name => new Listener());
                    s.WhenStarted(bot => bot.Start());
                    s.WhenStopped(bot => bot.Stop());
                });

                x.RunAsLocalSystem();
                x.StartAutomatically();
                x.EnableServiceRecovery(s =>
                {
                    s.RestartService(2);
                });

                x.SetDescription("FSI bot listener");
                x.SetDisplayName("FSI bot listener");
                x.SetServiceName("fsibotlistener");
            });           
        }
    }
}
