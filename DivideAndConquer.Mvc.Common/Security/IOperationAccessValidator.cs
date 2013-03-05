namespace DivideAndConquer.Mvc.Security
{
  public interface IOperationAccessValidator
  {
    bool HasPermission( IUserContext userContext, string operation );
  }
}