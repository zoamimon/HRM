using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Web.Controllers
{
    [Authorize]
    public abstract class BaseController : Controller
    {
        protected readonly IHttpClientFactory _httpClientFactory;

        protected BaseController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        protected HttpClient GetClient()
        {
            return _httpClientFactory.CreateClient("HRMApi");
        }
    }
}
