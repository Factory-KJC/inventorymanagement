using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryClient.Models
{
    public class AppSettings
    {
        public string Host { get; set; } = "https://localhost:44394";
        public string DefaultUsername { get; set; } = "";
        public string EncodedPassword { get; set; } = "";
        public string DecodedPassword()
        {
            if (string.IsNullOrEmpty(EncodedPassword)) return "";
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(EncodedPassword));
            }
            catch
            {
                return "";
            }
        }
    }
}