using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Web.Mvc;
using MathNet.Symbolics;
using Portable.Models;
using Expr = MathNet.Symbolics.SymbolicExpression;
using System.Linq;

namespace Portable.Controllers
{
    public class SecanteController : Controller
    {
        // Acción GET para mostrar la vista del método de la Secante
        public ActionResult VistaSecante()
        {
            return View();
        }

        // Acción GET para mostrar la vista del método de Müller (aunque no está implementado aquí)
        public ActionResult VistaMuller()
        {
            return View();
        }

        // Acción POST que recibe el modelo con datos para resolver la ecuación por método de la Secante
        [HttpPost]
        public ActionResult VistaSecante(Ecuaciones modelo)
        {
            // Validar que el modelo recibido sea válido (campos requeridos llenos)
            if (!ModelState.IsValid)
            {
                ViewBag.Mensaje = "Por favor, complete todos los campos requeridos.";
                return View(modelo);
            }

            // Validar que haya un usuario en sesión (autenticado)
            if (Session["UsuarioNombre"] == null)
            {
                // Redirigir a iniciar sesión si no hay usuario
                return RedirectToAction("IniciarSesion", "Cuenta");
            }

            // Obtener el ID del usuario que está realizando la operación
            int idUsuario = Models.MetodosEstaticos.ObtenerIdUsuario(Session["UsuarioNombre"].ToString());
            int idEcuacion;
            int idResultado;
            List<Iteraciones> iteraciones;

            // Abrir conexión a la base de datos
            using (SqlConnection conn = ConexionDBController.ObtenerConexion())
            {
                conn.Open();

                // 1. Insertar los datos de la ecuación ingresada en la tabla Ecuaciones
                string queryEcuacion = @"
                    INSERT INTO Ecuaciones (Funcion, ValorInicial1, ValorInicial2, Tolerancia, MaxIteraciones, IdUsuario)
                    OUTPUT INSERTED.IdEcuacion
                    VALUES (@Funcion, @Val1, @Val2, @Tol, @MaxIter, @IdUsuario)";
                using (SqlCommand cmd = new SqlCommand(queryEcuacion, conn))
                {
                    // Parámetros que se insertan en la consulta para prevenir inyección SQL
                    cmd.Parameters.AddWithValue("@Funcion", modelo.Funcion);
                    cmd.Parameters.AddWithValue("@Val1", (object)modelo.ValorInicial1 ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Val2", (object)modelo.ValorInicial2 ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Tol", modelo.Tolerancia);
                    cmd.Parameters.AddWithValue("@MaxIter", modelo.MaxIteraciones);
                    cmd.Parameters.AddWithValue("@IdUsuario", idUsuario);

                    // Ejecutar inserción y obtener el id generado de la ecuación insertada
                    idEcuacion = (int)cmd.ExecuteScalar();
                }

                // 2. Llamar al método que resuelve la ecuación con el método de la secante
                iteraciones = ResolverSecante(modelo.Funcion, modelo.ValorInicial1.Value, modelo.ValorInicial2.Value, modelo.Tolerancia, modelo.MaxIteraciones);

                if (iteraciones.Count > 0)
                {
                    var ultima = iteraciones.Last();

                    // 3. Insertar el resultado final en la tabla Resultados
                    string queryResultado = @"
                        INSERT INTO Resultados 
                        (ResultadoFinal, Iteraciones, ErrorEstimado, FechaResultado, IdEcuacion, IdMetodo)
                        OUTPUT INSERTED.IdResultado
                        VALUES (@ResultadoFinal, @Iteraciones, @ErrorEstimado, @FechaResultado, @IdEcuacion, @IdMetodo)";
                    using (SqlCommand cmd = new SqlCommand(queryResultado, conn))
                    {
                        // Parámetros para insertar el resultado final del método
                        cmd.Parameters.AddWithValue("@ResultadoFinal", ultima.X2);  // La raíz aproximada
                        cmd.Parameters.AddWithValue("@Iteraciones", iteraciones.Count); // Número de iteraciones realizadas
                        cmd.Parameters.AddWithValue("@ErrorEstimado", ultima.Error);    // Error estimado en la última iteración
                        cmd.Parameters.AddWithValue("@FechaResultado", DateTime.Now);  // Fecha y hora actual
                        cmd.Parameters.AddWithValue("@IdEcuacion", idEcuacion);        // FK hacia la ecuación
                        cmd.Parameters.AddWithValue("@IdMetodo", 2);                   // ID del método Secante (ajustar si cambia)

                        // Ejecutar inserción y obtener el id del resultado insertado
                        idResultado = (int)cmd.ExecuteScalar();
                    }

                    // 4. Insertar cada una de las iteraciones en la tabla Iteraciones
                    foreach (var it in iteraciones)
                    {
                        string queryIteracion = @"
                            INSERT INTO Iteraciones 
                            (Numero, X0, X1, X2, FX0, FX1, FX2, Error, IdResultado)
                            VALUES (@Numero, @X0, @X1, @X2, @FX0, @FX1, @FX2, @Error, @IdResultado)";
                        using (SqlCommand cmd = new SqlCommand(queryIteracion, conn))
                        {
                            // Parámetros para cada iteración
                            cmd.Parameters.AddWithValue("@Numero", it.Numero);
                            cmd.Parameters.AddWithValue("@X0", it.X0);
                            cmd.Parameters.AddWithValue("@X1", it.X1);
                            cmd.Parameters.AddWithValue("@X2", it.X2);
                            cmd.Parameters.AddWithValue("@FX0", it.FX0);
                            cmd.Parameters.AddWithValue("@FX1", it.FX1);
                            // Si FX2 es null, insertamos DBNull para no causar error
                            cmd.Parameters.AddWithValue("@FX2", it.FX2 ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Error", it.Error);
                            cmd.Parameters.AddWithValue("@IdResultado", idResultado);

                            // Ejecutar inserción de iteración
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Preparar los datos para la gráfica, con puntos (X2, FX2)
                    var puntosGrafica = iteraciones.Select(it => new { x = it.X2, y = it.FX2 ?? 0.0 }).ToList();

                    ViewBag.PuntosGrafica = puntosGrafica; // Enviar puntos a la vista
                    ViewBag.Iteraciones = iteraciones;     // Enviar lista de iteraciones a la vista
                    ViewBag.Mensaje = "Ecuación guardada y resuelta exitosamente."; // Mensaje de éxito
                }
                else
                {
                    // No se pudo generar iteraciones (posible error en función o parámetros)
                    ViewBag.Mensaje = "No se pudieron generar iteraciones. Verifica los datos de entrada.";
                }
            }

            // Devolver la vista con los datos para mostrar resultados y gráficos
            return View();
        }

        // Método privado que implementa el algoritmo del método de la Secante
        // Recibe la función como string, dos valores iniciales, tolerancia y máximo número de iteraciones
        // Devuelve una lista con las iteraciones calculadas
        private List<Iteraciones> ResolverSecante(string funcion, double x0, double x1, double tolerancia, int maxIteraciones)
        {
            var iteraciones = new List<Iteraciones>();
            Expr expresion;

            try
            {
                // Intentar parsear la función a expresión simbólica
                expresion = Expr.Parse(funcion);
            }
            catch
            {
                // Si falla el parseo, retornar lista vacía
                return iteraciones;
            }

            // Ciclo para hacer iteraciones hasta máximo permitido o hasta alcanzar tolerancia
            for (int i = 0; i < maxIteraciones; i++)
            {
                // Evaluar la función en los puntos x0 y x1
                double fx0 = Models.MetodosEstaticos.EvaluarFuncion(expresion, x0);
                double fx1 = Models.MetodosEstaticos.EvaluarFuncion(expresion, x1);

                // Evitar división por cero si fx1 - fx0 == 0
                if (fx1 - fx0 == 0)
                    break;

                // Calcular siguiente aproximación usando fórmula del método Secante
                double x2 = x1 - fx1 * (x1 - x0) / (fx1 - fx0);

                // Calcular error como la diferencia absoluta entre x2 y x1
                double error = Math.Abs(x2 - x1);

                // Evaluar la función en x2 para tener FX2
                double fx2 = Models.MetodosEstaticos.EvaluarFuncion(expresion, x2);

                // Añadir iteración a la lista con todos los valores calculados
                iteraciones.Add(new Iteraciones
                {
                    Numero = i + 1,
                    X0 = x0,
                    X1 = x1,
                    X2 = x2,
                    FX0 = fx0,
                    FX1 = fx1,
                    FX2 = fx2,
                    Error = error
                });

                // Si error es menor que tolerancia, detener iteraciones
                if (error < tolerancia)
                    break;

                // Actualizar valores para siguiente iteración
                x0 = x1;
                x1 = x2;
            }

            // Retornar todas las iteraciones calculadas
            return iteraciones;
        }
    }
}
