using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FsiBot;
using Topshelf;

namespace FsiBotService
{
    class Program
    {
        public static void Main(string[] args)
        {
            HostFactory.Run(x =>                                 
                    {
                        x.Service<Bot>(s =>                        
                            {
                               s.ConstructUsing(name => new Bot());     
                               s.WhenStarted(bot => bot.Start());              
                               s.WhenStopped(bot => bot.Stop());               
                            });
                        
                        x.RunAsLocalSystem();
                        x.StartAutomatically();
                        x.EnableServiceRecovery(s =>
                            {
                                s.RestartService(2);
                            });

                        x.SetDescription("FSI bot");
                        x.SetDisplayName("FSI bot");
                        x.SetServiceName("fsibot");
                    });           
        }
    }
}
