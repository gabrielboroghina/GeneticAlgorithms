using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNet.SignalR;

namespace GlassBall
{
    public class MyHub : Hub
    {
        public override Task OnConnected()
        {
            return base.OnConnected();
        }
        public void Send(string userid,string message)
        {
            // Call the broadcastMessage method to update clients.
            Clients.Client(userid).broadcastMessage(message);
        }
    }
}