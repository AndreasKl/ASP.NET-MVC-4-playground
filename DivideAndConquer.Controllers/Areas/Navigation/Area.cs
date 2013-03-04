using System.Web.Mvc;

namespace DivideAndConquer.Areas.Home
{
  public class Area : AreaRegistration
  {
    public override void RegisterArea( AreaRegistrationContext context )
    {
      context.MapRoute(
        "Navigation_default",
        "Navigation/{controller}/{action}/{id}",
        new
          {
            controller = "Home",
            action = "Index",
            id = UrlParameter.Optional
          },
        namespaces: new[] { "DivideAndConquer.Areas.Navigation.Controllers" }
        );
    }

    public override string AreaName
    {
      get
      {
        return "Navigation";
      }
    }
  }
}