using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MathNet.Symbolics;            // Para parsear y evaluar expresiones simbólicas
using Portable.Models;              // Modelos de datos del proyecto
using Expr = MathNet.Symbolics.SymbolicExpression; // Alias para expresiones simbólicas
using NCalc;                       // No se utiliza en este código, pero está importado
using NCalcExpression = NCalc.Expression; // Alias no utilizado
using Newtonsoft.Json;             // Para serializar datos a JSON (por ejemplo, para gráficos)

namespace Portable.Controllers
{
    public class MullerController : Controller
    {
        // Acción GET para mostrar la vista donde se ingresa la ecuación y parámetros
        public ActionResult VistaMuller()
        {
            return View();
        }

        // Acción POST que procesa la ecuación enviada desde la vista y resuelve con método de Müller
        [HttpPost]
        public ActionResult VistaMuller(Ecuaciones modelo)
        {
            // Validar que los datos ingresados en el formulario sean correctos
            if (!ModelState.IsValid)
            {
                ViewBag.Mensaje = "Por favor, complete todos los campos requeridos.";
                return View(modelo);
            }

            // Verificar que el usuario haya iniciado sesión; si no, redirigir a login
            if (Session["UsuarioNombre"] == null)
            {
                return RedirectToAction("IniciarSesion", "Cuenta");
            }

            // Obtener el Id del usuario en base al nombre almacenado en sesión
            int idUsuario = MetodosEstaticos.ObtenerIdUsuario(Session["UsuarioNombre"].ToString());

            int idEcuacion; // Para almacenar el Id generado al insertar la ecuación
            int idResultado; // Para almacenar el Id generado al insertar el resultado
            List<Iteraciones> iteraciones = new List<Iteraciones>(); // Lista para guardar las iteraciones

            // Abrir conexión a la base de datos
            using (SqlConnection conn = ConexionDBController.ObtenerConexion())
            {
                conn.Open();

                // 1. Insertar la ecuación ingresada por el usuario en la tabla Ecuaciones
                string queryEcuacion = @"
                    INSERT INTO Ecuaciones (Funcion, ValorInicial1, ValorInicial2, ValorInicial3, Tolerancia, MaxIteraciones, IdUsuario)
                    OUTPUT INSERTED.IdEcuacion
                    VALUES (@Funcion, @Val1, @Val2, @Val3, @Tol, @MaxIter, @IdUsuario)";
                using (SqlCommand cmd = new SqlCommand(queryEcuacion, conn))
                {
                    // Agregar parámetros con valores del modelo recibido
                    cmd.Parameters.AddWithValue("@Funcion", modelo.Funcion);
                    cmd.Parameters.AddWithValue("@Val1", (object)modelo.ValorInicial1 ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Val2", (object)modelo.ValorInicial2 ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Val3", (object)modelo.ValorInicial3 ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Tol", modelo.Tolerancia);
                    cmd.Parameters.AddWithValue("@MaxIter", modelo.MaxIteraciones);
                    cmd.Parameters.AddWithValue("@IdUsuario", idUsuario);

                    // Ejecutar la inserción y obtener el Id generado
                    idEcuacion = (int)cmd.ExecuteScalar();
                }

                // 2. Resolver la ecuación usando el método de Müller con los parámetros ingresados
                iteraciones = ResolverMuller(
                    modelo.Funcion,
                    modelo.ValorInicial1.Value,
                    modelo.ValorInicial2.Value,
                    modelo.ValorInicial3.Value,
                    modelo.Tolerancia,
                    modelo.MaxIteraciones
                );

                if (iteraciones.Count > 0)
                {
                    var ultima = iteraciones.Last(); // Obtener la última iteración para resultados finales

                    // Validar el error estimado para evitar valores inválidos
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
                        cmd.Parameters.AddWithValue("@ResultadoFinal", ultima.X2);
                        cmd.Parameters.AddWithValue("@Iteraciones", iteraciones.Count);
                        cmd.Parameters.AddWithValue("@ErrorEstimado", error);
                        cmd.Parameters.AddWithValue("@FechaResultado", DateTime.Now);
                        cmd.Parameters.AddWithValue("@IdEcuacion", idEcuacion);
                        cmd.Parameters.AddWithValue("@IdMetodo", 3); // 10 = Método de Müller

                        idResultado = (int)cmd.ExecuteScalar();
                    }

                    // 4. Insertar cada iteración en la tabla Iteraciones para el resultado registrado
                    foreach (var it in iteraciones)
                    {
                        double err = it.Error;
                        if (double.IsNaN(err) || double.IsInfinity(err))
                            err = 0.0;

                        string queryIteracion = @"
                            INSERT INTO Iteraciones 
                            (Numero, X0, X1, X2, FX0, FX1, FX2, Error, IdResultado)
                            VALUES (@Numero, @X0, @X1, @X2, @FX0, @FX1, @FX2, @Error, @IdResultado)";
                        using (SqlCommand cmd = new SqlCommand(queryIteracion, conn))
                        {
                            cmd.Parameters.AddWithValue("@Numero", it.Numero);
                            cmd.Parameters.AddWithValue("@X0", it.X0);
                            cmd.Parameters.AddWithValue("@X1", it.X1);
                            cmd.Parameters.AddWithValue("@X2", it.X2);
                            cmd.Parameters.AddWithValue("@FX0", it.FX0);
                            cmd.Parameters.AddWithValue("@FX1", it.FX1);
                            // FX2 puede ser null, por eso se usa DBNull si es null
                            cmd.Parameters.AddWithValue("@FX2", (object)it.FX2 ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Error", err);
                            cmd.Parameters.AddWithValue("@IdResultado", idResultado);

                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Preparar datos para graficar (se podrían usar en la vista con JS)
                    var puntosGrafica = iteraciones.Select(it => new { x = it.FX1, y = it.FX2 ?? 0.0 }).ToList();

                    ViewBag.PuntosGrafica = puntosGrafica;                    // Puntos para el gráfico
                    JsonConvert.SerializeObject(puntosGrafica);               // Serializar a JSON (aunque no se usa el resultado aquí)
                    ViewBag.Iteraciones = iteraciones;                        // Enviar iteraciones para mostrar tabla
                }
                else
                {
                    // Si no se pudieron generar iteraciones, mostrar mensaje de error
                    ViewBag.Mensaje = "No se pudieron generar iteraciones. Verifica los datos de entrada.";
                }
            }

            // Retornar la vista con el modelo original para mostrar resultados o errores
            return View(modelo);
        }


        // Método estático para calcular las iteraciones del método de Müller
        public static List<Iteraciones> ResolverMuller(string funcion, double x0, double x1, double x2, double tolerancia, int maxIteraciones)
        {
            List<Iteraciones> iteraciones = new List<Iteraciones>();
            int iter = 1;
            double x3 = 0, error = double.MaxValue;

            // Parsear la función en notación infija a expresión simbólica (MathNet.Symbolics)
            Expr expr = Infix.ParseOrThrow(funcion);

            // Función local para evaluar la expresión con un valor dado de x
            double f(double val)
            {
                // Evaluar la expresión sustituyendo 'x' por val
                var result = expr.Evaluate(
                    new Dictionary<string, FloatingPoint> { { "x", val } }
                );
                // Convertir el resultado a double (valor real)
                return (double)result.RealValue;
            }

            // Iterar mientras el error sea mayor que la tolerancia y no exceda maxIteraciones
            while (error > tolerancia && iter <= maxIteraciones)
            {
                double f0 = f(x0);
                double f1 = f(x1);
                double f2 = f(x2);

                // Calcular diferencias y coeficientes para el polinomio cuadrático de Müller
                double h1 = x1 - x0;
                double h2 = x2 - x1;
                double d1 = (f1 - f0) / h1;
                double d2 = (f2 - f1) / h2;
                double a = (d2 - d1) / (h2 + h1);
                double b = a * h2 + d2;
                double c = f2;

                // Calcular discriminante para la raíz cuadrada
                double discriminante = b * b - 4 * a * c;
                if (discriminante < 0)
                    break; // Raíz compleja, salir

                double raiz = Math.Sqrt(discriminante);

                // Elegir denominador con mayor valor absoluto para estabilidad numérica
                double denom = Math.Abs(b + raiz) > Math.Abs(b - raiz) ? b + raiz : b - raiz;
                if (denom == 0)
                    break; // Evitar división por cero

                // Calcular el incremento dx
                double dx = -2 * c / denom;
                x3 = x2 + dx;
                error = Math.Abs(dx);

                // Guardar datos de esta iteración
                iteraciones.Add(new Iteraciones
                {
                    Numero = iter,
                    X0 = x0,
                    X1 = x1,
                    X2 = x2,
                    FX0 = f0,
                    FX1 = f1,
                    FX2 = f2,
                    Error = error
                });

                // Preparar valores para la siguiente iteración
                x0 = x1;
                x1 = x2;
                x2 = x3;
                iter++;
            }

            // Devolver la lista de iteraciones calculadas
            return iteraciones;
        }

    }
}
