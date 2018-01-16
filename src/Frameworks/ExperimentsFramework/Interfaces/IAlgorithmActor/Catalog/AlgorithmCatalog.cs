using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using AdaptiveMedicine.Experiments.Actors.ServiceNames;
using AdaptiveMedicine.Experiments.AlgorithmActor.Attributes;

namespace AdaptiveMedicine.Experiments.AlgorithmActor {

   public enum AlgorithmCatalog {
      [ServiceName(NLMSAlgorithmService.Name)] NLMS
   }

   public static class Extensions {

      private static readonly ConcurrentDictionary<string, string> _ServiceNames = new ConcurrentDictionary<string, string>();

      public static string GetServiceName(this string algorithm) {

         if (!_ServiceNames.TryGetValue(algorithm, out string serviceName)) {

            var memberInfo = typeof(AlgorithmCatalog).GetMember(algorithm).FirstOrDefault();
            if (memberInfo != null) {

               var serviceNameAttribute = (ServiceNameAttribute)memberInfo.GetCustomAttributes(typeof(ServiceNameAttribute), false).FirstOrDefault();
               if (serviceNameAttribute != null && !String.IsNullOrWhiteSpace(serviceNameAttribute.Name)) {

                  serviceName = serviceNameAttribute.Name;
                  _ServiceNames.GetOrAdd(algorithm, serviceName);

               } else {
                  // throw
               }
            } else {
               // throw
            }
         }

         return serviceName;
      }
   }
}
