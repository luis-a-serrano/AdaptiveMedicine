using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace AdaptiveMedicine.Common.Statechart.Interfaces {
   public interface IState {
      string Type { get; }
      Task<IEnumerable<IEvent>> EntryActionAsync(IEvent anEvent, Actor actor);
      Task<IEnumerable<IEvent>> ExitActionAsync(IEvent anEvent, Actor actor);
      ITransition GetActivatedTransition(IEvent anEvent);
   }
}
