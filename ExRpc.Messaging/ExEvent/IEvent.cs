using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExRpc.Messaging.ExEvent
{
    public interface IEvent
    {
        string Sender { get; set; }
    }
}
