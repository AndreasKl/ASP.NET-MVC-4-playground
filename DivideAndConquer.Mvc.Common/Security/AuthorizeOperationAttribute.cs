using System;
using System.Globalization;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace DivideAndConquer.Mvc.Security
{
  /// <summary>
  ///   <see cref="IAuthorizationFilter" /> that validates if a user is permitted to execute an operation,
  ///   if the user is not authenticated a
  ///   Central authorization concept for Mvc operations, an Mvc action can compose its name
  ///   and then ask a central security authority if the authenticated user is permitted to
  ///   execute this operation.
  /// </summary>
  [AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    Inherited = true,
    AllowMultiple = true )]
  public sealed class AuthorizeOperationAttribute : FilterAttribute, IAuthorizationFilter
  {
    /// <summary>
    ///   DI: Sets the user context accessor.
    /// </summary>
    /// <value>
    ///   The user context accessor.
    /// </value>
    public IUserContextAccessor UserContextAccessor
    {
      private get;
      set;
    }

    /// <summary>
    ///   DI: Sets the operation access validator.
    /// </summary>
    /// <value>
    ///   The operation access validator.
    /// </value>
    public IOperationAccessValidator OperationAccessValidator
    {
      private get;
      set;
    }

    /// <summary>
    ///   Gets or sets the operation to authorize.
    ///   Providing a value would override the value calculated based on Mvc action data. This
    ///   should rarely be needed e.g. for a general use search dialog.
    /// </summary>
    /// <value>
    ///   The operation.
    /// </value>
    public string Operation
    {
      get;
      set;
    }

    public void OnAuthorization( AuthorizationContext filterContext )
    {
      if( filterContext == null )
      {
        throw new ArgumentNullException( "filterContext" );
      }

      if( UserContextAccessor == null )
      {
        throw new InvalidOperationException(
          "The UserContextAccessor was not set via property injection. " +
          "Are you sure that your DI container can inject in action filter attributes?" );
      }

      if( OperationAccessValidator == null )
      {
        throw new InvalidOperationException(
          "The OperationAccessValidator was not set via property injection. " +
          "Are you sure that your DI container can inject in action filter attributes?" );
      }

      if( OutputCacheAttribute.IsChildActionCacheActive( filterContext ) )
      {
        // If a child action cache block is active, we need to fail immediately, even if authorization
        // would have succeeded. The reason is that there's no way to hook a callback to rerun 
        // authorization before the fragment is served from the cache, so we can't guarantee that this 
        // filter will be re-run on subsequent requests.
        throw new InvalidOperationException( "AuthorizeAttribute cannot be used within a child action caching block." );
      }

      string operation = GetOperationDescriptor( filterContext );

      AuthorizeResult authorizeResult = AuthorizeCore( filterContext.HttpContext, operation );
      if( authorizeResult.HasAccess )
      {
        // ** IMPORTANT ** 
        // Since we're performing authorization at the action level, the authorization code runs
        // after the output caching module. In the worst case this could allow an authorized user 
        // to cause the page to be cached, then an unauthorized user would later be served the 
        // cached page. We work around this by telling proxies not to cache the sensitive page,
        // then we hook our custom authorization code into the caching mechanism so that we have 
        // the final say on whether a page should be served from the cache.

        HttpCachePolicyBase cachePolicy = filterContext.HttpContext.Response.Cache;
        cachePolicy.SetProxyMaxAge( new TimeSpan( 0 ) );
        cachePolicy.AddValidationCallback( CacheValidateHandler, operation );
      }
      else
      {
        HandleUnauthorizedRequest( filterContext, authorizeResult );
      }
    }

    /// <summary>
    ///   Build up a string based on the action and relevant arguments, if
    ///   the <code>Operation</code> field is set the value is used.
    /// </summary>
    /// <param name="filterContext"></param>
    /// <returns></returns>
    private string GetOperationDescriptor( ControllerContext filterContext )
    {
      if( !string.IsNullOrEmpty( Operation ) )
      {
        return Operation;
      }

      // Obtain a fully qualified name for the called action.
      string controllerName = filterContext.Controller.GetType().FullName;
      string httpMethod = filterContext.HttpContext.Request.HttpMethod;
      string action = filterContext.RouteData.Values[ "action" ] as string;

      string operation =
        string.Format(
          CultureInfo.InvariantCulture,
          "{0}|{1}|{2}",
          controllerName,
          action,
          httpMethod );

      return operation;
    }

    /// <summary>
    ///   This method must be thread-safe since it is called by the thread-safe OnCacheAuthorization() method.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="operation">The operation to authorize.</param>
    /// <returns></returns>
    private AuthorizeResult AuthorizeCore( HttpContextBase httpContext, string operation )
    {
      if( httpContext == null )
      {
        throw new ArgumentNullException( "httpContext" );
      }

      IUserContext userContext = UserContextAccessor.Current;
      if( !userContext.IsAuthenticated )
      {
        return new AuthorizeResult( false, HttpStatusCode.Unauthorized );
      }

      // Check if the operation that is called is permitted for the current user.
      if( !OperationAccessValidator.HasPermission( userContext, operation ) )
      {
        return new AuthorizeResult( false, HttpStatusCode.Forbidden );
      }
      return AuthorizeResult.Authorized;
    }

    private void CacheValidateHandler( HttpContext context, object data, ref HttpValidationStatus validationStatus )
    {
      validationStatus = OnCacheAuthorization( new HttpContextWrapper( context ), data );
    }

    /// <summary>
    ///   This method must be thread-safe since it is called by the caching module.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="data">The data.</param>
    /// <returns></returns>
    private HttpValidationStatus OnCacheAuthorization( HttpContextBase httpContext, object data )
    {
      if( httpContext == null )
      {
        throw new ArgumentNullException( "httpContext" );
      }
      var operation = data as string;
      if( operation == null )
      {
        throw new ArgumentNullException( "data" );
      }

      AuthorizeResult isAuthorized = AuthorizeCore( httpContext, operation );
      if( isAuthorized.HasAccess )
      {
        return HttpValidationStatus.Valid;
      }
      return HttpValidationStatus.IgnoreThisRequest;
    }

    private void HandleUnauthorizedRequest( AuthorizationContext filterContext, AuthorizeResult authorizeResult )
    {
      filterContext.Result = new HttpStatusCodeResult( authorizeResult.StatusCodeAsInt );
    }

    private class AuthorizeResult
    {
      public static readonly AuthorizeResult Authorized = new AuthorizeResult();
      private readonly bool m_HasAccess;
      private readonly HttpStatusCode m_StatusCode;

      private AuthorizeResult()
      {
        m_HasAccess = true;
      }

      public AuthorizeResult( bool hasAccess, HttpStatusCode statusCode )
      {
        m_HasAccess = hasAccess;
        m_StatusCode = statusCode;
      }

      public int StatusCodeAsInt
      {
        get
        {
          return (int) m_StatusCode;
        }
      }

      public bool HasAccess
      {
        get
        {
          return m_HasAccess;
        }
      }
    }
  }
}