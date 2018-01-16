using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AdaptiveMedicine.Common.Utilities {
   public static class DictionaryManagement {

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static valueType GetValueOrProvided<keyType, valueType>(this IDictionary<keyType, valueType> dictionary, keyType key, valueType provided) {
         return (dictionary.TryGetValue(key, out valueType value) ? value : provided);
      }
   }
}
