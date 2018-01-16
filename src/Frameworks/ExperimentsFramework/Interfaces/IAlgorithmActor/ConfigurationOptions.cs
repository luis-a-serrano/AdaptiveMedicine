using System.Collections.Generic;

namespace AdaptiveMedicine.Experiments.AlgorithmActor {
   public class ConfigurationOptions {
      public int Order { get; set; }
      public bool IsCooperating { get; set; }
      public IDictionary<string, double> Constants { get; set; }
   }
}
