using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace Portable.Models
{
    public class Ecuaciones
    {

        [Key]
        public int IdEcuacion { get; set; }

        [Required]
        [StringLength(255)]
        public string Funcion { get; set; }

        public string MatrizEntrada { get; set; }

        public double? ValorInicial1 { get; set; }

        public double? ValorInicial2 { get; set; }
        public double? ValorInicial3 { get; set; }
        public double? Derivada { get; set; }

        [Required]
        public double Tolerancia { get; set; }

        [Required]
        public int MaxIteraciones { get; set; }
        
        [Required]
        public DateTime FechaIngreso { get; set; } = DateTime.Now;

        [Required]
        public int IdUsuario { get; set; }

        // Navegación opcional si estás usando EF con relaciones
        [ForeignKey("IdUsuario")]
        public virtual Usuarios Usuario { get; set; }

    }
}