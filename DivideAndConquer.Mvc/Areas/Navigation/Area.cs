using System;
using System.Web.Mvc;

namespace DivideAndConquer.Areas.Home
{
  public class Area : AreaRegistration
  {
    public override void RegisterArea( AreaRegistrationContext context )
    {
      if( context == null )
      {
        throw new ArgumentNullException("context");
      }

      context.MapRoute(
        "Navigation_default",
        "Navigation/{controller}/{action}/{id}",
        new
          {
            controller = "Home",
            action = "Index",
            id = UrlParameter.Optional
          },
        namespaces: new[] { "DivideAndConquer.AreaNames.Navigation.Controllers" }
        );
    }

    public override string AreaName
    {
      get
      {
        return AreaNames.Navigation;
      }
    }
  }
}