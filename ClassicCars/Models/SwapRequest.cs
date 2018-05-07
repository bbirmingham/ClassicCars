using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace ClassicCars.Models
{
    public class SwapRequest
    {
        public SwapRequest(int id, int requestCarId, int targetCarId, int requestUserCarId, int targetUserCarId, int requestUserId, int targetUserId, String requestUserName, String targetUserName, String requestUserCarTitle, String targetUserCarTitle, String requestUserCarColour, String targetUserCarColour, int confirmed)
        {
            this.id = id;
            this.requestCarId = requestCarId;
            this.targetCarId = targetCarId;
            this.requestUserCarId = requestUserCarId;
            this.targetUserCarId = targetUserCarId;
            this.requestUserId = requestUserId;
            this.targetUserId = targetUserId;
            this.requestUserName = requestUserName;
            this.targetUserName = targetUserName;
            this.requestUserCarTitle = requestUserCarTitle;
            this.targetUserCarTitle = targetUserCarTitle;
            this.requestUserCarColour = requestUserCarColour;
            this.targetUserCarColour = targetUserCarColour;
            this.confirmed = confirmed;
        }
        public int id { get; set; }
        [Required]
        public int requestCarId { get; set; }
        [Required]
        public int targetCarId { get; set; }
        [Required]
        public int requestUserCarId { get; set; }
        [Required]
        public int targetUserCarId { get; set; }
        [Required]
        public int requestUserId { get; set; }
        [Required]
        public int targetUserId { get; set; }
        [Required]
        public String requestUserName { get; set; }
        [Required]
        public String targetUserName { get; set; }
        [Required]
        public String requestUserCarTitle { get; set; }
        [Required]
        public String targetUserCarTitle { get; set; }
        [Required]
        public String requestUserCarColour { get; set; }
        [Required]
        public String targetUserCarColour { get; set; }
        public int confirmed { get; set; }
    }
}