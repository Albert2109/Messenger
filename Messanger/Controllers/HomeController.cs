using Microsoft.AspNetCore.Mvc;

namespace Messanger.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
