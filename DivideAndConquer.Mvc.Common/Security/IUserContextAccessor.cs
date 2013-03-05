namespace DivideAndConquer.Mvc.Security
{
  public interface IUserContextAccessor
  {
    IUserContext Current
    {
      get;
    }
  }

  public interface IUserContext
  {
    bool IsAuthenticated
    {
      get;
    }
  }
}