using EosDataScraper.Api.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EosDataScraper.Api.Attributes
{
    public class ExportModelStateAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            if (!filterContext.ModelState.IsValid && filterContext.ActionDescriptor is ControllerActionDescriptor cad)
            {
                if (filterContext.Result is RedirectResult
                    || filterContext.Result is RedirectToRouteResult
                    || filterContext.Result is RedirectToActionResult)
                {
                    var modelState = ModelStateHelpers.SerializeModelState(filterContext.ModelState);
                    filterContext.HttpContext.Session.SetString(cad.ActionName, modelState);
                }
            }

            base.OnActionExecuted(filterContext);
        }
    }
}
