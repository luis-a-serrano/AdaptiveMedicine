using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AdaptiveMedicine.Common.Actors;
using AdaptiveMedicine.Common.Statechart.Attributes;
using AdaptiveMedicine.Common.Statechart.Interfaces;
using AdaptiveMedicine.Experiments.Actors.Interfaces;
using AdaptiveMedicine.Experiments.Actors.ServiceNames;
using AdaptiveMedicine.Experiments.AlgorithmActor;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using static AdaptiveMedicine.Common.Utilities.DictionaryManagement;

namespace AdaptiveMedicine.Experiments.Actors {
   [ActorService(Name = NLMSAlgorithmService.Name)]
   [StatePersistence(StatePersistence.Persisted)]
   internal class NLMSAlgorithmActor: StatechartActor, IAlgorithmActor {
      public const string ConfigurationLabel = "Algorithm.Configuration";

      public const string ParametersLabel = "Algorithm.Parameters";
      public const double ParametersDefaultForSingle = 0.0;

      public const string PastValuesLabel = "Algorithm.PastValues";
      public const double PastValuesDefaultForSingle = 0.0;

      public const string NormalizerLabel = "Algorithm.Normalizer";
      public const double NormalizerDefault = 0.0;

      public const string StepLabel = "Algorithm.Step";
      public const double StepDefault = 0.0;

      public NLMSAlgorithmActor(ActorService actorService, ActorId actorId)
          : base(actorService, actorId) {
      }

      protected override Task OnActivateAsync() {
         ActorEventSource.Current.ActorMessage(this, "Actor activated.");
         return base.OnActivateAsync();
      }

      public async Task<bool> ConfigurateAsync(DateTime timeStamp, ConfigurationOptions config) {
         await DispatchEventAsync(new StatechartEvent(Events.Initialize, timeStamp, config));
         return true;
      }

      public Task<bool> ProcessNewSignalAsync(DateTime timeStamp, double value) {
         return Task.FromResult(true);
      }

      /* Statechart Events & States */
      enum Events { Initialize, Delete, NewSignal, OthersSignals, ForegoSignals, Error, Reset }
      enum States { Uninitialized, Ready, Waiting, Illegal }
      ////////////////// Make sure all states react to all events
      // Check that all the states and transitions are correctly doing what they need to

      [InitialState(States.Uninitialized)]
      private sealed class Uninitialized: StatechartState {
         #region Singleton Pattern
         private static readonly Lazy<Uninitialized> _Lazy = new Lazy<Uninitialized>(() => new Uninitialized());
         public static Uninitialized Instance { get { return _Lazy.Value; } }
         private Uninitialized() : base() { }
         #endregion

         [Transition(Events.Initialize, States.Ready)]
         public async Task<IEnumerable<IEvent>> SetConfigurationAsync(IEvent anEvent, Actor actor) {
            var forwardedEvents = new List<IEvent>();

            var config = anEvent.Input as ConfigurationOptions;
            if (config != null) {

               if (!config.Constants.ContainsKey(NormalizerLabel)) {
                  config.Constants[NormalizerLabel] = NormalizerDefault;
               }
               if (!config.Constants.ContainsKey(StepLabel)) {
                  config.Constants[StepLabel] = StepDefault;
               }
               await actor.StateManager.SetStateAsync<ConfigurationOptions>(ConfigurationLabel, config);

               var parameters = new double[config.Order];
               for (int l = 0; l < parameters.Length; l++) {
                  parameters[l] = ParametersDefaultForSingle;
               }
               await actor.StateManager.SetStateAsync<double[]>(ParametersLabel, parameters);

               var pastValues = new double[config.Order];
               for (int l = 0; l < pastValues.Length; l++) {
                  pastValues[l] = PastValuesDefaultForSingle;
               }
               await actor.StateManager.SetStateAsync<double[]>(PastValuesLabel, pastValues);
            } else {
               forwardedEvents.Add(new StatechartEvent(Events.Error, anEvent.Id));
            }

            return forwardedEvents;
         }

         [Transition(Events.Delete)]
         [Transition(Events.NewSignal, States.Illegal)]
         [Transition(Events.OthersSignals, States.Illegal)]
         [Transition(Events.ForegoSignals, States.Illegal)]
         [Transition(Events.Error, States.Illegal)]
         [Transition(Events.Reset, States.Illegal)]
         public Task<IEnumerable<IEvent>> DoNothingAsync(IEvent anEvent, Actor actor) {
            return Task.FromResult<IEnumerable<IEvent>>(null);
         }
      }

      [State(States.Ready)]
      private sealed class Ready: StatechartState {
         #region Singleton Pattern
         private static readonly Lazy<Ready> _Lazy = new Lazy<Ready>(() => new Ready());
         public static Ready Instance { get { return _Lazy.Value; } }
         private Ready() : base() { }
         #endregion

         [Transition(Events.Delete, States.Uninitialized)]
         [Transition(Events.Initialize, States.Illegal)]
         [Transition(Events.OthersSignals, States.Illegal)]
         [Transition(Events.ForegoSignals, States.Illegal)]
         [Transition(Events.Error, States.Illegal)]
         [Transition(Events.Reset, States.Illegal)]
         public Task<IEnumerable<IEvent>> DoNothingAsync(IEvent anEvent, Actor actor) {
            return Task.FromResult<IEnumerable<IEvent>>(null);
         }

