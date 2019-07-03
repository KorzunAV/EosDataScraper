using EosDataScraper.Api.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EosDataScraper.Api.Attributes
{
    public class ImportModelStateAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            if (filterContext.Result is ViewResult && filterContext.ActionDescriptor is ControllerActionDescriptor cad)
            {
                var json = filterContext.HttpContext.Session.GetString(cad.ActionName);
                if (!string.IsNullOrEmpty(json))
                {
                    var modelState = ModelStateHelpers.DeserializeModelState(json);
                    filterContext.ModelState.Merge(modelState);
                    filterContext.HttpContext.Session.Remove(cad.ActionName);
                }
            }

            base.OnActionExecuted(filterContext);
        }
    }
}
