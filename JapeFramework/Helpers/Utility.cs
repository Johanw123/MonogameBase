using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JapeFramework.Helpers
{
  public class Utility
  {
    private static HashSet<int> CalledFunctions = [];

    public static void CallOnce(Action action)
    {
      if (CalledFunctions.Add(action.Method.GetHashCode()))
      {
        action?.Invoke();
      }
    }
  }
}
