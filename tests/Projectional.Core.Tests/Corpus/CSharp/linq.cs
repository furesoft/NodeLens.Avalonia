using System.Collections.Generic;
using System.Linq;

namespace Sample
{
  public class Linq
  {
    public IEnumerable<int> Positive(IEnumerable<int> values)
    {
      return values.Where((item) => item > 0);
    }

    public int Total(IEnumerable<int> values)
    {
      return values.Aggregate((left, right) => left + right);
    }

    public IEnumerable<int> Ordered(IEnumerable<int> values)
    {
      return from item in values
        where item > 0
        orderby item descending
        select item;
    }
  }
}
