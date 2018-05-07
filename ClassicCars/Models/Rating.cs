using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace ClassicCars.Models
{
    public class Rating
    {
        public Rating(int carId, int userId, string carTitle, int stars)
        {
            this.carId = carId;
            this.userId = userId;
            this.carTitle = carTitle;
            this.stars = stars;
        }
        public Rating(int id, int carId, int userId, string carTitle, int stars)
        {
            this.id = id;
            this.carId = carId;
            this.userId = userId;
            this.carTitle = carTitle;
            this.stars = stars;
        }
        public int id { get; set; }
        [Required]
        public int carId { get; set; }
        [Required]
        public int userId { get; set; }
        [Required]
        public string carTitle { get; set; }
        [Required]
        public int stars { get; set; }
    }
}
