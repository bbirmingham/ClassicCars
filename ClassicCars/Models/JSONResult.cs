using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ClassicCars.Models
{
    // class to send JSON responses
    // by using a class in a pseudo-JSON way
    // provides a neat way to send back data and errors to the client
    public class JSONResult
    {
        private bool success;
        private String error;

        // by default set success to true
        public JSONResult()
        {
            this.success = true;
        }

        // set error
        public JSONResult(String error)
        {
            this.error = error;
        }
    }
}