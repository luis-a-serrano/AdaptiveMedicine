using System;
using System.Reflection;
using AdaptiveMedicine.Common.Statechart.Attributes;
using AdaptiveMedicine.Common.Statechart.Interfaces;

namespace AdaptiveMedicine.Common.Actors {
   internal class StatechartTransition: ITransition {
      private TransitionAttribute _Transition { get; }

      public string EventTrigger { get { return _Transition.EventType; } }
      public string TargetState { get { return _Transition.StateType; } }
      public TransitionAction Action { get; }

      public StatechartTransition(TransitionAttribute transition, MethodInfo method, IState context) {
         _Transition = transition;
         Action = (TransitionAction) Delegate.CreateDelegate(typeof(TransitionAction), context, method);
      }
   }
}
