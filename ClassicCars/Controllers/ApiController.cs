using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Xml;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

using Newtonsoft.Json.Linq;

using NReco.CF.Taste.Common;
using NReco.CF.Taste.Impl.Common;
using NReco.CF.Taste.Impl.Model;
using NReco.CF.Taste.Model;
using NReco.CF.Taste.Impl.Eval;
using NReco.CF.Taste.Eval;
using NReco.CF.Taste.Impl.Similarity;
using NReco.CF.Taste.Impl.Neighborhood;
using NReco.CF.Taste.Impl.Recommender;
using NReco.CF.Taste.Impl.Recommender.SVD;
using NReco.CF.Taste.Recommender;

using Microsoft.AspNet.Identity;

using MySql.Data.MySqlClient;

using ClassicCars.Models;

namespace ClassicCars.Controllers
{
    [Authorize]
    public class apiController : Controller
    {
        private const String CONNECTION_STRING = "server=127.0.0.1;port=3306;username=root;password='';database=fyp;"; // MySQL connection String
        private const int MAX_PREFERENCES = 500; // maximum number of preferences to parse per user

        static IDataModel dataModel; // the recommender data model

        /* START Ratings */

        // GET: All Ratings [JSON]
        [HttpGet]
        public ActionResult Ratings()
        {
            // return ratings that belong to the user
            List<Rating> ratings = new List<Rating>();
            int userId = int.Parse(User.Identity.GetUserId());
            const String QUERY_STRING = "SELECT * FROM Ratings WHERE userId = @userId";

            using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
            {
                MySqlCommand cmd = new MySqlCommand(QUERY_STRING, connection);
                cmd.Parameters.AddWithValue("@userId", userId);

                connection.Open();

                MySqlDataReader reader = cmd.ExecuteReader();

                try
                {
                    while (reader.Read())
                    {
                        ratings.Add(
                            new Rating(
                                int.Parse(reader["id"].ToString().Trim()),
                                int.Parse(reader["carId"].ToString().Trim()),
                                int.Parse(reader["userId"].ToString().Trim()),
                                reader["carTitle"].ToString().Trim(),
                                int.Parse(reader["stars"].ToString().Trim())
                            )
                        );
                    }
                }
                finally
                {
                    reader.Close();
                    connection.Close();
                }
            }

            return Json(ratings, JsonRequestBehavior.AllowGet);
        }

        // POST: Create Rating
        [HttpPost]
        public ActionResult CreateRating(String json)
        {
            dynamic rating = JObject.Parse(json);

            try
            {
                int stars = rating.rating;

                if (stars < 1 || stars > 5)
                {
                    return Json(new JSONResult("Ratings must be integers between 1 and 5 inclusive!"), JsonRequestBehavior.AllowGet);
                }

                int carId = rating.carId;

                if (carId == 0)
                {
                    return Json(new JSONResult("No car ID provided!"), JsonRequestBehavior.AllowGet);
                }

                String carTitle = rating.carTitle;

                if (carTitle == null)
                {
                    return Json(new JSONResult("No car title provided!"), JsonRequestBehavior.AllowGet);
                }

                int userId = int.Parse(User.Identity.GetUserId());

                const String QUERY_STRING = "INSERT INTO ratings(carId, userId, carTitle, stars) VALUES (@carId, @userId, @carTitle, @stars)";

                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING, connection);
                    cmd.Parameters.AddWithValue("@carId", carId);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@carTitle", carTitle);
                    cmd.Parameters.AddWithValue("@stars", stars);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                    connection.Close();
                }
            }
            catch
            {
                return Json(new JSONResult("Unknown error occurred!"), JsonRequestBehavior.AllowGet);
            }

