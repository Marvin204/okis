using System;
using System.Collections.Generic;
using System.Data.SqlClient;       // Para conexión y comandos SQL
using System.Linq;
using System.Security.Cryptography; // Para encriptar la contraseña (hash)
using System.Text;
using System.Web;
using System.Web.Mvc;              // MVC para controladores y vistas
using Portable.Models;             // Modelo Usuarios

namespace Portable.Controllers
{
    public class CuentaController : Controller
    {
        // GET: Cuenta/CrearCuenta
        // Muestra la vista para crear una nueva cuenta de usuario
        public ActionResult CrearCuenta()
        {
            return View();
        }

        // Acción para mostrar una vista con información "AcercaDe"
        public ActionResult AcercaDe()
        {
            return View();
        }

        // Acción para mostrar un menú con métodos (quizás de la app)
        public ActionResult MenuMetodos()
        {
            return View();
        }

        // Acción POST para crear cuenta, recibe un objeto usuario desde el formulario
        [HttpPost]
        public ActionResult CrearCuenta(Usuarios usuario)
        {
            // Valida que el modelo recibido sea válido según las reglas del modelo
            if (ModelState.IsValid)
            {
                // Abrimos conexión a la base de datos usando un método externo
                using (SqlConnection conn = ConexionDBController.ObtenerConexion())
                {
                    conn.Open();

                    // Verificamos si ya existe un usuario con el mismo nombre o correo
                    string verificarQuery = "SELECT COUNT(*) FROM Usuarios WHERE Nombre = @Nombre OR Correo = @Correo";
                    using (SqlCommand verificarCmd = new SqlCommand(verificarQuery, conn))
                    {
                        // Añadimos parámetros para evitar SQL Injection
                        verificarCmd.Parameters.AddWithValue("@Nombre", usuario.Nombre);
                        verificarCmd.Parameters.AddWithValue("@Correo", usuario.Correo);

                        // Ejecutamos la consulta y obtenemos el número de coincidencias
                        int count = Convert.ToInt32(verificarCmd.ExecuteScalar());
                        if (count > 0)
                        {
                            // Si ya existe, enviamos mensaje de error a la vista y retornamos el formulario con datos
                            ViewBag.Mensaje = "Ya existe una cuenta con ese nombre o correo.";
                            return View(usuario);
                        }
                    }

                    // Si no existe, procedemos a crear la cuenta

                    // Hasheamos (encriptamos) la contraseña recibida
                    string hash = HashearContrasena(usuario.ContrasenaHash);

                    // Consulta para insertar el nuevo usuario con la contraseña ya hasheada
                    string query = "INSERT INTO Usuarios (Nombre, Correo, ContrasenaHash) VALUES (@Nombre, @Correo, @Hash)";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Nombre", usuario.Nombre);
                        cmd.Parameters.AddWithValue("@Correo", usuario.Correo);
                        cmd.Parameters.AddWithValue("@Hash", hash); // Contraseña ya en hash (string base64)
                        cmd.ExecuteNonQuery(); // Ejecuta la inserción
                    }
                }
                // Redirige a la acción para iniciar sesión después de crear la cuenta exitosamente
                return RedirectToAction("IniciarSesion");
            }
            // Si el modelo no es válido, regresa la vista con los datos ingresados para corrección
            return View(usuario);
        }

        // Método privado para convertir la contraseña en un hash SHA256 en Base64
        private string HashearContrasena(string contrasena)
        {
            if (string.IsNullOrEmpty(contrasena))
                return string.Empty;

            using (SHA256 sha256 = SHA256.Create())
            {
                // Convierte la contraseña a bytes
                byte[] bytes = Encoding.UTF8.GetBytes(contrasena);
                // Calcula el hash SHA256 de los bytes
                byte[] hash = sha256.ComputeHash(bytes);
                // Retorna el hash en formato Base64 para almacenar en texto
                return Convert.ToBase64String(hash);
            }
        }

        // GET: Cuenta/IniciarSesion
        // Muestra la vista para iniciar sesión
        public ActionResult IniciarSesion()
        {
            return View();
        }

        // POST: Cuenta/IniciarSesion
        // Recibe nombre y contraseña para validar usuario
        [HttpPost]
        public ActionResult IniciarSesion(string nombre, string contrasena)
        {
            // Validar que se hayan ingresado ambos campos
            if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(contrasena))
            {
                ViewBag.Error = "Debe ingresar el nombre y la contraseña.";
                return View();
            }

            using (SqlConnection conn = ConexionDBController.ObtenerConexion())
            {
                conn.Open();
                // Hasheamos la contraseña para comparar con la base de datos
                string hash = HashearContrasena(contrasena);

                // Consulta para buscar usuario con nombre y contraseña hash coincidente
                string query = "SELECT IdUsuario FROM Usuarios WHERE Nombre = @Nombre AND ContrasenaHash = @Hash";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Nombre", nombre);
                    cmd.Parameters.AddWithValue("@Hash", hash);

                    // Ejecuta la consulta y devuelve el IdUsuario si hay coincidencia
                    object result = cmd.ExecuteScalar();

                    if (result != null)
                    {
                        // Si encontró usuario, guardar datos en sesión
                        int idUsuario = Convert.ToInt32(result);
                        Session["UsuarioNombre"] = nombre;
                        Session["IdUsuario"] = idUsuario;  // Guardamos el IdUsuario para uso posterior

                        // Redirigir a página bienvenida
                        return RedirectToAction("Bienvenido");
                    }
                    else
                    {
                        // Si no encontró usuario, mostrar error
                        ViewBag.Error = "Nombre o contraseña incorrectos.";
                        return View();
                    }
                }
            }
        }

        // GET: Cuenta/Bienvenido
        // Vista para dar la bienvenida al usuario ya autenticado
        public ActionResult Bienvenido()
        {
            // Si no hay usuario en sesión, redirige a inicio de sesión para proteger la vista
            if (Session["UsuarioNombre"] == null)
                return RedirectToAction("IniciarSesion");

            // Pasar el nombre de usuario a la vista para mostrarlo
            ViewBag.NombreUsuario = Session["UsuarioNombre"].ToString();
            return View();
        }
    }
}
