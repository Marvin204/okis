using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using iTextSharp.text.pdf.draw;
using iTextSharp.text.pdf;
using iTextSharp.text;
using Portable.Models;

namespace Portable.Controllers
{
    public class ReportesController : Controller
    {
        // Muestra todos los reportes para el usuario, opcionalmente filtrados por método
        public ActionResult Index(int? idMetodo = null)
        {
            if (Session["UsuarioNombre"] == null)
                return RedirectToAction("IniciarSesion", "Cuenta");

            int idUsuario = MetodosEstaticos.ObtenerIdUsuario(Session["UsuarioNombre"].ToString());
            var listaReportes = new List<Resultados>();

            using (SqlConnection conn = ConexionDBController.ObtenerConexion())
            {
                conn.Open();

                string query = @"
                    SELECT r.IdResultado, r.ResultadoFinal, r.Iteraciones, r.ErrorEstimado, r.DetalleResultado, r.FechaResultado,
                           r.IdEcuacion, r.IdMetodo,
                           e.Funcion,
                           m.NombreMetodo
                    FROM Resultados r
                    INNER JOIN Ecuaciones e ON r.IdEcuacion = e.IdEcuacion
                    INNER JOIN MetodosNumericos m ON r.IdMetodo = m.IdMetodo
                    WHERE e.IdUsuario = @IdUsuario";

                if (idMetodo.HasValue)
                {
                    query += " AND r.IdMetodo = @IdMetodo";
                }

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@IdUsuario", idUsuario);
                    if (idMetodo.HasValue)
                        cmd.Parameters.AddWithValue("@IdMetodo", idMetodo.Value);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var resultado = new Resultados
                            {
                                IdResultado = reader.GetInt32(0),
                                ResultadoFinal = reader.GetDouble(1),
                                Iteraciones = reader.GetInt32(2),
                                ErrorEstimado = reader.GetDouble(3),
                                DetalleResultado = reader.IsDBNull(4) ? null : reader.GetString(4),
                                FechaResultado = reader.GetDateTime(5),
                                IdEcuacion = reader.GetInt32(6),
                                IdMetodo = reader.GetInt32(7),
                                Ecuacion = new Ecuaciones { Funcion = reader.GetString(8) },
                                MetodoNumerico = new MetodosNumericos { NombreMetodo = reader.GetString(9) }
                            };
                            listaReportes.Add(resultado);
                        }
                    }
                }
            }

            ViewBag.IdMetodoFiltro = idMetodo;
            return View(listaReportes);
        }

        // Muestra detalle con iteraciones para un resultado
        public ActionResult Detalles(int idResultado)
        {
            if (Session["UsuarioNombre"] == null)
                return RedirectToAction("IniciarSesion", "Cuenta");

            Resultados resultado = null;

            using (SqlConnection conn = ConexionDBController.ObtenerConexion())
            {
                conn.Open();

                // Consulta para cargar datos básicos del resultado
                string queryResultado = @"
                    SELECT r.IdResultado, r.ResultadoFinal, r.Iteraciones, r.ErrorEstimado, r.DetalleResultado, r.FechaResultado,
                           r.IdEcuacion, r.IdMetodo,
                           e.Funcion,
                           m.NombreMetodo
                    FROM Resultados r
                    INNER JOIN Ecuaciones e ON r.IdEcuacion = e.IdEcuacion
                    INNER JOIN MetodosNumericos m ON r.IdMetodo = m.IdMetodo
                    WHERE r.IdResultado = @IdResultado";

                using (SqlCommand cmd = new SqlCommand(queryResultado, conn))
                {
                    cmd.Parameters.AddWithValue("@IdResultado", idResultado);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            resultado = new Resultados
                            {
                                IdResultado = reader.GetInt32(0),
                                ResultadoFinal = reader.GetDouble(1),
                                Iteraciones = reader.GetInt32(2),
                                ErrorEstimado = reader.GetDouble(3),
                                DetalleResultado = reader.IsDBNull(4) ? null : reader.GetString(4),
                                FechaResultado = reader.GetDateTime(5),
                                IdEcuacion = reader.GetInt32(6),
                                IdMetodo = reader.GetInt32(7),
                                Ecuacion = new Ecuaciones { Funcion = reader.GetString(8) },
                                MetodoNumerico = new MetodosNumericos { NombreMetodo = reader.GetString(9) }
                            };
                        }
                        else
                        {
                            return HttpNotFound();
                        }
                    }
                }

                // Cargar las iteraciones de ese resultado
                string queryIteraciones = @"
                    SELECT IdIteracion, Numero, X0, X1, X2, FX0, FX1, FX2, Error, MetodoUsado, IdResultado
                    FROM Iteraciones
                    WHERE IdResultado = @IdResultado
                    ORDER BY Numero";

                resultado.ListaIteraciones = new List<Iteraciones>();

                using (SqlCommand cmd = new SqlCommand(queryIteraciones, conn))
                {
                    cmd.Parameters.AddWithValue("@IdResultado", idResultado);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            resultado.ListaIteraciones.Add(new Iteraciones
                            {
                                IdIteracion = reader.GetInt32(0),
                                Numero = reader.GetInt32(1),
                                X0 = reader.GetDouble(2),
                                X1 = reader.GetDouble(3),
                                X2 = reader.GetDouble(4),
                                FX0 = reader.GetDouble(5),
                                FX1 = reader.GetDouble(6),
                                FX2 = reader.IsDBNull(7) ? (double?)null : reader.GetDouble(7),
                                Error = reader.GetDouble(8),
                                MetodoUsado = reader.GetString(9),
                                IdResultado = reader.GetInt32(10)
                            });
                        }
                    }
                }
            }

            return View(resultado);
        }
    }

}
