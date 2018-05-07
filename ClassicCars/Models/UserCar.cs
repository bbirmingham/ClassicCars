using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace ClassicCars.Models
{
    public class UserCar
    {
        public UserCar(int id, int carId, int userId, string userName, string carTitle, string colour, int isMatched)
        {
            this.id = id;
            this.carId = carId;
            this.userId = userId;
            this.userName = userName;
            this.carTitle = carTitle;
            this.colour = colour;
            this.isMatched = isMatched;
        }
        public UserCar(int carId, int userId, string userName, string carTitle, string colour, int isMatched)
        {
            this.carId = carId;
            this.userId = userId;
            this.userName = userName;
            this.carTitle = carTitle;
            this.colour = colour;
            this.isMatched = isMatched;
        }
        public int id { get; set; }
        [Required]
        public int carId { get; set; }
        [Required]
        public int userId { get; set; }
        [Required]
        public string userName { get; set; }
        [Required]
        public string carTitle { get; set; }
        [Required]
        public string colour { get; set; }
        public int isMatched { get; set; }
    }
}