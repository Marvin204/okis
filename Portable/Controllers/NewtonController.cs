using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using MathNet.Symbolics;
using Portable.Models;
using Newtonsoft.Json;

namespace Portable.Controllers
{
    public class NewtonController : Controller
    {
        public ActionResult VistaNewton()
        {
            return View();
        }

        [HttpPost]
        public ActionResult VistaNewton(Ecuaciones modelo)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Mensaje = "Por favor, complete todos los campos requeridos.";
                return View(modelo);
            }

            if (Session["UsuarioNombre"] == null)
            {
                return RedirectToAction("IniciarSesion", "Cuenta");
            }

            try
            {
                int idUsuario = MetodosEstaticos.ObtenerIdUsuario(Session["UsuarioNombre"].ToString());
                int idEcuacion;
                int idResultado;
                var iteraciones = new List<Iteraciones>();

                using (SqlConnection conn = ConexionDBController.ObtenerConexion())
                {
                    conn.Open();

                    // 1. Insertar ecuación
                    string queryEcuacion = @"
                        INSERT INTO Ecuaciones (Funcion, ValorInicial1, Tolerancia, MaxIteraciones, IdUsuario)
                        OUTPUT INSERTED.IdEcuacion
                        VALUES (@Funcion, @Val1, @Tol, @MaxIter, @IdUsuario)";
                    using (var cmd = new SqlCommand(queryEcuacion, conn))
                    {
                        cmd.Parameters.AddWithValue("@Funcion", modelo.Funcion);
                        cmd.Parameters.AddWithValue("@Val1", (object)modelo.ValorInicial1 ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Tol", modelo.Tolerancia);
                        cmd.Parameters.AddWithValue("@MaxIter", modelo.MaxIteraciones);
                        cmd.Parameters.AddWithValue("@IdUsuario", idUsuario);

                        idEcuacion = (int)cmd.ExecuteScalar();
                    }

                    // 2. Resolver método Newton-Raphson
                    var xVar = SymbolicExpression.Variable("x");
                    var funcion = SymbolicExpression.Parse(modelo.Funcion);
                    var derivada = funcion.Differentiate(xVar);

                    iteraciones = ResolverNewtonRaphson(funcion, derivada, modelo.ValorInicial1.Value, modelo.Tolerancia, modelo.MaxIteraciones);

                    if (iteraciones.Count == 0)
                    {
                        ViewBag.Mensaje = "No se generaron iteraciones. Verifique los datos ingresados.";
                        return View(modelo);
                    }

                    // Última iteración
                    var ultima = iteraciones.Last();
                    double errorFinal = double.IsNaN(ultima.Error) || double.IsInfinity(ultima.Error) ? 0.0 : ultima.Error;

                    // 3. Insertar resultado
                    string queryResultado = @"
                        INSERT INTO Resultados (ResultadoFinal, Iteraciones, ErrorEstimado, FechaResultado, IdEcuacion, IdMetodo)
                        OUTPUT INSERTED.IdResultado
                        VALUES (@ResultadoFinal, @Iteraciones, @ErrorEstimado, @FechaResultado, @IdEcuacion, @IdMetodo)";
                    using (var cmd = new SqlCommand(queryResultado, conn))
                    {
                        cmd.Parameters.AddWithValue("@ResultadoFinal", ultima.X0);
                        cmd.Parameters.AddWithValue("@Iteraciones", iteraciones.Count);
                        cmd.Parameters.AddWithValue("@ErrorEstimado", errorFinal);
                        cmd.Parameters.AddWithValue("@FechaResultado", DateTime.Now);
                        cmd.Parameters.AddWithValue("@IdEcuacion", idEcuacion);
                        cmd.Parameters.AddWithValue("@IdMetodo", 1); // Newton-Raphson

                        idResultado = (int)cmd.ExecuteScalar();

                    }


                    // 4. Insertar iteraciones
                    foreach (var it in iteraciones)
                    {
                        double err = double.IsNaN(it.Error) || double.IsInfinity(it.Error) ? 0.0 : it.Error;

                        string queryIteracion = @"
                            INSERT INTO Iteraciones (Numero, X0, FX0, FX1, Error, IdResultado)
                            VALUES (@Numero, @X0, @FX0, @FX1, @Error, @IdResultado)";
                        using (var cmd = new SqlCommand(queryIteracion, conn))
                        {
                            cmd.Parameters.AddWithValue("@Numero", it.Numero);
                            cmd.Parameters.AddWithValue("@X0", it.X0);
                            cmd.Parameters.AddWithValue("@FX0", it.FX0);
                            cmd.Parameters.AddWithValue("@FX1", it.FX1);
                            cmd.Parameters.AddWithValue("@Error", err);
                            cmd.Parameters.AddWithValue("@IdResultado", idResultado);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Enviar datos para graficar
                    ViewBag.PuntosGrafica = JsonConvert.SerializeObject(
                        iteraciones.Select(it => new { x = it.X0, y = it.FX0 }).ToList()
                    );
                    ViewBag.IdResultado = idResultado;
                    ViewBag.Iteraciones = iteraciones;
                    ViewBag.Mensaje = "✅ Ecuación resuelta correctamente.";
                }

                return View(modelo);
            }
            catch (Exception)
            {
                ViewBag.Mensaje = "⚠️ Error al procesar la ecuación. Asegúrese de usar solo la variable 'x' y escribir la función correctamente.";
                return View(modelo);
            }
        }

        private List<Iteraciones> ResolverNewtonRaphson(SymbolicExpression funcion, SymbolicExpression derivada, double x0, double tolerancia, int maxIteraciones)
        {
            var iteraciones = new List<Iteraciones>();
            double x = x0;
            double error = double.MaxValue;
            int iter = 1;

            while (error > tolerancia && iter <= maxIteraciones)
            {
                var valores = new Dictionary<string, FloatingPoint> { { "x", x } };
                double fx = funcion.Evaluate(valores).RealValue;
                double dfx = derivada.Evaluate(valores).RealValue;

                if (Math.Abs(dfx) < 1e-14)
                    throw new Exception($"La derivada es cero en la iteración {iter}");

                double xNuevo = x - fx / dfx;
                error = Math.Abs(xNuevo - x);

                iteraciones.Add(new Iteraciones
                {
                    Numero = iter,
                    X0 = x,
                    FX0 = fx,
                    FX1 = dfx,
                    Error = error
                });

                x = xNuevo;
                iter++;
            }

            return iteraciones;
        }
    }
}
