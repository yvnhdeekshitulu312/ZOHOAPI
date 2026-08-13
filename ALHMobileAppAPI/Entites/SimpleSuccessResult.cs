using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ALHMobileAppAPI.Entites
{
    public class SimpleSuccessResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public long Id { get; set; }
    }
}