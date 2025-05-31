using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using MathNet.Symbolics;
using Portable.Controllers;
using Expr = MathNet.Symbolics.SymbolicExpression;

namespace Portable.Models
{
    public class MetodosEstaticos
    {
        public static double EvaluarFuncion(Expr expresion, double x)
        {
            var simbolos = new Dictionary<string, FloatingPoint> { { "x", x } };
            return expresion.Evaluate(simbolos).RealValue;
        }

        public static int ObtenerIdUsuario(string nombre)
        {
            using (SqlConnection conn = ConexionDBController.ObtenerConexion())
            {
                conn.Open();
                string query = "SELECT IdUsuario FROM Usuarios WHERE Nombre = @Nombre";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Nombre", nombre);
                    return (int)cmd.ExecuteScalar();
                }
            }
        }
    }
}