         [Transition(Events.NewSignal, States.Waiting)]
         public async Task<IEnumerable<IEvent>> SignalProcessingAsync(IEvent anEvent, Actor actor) {
            var forwardedEvents = new List<IEvent>();
            var config = await actor.StateManager.TryGetStateAsync<ConfigurationOptions>(ConfigurationLabel);
            if (config.HasValue) {
               var parameters = await actor.StateManager.TryGetStateAsync<double[]>(ParametersLabel);
               if (parameters.HasValue) {
                  var pastValues = await actor.StateManager.TryGetStateAsync<double[]>(PastValuesLabel);
                  if (pastValues.HasValue) {

                     var signal = (double) anEvent.Input;
                     var estimatedSignal = 0.0;
                     var normalizedStep = config.Value.Constants[NormalizerLabel];

                     for (int l = 0; l < parameters.Value.Length; l++) {
                        estimatedSignal += parameters.Value[l] * pastValues.Value[l];
                        normalizedStep += pastValues.Value[l] * pastValues.Value[l];
                     }

                     normalizedStep = config.Value.Constants[StepLabel] / normalizedStep;
                     var error = signal - estimatedSignal;

                     var updatedParams = new double[parameters.Value.Length];
                     for (int l = 0; l < updatedParams.Length; l++) {
                        updatedParams[l] = parameters.Value[l] + (normalizedStep * error * pastValues.Value[l]);
                     }

                     var updatedValues = new double[pastValues.Value.Length];
                     for (int l = updatedValues.Length - 1; l >= 1; l++) {
                        updatedValues[l] = pastValues.Value[l - 1];
                     }
                     updatedValues[0] = signal;

                     await actor.StateManager.SetStateAsync<double[]>(ParametersLabel, updatedParams);
                     await actor.StateManager.SetStateAsync<double[]>(PastValuesLabel, updatedValues);

                     if (!config.Value.IsCooperating) {
                        forwardedEvents.Add(new StatechartEvent(Events.ForegoSignals, anEvent.Id));
                     } else {
                        // Send parameters to network manager.
                     }

                  } else {
                     forwardedEvents.Add(new StatechartEvent(Events.Error, anEvent.Id));
                  }
               } else {
                  forwardedEvents.Add(new StatechartEvent(Events.Error, anEvent.Id));
               }
            } else {
               forwardedEvents.Add(new StatechartEvent(Events.Error, anEvent.Id));
            }

            return forwardedEvents;
         }
      }

      [State(States.Waiting)]
      private sealed class Waiting: StatechartState {
         #region Singleton Pattern
         private static readonly Lazy<Waiting> _Lazy = new Lazy<Waiting>(() => new Waiting());
         public static Waiting Instance { get { return _Lazy.Value; } }
         private Waiting() : base() { }
         #endregion

         [Transition(Events.ForegoSignals, States.Ready)]
         [Transition(Events.Delete, States.Uninitialized)]
         [Transition(Events.Initialize, States.Illegal)]
         [Transition(Events.NewSignal, States.Illegal)]
         [Transition(Events.Error, States.Illegal)]
         [Transition(Events.Reset, States.Illegal)]
         public Task<IEnumerable<IEvent>> DoNothingAsync(IEvent anEvent, Actor actor) {
            return Task.FromResult<IEnumerable<IEvent>>(null);
         }

         [Transition(Events.OthersSignals, States.Ready)]
         public async Task<IEnumerable<IEvent>> ApplyCooperationAsync(IEvent anEvent, Actor actor) {
            var forwardedEvents = new List<IEvent>();
            var neighborParams = anEvent.Input as double[][];
            if (neighborParams != null) {
               if (neighborParams.Length > 0) {

                  var updatedParams = new double[neighborParams[0].Length];
                  foreach (var singleNeighbor in neighborParams) {
                     for (var l = 0; l < updatedParams.Length; l++) {
                        updatedParams[l] += singleNeighbor[l];
                     }
                  }
                  for (var l = 0; l < updatedParams.Length; l++) {
                     updatedParams[l] /= neighborParams.Length;
                  }

                  await actor.StateManager.SetStateAsync<double[]>(ParametersLabel, updatedParams);

               } else {
                  forwardedEvents.Add(new StatechartEvent(Events.Error, anEvent.Id));
               }
            } else {
               forwardedEvents.Add(new StatechartEvent(Events.Error, anEvent.Id));
            }

            return forwardedEvents;
         }
      }

      [State(States.Illegal)]
      private sealed class Illegal: StatechartState {
         #region Singleton Pattern
         private static readonly Lazy<Illegal> _Lazy = new Lazy<Illegal>(() => new Illegal());
         public static Illegal Instance { get { return _Lazy.Value; } }
         private Illegal() : base() { }
         #endregion

         public override Task<IEnumerable<IEvent>> EntryActionAsync(IEvent anEvent, Actor actor) {
            return Task.FromResult<IEnumerable<IEvent>>(
               new IEvent[] {
                  new StatechartEvent(Events.Reset, anEvent.Id)});
         }

         [Transition(Events.Initialize, States.Uninitialized)]
         [Transition(Events.NewSignal, States.Uninitialized)]
         [Transition(Events.OthersSignals, States.Uninitialized)]
         [Transition(Events.ForegoSignals, States.Uninitialized)]
         [Transition(Events.Delete, States.Uninitialized)]
         [Transition(Events.Error, States.Uninitialized)]
         [Transition(Events.Reset, States.Uninitialized)]
         public Task<IEnumerable<IEvent>> DoNothingAsync(IEvent anEvent, Actor actor) {
            return Task.FromResult<IEnumerable<IEvent>>(null);
         }
      }

   }
}
