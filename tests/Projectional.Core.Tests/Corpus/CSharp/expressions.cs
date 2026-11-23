namespace Sample
{
  public class Expressions
  {
    public int Precedence(int amount)
    {
      return amount + 10 * 2;
    }

    public int Grouped(int amount)
    {
      return (amount + 10) * 2;
    }

    public bool Logic(bool flag, int amount)
    {
      return flag && amount > 10;
    }

    public int Choose(bool flag, int amount)
    {
      return flag ? amount : -amount;
    }

    public int Cast(double value)
    {
      return (int)value;
    }

    public bool Test(object value)
    {
      return value is string;
    }

    public int[] Build(int first)
    {
      return [first, 2];
    }

    public int First(int[] values)
    {
      return values[0];
    }
  }
}
