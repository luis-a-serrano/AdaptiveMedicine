using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;

namespace AdaptiveMedicine.Common.Statechart.Interfaces {
   public interface IStatechart: IActor {
      Task DispatchEventAsync(IEvent anEvent);
   }
}
