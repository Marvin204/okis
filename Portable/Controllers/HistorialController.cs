using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Web.Mvc;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Portable.Models;

namespace Portable.Controllers
{
    public class HistorialController : Controller
    {
        public ActionResult ExportarIteracionesPDF(int idResultado)
        {
            List<Iteraciones> iteraciones = new List<Iteraciones>();

            // Variables para info de la ecuación
            string funcion = "";
            double? valorInicial = null;
            double tolerancia = 0;
            int maxIteraciones = 0;
            DateTime fechaIngreso = DateTime.Now;

            try
            {
                using (SqlConnection conn = ConexionDBController.ObtenerConexion())
                {
                    conn.Open();

                    // Obtener datos de la ecuación
                    string ecuacionQuery = @"
                        SELECT e.Funcion, e.ValorInicial1, e.Tolerancia, e.MaxIteraciones, e.FechaIngreso
                        FROM Resultados r
                        INNER JOIN Ecuaciones e ON r.IdEcuacion = e.IdEcuacion
                        WHERE r.IdResultado = @IdResultado";

                    using (SqlCommand cmd = new SqlCommand(ecuacionQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@IdResultado", idResultado);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                funcion = reader["Funcion"].ToString();
                                valorInicial = reader["ValorInicial1"] != DBNull.Value ? (double?)Convert.ToDouble(reader["ValorInicial1"]) : null;
                                tolerancia = Convert.ToDouble(reader["Tolerancia"]);
                                maxIteraciones = Convert.ToInt32(reader["MaxIteraciones"]);
                                fechaIngreso = Convert.ToDateTime(reader["FechaIngreso"]);
                            }
                        }
                    }

                    // Obtener iteraciones
                    string query = "SELECT Numero, X0, X1, X2, FX0, FX1, FX2, Error FROM Iteraciones WHERE IdResultado = @IdResultado";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@IdResultado", idResultado);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int numero = reader["Numero"] != DBNull.Value ? Convert.ToInt32(reader["Numero"]) : 0;
                                double x0 = reader["X0"] != DBNull.Value ? Convert.ToDouble(reader["X0"]) : 0;
                                double x1 = reader["X1"] != DBNull.Value ? Convert.ToDouble(reader["X1"]) : 0;
                                double x2 = reader["X2"] != DBNull.Value ? Convert.ToDouble(reader["X2"]) : 0;
                                double fx0 = reader["FX0"] != DBNull.Value ? Convert.ToDouble(reader["FX0"]) : 0;
                                double fx1 = reader["FX1"] != DBNull.Value ? Convert.ToDouble(reader["FX1"]) : 0;
                                double? fx2 = reader["FX2"] != DBNull.Value ? (double?)Convert.ToDouble(reader["FX2"]) : null;
                                double error = reader["Error"] != DBNull.Value ? Convert.ToDouble(reader["Error"]) : 0;

                                iteraciones.Add(new Iteraciones
                                {
                                    Numero = numero,
                                    X0 = x0,
                                    X1 = x1,
                                    X2 = x2,
                                    FX0 = fx0,
                                    FX1 = fx1,
                                    FX2 = fx2,
                                    Error = error
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(500, "Error al obtener datos: " + ex.Message);
            }

            // Generar PDF
            MemoryStream ms = new MemoryStream();
            Document doc = new Document(PageSize.A4, 25, 25, 30, 30);

            try
            {
                PdfWriter writer = PdfWriter.GetInstance(doc, ms);
                writer.CloseStream = false;
                doc.Open();

                // Título
                Paragraph title = new Paragraph("Reporte de Iteraciones", new Font(Font.FontFamily.HELVETICA, 18, Font.BOLD));
                title.Alignment = Element.ALIGN_CENTER;
                doc.Add(title);
                doc.Add(new Paragraph(" "));

                // Información de la ecuación
                doc.Add(new Paragraph("Detalles del Cálculo", new Font(Font.FontFamily.HELVETICA, 14, Font.BOLD)));
                doc.Add(new Paragraph("Función: " + funcion));
                doc.Add(new Paragraph("Valor Inicial: " + (valorInicial.HasValue ? valorInicial.Value.ToString("F6") : "-")));
                doc.Add(new Paragraph("Tolerancia: " + tolerancia));
                doc.Add(new Paragraph("Máx. Iteraciones: " + maxIteraciones));
                doc.Add(new Paragraph("Fecha de ingreso: " + fechaIngreso.ToString("dd/MM/yyyy HH:mm")));
                doc.Add(new Paragraph(" "));

                if (iteraciones.Count == 0)
                {
                    Paragraph noData = new Paragraph("No se encontraron iteraciones para este resultado.",
                        new Font(Font.FontFamily.HELVETICA, 12, Font.ITALIC, BaseColor.RED));
                    noData.Alignment = Element.ALIGN_CENTER;
                    doc.Add(noData);
                }
                else
                {
                    PdfPTable table = new PdfPTable(8);
                    table.WidthPercentage = 100;

                    // Encabezados
                    string[] headers = { "N°", "X0", "X1", "X2", "F(X0)", "F(X1)", "F(X2)", "Error" };
                    foreach (string header in headers)
                    {
                        PdfPCell cell = new PdfPCell(new Phrase(header))
                        {
                            BackgroundColor = BaseColor.LIGHT_GRAY,
                            HorizontalAlignment = Element.ALIGN_CENTER
                        };
                        table.AddCell(cell);
                    }

                    // Filas
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
                }

                doc.Close();
                ms.Position = 0;

                return File(ms, "application/pdf", $"IteracionesResultado_{idResultado}.pdf");
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(500, "Error al generar PDF: " + ex.Message);
            }
            finally
            {
                if (doc.IsOpen()) doc.Close();
            }
        }
    }
}
