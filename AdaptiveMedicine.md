# AdaptiveMedicine

## TODO:

### General
   + Add behaviour to the Uninitialize state as an entry action that would delete the actor.

### Experiment
   + When adding a new patient, think about waiting until everything is initialized before adding another one. This is to have no problems with the NetworkManager. On the other hand, that can be a huge bootleneck so maybe design it as to allow this transitional behavior.

### Statechart
   + Check if the transition methods can be defined as extension methods? (Or at least to bind "this").
   + Consider two other implementations of the StatechartActor:
      1. States defined in a way as to allow setting the transitions as extension methods (for a more elegant "this" binding).
      2. Transitions specified inside the constructor.
   + Statecharts without an initial state not allowed.
   + Think about adding hierarchy capabilities to the statechart. (This simplifies specifying transitions)
   + Move the OnActivate code to another function and then call it both during the activation or during a DispatchEvent call if necessary.