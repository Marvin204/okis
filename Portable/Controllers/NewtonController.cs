using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MathNet.Symbolics;
using Portable.Models;
using Newtonsoft.Json;

namespace Portable.Controllers
{
    public class NewtonController : Controller
    {
        // Acción GET para mostrar la vista inicial del método Newton-Raphson
        public ActionResult VistaNewton()
        {
            return View();
        }

        // Acción POST que recibe el modelo con los datos de la ecuación para resolver Newton-Raphson
        [HttpPost]
        public ActionResult VistaNewton(Ecuaciones modelo)
        {
            // Validar que el modelo sea válido (todos los campos requeridos completos)
            if (!ModelState.IsValid)
            {
                ViewBag.Mensaje = "Por favor, complete todos los campos requeridos.";
                return View(modelo);
            }

            // Verificar que haya un usuario en sesión; si no, redirigir a iniciar sesión
            if (Session["UsuarioNombre"] == null)
            {
                return RedirectToAction("IniciarSesion", "Cuenta");
            }

            try
            {

                // Obtener el ID del usuario actual con base en su nombre de sesión
                int idUsuario = MetodosEstaticos.ObtenerIdUsuario(Session["UsuarioNombre"].ToString());

            int idEcuacion;   // Para almacenar el id generado al insertar la ecuación
            int idResultado;  // Para almacenar el id generado al insertar el resultado final
            List<Iteraciones> iteraciones = new List<Iteraciones>();  // Lista para guardar las iteraciones del método

            // Abrir conexión a la base de datos
            using (SqlConnection conn = ConexionDBController.ObtenerConexion())
            {
                conn.Open();

                // 1. Insertar la ecuación recibida en la tabla Ecuaciones
                string queryEcuacion = @"
                    INSERT INTO Ecuaciones (Funcion, ValorInicial1, Tolerancia, MaxIteraciones, IdUsuario)
                    OUTPUT INSERTED.IdEcuacion
                    VALUES (@Funcion, @Val1, @Tol, @MaxIter, @IdUsuario)";
                using (SqlCommand cmd = new SqlCommand(queryEcuacion, conn))
                {
                    cmd.Parameters.AddWithValue("@Funcion", modelo.Funcion);
                    cmd.Parameters.AddWithValue("@Val1", (object)modelo.ValorInicial1 ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Tol", modelo.Tolerancia);
                    cmd.Parameters.AddWithValue("@MaxIter", modelo.MaxIteraciones);
                    cmd.Parameters.AddWithValue("@IdUsuario", idUsuario);

                    // Ejecutar inserción y obtener el id generado
                    idEcuacion = (int)cmd.ExecuteScalar();
                }

                

                    // 2. Parsear la función y calcular su derivada simbólicamente usando MathNet.Symbolics
                    var xVar = SymbolicExpression.Variable("x");
                    var funcion = SymbolicExpression.Parse(modelo.Funcion);
                    var derivada = funcion.Differentiate(xVar);

                    // Ejecutar el método Newton-Raphson para obtener las iteraciones
                    iteraciones = ResolverNewtonRaphson(funcion, derivada, modelo.ValorInicial1.Value, modelo.Tolerancia, modelo.MaxIteraciones);

                    if (iteraciones.Count > 0)
                    {
                        // Tomar la última iteración para obtener resultado y error final
                        var ultima = iteraciones.Last();
                        double error = ultima.Error;
                        if (double.IsNaN(error) || double.IsInfinity(error))
                            error = 0.0;

                        // 3. Insertar el resultado final en la tabla Resultados
                        string queryResultado = @"
                        INSERT INTO Resultados 
                        (ResultadoFinal, Iteraciones, ErrorEstimado, FechaResultado, IdEcuacion, IdMetodo)
                        OUTPUT INSERTED.IdResultado
                        VALUES (@ResultadoFinal, @Iteraciones, @ErrorEstimado, @FechaResultado, @IdEcuacion, @IdMetodo)";
                        using (SqlCommand cmd = new SqlCommand(queryResultado, conn))
                        {
                            cmd.Parameters.AddWithValue("@ResultadoFinal", ultima.X0); // Raíz aproximada
                            cmd.Parameters.AddWithValue("@Iteraciones", iteraciones.Count);
                            cmd.Parameters.AddWithValue("@ErrorEstimado", error);
                            cmd.Parameters.AddWithValue("@FechaResultado", DateTime.Now);
                            cmd.Parameters.AddWithValue("@IdEcuacion", idEcuacion);
                            cmd.Parameters.AddWithValue("@IdMetodo", 1); // ID del método Newton-Raphson (ajustar según BD)

                            // Ejecutar inserción y obtener el id del resultado generado
                            idResultado = (int)cmd.ExecuteScalar();
                        }

                        // 4. Insertar cada iteración en la tabla Iteraciones
                        foreach (var it in iteraciones)
                        {
                            double err = it.Error;
                            if (double.IsNaN(err) || double.IsInfinity(err))
                                err = 0.0;

                            // Nota: si tu tabla Iteraciones tiene columna FX1, agrega en el SQL y aquí, si no, elimina este parámetro
                            string queryIteracion = @"
                            INSERT INTO Iteraciones 
                            (Numero, X0, FX0, FX1, Error, IdResultado)
                            VALUES (@Numero, @X0, @FX0, @FX1, @Error, @IdResultado)";
                            using (SqlCommand cmd = new SqlCommand(queryIteracion, conn))
                            {
                                cmd.Parameters.AddWithValue("@Numero", it.Numero);
                                cmd.Parameters.AddWithValue("@X0", it.X0);
                                cmd.Parameters.AddWithValue("@FX0", it.FX0);
                                cmd.Parameters.AddWithValue("@FX1", it.FX1);
                                cmd.Parameters.AddWithValue("@Error", err);
                                cmd.Parameters.AddWithValue("@IdResultado", idResultado);

                                // Ejecutar inserción de iteración
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // Preparar los puntos para graficar (x = valor actual, y = función evaluada)
                        var puntosGrafica = iteraciones.Select(it => new { x = it.X0, y = it.FX0 }).ToList();
                        ViewBag.PuntosGrafica = JsonConvert.SerializeObject(puntosGrafica);
                        ViewBag.Iteraciones = iteraciones;

                        // Mensaje de éxito
                        ViewBag.Mensaje = "Ecuación resuelta correctamente.";
                    }
                    else
                    {
                        // Si no se generaron iteraciones, mostrar mensaje de error
                        ViewBag.Mensaje = "No se pudieron generar iteraciones. Verifica los datos de entrada.";
                    }
                }

                return View(modelo);
            }
            catch (Exception ex)
            {
                // Mensaje personalizado para el usuario
                ViewBag.Mensaje = "⚠️ Error al procesar la ecuación. Verifica que esté escrita correctamente usando solo la variable 'x'.";
                return View(modelo);
            }
        }
        // Método privado que implementa el algoritmo de Newton-Raphson
        // Recibe función, derivada, valor inicial, tolerancia y máximo de iteraciones
        // Devuelve una lista con las iteraciones realizadas
        private List<Iteraciones> ResolverNewtonRaphson(SymbolicExpression funcion, SymbolicExpression derivada, double x0, double tolerancia, int maxIteraciones)
        {
            var iteraciones = new List<Iteraciones>();
            double x = x0; // Valor inicial
            double error = double.MaxValue; // Inicializamos error grande
            int iter = 1; // Contador de iteraciones

            while (error > tolerancia && iter <= maxIteraciones)
            {
                // Evaluar función y derivada en el punto x
                var valores = new Dictionary<string, FloatingPoint> { { "x", x } };
                double fx = funcion.Evaluate(valores).RealValue;
                double dfx = derivada.Evaluate(valores).RealValue;

                // Validar que la derivada no sea cero para evitar división por cero
                if (Math.Abs(dfx) < 1e-14)
                    throw new Exception("La derivada es cero en la iteración " + iter);

                // Calcular nuevo valor de x según fórmula Newton-Raphson
                double xNuevo = x - fx / dfx;

                // Calcular error como la diferencia absoluta entre el nuevo y viejo x
                error = Math.Abs(xNuevo - x);

                // Guardar datos de esta iteración
                iteraciones.Add(new Iteraciones
                {
                    Numero = iter,
                    X0 = x,
                    FX0 = fx,
                    FX1 = dfx,
                    Error = error
                });

                // Actualizar valor de x para la siguiente iteración
                x = xNuevo;
                iter++;
            }

            // Devolver todas las iteraciones calculadas
            return iteraciones;
        }
    }
}
