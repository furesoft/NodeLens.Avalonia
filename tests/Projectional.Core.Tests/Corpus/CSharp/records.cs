namespace Sample
{
  public record Point(int X, int Y);

  public interface IMeasured
  {
    public int Measure();
  }

  public record Segment(int X, int Y) : IMeasured
  {
    public int Measure()
    {
      return X;
    }
  }
}
