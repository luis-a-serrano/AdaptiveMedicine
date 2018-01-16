using System;

namespace AdaptiveMedicine.Common.Statechart.Attributes {
   [AttributeUsage(
         AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
   public class FinalStateAttribute: StateAttribute {

      public FinalStateAttribute(object stateType)
         : base(stateType) {
      }
   }
}
