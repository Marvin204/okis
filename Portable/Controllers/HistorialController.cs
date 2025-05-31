using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Web.Mvc;
using iTextSharp.text.pdf;
using iTextSharp.text;
using Portable.Models;

namespace Portable.Controllers
{
    public class HistorialController : Controller
    {


        public ActionResult GraficaIteraciones(int idResultado)
        {
            List<Iteraciones> iteraciones = new List<Iteraciones>();

            using (SqlConnection conn = ConexionDBController.ObtenerConexion())
            {
                conn.Open();
                string query = "SELECT X0, FX0 FROM Iteraciones WHERE IdResultado = @IdResultado ORDER BY Numero";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@IdResultado", idResultado);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            iteraciones.Add(new Iteraciones
                            {
                                X0 = reader["X0"] != DBNull.Value ? Convert.ToDouble(reader["X0"]) : 0,
                                FX0 = reader["FX0"] != DBNull.Value ? Convert.ToDouble(reader["FX0"]) : 0
                            });
                        }
                    }
                }
            }

            return PartialView("_GraficaIteraciones", iteraciones);
        }


        public ActionResult ExportarIteracionesPDF(int idResultado)
        {
            // Obtener las iteraciones asociadas a ese resultado
            List<Iteraciones> iteraciones = new List<Iteraciones>();

            using (SqlConnection conn = ConexionDBController.ObtenerConexion())
            {
                conn.Open();
                string query = "SELECT Numero, X0, X1, X2, FX0, FX1, FX2, Error FROM Iteraciones WHERE IdResultado = @IdResultado";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@IdResultado", idResultado);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            iteraciones.Add(new Iteraciones
                            {
                                Numero = reader["Numero"] != DBNull.Value ? Convert.ToInt32(reader["Numero"]) : 0,
                                X0 = reader["X0"] != DBNull.Value ? Convert.ToDouble(reader["X0"]) : 0,
                                X1 = reader["X1"] != DBNull.Value ? Convert.ToDouble(reader["X1"]) : 0,
                                X2 = reader["X2"] != DBNull.Value ? Convert.ToDouble(reader["X2"]) : 0,
                                FX0 = reader["FX0"] != DBNull.Value ? Convert.ToDouble(reader["FX0"]) : 0,
                                FX1 = reader["FX1"] != DBNull.Value ? Convert.ToDouble(reader["FX1"]) : 0,
                                FX2 = reader["FX2"] != DBNull.Value ? Convert.ToDouble(reader["FX2"]) : (double?)null,
                                Error = reader["Error"] != DBNull.Value ? Convert.ToDouble(reader["Error"]) : 0
                            });
                        }


                    }

                }
            }

            // Crear el documento PDF
            MemoryStream ms = new MemoryStream();
            Document doc = new Document();
            PdfWriter.GetInstance(doc, ms).CloseStream = false;
            doc.Open();

            Paragraph title = new Paragraph("Tabla de Iteraciones", new Font(Font.FontFamily.HELVETICA, 18, Font.BOLD));
            title.Alignment = Element.ALIGN_CENTER;
            doc.Add(title);
            doc.Add(new Paragraph(" ")); // Espacio

            PdfPTable table = new PdfPTable(8); // 8 columnas
            table.WidthPercentage = 100;
            table.AddCell("N°");
            table.AddCell("X0");
            table.AddCell("X1");
            table.AddCell("X2");
            table.AddCell("F(X0)");
            table.AddCell("F(X1)");
            table.AddCell("F(X2)");
            table.AddCell("Error");

            foreach (var it in iteraciones)
            {
                table.AddCell(it.Numero.ToString());
                table.AddCell(it.X0.ToString("F6"));
                table.AddCell(it.X1.ToString("F6"));
                table.AddCell(it.X2.ToString("F6"));
                table.AddCell(it.FX0.ToString("F6"));
                table.AddCell(it.FX1.ToString("F6"));
                table.AddCell(it.FX2.HasValue ? it.FX2.Value.ToString("F6") : "-");
                table.AddCell(it.Error.ToString("F6"));
            }

            doc.Add(table);
            doc.Close();

            ms.Position = 0;
            return File(ms, "application/pdf", "IteracionesResultado_" + idResultado + ".pdf");
        }
        public ActionResult VistaHistorial()
        {
            if (Session["IdUsuario"] == null)
                return RedirectToAction("IniciarSesion", "Cuenta");

            int idUsuario = (int)Session["IdUsuario"];
            List<Resultados> resultados = new List<Resultados>();

            using (SqlConnection conn = ConexionDBController.ObtenerConexion())
            {
                conn.Open();

                string queryResultados = @"
                    SELECT r.IdResultado, r.ResultadoFinal, r.Iteraciones, r.ErrorEstimado, r.FechaResultado,
                           r.IdMetodo, r.IdEcuacion, m.NombreMetodo, e.Funcion
                    FROM Resultados r
                    INNER JOIN MetodosNumericos m ON r.IdMetodo = m.IdMetodo
                    INNER JOIN Ecuaciones e ON r.IdEcuacion = e.IdEcuacion
                    WHERE e.IdUsuario = @IdUsuario
                    ORDER BY r.FechaResultado DESC";

                using (SqlCommand cmdResultados = new SqlCommand(queryResultados, conn))
                {
                    cmdResultados.Parameters.AddWithValue("@IdUsuario", idUsuario);

                    using (SqlDataReader reader = cmdResultados.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            resultados.Add(new Resultados
                            {
                                IdResultado = Convert.ToInt32(reader["IdResultado"]),
                                ResultadoFinal = reader["ResultadoFinal"] == DBNull.Value ? 0.0 : Convert.ToDouble(reader["ResultadoFinal"]),
                                Iteraciones = reader["Iteraciones"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Iteraciones"]),
                                ErrorEstimado = reader["ErrorEstimado"] == DBNull.Value ? 0.0 : Convert.ToDouble(reader["ErrorEstimado"]),
                                FechaResultado = Convert.ToDateTime(reader["FechaResultado"]),
                                IdMetodo = Convert.ToInt32(reader["IdMetodo"]),
                                IdEcuacion = Convert.ToInt32(reader["IdEcuacion"]),
                                MetodoNumerico = new MetodosNumericos
                                {
                                    NombreMetodo = reader["NombreMetodo"].ToString()
                                },
                                Ecuacion = new Ecuaciones
                                {
                                    Funcion = reader["Funcion"].ToString()
                                },
                                ListaIteraciones = new List<Iteraciones>()
                            });
                        }

                    }
                }

                string queryIteraciones = @"
                    SELECT IdIteracion, Numero, X0, X1, X2, FX0, FX1, FX2, Error, IdResultado
                    FROM Iteraciones
                    WHERE IdResultado = @IdResultado
                    ORDER BY Numero";

                foreach (var resultado in resultados)
                {
                    using (SqlCommand cmdIter = new SqlCommand(queryIteraciones, conn))
                    {
                        cmdIter.Parameters.Clear();
                        cmdIter.Parameters.AddWithValue("@IdResultado", resultado.IdResultado);

                        using (SqlDataReader readerIter = cmdIter.ExecuteReader())
                        {
                            while (readerIter.Read())
                            {
                                // Validar que al menos una columna clave no esté vacía
                                bool tieneDatos = !(readerIter["X0"] is DBNull &&
                                                    readerIter["X1"] is DBNull &&
                                                    readerIter["X2"] is DBNull &&
                                                    readerIter["FX0"] is DBNull &&
                                                    readerIter["FX1"] is DBNull &&
                                                    readerIter["FX2"] is DBNull &&
                                                    readerIter["Error"] is DBNull);

                                if (tieneDatos)
                                {
                                    resultado.ListaIteraciones.Add(new Iteraciones
                                    {
                                        IdIteracion = Convert.ToInt32(readerIter["IdIteracion"]),
                                        Numero = Convert.ToInt32(readerIter["Numero"]),
                                        X0 = readerIter["X0"] == DBNull.Value ? 0.0 : Convert.ToDouble(readerIter["X0"]),
                                        X1 = readerIter["X1"] == DBNull.Value ? 0.0 : Convert.ToDouble(readerIter["X1"]),
                                        X2 = readerIter["X2"] == DBNull.Value ? 0.0 : Convert.ToDouble(readerIter["X2"]),
                                        FX0 = readerIter["FX0"] == DBNull.Value ? 0.0 : Convert.ToDouble(readerIter["FX0"]),
                                        FX1 = readerIter["FX1"] == DBNull.Value ? 0.0 : Convert.ToDouble(readerIter["FX1"]),
                                        FX2 = readerIter["FX2"] == DBNull.Value ? (double?)null : Convert.ToDouble(readerIter["FX2"]),
                                        Error = readerIter["Error"] == DBNull.Value ? 0.0 : Convert.ToDouble(readerIter["Error"]),
                                        IdResultado = Convert.ToInt32(readerIter["IdResultado"])
                                    });
                                }
                            }


                        }
                    }
                }
            }

            return View(resultados);
        }
    }
}
