using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AdaptiveMedicine.Common.Statechart.Attributes;
using AdaptiveMedicine.Common.Statechart.Interfaces;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace AdaptiveMedicine.Common.Actors {
   public abstract class StatechartState: IState {
      private IReadOnlyList<ITransition> _Transitions { get; }
      public string Type { get; }

      public StatechartState() {
         var stateTypes = this.GetType().GetCustomAttributes<StateAttribute>();
         if (stateTypes.Count() == 1) {
            Type = stateTypes.First().Type;

            var possibleTransitions = new List<ITransition>();
            foreach (var member in this.GetType().GetMethods()) {
               var transitions = member.GetCustomAttributes<TransitionAttribute>(true);

               foreach (var transition in transitions) {
                  if (!String.IsNullOrWhiteSpace(transition.EventType)
                     && !possibleTransitions.Any(trans => transition.EventType == trans.EventTrigger)
                     && member.ReturnType == typeof(Task<IEnumerable<IEvent>>)) {

                     var methodParameters = member.GetParameters();
                     if (methodParameters.Length == 2
                        && methodParameters[0].ParameterType == typeof(IEvent)
                        && methodParameters[1].ParameterType == typeof(Actor)) {
                        possibleTransitions.Add(new StatechartTransition(transition, member, this));
                     }
                  }
               }
            }

            _Transitions = possibleTransitions.AsReadOnly();
         }
      }

      public virtual Task<IEnumerable<IEvent>> EntryActionAsync(IEvent anEvent, Actor actor) {
         return Task.FromResult<IEnumerable<IEvent>>(null);
      }

      public virtual Task<IEnumerable<IEvent>> ExitActionAsync(IEvent anEvent, Actor actor) {
         return Task.FromResult<IEnumerable<IEvent>>(null);
      }

      public ITransition GetActivatedTransition(IEvent anEvent) {
         ITransition activeTransition = null;
         foreach (var transition in _Transitions) {
            if (transition.EventTrigger == anEvent.Type) {
               activeTransition = transition;
               break;
            }
         }
         return activeTransition;
      }

   }
}