            return Json(new JSONResult(), JsonRequestBehavior.AllowGet);
        }

        // PUT: Edit Rating
        [HttpPut]
        public ActionResult EditRating(String json)
        {
            try
            {
                dynamic rating = JObject.Parse(json);

                int stars = rating.rating;

                if (stars < 1 || stars > 5)
                {
                    return Json(new JSONResult("Ratings must be integers between 1 and 5 inclusive!"), JsonRequestBehavior.AllowGet);
                }

                int ratingId = rating.id;

                // check if rating ID provided
                if (ratingId == 0)
                {
                    return Json(new JSONResult("No rating ID provided!"), JsonRequestBehavior.AllowGet);
                }

                Rating theRating = null;
                const String QUERY_STRING1 = "SELECT * FROM Ratings WHERE id = @ratingId";

                // check if rating exists
                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING1, connection);
                    cmd.Parameters.AddWithValue("@ratingId", ratingId);

                    connection.Open();

                    MySqlDataReader reader = cmd.ExecuteReader();

                    try
                    {
                        while (reader.Read())
                        {
                            theRating = new Rating(
                                    int.Parse(reader["id"].ToString().Trim()),
                                    int.Parse(reader["carId"].ToString().Trim()),
                                    int.Parse(reader["userId"].ToString().Trim()),
                                    reader["carTitle"].ToString().Trim(),
                                    int.Parse(reader["stars"].ToString().Trim())
                            );
                        }
                    }
                    finally
                    {
                        reader.Close();
                        connection.Close();
                    }
                }

                if (theRating == null)
                {
                    return Json(new JSONResult("Rating does not exist!"), JsonRequestBehavior.AllowGet);
                }

                int userId = int.Parse(User.Identity.GetUserId());

                // only let users edit their own ratings
                if (theRating.userId != userId)
                {
                    return Json(new JSONResult("You can only edit your own ratings!"), JsonRequestBehavior.AllowGet);
                }

                // edit rating
                const String QUERY_STRING2 = "UPDATE Ratings SET stars = @stars WHERE id = @ratingId";
                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING2, connection);
                    cmd.Parameters.AddWithValue("@stars", stars);
                    cmd.Parameters.AddWithValue("@ratingId", ratingId);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                    connection.Close();
                }

            }
            catch
            {
                return Json(new JSONResult("Unknown error occurred!"), JsonRequestBehavior.AllowGet);
            }

            return Json(new JSONResult(), JsonRequestBehavior.AllowGet);
        }

        // DELETE: Rating
        [HttpDelete]
        public ActionResult DeleteRating(String json)
        {
            try
            {
                dynamic rating = JObject.Parse(json);

                int userId = int.Parse(User.Identity.GetUserId());
                int ratingId = rating.id;

                Rating theRating = null;
                const String QUERY_STRING1 = "SELECT * FROM Ratings WHERE id = @ratingId";

                // check if rating exists
                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING1, connection);
                    cmd.Parameters.AddWithValue("@ratingId", ratingId);

                    connection.Open();

                    MySqlDataReader reader = cmd.ExecuteReader();
                    try
                    {
                        while (reader.Read())
                        {
                            theRating = new Rating(
                                int.Parse(reader["id"].ToString().Trim()),
                                int.Parse(reader["carId"].ToString().Trim()),
                                int.Parse(reader["userId"].ToString().Trim()),
                                reader["carTitle"].ToString().Trim(),
                                int.Parse(reader["stars"].ToString().Trim())
                            );
                        }
                    }
                    finally
                    {
                        reader.Close();
                        connection.Close();
                    }
                }

                // check if rating exists
                if (theRating == null)
                {
                    return Json(new JSONResult("Rating does not exist!"), JsonRequestBehavior.AllowGet);
                }

                // only let users delete their own ratings
                if (theRating.userId != userId)
                {
                    return Json(new JSONResult("You can only delete your own ratings!"), JsonRequestBehavior.AllowGet);
                }

                const String QUERY_STRING2 = "DELETE FROM Ratings WHERE id = @ratingId";

                // delete rating
                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING2, connection);
                    cmd.Parameters.AddWithValue("@ratingId", ratingId);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                    connection.Close();
                }

                return Json(new JSONResult(), JsonRequestBehavior.AllowGet);
            }
            catch
            {
                return Json(new JSONResult("Unknown error occurred!"), JsonRequestBehavior.AllowGet);
            }
        }
        /* END Ratings */

        /* START Cars */

        // GET: All cars [JSON]
        [AllowAnonymous]
        [HttpGet]
        public ActionResult AllCars()
        {
            const String QUERY_STRING = "SELECT * FROM Cars";
            List<Car> cars = new List<Car>();

            using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
            {
                MySqlCommand cmd = new MySqlCommand(QUERY_STRING, connection);

                connection.Open();

                MySqlDataReader reader = cmd.ExecuteReader();
                try
                {
                    while (reader.Read())
                    {
                        cars.Add(
                            new Car(
                                int.Parse(reader["id"].ToString().Trim()),
                                reader["mpg"].ToString().Trim(),
                                reader["cylinders"].ToString().Trim(),
                                reader["fuel"].ToString().Trim(),
                                reader["engine"].ToString().Trim(),
                                reader["horsepower"].ToString().Trim(),
                                reader["weight"].ToString().Trim(),
                                reader["acceleration"].ToString().Trim(),
                                reader["year"].ToString().Trim(),
                                reader["origin"].ToString().Trim(),
                                reader["manufacturer"].ToString().Trim(),
                                reader["title"].ToString().Trim()
                            )
                        );
                    }
                }
                finally
                {
                    reader.Close();
                    connection.Close();
                }
            }

            return Json(cars, JsonRequestBehavior.AllowGet);
        }

        // POST: New Car
        [HttpPost]
        public ActionResult CreateCar(String json)
        {
            dynamic car = JObject.Parse(json);

            try
            {

                String mpg = car.mpg;
                String cylinders = car.cylinders;
                String fuel = car.fuel;
                String engine = car.engine;
                String horsepower = car.horsepower;
                String weight = car.weight;
                String acceleration = car.acceleration;
                String year = car.year;
                String origin = car.origin;
                String manufacturer = car.manufacturer;
                String title = car.title;

                if (title == null || mpg == null || cylinders == null || fuel == null || engine == null || horsepower == null || weight == null || acceleration == null || year == null || origin == null || manufacturer == null)
                {
                    return Json(new JSONResult("Not enough parameters provided!"), JsonRequestBehavior.AllowGet);
                }

                const String QUERY_STRING = "INSERT INTO Cars(title, mpg, cylinders, fuel, engine, horsepower, weight, acceleration, year, origin, manufacturer) VALUES (@title, @mpg, @cylinders, @fuel, @engine, @horsepower, @weight, @acceleration, @year, @origin, @manufacturer)";

                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING, connection);

                    cmd.Parameters.AddWithValue("@mpg", mpg);
                    cmd.Parameters.AddWithValue("@cylinders", cylinders);
                    cmd.Parameters.AddWithValue("@fuel", @fuel);
                    cmd.Parameters.AddWithValue("@engine", engine);
                    cmd.Parameters.AddWithValue("@horsepower", horsepower);
                    cmd.Parameters.AddWithValue("@weight", weight);
                    cmd.Parameters.AddWithValue("@acceleration", acceleration);
                    cmd.Parameters.AddWithValue("@year", year);
                    cmd.Parameters.AddWithValue("@origin", origin);
                    cmd.Parameters.AddWithValue("@manufacturer", manufacturer);
                    cmd.Parameters.AddWithValue("@title", title);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                    connection.Close();

                    return Json(new JSONResult(), JsonRequestBehavior.AllowGet);
                }
            }
            catch
            {
                return Json(new JSONResult("Unknown error occurred!"), JsonRequestBehavior.AllowGet);
            }
        }

        // PUT: Edit Car
        [HttpPut]
        public ActionResult EditCar(String json)
        {
            try
            {
                dynamic car = JObject.Parse(json);

                int carId = car.id;

                // check if car ID provided
                if (carId == 0)
                {
                    return Json(new JSONResult("No car ID provided!"), JsonRequestBehavior.AllowGet);
                }

                Car theCar = null;
                const String QUERY_STRING1 = "SELECT * FROM Cars WHERE id = @carId";

                // check if car exists
                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING1, connection);
                    cmd.Parameters.AddWithValue("@carId", carId);

                    connection.Open();

                    MySqlDataReader reader = cmd.ExecuteReader();

                    try
                    {
                        while (reader.Read())
                        {
                            theCar = new Car(
                                int.Parse(reader["id"].ToString().Trim()),
                                reader["mpg"].ToString().Trim(),
                                reader["cylinders"].ToString().Trim(),
                                reader["fuel"].ToString().Trim(),
                                reader["engine"].ToString().Trim(),
                                reader["horsepower"].ToString().Trim(),
                                reader["weight"].ToString().Trim(),
                                reader["acceleration"].ToString().Trim(),
                                reader["year"].ToString().Trim(),
                                reader["origin"].ToString().Trim(),
                                reader["manufacturer"].ToString().Trim(),
                                reader["title"].ToString().Trim()
                            );
                        }
                    }
                    finally
                    {
                        reader.Close();
                        connection.Close();
                    }
                }

                if (theCar == null)
                {
                    return Json(new JSONResult("Car does not exist!"), JsonRequestBehavior.AllowGet);
                }

                String mpg = car.mpg;
                String cylinders = car.cylinders;
                String fuel = car.fuel;
                String engine = car.engine;
                String horsepower = car.horsepower;
                String weight = car.weight;
                String acceleration = car.acceleration;
                String year = car.year;
                String origin = car.origin;
                String manufacturer = car.manufacturer;
                String title = car.title;

                // edit car
                const String QUERY_STRING2 = "UPDATE Cars SET mpg = @mpg, cylinders = @cylinders, fuel = @fuel, engine = @engine, horsepower = @horsepower, weight = @weight, acceleration = @acceleration, year = @year, origin = @origin, manufacturer = @manufacturer, title = @title WHERE id = @carId";
                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING2, connection);

                    cmd.Parameters.AddWithValue("@carId", carId);
                    cmd.Parameters.AddWithValue("@mpg", mpg);
                    cmd.Parameters.AddWithValue("@cylinders", cylinders);
                    cmd.Parameters.AddWithValue("@fuel", @fuel);
                    cmd.Parameters.AddWithValue("@engine", engine);
                    cmd.Parameters.AddWithValue("@horsepower", horsepower);
                    cmd.Parameters.AddWithValue("@weight", weight);
                    cmd.Parameters.AddWithValue("@acceleration", acceleration);
                    cmd.Parameters.AddWithValue("@year", year);
                    cmd.Parameters.AddWithValue("@origin", origin);
                    cmd.Parameters.AddWithValue("@manufacturer", manufacturer);
                    cmd.Parameters.AddWithValue("@title", title);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                    connection.Close();
                }

            }
            catch
            {
                return Json(new JSONResult("Unknown error occurred!"), JsonRequestBehavior.AllowGet);
            }

            return Json(new JSONResult(), JsonRequestBehavior.AllowGet);
        }

        // DELETE: Car
        [HttpDelete]
        public ActionResult DeleteCar(String json)
        {
            try
            {
                dynamic car = JObject.Parse(json);

                int carId = car.id;

                Car theCar = null;
                const String QUERY_STRING1 = "SELECT * FROM Cars WHERE id = @carId";

                // check if car exists
                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING1, connection);
                    cmd.Parameters.AddWithValue("@carId", carId);

                    connection.Open();

                    MySqlDataReader reader = cmd.ExecuteReader();
                    try
                    {
                        while (reader.Read())
                        {
                            theCar = new Car(
                                int.Parse(reader["id"].ToString().Trim()),
                                reader["mpg"].ToString().Trim(),
                                reader["cylinders"].ToString().Trim(),
                                reader["fuel"].ToString().Trim(),
                                reader["engine"].ToString().Trim(),
                                reader["horsepower"].ToString().Trim(),
                                reader["weight"].ToString().Trim(),
                                reader["acceleration"].ToString().Trim(),
                                reader["year"].ToString().Trim(),
                                reader["origin"].ToString().Trim(),
                                reader["manufacturer"].ToString().Trim(),
                                reader["title"].ToString().Trim()
                            );
                        }
                    }
                    finally
                    {
                        reader.Close();
                        connection.Close();
                    }
                }

                // check if car exists
                if (theCar == null)
                {
                    return Json(new JSONResult("Car does not exist!"), JsonRequestBehavior.AllowGet);
                }

                const String QUERY_STRING2 = "DELETE FROM Cars WHERE id = @carId";

                // delete user car
                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING2, connection);
                    cmd.Parameters.AddWithValue("@carId", carId);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                    connection.Close();
                }

                return Json(new JSONResult(), JsonRequestBehavior.AllowGet);
            }
            catch
            {
                return Json(new JSONResult("Unknown error occurred!"), JsonRequestBehavior.AllowGet);
            }
        }

        // GET: One car [JSON]
        [HttpGet]
        public ActionResult Car(String json)
        {
            Car theCar = null;

            try
            {
                dynamic car = JObject.Parse(json);

                int carId = car.id;

                // check if car ID provided
                if (carId == 0)
                {
                    return Json(new JSONResult("No car ID provided!"), JsonRequestBehavior.AllowGet);
                }

                const String QUERY_STRING = "SELECT * FROM Cars WHERE id = @carId";

                // check if car exists
                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING, connection);
                    cmd.Parameters.AddWithValue("@carId", carId);

                    connection.Open();

                    MySqlDataReader reader = cmd.ExecuteReader();

                    try
                    {
                        while (reader.Read())
                        {
                            theCar = new Car(
                                int.Parse(reader["id"].ToString().Trim()),
                                reader["mpg"].ToString().Trim(),
                                reader["cylinders"].ToString().Trim(),
                                reader["fuel"].ToString().Trim(),
                                reader["engine"].ToString().Trim(),
                                reader["horsepower"].ToString().Trim(),
                                reader["weight"].ToString().Trim(),
                                reader["acceleration"].ToString().Trim(),
                                reader["year"].ToString().Trim(),
                                reader["origin"].ToString().Trim(),
                                reader["manufacturer"].ToString().Trim(),
                                reader["title"].ToString().Trim()
                            );
                        }
                    }
                    finally
                    {
                        reader.Close();
                        connection.Close();
                    }
                }
            }
            catch
            {
                return Json(new JSONResult("Unknown error occurred!"), JsonRequestBehavior.AllowGet);
            }

            if (theCar == null)
            {
                return Json(new JSONResult("Car does not exist!"), JsonRequestBehavior.AllowGet);
            }

            return Json(theCar, JsonRequestBehavior.AllowGet);
        }

        // GET: cars the user hasn't rated yet [JSON]
        [HttpGet]
        public ActionResult AllCarsUserNotRated()
        {
            List<Car> allCars = new List<Car>();
            List<Rating> ratings = new List<Rating>();
            List<Car> unratedCars = new List<Car>();

            const String QUERY_STRING1 = "SELECT * FROM Cars";

            using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
            {
                MySqlCommand cmd = new MySqlCommand(QUERY_STRING1, connection);

                connection.Open();

                MySqlDataReader reader = cmd.ExecuteReader();
                try
                {
                    while (reader.Read())
                    {
                        allCars.Add(
                            new Car(
                                int.Parse(reader["id"].ToString().Trim()),
                                reader["mpg"].ToString().Trim(),
                                reader["cylinders"].ToString().Trim(),
                                reader["fuel"].ToString().Trim(),
                                reader["engine"].ToString().Trim(),
                                reader["horsepower"].ToString().Trim(),
                                reader["weight"].ToString().Trim(),
                                reader["acceleration"].ToString().Trim(),
                                reader["year"].ToString().Trim(),
                                reader["origin"].ToString().Trim(),
                                reader["manufacturer"].ToString().Trim(),
                                reader["title"].ToString().Trim()
                            )
                        );
                    }
                }
                finally
                {
                    reader.Close();
                    connection.Close();
                }
            }

            int userId = int.Parse(User.Identity.GetUserId());
            
            const String QUERY_STRING2 = "SELECT * FROM Ratings WHERE userId = @userId";

            using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
            {
                MySqlCommand cmd = new MySqlCommand(QUERY_STRING2, connection);
                cmd.Parameters.AddWithValue("@userId", userId);

                connection.Open();

                MySqlDataReader reader = cmd.ExecuteReader();
                try
                {
                    while (reader.Read())
                    {
                        ratings.Add(
                            new Rating(
                                int.Parse(reader["id"].ToString().Trim()),
                                int.Parse(reader["carId"].ToString().Trim()),
                                int.Parse(reader["userId"].ToString().Trim()),
                                reader["carTitle"].ToString().Trim(),
                                int.Parse(reader["stars"].ToString().Trim())
                            )
                        );
                    }
                }
                finally
                {
                    reader.Close();
                    connection.Close();
                }
            }

            // determine the cars the user has not rated yet

            // for each car
            for (int i = 0; i < allCars.Count; i++)
            {
                Car theCar = allCars[i];
                bool hasRated = false;

                // for each rating
                for (int j = 0; j < ratings.Count; j++)
                {
                    Rating theRating = ratings[j];

                    if (theCar.id == theRating.carId)
                    {
                        hasRated = true;
                        break; // exit inner loop
                    }
                }

                // add unrated car to new list
                if (!hasRated)
                {
                    unratedCars.Add(theCar);
                }
            }

            return Json(unratedCars, JsonRequestBehavior.AllowGet);
        }
        /* END Cars */

        /* START User Cars */

        // GET: return all of the user's cars [JSON]
        [HttpGet]
        public ActionResult UserCars()
        {
            List<UserCar> cars = new List<UserCar>();

            int userId = int.Parse(User.Identity.GetUserId());
            const String QUERY_STRING = "SELECT * FROM Usercars WHERE userId = @userId";

            using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
            {
                MySqlCommand cmd = new MySqlCommand(QUERY_STRING, connection);
                cmd.Parameters.AddWithValue("@userId", userId);

                connection.Open();

                MySqlDataReader reader = cmd.ExecuteReader();
                try
                {
                    while (reader.Read())
                    {
                        cars.Add(
                            new UserCar(
                                int.Parse(reader["id"].ToString().Trim()),
                                int.Parse(reader["carId"].ToString().Trim()),
                                int.Parse(reader["userId"].ToString().Trim()),
                                reader["userName"].ToString().Trim(),
                                reader["carTitle"].ToString().Trim(),
                                reader["colour"].ToString().Trim(),
                                int.Parse(reader["isMatched"].ToString().Trim())
                            )
                        );
                    }
                }
                finally
                {
                    reader.Close();
                    connection.Close();
                }
            }

            return Json(cars, JsonRequestBehavior.AllowGet);
        }

        // GET: return all of the user's cars that aren't matched [JSON]
        [HttpGet]
        public ActionResult UserCarsNotMatched()
        {
            List<UserCar> cars = new List<UserCar>();

            int userId = int.Parse(User.Identity.GetUserId());
            const String QUERY_STRING = "SELECT * FROM Usercars WHERE userId = @userId AND isMatched = 0";

            using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
            {
                MySqlCommand cmd = new MySqlCommand(QUERY_STRING, connection);
                cmd.Parameters.AddWithValue("@userId", userId);

                connection.Open();

                MySqlDataReader reader = cmd.ExecuteReader();
                try
                {
                    while (reader.Read())
                    {
                        cars.Add(
                            new UserCar(
                                int.Parse(reader["id"].ToString().Trim()),
                                int.Parse(reader["carId"].ToString().Trim()),
                                int.Parse(reader["userId"].ToString().Trim()),
                                reader["userName"].ToString().Trim(),
                                reader["carTitle"].ToString().Trim(),
                                reader["colour"].ToString().Trim(),
                                int.Parse(reader["isMatched"].ToString().Trim())
                            )
                        );
                    }
                }
                finally
                {
                    reader.Close();
                    connection.Close();
                }
            }

            return Json(cars, JsonRequestBehavior.AllowGet);
        }

        // GET: return all user cars that don't belong to the user and aren't matched [JSON]
        [HttpGet]
        public ActionResult UserCarsNotMatchedNotUser()
        {
            List<UserCar> cars = new List<UserCar>();

            int userId = int.Parse(User.Identity.GetUserId());
            const String QUERY_STRING = "SELECT * FROM Usercars WHERE userId != @userId AND isMatched = 0";

            using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
            {
                MySqlCommand cmd = new MySqlCommand(QUERY_STRING, connection);
                cmd.Parameters.AddWithValue("@userId", userId);

                connection.Open();

                MySqlDataReader reader = cmd.ExecuteReader();
                try
                {
                    while (reader.Read())
                    {
                        cars.Add(
                            new UserCar(
                                int.Parse(reader["id"].ToString().Trim()),
                                int.Parse(reader["carId"].ToString().Trim()),
                                int.Parse(reader["userId"].ToString().Trim()),
                                reader["userName"].ToString().Trim(),
                                reader["carTitle"].ToString().Trim(),
                                reader["colour"].ToString().Trim(),
                                int.Parse(reader["isMatched"].ToString().Trim())
                            )
                        );
                    }
                }
                finally
                {
                    reader.Close();
                    connection.Close();
                }
            }

            return Json(cars, JsonRequestBehavior.AllowGet);
        }

        // GET: search for user cars that don't belong to the user and aren't matched [JSON]
        [HttpGet]
        public ActionResult SearchUserCarsNotMatched(String json)
        {
            List<UserCar> cars = new List<UserCar>();

            try
            {
                dynamic data = JObject.Parse(json);

                String title = data["title"];
                String colour = data["colour"];

                int userId = int.Parse(User.Identity.GetUserId());

                String queryString = "SELECT * FROM Usercars WHERE userId != @userId AND isMatched = 0";

                if (title != "")
                    queryString += " AND carTitle LIKE CONCAT('%', @title, '%')";

                if (colour != "")
                    queryString += " AND colour LIKE CONCAT('%', @colour, '%')";

                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(queryString, connection);
                    cmd.Parameters.AddWithValue("@userId", userId);

                    if (title != "")
                        cmd.Parameters.AddWithValue("@title", title);

                    if (colour != "")
                        cmd.Parameters.AddWithValue("@colour", colour);

                    connection.Open();

                    MySqlDataReader reader = cmd.ExecuteReader();

                    try
                    {
                        while (reader.Read())
                        {
                            cars.Add(
                                new UserCar(
                                    int.Parse(reader["id"].ToString().Trim()),
                                    int.Parse(reader["carId"].ToString().Trim()),
                                    int.Parse(reader["userId"].ToString().Trim()),
                                    reader["userName"].ToString().Trim(),
                                    reader["carTitle"].ToString().Trim(),
                                    reader["colour"].ToString().Trim(),
                                    0 // because we know its not matched
                                )
                            );
                        }
                    }
                    finally
                    {
                        reader.Close();
                        connection.Close();
                    }
                }
            }
            catch
            {
                return Json(new JSONResult("Unknown error occurred!"), JsonRequestBehavior.AllowGet);
            }
            return Json(cars, JsonRequestBehavior.AllowGet);
        }

        // GET: return details of a specific user car [JSON]
        [HttpGet]
        public ActionResult UserCar(String json)
        {
            UserCar car = null;

            try
            {
                dynamic data = JObject.Parse(json);

                int userCarId = data.id;

                // check if user car ID provided
                if (userCarId == 0)
                {
                    return Json(new JSONResult("No user car ID provided!"), JsonRequestBehavior.AllowGet);
                }

                const String QUERY_STRING = "SELECT * FROM Usercars WHERE id = @usercarId";

                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING, connection);
                    cmd.Parameters.AddWithValue("@usercarId", userCarId);

                    connection.Open();

                    MySqlDataReader reader = cmd.ExecuteReader();
                    try
                    {
                        while (reader.Read())
                        {
                            car = new UserCar(
                                int.Parse(reader["id"].ToString().Trim()),
                                int.Parse(reader["carId"].ToString().Trim()),
                                int.Parse(reader["userId"].ToString().Trim()),
                                reader["userName"].ToString().Trim(),
                                reader["carTitle"].ToString().Trim(),
                                reader["colour"].ToString().Trim(),
                                int.Parse(reader["isMatched"].ToString().Trim())
                            );
                        }
                    }
                    finally
                    {
                        reader.Close();
                        connection.Close();
                    }
                }
            }
            catch
            {
                return Json(new JSONResult("Unknown error occurred"), JsonRequestBehavior.AllowGet);
            }

            return Json(car, JsonRequestBehavior.AllowGet);
        }

        // POST: New User Car
        // TODO check if given id exists in car table so its valid
        [HttpPost]
        public ActionResult CreateUserCar(String json)
        {
            dynamic data = JObject.Parse(json);

            try
            {
                int carId = data.carId;

                if (carId == 0)
                {
                    return Json(new JSONResult("No car ID provided!"), JsonRequestBehavior.AllowGet);
                }

                String carTitle = data.carTitle;
                String colour = data.colour;

                if (carTitle == null || colour == null)
                {
                    return Json(new JSONResult("Missing car details!"), JsonRequestBehavior.AllowGet);
                }

                int userId = int.Parse(User.Identity.GetUserId());
                String userName = User.Identity.GetUserName();

                const String QUERY_STRING = "INSERT INTO usercars(carId, userId, userName, carTitle, colour, isMatched) VALUES (@carId, @userId, @userName, @carTitle, @colour, 0)";

                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING, connection);
                    cmd.Parameters.AddWithValue("@carId", carId);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@userName", userName);
                    cmd.Parameters.AddWithValue("@carTitle", carTitle);
                    cmd.Parameters.AddWithValue("@colour", colour);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                    connection.Close();
                }
            }
            catch
            {
                return Json(new JSONResult("Unknown error occurred!"), JsonRequestBehavior.AllowGet);
            }

            return Json(new JSONResult(), JsonRequestBehavior.AllowGet);
        }

        // DELETE: User Car [JSON]
        [HttpDelete]
        public ActionResult DeleteUserCar(String json)
        {
            try
            {
                dynamic data = JObject.Parse(json);

                int userId = int.Parse(User.Identity.GetUserId());
                int userCarId = data.id;

                UserCar theCar = null;
                const String QUERY_STRING1 = "SELECT * FROM Usercars WHERE id = @userCarId";

                // check if car exists
                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING1, connection);
                    cmd.Parameters.AddWithValue("@userCarId", userCarId);

                    connection.Open();

                    MySqlDataReader reader = cmd.ExecuteReader();
                    try
                    {
                        while (reader.Read())
                        {
                            theCar = new UserCar(
                                int.Parse(reader["id"].ToString().Trim()),
                                int.Parse(reader["carId"].ToString().Trim()),
                                int.Parse(reader["userId"].ToString().Trim()),
                                reader["userName"].ToString().Trim(),
                                reader["carTitle"].ToString().Trim(),
                                reader["colour"].ToString().Trim(),
                                int.Parse(reader["isMatched"].ToString().Trim())
                            );
                        }
                    }
                    finally
                    {
                        reader.Close();
                        connection.Close();
                    }
                }

                // check if car exists
                if (theCar == null)
                {
                    return Json(new JSONResult("Car does not exist!"), JsonRequestBehavior.AllowGet);
                }

                // only let users delete their own cars
                if (theCar.userId != userId)
                {
                    return Json(new JSONResult("You can only delete your own cars!"), JsonRequestBehavior.AllowGet);
                }

                const String QUERY_STRING2 = "DELETE FROM Usercars WHERE id = @userCarId";

                // delete user car
                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING2, connection);
                    cmd.Parameters.AddWithValue("@userCarId", userCarId);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                    connection.Close();
                }

                return Json(new JSONResult(), JsonRequestBehavior.AllowGet);
            }
            catch
            {
                return Json(new JSONResult("Unknown error occurred!"), JsonRequestBehavior.AllowGet);
            }
        }

        /* END User Cars */

        /* START Swap Requests */

        // GET: return all of the user's active swap requests that they have made and unconfirmed [JSON]
        [HttpGet]
        public ActionResult SwapRequestsMade()
        {
            List<SwapRequest> requests = new List<SwapRequest>();

            String userName = User.Identity.GetUserName();

            const String QUERY_STRING = "SELECT * FROM Swaprequests WHERE requestUserName = @requestUserName";

            using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
            {
                MySqlCommand cmd = new MySqlCommand(QUERY_STRING, connection);

                cmd.Parameters.AddWithValue("@requestUserName", userName);

                connection.Open();

                MySqlDataReader reader = cmd.ExecuteReader();
                try
                {
                    while (reader.Read())
                    {
                        requests.Add(
                            new SwapRequest(
                                int.Parse(reader["id"].ToString().Trim()),
                                int.Parse(reader["requestCarId"].ToString().Trim()),
                                int.Parse(reader["targetCarId"].ToString().Trim()),
                                int.Parse(reader["requestUserCarId"].ToString().Trim()),
                                int.Parse(reader["targetUserCarId"].ToString().Trim()),
                                int.Parse(reader["requestUserId"].ToString().Trim()),
                                int.Parse(reader["targetUserId"].ToString().Trim()),
                                reader["requestUserName"].ToString().Trim(),
                                reader["targetUserName"].ToString().Trim(),
                                reader["requestUserCarTitle"].ToString().Trim(),
                                reader["targetUserCarTitle"].ToString().Trim(),
                                reader["requestUserCarColour"].ToString().Trim(),
                                reader["targetUserCarColour"].ToString().Trim(),
                                int.Parse(reader["confirmed"].ToString().Trim())
                            )
                        );
                    }
                }
                finally
                {
                    reader.Close();
                    connection.Close();
                }
            }

            return Json(requests, JsonRequestBehavior.AllowGet);
        }

        // GET: return all of the user's active swap requests that they have received and unconfirmed [JSON]
        [HttpGet]
        public ActionResult SwapRequestsReceived()
        {
            List<SwapRequest> requests = new List<SwapRequest>();

            String userName = User.Identity.GetUserName();

            const String QUERY_STRING = "SELECT * FROM Swaprequests WHERE targetUserName = @targetUserName";

            using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
            {
                MySqlCommand cmd = new MySqlCommand(QUERY_STRING, connection);

                cmd.Parameters.AddWithValue("@targetUserName", userName);

                connection.Open();

                MySqlDataReader reader = cmd.ExecuteReader();
                try
                {
                    while (reader.Read())
                    {
                        requests.Add(
                            new SwapRequest(
                                int.Parse(reader["id"].ToString().Trim()),
                                int.Parse(reader["requestCarId"].ToString().Trim()),
                                int.Parse(reader["targetCarId"].ToString().Trim()),
                                int.Parse(reader["requestUserCarId"].ToString().Trim()),
                                int.Parse(reader["targetUserCarId"].ToString().Trim()),
                                int.Parse(reader["requestUserId"].ToString().Trim()),
                                int.Parse(reader["targetUserId"].ToString().Trim()),
                                reader["requestUserName"].ToString().Trim(),
                                reader["targetUserName"].ToString().Trim(),
                                reader["requestUserCarTitle"].ToString().Trim(),
                                reader["targetUserCarTitle"].ToString().Trim(),
                                reader["requestUserCarColour"].ToString().Trim(),
                                reader["targetUserCarColour"].ToString().Trim(),
                                int.Parse(reader["confirmed"].ToString().Trim())
                            )
                        );
                    }
                }
                finally
                {
                    reader.Close();
                    connection.Close();
                }
            }

            return Json(requests, JsonRequestBehavior.AllowGet);
        }

        // POST: New Swap Request
        [HttpPost]
        public ActionResult CreateSwapRequest(String json)
        {
            dynamic data = JObject.Parse(json);

            try
            {
                int requestCarId = data.requestCarId;
                int targetCarId = data.targetCarId;
                int requestUserCarId = data.requestUserCarId;
                int targetUserCarId = data.targetUserCarId;
                int targetUserId = data.targetUserId;
                String requestUserCarTitle = data.requestUserCarTitle;
                String targetUserCarTitle = data.targetUserCarTitle;
                String requestUserCarColour = data.requestUserCarColour;
                String targetUserCarColour = data.targetUserCarColour;
                String targetUserName = data.targetUserName;

                const String QUERY_STRING = "INSERT INTO Swaprequests(requestCarId, targetCarId, requestUserId, targetUserId, requestUserCarId, targetUserCarId, requestUserName, targetUserName, requestUserCarTitle, targetUserCarTitle, requestUserCarColour, targetUserCarColour, confirmed) VALUES (@requestCarId, @targetCarId, @requestUserId, @targetUserId, @requestUserCarId, @targetUserCarId, @requestUserName, @targetUserName, @requestUserCarTitle, @targetUserCarTitle, @requestUserCarColour, @targetUserCarColour, 0)";

                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING, connection);
                    cmd.Parameters.AddWithValue("@requestCarId", requestCarId);
                    cmd.Parameters.AddWithValue("@targetCarId", targetCarId);
                    cmd.Parameters.AddWithValue("@requestUserId", User.Identity.GetUserId());
                    cmd.Parameters.AddWithValue("@targetUserId", targetUserId);
                    cmd.Parameters.AddWithValue("@requestUserCarId", requestUserCarId);
                    cmd.Parameters.AddWithValue("@targetUserCarId", targetUserCarId);
                    cmd.Parameters.AddWithValue("@requestUserName", User.Identity.GetUserName());
                    cmd.Parameters.AddWithValue("@targetUserName", targetUserName);
                    cmd.Parameters.AddWithValue("@requestUserCarTitle", requestUserCarTitle);
                    cmd.Parameters.AddWithValue("@targetUserCarTitle", targetUserCarTitle);
                    cmd.Parameters.AddWithValue("@requestUserCarColour", requestUserCarColour);
                    cmd.Parameters.AddWithValue("@targetUserCarColour", targetUserCarColour);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                    connection.Close();
                }
            }
            catch
            {
                return Json(new JSONResult("Unknown error occurred!"), JsonRequestBehavior.AllowGet);
            }

            return Json(new JSONResult(), JsonRequestBehavior.AllowGet);
        }

        // PUT: Edit Swap Request [JSON]
        [HttpPut]
        public ActionResult EditSwapRequest(String json)
        {
            try
            {
                dynamic data = JObject.Parse(json);

                int requestId = data.id;
                int carId = data.carId;
                int confirmed = data.confirmed;
                int userId = int.Parse(User.Identity.GetUserId());
                UserCar theCar = null;

                const String QUERY_STRING1 = "SELECT * FROM Usercars WHERE id = @carId AND userId = @userId";

                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING1, connection);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@carId", carId);

                    connection.Open();

                    MySqlDataReader reader = cmd.ExecuteReader();
                    try
                    {
                        while (reader.Read())
                        {
                            theCar = new UserCar(
                                int.Parse(reader["id"].ToString().Trim()),
                                int.Parse(reader["carId"].ToString().Trim()),
                                int.Parse(reader["userId"].ToString().Trim()),
                                reader["userName"].ToString().Trim(),
                                reader["carTitle"].ToString().Trim(),
                                reader["colour"].ToString().Trim(),
                                int.Parse(reader["isMatched"].ToString().Trim())
                            );
                        }
                    }
                    finally
                    {
                        reader.Close();
                        connection.Close();
                    }
                }

                // check if car exists
                if (theCar == null)
                {
                    return Json(new JSONResult("Car does not exist, or it is not yours!"), JsonRequestBehavior.AllowGet);
                }

                SwapRequest request = null;
                const String QUERY_STRING2 = "SELECT * FROM Swaprequests WHERE id = @requestId AND targetUserCarId = @carId";

                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING2, connection);
                    cmd.Parameters.AddWithValue("@requestId", requestId);
                    cmd.Parameters.AddWithValue("@carId", carId);

                    connection.Open();

                    MySqlDataReader reader = cmd.ExecuteReader();
                    try
                    {
                        while (reader.Read())
                        {
                            request = new SwapRequest(
                                int.Parse(reader["id"].ToString().Trim()),
                                int.Parse(reader["requestCarId"].ToString().Trim()),
                                int.Parse(reader["targetCarId"].ToString().Trim()),
                                int.Parse(reader["requestUserCarId"].ToString().Trim()),
                                int.Parse(reader["targetUserCarId"].ToString().Trim()),
                                int.Parse(reader["requestUserId"].ToString().Trim()),
                                int.Parse(reader["targetUserId"].ToString().Trim()),
                                reader["requestUserName"].ToString().Trim(),
                                reader["targetUserName"].ToString().Trim(),
                                reader["requestUserCarTitle"].ToString().Trim(),
                                reader["targetUserCarTitle"].ToString().Trim(),
                                reader["requestUserCarColour"].ToString().Trim(),
                                reader["targetUserCarColour"].ToString().Trim(),
                                int.Parse(reader["confirmed"].ToString().Trim())
                            );
                        }
                    }
                    finally
                    {
                        reader.Close();
                        connection.Close();
                    }
                }

                // check if car ID is same as given in request
                if (request.targetUserCarId != carId)
                {
                    return Json(new JSONResult("Car ID and request ID do not match!"), JsonRequestBehavior.AllowGet);
                }

                const String QUERY_STRING3 = "UPDATE Swaprequests SET confirmed = @confirmed WHERE id = @requestId";

                // edit request
                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING3, connection);
                    cmd.Parameters.AddWithValue("@requestId", requestId);
                    cmd.Parameters.AddWithValue("@confirmed", confirmed);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                    connection.Close();
                }

                int isMatched = 1;

                if (confirmed == 2)
                    isMatched = 0;

                const String QUERY_STRING4 = "UPDATE Usercars SET isMatched = @isMatched WHERE id = @carId";

                // edit target car to say its unavailable
                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING4, connection);
                    cmd.Parameters.AddWithValue("@carId", carId);
                    cmd.Parameters.AddWithValue("@isMatched", isMatched);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                    connection.Close();
                }

                // edit request car to say its unavailable
                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING4, connection);
                    cmd.Parameters.AddWithValue("@carId", request.requestUserCarId);
                    cmd.Parameters.AddWithValue("@isMatched", isMatched);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                    connection.Close();
                }

                return Json(new JSONResult(), JsonRequestBehavior.AllowGet);
            }
            catch
            {
                return Json(new JSONResult("Unknown error occurred!"), JsonRequestBehavior.AllowGet);
            }
        }

        // DELETE: Swap Request [JSON]
        [HttpDelete]
        public ActionResult DeleteSwapRequest(String json)
        {
            try
            {
                dynamic data = JObject.Parse(json);

                int requestId = data.id;
                int carId = data.carId;
                int userId = int.Parse(User.Identity.GetUserId());

                UserCar theCar = null;
                const String QUERY_STRING1 = "SELECT * FROM Usercars WHERE id = @carId AND userId = @userId";

                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING1, connection);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@carId", carId);

                    connection.Open();

                    MySqlDataReader reader = cmd.ExecuteReader();
                    try
                    {
                        while (reader.Read())
                        {
                            theCar = new UserCar(
                                int.Parse(reader["id"].ToString().Trim()),
                                int.Parse(reader["carId"].ToString().Trim()),
                                int.Parse(reader["userId"].ToString().Trim()),
                                reader["userName"].ToString().Trim(),
                                reader["carTitle"].ToString().Trim(),
                                reader["colour"].ToString().Trim(),
                                int.Parse(reader["isMatched"].ToString().Trim())
                            );
                        }
                    }
                    finally
                    {
                        reader.Close();
                        connection.Close();
                    }
                }

                // check if car exists
                if (theCar == null)
                {
                    return Json(new JSONResult("Car does not exist, or it is not yours!"), JsonRequestBehavior.AllowGet);
                }

                SwapRequest request = null;
                const String QUERY_STRING2 = "SELECT * FROM Swaprequests WHERE id = @requestId AND requestUserCarId = @carId";

                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING2, connection);
                    cmd.Parameters.AddWithValue("@requestId", requestId);
                    cmd.Parameters.AddWithValue("@carId", carId);

                    connection.Open();

                    MySqlDataReader reader = cmd.ExecuteReader();
                    try
                    {
                        while (reader.Read())
                        {
                            request = new SwapRequest(
                                int.Parse(reader["id"].ToString().Trim()),
                                int.Parse(reader["requestCarId"].ToString().Trim()),
                                int.Parse(reader["targetCarId"].ToString().Trim()),
                                int.Parse(reader["requestUserCarId"].ToString().Trim()),
                                int.Parse(reader["targetUserCarId"].ToString().Trim()),
                                int.Parse(reader["requestUserId"].ToString().Trim()),
                                int.Parse(reader["targetUserId"].ToString().Trim()),
                                reader["requestUserName"].ToString().Trim(),
                                reader["targetUserName"].ToString().Trim(),
                                reader["requestUserCarTitle"].ToString().Trim(),
                                reader["targetUserCarTitle"].ToString().Trim(),
                                reader["requestUserCarColour"].ToString().Trim(),
                                reader["targetUserCarColour"].ToString().Trim(),
                                int.Parse(reader["confirmed"].ToString().Trim())
                            );
                        }
                    }
                    finally
                    {
                        reader.Close();
                        connection.Close();
                    }
                }

                // check if car ID is same as given in request
                if (request.requestUserCarId != carId)
                {
                    return Json(new JSONResult("Car ID and request ID do not match!"), JsonRequestBehavior.AllowGet);
                }

                const String QUERY_STRING3 = "DELETE FROM Swaprequests WHERE id = @requestId";

                // delete request
                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING3, connection);
                    cmd.Parameters.AddWithValue("@requestId", requestId);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                    connection.Close();
                }

                return Json(new JSONResult(), JsonRequestBehavior.AllowGet);
            }
            catch
            {
                return Json(new JSONResult("Unknown error occurred!"), JsonRequestBehavior.AllowGet);
            }
        }
        /* END Swap Requests */

        /* START Matching Engine */

        // Generates the latest data model for the recommender [Static]
        IDataModel GetDataModel()
        {
            List<Rating> ratings = new List<Rating>();
            List<SwapRequest> requests = new List<SwapRequest>();
            
            const String QUERY_STRING1 = "SELECT * FROM Ratings";

            using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
            {
                MySqlCommand cmd = new MySqlCommand(QUERY_STRING1, connection);

                connection.Open();

                MySqlDataReader reader = cmd.ExecuteReader();

                try
                {
                    while (reader.Read())
                    {
                        ratings.Add(
                            new Rating(
                                int.Parse(reader["id"].ToString().Trim()),
                                int.Parse(reader["carId"].ToString().Trim()),
                                int.Parse(reader["userId"].ToString().Trim()),
                                reader["carTitle"].ToString().Trim(),
                                int.Parse(reader["stars"].ToString().Trim())
                            )
                        );
                    }

                }
                finally
                {
                    reader.Close();
                    connection.Close();
                }
            }

            const String QUERY_STRING2 = "SELECT * FROM Swaprequests WHERE confirmed != 0";

            using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
            {
                MySqlCommand cmd = new MySqlCommand(QUERY_STRING2, connection);

                connection.Open();

                MySqlDataReader reader = cmd.ExecuteReader();
                try
                {
                    while (reader.Read())
                    {
                        requests.Add(
                            new SwapRequest(
                                int.Parse(reader["id"].ToString().Trim()),
                                int.Parse(reader["requestCarId"].ToString().Trim()),
                                int.Parse(reader["targetCarId"].ToString().Trim()),
                                int.Parse(reader["requestUserCarId"].ToString().Trim()),
                                int.Parse(reader["targetUserCarId"].ToString().Trim()),
                                int.Parse(reader["requestUserId"].ToString().Trim()),
                                int.Parse(reader["targetUserId"].ToString().Trim()),
                                reader["requestUserName"].ToString().Trim(),
                                reader["targetUserName"].ToString().Trim(),
                                reader["requestUserCarTitle"].ToString().Trim(),
                                reader["targetUserCarTitle"].ToString().Trim(),
                                reader["requestUserCarColour"].ToString().Trim(),
                                reader["targetUserCarColour"].ToString().Trim(),
                                int.Parse(reader["confirmed"].ToString().Trim())
                            )
                        );
                    }
                }
                finally
                {
                    reader.Close();
                    connection.Close();
                }
            }

            FastByIDMap<IList<IPreference>> data = new FastByIDMap<IList<IPreference>>();

            // for each rating
            // add preference score as same number as the rating
            for (int i = 0; i < ratings.Count; i++)
            {
                Rating rating = ratings[i];

                long userId = Convert.ToInt64(rating.userId);
                long itemId = Convert.ToInt64(rating.carId);

                var userPrefs = data.Get(userId);

                if (userPrefs == null)
                {
                    userPrefs = new List<IPreference>(MAX_PREFERENCES);
                    data.Put(userId, userPrefs);
                }

                float prefVal = Convert.ToSingle(rating.stars);
                userPrefs.Add(new GenericPreference(userId, itemId, prefVal));
            }

            // for each request
            // for rejected requests, we will add a preference score of 1 (lowest)
            // for accepted requests, we will add a preference score of 5 (highest)
            // NOTE: the initiator of the request doesn't have their ratings modified as
            // we are assuming that just because they offer it to swap doesn't mean they prefer it.

            for (int i = 0; i < requests.Count; i++)
            {
                SwapRequest request = requests[i];

                long userId = Convert.ToInt64(request.targetUserId);
                long itemId = Convert.ToInt64(request.requestCarId);

                var userPrefs = data.Get(userId);

                if (userPrefs == null)
                {
                    userPrefs = new List<IPreference>(MAX_PREFERENCES);
                    data.Put(userId, userPrefs);
                }

                // by default set to accepted
                float prefVal = 5;

                // 2 means rejected, so we assume that the recipient didn't like the car
                if (request.confirmed == 2)
                    prefVal = 1;

                userPrefs.Add(new GenericPreference(userId, itemId, prefVal));
            }

            var newData = new FastByIDMap<IPreferenceArray>(data.Count());

            foreach (var entry in data.EntrySet())
            {
                var prefList = (List<IPreference>)entry.Value;
                newData.Put(entry.Key, 
                    (IPreferenceArray)new GenericUserPreferenceArray(prefList));
            }

            return new GenericDataModel(newData);
        }

        // GET: Get cars of a user with a similar taste of the given ratings by an anonymous user [JSON]
        [HttpGet]
        [AllowAnonymous]
        public ActionResult RecommenderDemo(String json)
        {
            // we only want unregistered users to use this
            if (Request.IsAuthenticated)
                return Json(new JSONResult("This is for unregistered users only!"), JsonRequestBehavior.AllowGet);

            dynamic ratings = JArray.Parse(json);

            // create a temporary model to process the given anonymous user data
            var model = GetDataModel();
            var plusAnonymModel = new PlusAnonymousUserDataModel(model);

            // set the ratings given by the anonymous user
            var prefArr = new GenericUserPreferenceArray(ratings.Count);
            prefArr.SetUserID(0, PlusAnonymousUserDataModel.TEMP_USER_ID);

            for (int i = 0; i < ratings.Count; i++)
            {
                var rating = ratings[i];

                prefArr.SetItemID(i, (long) rating.carId);
                prefArr.SetValue(i, (float) rating.stars);
            }

            plusAnonymModel.SetTempPrefs(prefArr);
            var similarity = new LogLikelihoodSimilarity(plusAnonymModel);
            var neighborhood = new NearestNUserNeighborhood(15, similarity, plusAnonymModel);
            var recommender = new GenericUserBasedRecommender(plusAnonymModel, neighborhood, similarity);

            // get a user with similar tastes
            long userId;

            try
            {
                userId = recommender.MostSimilarUserIDs(PlusAnonymousUserDataModel.TEMP_USER_ID, 1).First();
            }
            catch
            {
                return Json(new JSONResult("Not enough rating data! Try rating more cars."), JsonRequestBehavior.AllowGet);
            }

            // get the similar user's cars
            List<UserCar> cars = new List<UserCar>();

            const String QUERY_STRING = "SELECT * FROM Usercars WHERE userId = @userId";

            using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
            {
                MySqlCommand cmd = new MySqlCommand(QUERY_STRING, connection);
                cmd.Parameters.AddWithValue("@userId", userId);

                connection.Open();

                MySqlDataReader reader = cmd.ExecuteReader();

                try
                {
                    while (reader.Read() && cars.Count < 20) // return maximum of 20
                    {
                        cars.Add(
                            new UserCar(
                                int.Parse(reader["id"].ToString().Trim()),
                                int.Parse(reader["carId"].ToString().Trim()),
                                int.Parse(reader["userId"].ToString().Trim()),
                                reader["userName"].ToString().Trim(),
                                reader["carTitle"].ToString().Trim(),
                                reader["colour"].ToString().Trim(),
                                int.Parse(reader["isMatched"].ToString().Trim())
                            )
                        );
                    }
                }
                finally
                {
                    reader.Close();
                    connection.Close();
                }
            }

            return Json(cars, JsonRequestBehavior.AllowGet);
        }

        // GET: Get estimated preference of a car for a single user
        [HttpGet]
        public ActionResult EstimatePreferenceForCar(String json)
        {
            int creditType = availableCredits(10);

            if (creditType == 0)
            {
                return Json("Not enough credits!", JsonRequestBehavior.AllowGet);
            }
            else if (creditType == 2) {
                deductCredits(10);
            }

            dynamic data = JObject.Parse(json);

            long userId = data.userId;
            long itemId = data.carId;

            var model = GetDataModel();
            var similarity = new LogLikelihoodSimilarity(model);
            var neighborhood = new NearestNUserNeighborhood(15, similarity, model);
            var recommender = new GenericUserBasedRecommender(model, neighborhood, similarity);
            float preference = recommender.EstimatePreference(userId, itemId);

            return Json(preference.ToString("#.##"), JsonRequestBehavior.AllowGet);
        }

        // GET: Cars from similar users
        [HttpGet]
        public ActionResult GetSimilarUserCars()
        {
            int creditType = availableCredits(20);

            if (creditType == 0)
            {
                return Json("Not enough credits!", JsonRequestBehavior.AllowGet);
            }
            else if (creditType == 2)
            {
                deductCredits(20);
            }

            var model = GetDataModel();
            var similarity = new LogLikelihoodSimilarity(model);
            var neighborhood = new NearestNUserNeighborhood(15, similarity, model);
            var recommender = new GenericUserBasedRecommender(model, neighborhood, similarity);
            int userId = int.Parse(User.Identity.GetUserId());

            long[] userIds;

            // get users with similar tastes
            try
            {
                userIds = recommender.MostSimilarUserIDs(userId, 3);
            }
            catch
            {
                return Json(new JSONResult("Not enough rating data! Try rating more cars."), JsonRequestBehavior.AllowGet);
            }

            List<UserCar> cars = new List<UserCar>();

            int i = 0;

            while (i < userIds.Length && cars.Count < 10)
            {
                const String QUERY_STRING = "SELECT * FROM Usercars WHERE userId = @userId";

                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING, connection);
                    cmd.Parameters.AddWithValue("@userId", userIds[i]);

                    connection.Open();

                    MySqlDataReader reader = cmd.ExecuteReader();

                    try
                    {
                        while (reader.Read() && cars.Count < 10) // return maximum of 10 cars
                        {
                            cars.Add(
                                new UserCar(
                                    int.Parse(reader["id"].ToString().Trim()),
                                    int.Parse(reader["carId"].ToString().Trim()),
                                    int.Parse(reader["userId"].ToString().Trim()),
                                    reader["userName"].ToString().Trim(),
                                    reader["carTitle"].ToString().Trim(),
                                    reader["colour"].ToString().Trim(),
                                    int.Parse(reader["isMatched"].ToString().Trim())
                                )
                            );
                        }
                    }
                    finally
                    {
                        reader.Close();
                        connection.Close();
                    }
                }

                i++;
            }

            if (cars.Count < 1)
                return Json(new JSONResult("Not enough rating data! Try rating more cars."), JsonRequestBehavior.AllowGet);

            return Json(cars, JsonRequestBehavior.AllowGet);
        }

        /* END Matching Engine */

        /* START Credits */
        // Check if can do an action
        [HttpGet]
        public ActionResult CanDoCommercialAction(String json)
        {
            dynamic data = JObject.Parse(json);
            int amount = data.amount;
            int credits = 0;
            bool hasSubscription = false;

            int userId = int.Parse(User.Identity.GetUserId());

            const String QUERY_STRING = "SELECT * FROM Userextend WHERE userId = @userId";

            using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
            {
                MySqlCommand cmd = new MySqlCommand(QUERY_STRING, connection);
                cmd.Parameters.AddWithValue("@userId", userId);

                connection.Open();

                MySqlDataReader reader = cmd.ExecuteReader();

                try
                {
                    while (reader.Read())
                    {
                        credits = int.Parse(reader["credits"].ToString().Trim());
                        if (reader["subscriptionExpiry"].ToString().Trim() != "")
                        {
                            hasSubscription = DateTime.ParseExact(reader["subscriptionExpiry"].ToString().Trim(), "dd/MM/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) >= DateTime.Now;
                        }
                    }
                }
                finally
                {
                    reader.Close();
                    connection.Close();
                }
            }

            // if there's a valid subscription then they can do it without deducting credits
            if (hasSubscription)
                return Json(true, JsonRequestBehavior.AllowGet);

            // determine how many credits are left after deducting the amount
            int newCredits = credits - amount;

            // stop if not enough credits to perform the action
            if (newCredits < 0)
                return Json(false, JsonRequestBehavior.AllowGet);

            return Json(true, JsonRequestBehavior.AllowGet);
        }


        // Get the type of credits available
        public int availableCredits(int amount)
        {
            bool hasSubscription = false;
            int credits = 0;

            int userId = int.Parse(User.Identity.GetUserId());

            const String QUERY_STRING = "SELECT * FROM Userextend WHERE userId = @userId";

            using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
            {
                MySqlCommand cmd = new MySqlCommand(QUERY_STRING, connection);
                cmd.Parameters.AddWithValue("@userId", userId);

                connection.Open();

                MySqlDataReader reader = cmd.ExecuteReader();

                try
                {
                    while (reader.Read())
                    {
                        credits = int.Parse(reader["credits"].ToString().Trim());
                        if (reader["subscriptionExpiry"].ToString().Trim() != "")
                        {
                            hasSubscription = DateTime.ParseExact(reader["subscriptionExpiry"].ToString().Trim(), "dd/MM/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) >= DateTime.Now;
                        }
                    }
                }
                finally
                {
                    reader.Close();
                    connection.Close();
                }
            }

            // if there's a valid subscription then they can do it without deducting credits
            if (hasSubscription)
                return 1;

            // determine how many credits are left after deducting the amount
            int newCredits = credits - amount;

            // stop if not enough credits to perform the action
            if (newCredits < 0)
                return 0;

            return 2;
        }

        // Deduct credits from the user's account
        public void deductCredits(int amount)
        {
            int userId = int.Parse(User.Identity.GetUserId());
            int credits = 0;

            const String QUERY_STRING1 = "SELECT * FROM Userextend WHERE userId = @userId";

            using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
            {
                MySqlCommand cmd = new MySqlCommand(QUERY_STRING1, connection);
                cmd.Parameters.AddWithValue("@userId", userId);

                connection.Open();

                MySqlDataReader reader = cmd.ExecuteReader();

                try
                {
                    while (reader.Read())
                    {
                        credits = int.Parse(reader["credits"].ToString().Trim());
                    }
                }
                finally
                {
                    reader.Close();
                    connection.Close();
                }
            }

            int newCredits = credits - amount;

            // update new credits
            const String QUERY_STRING2 = "UPDATE Userextend SET credits = @credits WHERE userId = @userId";

            using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
            {
                MySqlCommand cmd = new MySqlCommand(QUERY_STRING2, connection);
                cmd.Parameters.AddWithValue("@credits", newCredits);
                cmd.Parameters.AddWithValue("@userId", userId);

                connection.Open();
                cmd.ExecuteNonQuery();
                connection.Close();
            }
        }

        /* END Credits */

        /* START Debug */
        [HttpPost]
        public ActionResult SetSubscription(String json)
        {
            dynamic data = JObject.Parse(json);
            String dExpiry = data.expiry;
            DateTime expiryDate = DateTime.ParseExact(dExpiry, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

            // MySQL format
            String expiry = expiryDate.ToString("yyyy-MM-dd HH:mm:ss");

            String credits = "";
            int userId = int.Parse(User.Identity.GetUserId());

            const String QUERY_STRING1 = "SELECT * FROM Userextend WHERE userId = @userId";

            using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
            {
                MySqlCommand cmd = new MySqlCommand(QUERY_STRING1, connection);
                cmd.Parameters.AddWithValue("@userId", userId);

                connection.Open();

                MySqlDataReader reader = cmd.ExecuteReader();

                try
                {
                    while (reader.Read())
                    {
                        credits = reader["credits"].ToString().Trim();
                    }
                }
                finally
                {
                    reader.Close();
                    connection.Close();
                }
            }

            if (credits == "")
            {
                String userName = User.Identity.GetUserName();

                const String QUERY_STRING2 = "INSERT INTO Userextend(userId, username, credits, subscriptionExpiry) VALUES (@userId, @username, 0, @expiry)";

                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING2, connection);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@username", userName);
                    cmd.Parameters.AddWithValue("@expiry", expiry);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                    connection.Close();
                }
            }
            else
            {
                const String QUERY_STRING3 = "UPDATE Userextend SET subscriptionExpiry = @expiry WHERE userId = @userId";

                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING3, connection);
                    cmd.Parameters.AddWithValue("@expiry", expiry);
                    cmd.Parameters.AddWithValue("@userId", userId);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                    connection.Close();
                }
            }

            return Json(true, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult RemoveSubscription()
        {
            String credits = "";
            int userId = int.Parse(User.Identity.GetUserId());

            const String QUERY_STRING1 = "SELECT * FROM Userextend WHERE userId = @userId";

            using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
            {
                MySqlCommand cmd = new MySqlCommand(QUERY_STRING1, connection);
                cmd.Parameters.AddWithValue("@userId", userId);

                connection.Open();

                MySqlDataReader reader = cmd.ExecuteReader();

                try
                {
                    while (reader.Read())
                    {
                        credits = reader["credits"].ToString().Trim();
                    }
                }
                finally
                {
                    reader.Close();
                    connection.Close();
                }
            }

            if (credits == "")
            {
                String userName = User.Identity.GetUserName();

                const String QUERY_STRING2 = "INSERT INTO Userextend(userId, username, credits, subscriptionExpiry) VALUES (@userId, @userName, 0, NULL)";

                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING2, connection);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@userName", userName);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                    connection.Close();
                }
            }
            else
            {
                const String QUERY_STRING3 = "UPDATE Userextend SET subscriptionExpiry = NULL WHERE userId = @userId";

                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING3, connection);
                    cmd.Parameters.AddWithValue("@userId", userId);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                    connection.Close();
                }
            }

            return Json(true, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult Set100Credits()
        {
            String credits = "";
            int userId = int.Parse(User.Identity.GetUserId());

            const String QUERY_STRING1 = "SELECT * FROM Userextend WHERE userId = @userId";

            using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
            {
                MySqlCommand cmd = new MySqlCommand(QUERY_STRING1, connection);
                cmd.Parameters.AddWithValue("@userId", userId);

                connection.Open();

                MySqlDataReader reader = cmd.ExecuteReader();

                try
                {
                    while (reader.Read())
                    {
                        credits = reader["credits"].ToString().Trim();
                    }
                }
                finally
                {
                    reader.Close();
                    connection.Close();
                }
            }

            if (credits == "")
            {
                String userName = User.Identity.GetUserName();

                const String QUERY_STRING2 = "INSERT INTO Userextend(userId, username, credits, subscriptionExpiry) VALUES (@userId, @userName, 100, NULL)";

                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING2, connection);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@userName", userName);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                    connection.Close();
                }
            }
            else
            {
                const String QUERY_STRING3 = "UPDATE Userextend SET credits = 100 WHERE userId = @userId";

                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING3, connection);
                    cmd.Parameters.AddWithValue("@userId", userId);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                    connection.Close();
                }
            }

            return Json(true, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult RemoveCredits()
        {
            String credits = "";
            int userId = int.Parse(User.Identity.GetUserId());

            const String QUERY_STRING1 = "SELECT * FROM Userextend WHERE userId = @userId";

            using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
            {
                MySqlCommand cmd = new MySqlCommand(QUERY_STRING1, connection);
                cmd.Parameters.AddWithValue("@userId", userId);

                connection.Open();

                MySqlDataReader reader = cmd.ExecuteReader();

                try
                {
                    while (reader.Read())
                    {
                        credits = reader["credits"].ToString().Trim();
                    }
                }
                finally
                {
                    reader.Close();
                    connection.Close();
                }
            }

            if (credits == "")
            {
                String userName = User.Identity.GetUserName();

                const String QUERY_STRING2 = "INSERT INTO Userextend(userId, username, credits, subscriptionExpiry) VALUES (@userId, @userName, 0, NULL)";

                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING2, connection);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@userName", userName);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                    connection.Close();
                }
            }
            else
            {
                const String QUERY_STRING3 = "UPDATE Userextend SET credits = 0 WHERE userId = @userId";

                using (MySqlConnection connection = new MySqlConnection(CONNECTION_STRING))
                {
                    MySqlCommand cmd = new MySqlCommand(QUERY_STRING3, connection);
                    cmd.Parameters.AddWithValue("@userId", userId);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                    connection.Close();
                }
            }

            return Json(true, JsonRequestBehavior.AllowGet);
        }
        /* END Debug */
    }
}