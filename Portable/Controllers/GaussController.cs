using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Portable.Models;

namespace Portable.Controllers
{
    public class GaussController : Controller
    {
        // Acción GET que muestra la vista para ingresar datos del método Gauss-Jordan
        public ActionResult VistaGauss()
        {
            return View();
        }

        // Acción POST que recibe el modelo Ecuaciones con datos ingresados por el usuario
        [HttpPost]
        public ActionResult VistaGauss(Ecuaciones modelo)
        {
            // Validar que el modelo tenga los campos requeridos
            if (!ModelState.IsValid)
            {
                ViewBag.Mensaje = "Por favor, complete todos los campos requeridos.";
                return View(modelo);
            }

            // Verificar que haya usuario en sesión (login)
            if (Session["UsuarioNombre"] == null)
            {
                // Si no hay sesión, redirigir a login
                return RedirectToAction("IniciarSesion", "Cuenta");
            }

            // Obtener id del usuario logueado desde un método estático auxiliar
            int idUsuario = MetodosEstaticos.ObtenerIdUsuario(Session["UsuarioNombre"].ToString());

            int idEcuacion;  // Para guardar el id insertado de la ecuación
            int idResultado; // Para guardar el id insertado del resultado
            List<Iteraciones> iteraciones = new List<Iteraciones>(); // Lista para guardar iteraciones

            using (SqlConnection conn = ConexionDBController.ObtenerConexion())
            {
                conn.Open();

                // 1. Insertar la ecuación con todos sus datos en la tabla Ecuaciones y obtener el Id insertado
                string queryEcuacion = @"
INSERT INTO Ecuaciones 
(Funcion, ValorInicial1, ValorInicial2, ValorInicial3, Derivada, Tolerancia, MaxIteraciones, IdUsuario, MatrizEntrada)
OUTPUT INSERTED.IdEcuacion
VALUES (@Funcion, @Val1, @Val2, @Val3, @Derivada, @Tol, @MaxIter, @IdUsuario, @MatrizEntrada)";

                using (SqlCommand cmd = new SqlCommand(queryEcuacion, conn))
                {
                    cmd.Parameters.AddWithValue("@Funcion", modelo.Funcion);
                    cmd.Parameters.AddWithValue("@Val1", (object)modelo.ValorInicial1 ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Val2", (object)modelo.ValorInicial2 ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Val3", (object)modelo.ValorInicial3 ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Derivada", (object)modelo.Derivada ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Tol", modelo.Tolerancia);
                    cmd.Parameters.AddWithValue("@MaxIter", modelo.MaxIteraciones);
                    cmd.Parameters.AddWithValue("@IdUsuario", idUsuario);
                    cmd.Parameters.AddWithValue("@MatrizEntrada", (object)modelo.MatrizEntrada);

                    idEcuacion = (int)cmd.ExecuteScalar(); // Obtener id generado
                }

                // 2. Parsear la matriz aumentada de texto a estructura de datos numéricos
                string[] filas = modelo.MatrizEntrada?.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                if (filas == null || filas.Length == 0)
                {
                    ViewBag.Mensaje = "La matriz aumentada está vacía o no es válida.";
                    return View(modelo);
                }

                int n = filas.Length; // número de filas
                double[,] matriz = new double[n, n];   // matriz cuadrada de coeficientes
                double[] resultados = new double[n];   // vector de términos independientes

                try
                {
                    for (int i = 0; i < n; i++)
                    {
                        string[] valores = filas[i].Split(',');

                        // Validar que cada fila tenga n coeficientes + 1 término independiente
                        if (valores.Length != n + 1)
                        {
                            ViewBag.Mensaje = $"La fila {i + 1} debe contener {n + 1} valores separados por coma.";
                            return View(modelo);
                        }

                        // Parsear coeficientes
                        for (int j = 0; j < n; j++)
                        {
                            matriz[i, j] = double.Parse(valores[j]);
                        }

                        // Parsear término independiente
                        resultados[i] = double.Parse(valores[n]);
                    }
                }
                catch (FormatException)
                {
                    ViewBag.Mensaje = "Error en el formato de los números. Usa solo números y comas.";
                    return View(modelo);
                }

                // 3. Ejecutar el método Gauss-Jordan para resolver el sistema
                iteraciones = ResolverGaussJordan(matriz, resultados, 0);

                if (iteraciones.Any())
                {
                    var ultima = iteraciones.Last();

                    // 4. Insertar el resultado final en la tabla Resultados y obtener su id
                    string queryResultado = @"
                INSERT INTO Resultados 
                (ResultadoFinal, Iteraciones, ErrorEstimado, FechaResultado, IdEcuacion, IdMetodo)
                OUTPUT INSERTED.IdResultado
                VALUES (@ResultadoFinal, @Iteraciones, @ErrorEstimado, @FechaResultado, @IdEcuacion, @IdMetodo)";

                    using (SqlCommand cmd = new SqlCommand(queryResultado, conn))
                    {
                        // Guardamos el último valor X2 como resultado final, aunque se puede mejorar (ver modelo)
                        cmd.Parameters.AddWithValue("@ResultadoFinal", ultima.X2);
                        cmd.Parameters.AddWithValue("@Iteraciones", iteraciones.Count);
                        cmd.Parameters.AddWithValue("@ErrorEstimado", ultima.Error);
                        cmd.Parameters.AddWithValue("@FechaResultado", DateTime.Now);
                        cmd.Parameters.AddWithValue("@IdEcuacion", idEcuacion);
                        cmd.Parameters.AddWithValue("@IdMetodo", 4); // Código para Gauss-Jordan

                        idResultado = (int)cmd.ExecuteScalar();
                    }

                    // 5. Insertar cada iteración en la tabla Iteraciones
                    foreach (var it in iteraciones)
                    {
                        double err = double.IsNaN(it.Error) || double.IsInfinity(it.Error) ? 0.0 : it.Error;

                        string queryIteracion = @"
                    INSERT INTO Iteraciones 
                    (Numero, X0, X1, X2, FX0, FX1, FX2, Error, IdResultado)
                    VALUES (@Num, @X0, @X1, @X2, @FX0, @FX1, @FX2, @Error, @IdResultado)";

                        using (SqlCommand cmd = new SqlCommand(queryIteracion, conn))
                        {
                            cmd.Parameters.AddWithValue("@Num", it.Numero);
                            cmd.Parameters.AddWithValue("@X0", it.X0);
                            cmd.Parameters.AddWithValue("@X1", it.X1);
                            cmd.Parameters.AddWithValue("@X2", it.X2);
                            cmd.Parameters.AddWithValue("@FX0", it.FX0);
                            cmd.Parameters.AddWithValue("@FX1", it.FX1);
                            cmd.Parameters.AddWithValue("@FX2", (object)it.FX2 ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Error", err);
                            cmd.Parameters.AddWithValue("@IdResultado", idResultado);

                            cmd.ExecuteNonQuery();
                        }
                    }

                    ViewBag.Total = iteraciones.Count;
                    ViewBag.Mensaje = "Ecuación resuelta y datos guardados correctamente.";
                }
                else
                {
                    ViewBag.Mensaje = "No se pudieron generar iteraciones. Revisa los valores ingresados.";
                }
            }

            // Pasar iteraciones a la vista para mostrar
            ViewBag.Iteraciones = iteraciones;

            return View(modelo);
        }

        // Método que implementa Gauss-Jordan para resolver el sistema lineal
        // Retorna una lista de iteraciones que contienen el estado de la matriz en cada paso
        public static List<Iteraciones> ResolverGaussJordan(double[,] matriz, double[] resultados, int idResultado)
        {
            int n = resultados.Length;
            var iteraciones = new List<Iteraciones>();

            // Construir matriz aumentada [A|b]
            double[,] aug = new double[n, n + 1];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                    aug[i, j] = matriz[i, j];
                aug[i, n] = resultados[i];
            }

            // Proceso de Gauss-Jordan
            for (int i = 0; i < n; i++)
            {
                // Pivotaje parcial: si pivote es 0, intercambiar filas
                if (aug[i, i] == 0)
                {
                    bool intercambiado = false;
                    for (int k = i + 1; k < n; k++)
                    {
                        if (aug[k, i] != 0)
                        {
                            for (int j = 0; j <= n; j++)
                            {
                                double temp = aug[i, j];
                                aug[i, j] = aug[k, j];
                                aug[k, j] = temp;
                            }
                            intercambiado = true;
                            break;
                        }
                    }
                    if (!intercambiado)
                        throw new InvalidOperationException("No se pudo encontrar un pivote distinto de cero. El sistema podría no tener solución única.");
                }

                // Normalizar fila i (dividir por pivote)
                double pivote = aug[i, i];
                for (int j = 0; j <= n; j++)
                    aug[i, j] /= pivote;

                // Eliminar otras filas en columna i
                for (int k = 0; k < n; k++)
                {
                    if (k == i) continue;
                    double factor = aug[k, i];
                    for (int j = 0; j <= n; j++)
                        aug[k, j] -= factor * aug[i, j];
                }

                // Guardar estado actual de la matriz aumentada en iteraciones
                for (int fila = 0; fila < n; fila++)
                {
                    iteraciones.Add(new Iteraciones
                    {
                        Numero = i + 1,             // Número de paso
                        X0 = aug[fila, 0],          // Valores de la fila
                        X1 = n >= 2 ? aug[fila, 1] : 0,
                        X2 = n >= 3 ? aug[fila, 2] : 0,
                        FX0 = 0,                    // No aplican en Gauss-Jordan
                        FX1 = 0,
                        FX2 = null,
                        Error = 0,
                        IdResultado = idResultado
                    });
                }
            }

            return iteraciones;
        }
    }
}
