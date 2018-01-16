using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AdaptiveMedicine.Common.Actors;
using AdaptiveMedicine.Common.Statechart.Attributes;
using AdaptiveMedicine.Common.Statechart.Interfaces;
using AdaptiveMedicine.Common.Utilities;
using AdaptiveMedicine.Experiments.Actors.Interfaces;
using AdaptiveMedicine.Experiments.Actors.ServiceNames;
using AdaptiveMedicine.Experiments.ExperimentManagerActor;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace AdaptiveMedicine.Experiments.Actors {
   using ConfigOptions = Experiments.ExperimentManagerActor.ConfigurationOptions;
   using ModelsConfigOptions = Experiments.ModelManagerActor.ConfigurationOptions;

   [ActorService(Name = ExperimentManagerService.Name)]
   [StatePersistence(StatePersistence.Persisted)]
   internal class ExperimentManagerActor: StatechartActor, IExperimentManagerActor {
      public const string ConfigurationLabel = "Experiment.Configuration";
      public const string ParticipantPrefix = "Experiment.Participant:";

      public ExperimentManagerActor(ActorService actorService, ActorId actorId)
          : base(actorService, actorId) {
      }

      protected override Task OnActivateAsync() {
         ActorEventSource.Current.ActorMessage(this, "Actor activated.");
         return base.OnActivateAsync();
      }

      public async Task<bool> ConfigurateAsync(DateTime timeStamp, ConfigOptions config) {
         await DispatchEventAsync(new StatechartEvent(Events.Initialize, timeStamp, config));
         return true;
      }

      public async Task<bool> AddParticipantAsync(DateTime timeStamp, ParticipantDetails participant) {
         await DispatchEventAsync(new StatechartEvent(Events.AddParticipant, timeStamp, participant));
         return true;
      }

      /* Statechart Events & States */
      enum Events { Initialize, Delete, AddParticipant, Error, Reset }
      enum States { Uninitialized, Initialized, Illegal }


      [InitialState(States.Uninitialized)]
      private sealed class Uninitialized: StatechartState {
         #region Singleton Pattern
         private static readonly Lazy<Uninitialized> _Lazy = new Lazy<Uninitialized>(() => new Uninitialized());
         public static Uninitialized Instance { get { return _Lazy.Value; } }
         private Uninitialized() : base() { }
         #endregion

         [Transition(Events.Initialize, States.Initialized)]
         public async Task<IEnumerable<IEvent>> SetConfigurationAsync(IEvent anEvent, Actor actor) {
            var forwardedEvents = new List<IEvent>();

            var config = anEvent.Input as ConfigOptions;
            if (config != null) {
               await actor.StateManager.SetStateAsync<ConfigOptions>(ConfigurationLabel, config);
            } else {
               forwardedEvents.Add(new StatechartEvent(Events.Error, anEvent.Id));
            }
            
            return forwardedEvents;
         }

         [Transition(Events.Delete)]
         [Transition(Events.AddParticipant, States.Illegal)]
         [Transition(Events.Error, States.Illegal)]
         [Transition(Events.Reset, States.Illegal)]
         public Task<IEnumerable<IEvent>> DoNothingAsync(IEvent anEvent, Actor actor) {
            return Task.FromResult<IEnumerable<IEvent>>(null);
         }
      }

      [State(States.Initialized)]
      private sealed class Initialized: StatechartState {
         #region Singleton Pattern
         private static readonly Lazy<Initialized> _Lazy = new Lazy<Initialized>(() => new Initialized());
         public static Initialized Instance { get { return _Lazy.Value; } }
         private Initialized() : base() { }
         #endregion

         [Transition(Events.Initialize, States.Illegal)]
         [Transition(Events.Delete, States.Uninitialized)]
         [Transition(Events.Error, States.Illegal)]
         [Transition(Events.Reset, States.Illegal)]
         public Task<IEnumerable<IEvent>> DoNothingAsync(IEvent anEvent, Actor actor) {
            return Task.FromResult<IEnumerable<IEvent>>(null);
         }

         [Transition(Events.AddParticipant)]
         public async Task<IEnumerable<IEvent>> AddParticipantAsync(IEvent anEvent, Actor actor) {
            var forwardedEvents = new List<IEvent>();
            var config = await actor.StateManager.TryGetStateAsync<ConfigOptions>(ConfigurationLabel);

            if (config.HasValue) {
               var participant = anEvent.Input as ParticipantDetails;

               if (participant != null) {
                  var participationKey = ParticipantPrefix + participant.Id;
                  var participationDetails = await actor.StateManager.TryGetStateAsync<bool>(participationKey);

                  if (!participationDetails.HasValue) {
                     var modelsInfo = new List<ModelsConfigOptions.ModelInfo>();
                     foreach (var modelInfo in config.Value.ModelsInfo) {
                        modelsInfo.Add(new ModelsConfigOptions.ModelInfo {
                           Order = modelInfo.Order,
                           Algorithm = modelInfo.Algorithm
                        });
                     }
                     var modelsConfig = new ModelsConfigOptions { ModelsInfo = modelsInfo };

                     var participantActorId = new ActorId($"{actor.Id}:{participant.Id}");
                     var configuringManagers = new Task<bool>[] {
                        ActorProxy.Create<IModelManagerActor>(participantActorId, ModelManagerService.Name.ToServiceUri())
                           .ConfigurateAsync(anEvent.Id, modelsConfig),
                        ActorProxy.Create<INetworkManagerActor>(participantActorId, NetworkManagerService.Name.ToServiceUri())
                           .ConfigureModelsAsync(),
                        ActorProxy.Create<IPerformanceManagerActor>(participantActorId, PerformanceManagerService.Name.ToServiceUri())
                           .ConfigureModelsAsync()
                     };

                     await Task.WhenAll(configuringManagers);
                     await actor.StateManager.SetStateAsync<bool>(participationKey, true);

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
         [Transition(Events.Delete, States.Uninitialized)]
         [Transition(Events.AddParticipant, States.Uninitialized)]
         [Transition(Events.Error, States.Uninitialized)]
         [Transition(Events.Reset, States.Uninitialized)]
         public Task<IEnumerable<IEvent>> DoNothingAsync(IEvent anEvent, Actor actor) {
            return Task.FromResult<IEnumerable<IEvent>>(null);
         }
      }

   }
}
