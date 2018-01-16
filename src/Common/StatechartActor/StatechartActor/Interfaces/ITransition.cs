using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace AdaptiveMedicine.Common.Statechart.Interfaces {
   public delegate Task<IEnumerable<IEvent>> TransitionAction(IEvent anEvent, Actor actor);

   public interface ITransition {
      string EventTrigger { get; }
      string TargetState { get; }
      TransitionAction Action { get; }
   }
}
