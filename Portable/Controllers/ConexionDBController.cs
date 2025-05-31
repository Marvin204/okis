using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Portable.Controllers
{
    public class ConexionDBController : Controller
    {
        public static SqlConnection ObtenerConexion()
        {
            string cadena = ConfigurationManager.ConnectionStrings["Conexion5"].ConnectionString;
            return new SqlConnection(cadena);
        }
    }
}