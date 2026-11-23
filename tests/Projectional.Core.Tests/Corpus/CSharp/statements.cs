namespace Sample
{
  public class Statements
  {
    private int seen;

    public Statements(int seen)
    {
      this.seen = seen;
    }

    public int Seen
    {
      get
      {
        return seen;
      }
    }

    public int Decide(int amount)
    {
      if (amount > 100)
      {
        return 1;
      }
      else
      {
        if (amount > 10)
        {
          return 2;
        }
        else
        {
          return 0;
        }
      }
    }

    public void Count(int count)
    {
      for (int i = 0; i < count; i++)
      {
        if (i == 2)
        {
          continue;
        }
      }
    }

    public void Walk(int[] values)
    {
      int index = 0;
      while (index < values.Length)
      {
        index++;
      }
    }

    public string Classify(int value)
    {
      switch (value)
      {
        case 1:
          return "one";
        default:
          return "many";
      }
    }
  }
}
