using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace ClassicCars.Models
{
    public class Car
    {
        public Car(int id, string mpg, string cylinders, string fuel, string engine, string horsepower, string weight, string acceleration, string year, string origin, string manufacturer, string title)
        {
            this.id = id;
            this.mpg = mpg;
            this.cylinders = cylinders;
            this.fuel = fuel;
            this.engine = engine;
            this.horsepower = horsepower;
            this.weight = weight;
            this.acceleration = acceleration;
            this.year = year;
            this.origin = origin;
            this.manufacturer = manufacturer;
            this.title = title;
        }
        public int id { get; set; }
        [Required]
        public string mpg { get; set; }
        [Required]
        public string cylinders { get; set; }
        [Required]
        public string fuel { get; set; }
        [Required]
        public string engine { get; set; }
        [Required]
        public string horsepower { get; set; }
        [Required]
        public string weight { get; set; }
        [Required]
        public string acceleration { get; set; }
        [Required]
        public string year { get; set; }
        [Required]
        public string origin { get; set; }
        [Required]
        public string manufacturer { get; set; }
        [Required]
        public string title { get; set; }
    }
}
