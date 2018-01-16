using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AdaptiveMedicine.Common.Statechart.Attributes;
using AdaptiveMedicine.Common.Statechart.Interfaces;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace AdaptiveMedicine.Common.Actors {
   public abstract class StatechartActor: Actor, IStatechart {
      public const string CurrentStateLabel = "Statechart.CurrentState";
      public const string PastEventsLabel = "Statechart.PastEvents";
      private static readonly ConditionalWeakTable<Type, StatesMap> _StatesPerStatechart = new ConditionalWeakTable<Type, StatesMap>();

      private class StatesMap {
         public string Initial { get; }
         public IReadOnlyCollection<string> Finals { get; }
         public IReadOnlyDictionary<string, IState> All { get; }

         public StatesMap(string initialState, IEnumerable<string> finalStates, IDictionary<string, IState> statesList) {
            Initial = initialState;
            Finals = Array.AsReadOnly((string[]) finalStates.ToArray().Clone());
            All = new ReadOnlyDictionary<string, IState>(new Dictionary<string, IState>(statesList));
         }
      }

      public StatechartActor(ActorService actorService, ActorId actorId)
         : base(actorService, actorId) {

         var thisType = this.GetType();
         if (!_StatesPerStatechart.TryGetValue(thisType, out StatesMap statesMap)) {
            string initialState = null;
            HashSet<string> finalStates = new HashSet<string>();
            var statesList = new Dictionary<string, IState>();

            var potentialStates = new List<Type>();
            var currentType = thisType;
            while(currentType != null && currentType != typeof(StatechartActor)) {
               potentialStates.AddRange(currentType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic));
               currentType = currentType.BaseType;
            }

            foreach (var potentialState in potentialStates) {
               if (potentialState.IsClass && potentialState.GetInterfaces().Contains(typeof(IState))) {

                  var initialStateAttributes = potentialState.GetCustomAttributes<InitialStateAttribute>();
                  if (initialStateAttributes.Count() == 1) {
                     initialState = initialStateAttributes.First().Type;
                     if (!String.IsNullOrWhiteSpace(initialState)) {
                        statesList[initialState] = (IState)potentialState.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                     }
                  }

                  var finalStateAttributes = potentialState.GetCustomAttributes<FinalStateAttribute>();
                  foreach (var finalStateAttribute in finalStateAttributes) {
                     if (!String.IsNullOrWhiteSpace(finalStateAttribute.Type)) {
                        finalStates.Add(finalStateAttribute.Type);
                        statesList[finalStateAttribute.Type] = (IState)potentialState.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                     }
                  }

                  var stateAttributes = potentialState.GetCustomAttributes<StateAttribute>();
                  foreach (var stateAttribute in stateAttributes) {
                     if (!String.IsNullOrWhiteSpace(stateAttribute.Type)) {
                        statesList[stateAttribute.Type] = (IState)potentialState.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                     }
                  }

               }
            }

            _StatesPerStatechart.GetValue(thisType, _ => new StatesMap(initialState, finalStates, statesList));
         }
      }

      protected override async Task OnActivateAsync() {
         if (_StatesPerStatechart.TryGetValue(this.GetType(), out StatesMap statesMap)) {
            var currentState = await StateManager.TryGetStateAsync<string>(CurrentStateLabel);
            if (!currentState.HasValue || !statesMap.All.ContainsKey(currentState.Value)) {
               await StateManager.SetStateAsync<string>(CurrentStateLabel, statesMap.Initial);
            }

            var pastEvents = await StateManager.TryGetStateAsync<Dictionary<string, DateTime>>(PastEventsLabel);
            if (!pastEvents.HasValue) {
               await StateManager.SetStateAsync<Dictionary<string, DateTime>>(PastEventsLabel, new Dictionary<string, DateTime>());
            }
         } else {
            // throw
         }

         await base.OnActivateAsync();
         return;
      }

      public async Task DispatchEventAsync(IEvent anEvent) {
         if (_StatesPerStatechart.TryGetValue(this.GetType(), out StatesMap statesMap)) {
            var pastEvents = await StateManager.TryGetStateAsync<Dictionary<string, DateTime>>(PastEventsLabel);
            if (pastEvents.HasValue) {

               var currentState = await StateManager.TryGetStateAsync<string>(CurrentStateLabel);
               if (currentState.HasValue) {
                  var theEvents = new List<IEvent>();
                  var iterationState = currentState.Value;

                  if (statesMap.Finals.Contains(iterationState)) {
                     return;
                  }

                  theEvents.Add(anEvent);
                  while (theEvents.Count > 0) {
                     var iterationEvent = theEvents[0];

                     if (pastEvents.Value.TryGetValue(iterationEvent.Type.ToString(), out DateTime lastEventId) && iterationEvent.Id <= lastEventId) {
                        // We already processed this event or we haven't but we already processed a newer one.
                        break;
                     }

                     if (statesMap.All.TryGetValue(iterationState, out IState exitState)) {
                        var transition = exitState.GetActivatedTransition(iterationEvent);
                        if (transition != null) {

                           if (transition.TargetState != null && transition.TargetState != iterationState) {
                              var exitEvents = await exitState.ExitActionAsync(iterationEvent, this);
                              if (exitEvents != null) {
                                 theEvents.AddRange(exitEvents);
                              }
                           }

                           var chainEvents = await transition.Action(iterationEvent, this);
                           if (chainEvents != null) {
                              theEvents.AddRange(chainEvents);
                           }

                           if (transition.TargetState != null && transition.TargetState != iterationState) {
                              if (statesMap.All.TryGetValue(transition.TargetState, out IState entryState)) {
                                 var entryEvents = await entryState.EntryActionAsync(iterationEvent, this);
                                 iterationState = transition.TargetState;
                                 pastEvents.Value[iterationEvent.Type.ToString()] = iterationEvent.Id;

                                 if (statesMap.Finals.Contains(iterationState)) {
                                    break;
                                 }
                                 if (entryEvents != null) {
                                    theEvents.AddRange(entryEvents);
                                 }
                              } else {
                                 //throw
                              }
                           }

                           theEvents.RemoveAt(0);

                        } else {
                           // throw
                        }
                     } else {
                        // throw
                     }
                  }

                  await StateManager.SetStateAsync<string>(CurrentStateLabel, iterationState);
                  await StateManager.SetStateAsync<Dictionary<string, DateTime>>(PastEventsLabel, pastEvents.Value);
               } else {
                  // throw
               }
            } else {
               // throw
            }
         } else {
            // throw
         }
      }

   }
}