using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;

using Microsoft.AspNet.Identity;

namespace ClassicCars.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private const String CONNECTION_String = "server=127.0.0.1;port=3306;username=root;password='';database=fyp;"; // MySQL connection String

        /* START Ratings */

        // GET: All Ratings
        public ActionResult AllRatings()
        {
            return View();
        }

        // GET: New Ratings
        public ActionResult NewRatings()
        {
            return View();
        }

        // GET: Manage Ratings
        public ActionResult ManageRatings()
        {
            return View();
        }

        /* END Ratings */

        /* START Cars */

        // GET: New Car
        public ActionResult NewCar()
        {
            return View();
        }

        // GET: Manage Cars
        public ActionResult ManageCars()
        {
            return View();
        }

        // GET: Manage Car
        public ActionResult ManageCar()
        {
            return View();
        }

        /* END Cars */

        /* START User Cars */

        // GET: New User Car
        public ActionResult NewUserCar()
        {
            return View();
        }

        // GET: Manage User Cars
        public ActionResult ManageUserCars()
        {
            return View();
        }

        // GET: View User Car
        public ActionResult ViewUserCar()
        {
            return View();
        }

        // GET: Browse Cars
        public ActionResult BrowseCars()
        {
            return View();
        }

        /* END User Cars */

        /* START Swap Requests */

        // GET: New Swap Request
        public ActionResult NewSwapRequest()
        {
            return View();
        }

        // GET: Manage Swap Requests
        public ActionResult ManageSwapRequests()
        {
            return View();
        }

        /* END Swap Requests */

        /* START Miscellaneous */

        // GET: Home
        [AllowAnonymous]
        public ActionResult Index()
        {
            return View();
        }

        // GET: Recommender Demo
        [AllowAnonymous]
        public ActionResult RecommenderDemo()
        {
            // it's just for unregistered users...
            if (!Request.IsAuthenticated)
                return View();

            return RedirectToAction("Index");
        }

        /* END Miscellaneous */

        /* START Test */

        // GET: Credits Test
        public ActionResult CreditsTest()
        {
            return View();
        }

        /* END Test */
    }
}